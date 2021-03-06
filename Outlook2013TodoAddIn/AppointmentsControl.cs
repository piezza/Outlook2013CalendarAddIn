﻿using Outlook2013TodoAddIn.Forms;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace Outlook2013TodoAddIn
{
    /// User control to hold the calendar, etc...
    /// </summary>
    public partial class AppointmentsControl : UserControl
    {
        #region "Properties"

        /// <summary>
        /// Number of days (including today) to retrieve appointments from in the future
        /// </summary>
        public decimal NumDays { get; set; }

        /// <summary>
        /// Gets/sets whether mail notifications are enabled or not
        /// </summary>
        public bool MailAlertsEnabled { get; set; }

        /// <summary>
        /// Gets/sets whether to show past appointments in the current day or not
        /// </summary>
        public bool ShowPastAppointments { get; set; }

        /// <summary>
        /// Gets/sets a list of all stores/accounts to retrieve information from
        /// </summary>
        public StringCollection Accounts { get; set; }

        /// <summary>
        /// Gets/sets whether to show friendly group headers (yesterday, today, tomorrow)
        /// </summary>
        public bool ShowFriendlyGroupHeaders { get; set; }

        /// <summary>
        /// Gets/sets whether to show localized day names next to the days
        /// </summary>
        public bool ShowDayNames { get; set; }

        /// <summary>
        /// Gets/sets whether to show week numbers
        /// </summary>
        public bool ShowWeekNumbers { get; set; }

        /// <summary>
        /// Gets/sets the selected calendar date
        /// </summary>
        public DateTime SelectedDate
        {
            get { return this.apptCalendar.SelectedDate; }
            set { this.apptCalendar.SelectedDate = value; }
        }

        /// <summary>
        /// Gets/sets whether to show the tasks list
        /// </summary>
        public bool ShowTasks { get; set; }

        /// <summary>
        /// Gets/sets whether to show the completed tasks in the list
        /// </summary>
        public bool ShowCompletedTasks { get; set; }

        /// <summary>
        /// Gets/sets the first day of the week for the calendar
        /// </summary>
        public System.DayOfWeek FirstDayOfWeek { get; set; }

        #endregion "Properties"

        #region "Methods"

        /// <summary>
        /// Default constructor
        /// </summary>
        public AppointmentsControl()
        {
            InitializeComponent();

            if (Properties.Settings.Default.SplitterDistance >= this.splitContainer1.Panel1MinSize && Properties.Settings.Default.SplitterDistance <= this.splitContainer1.Height - this.splitContainer1.Panel2MinSize)
            {
                // This is to avoid the bug "SplitterDistance must be between Panel1MinSize and Width - Panel2MinSize."
                this.splitContainer1.SplitterDistance = Properties.Settings.Default.SplitterDistance;
                // TODO: This doesn't work, need to fix (race condition?)
                // TODO: Another event is fired after controls are added...
            }
        }

        /// <summary>
        /// Respond to calendar changes
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">DateRangeEventArgs</param>
        private void apptCalendar_SelectedDateChanged(object sender, EventArgs e)
        {
            this.RetrieveData();
        }

        /// <summary>
        /// Retrieves appointments and tasks if configured
        /// </summary>
        public void RetrieveData()
        {
            this.apptCalendar.FirstDayOfWeek = this.FirstDayOfWeek;
            this.RetrieveAppointments();
            if (this.ShowTasks)
            {
                this.RetrieveTasks();
                this.splitContainer1.Panel2Collapsed = false;
            }
            else
            {
                this.splitContainer1.Panel2Collapsed = true;
            }
        }

        /// <summary>
        /// Retrieves tasks for all selected stores
        /// </summary>
        private void RetrieveTasks()
        {
            List<OLTaskItem> tasks = new List<OLTaskItem>();
            foreach (Outlook.Store store in Globals.ThisAddIn.Application.Session.Stores)
            {
                if (Properties.Settings.Default.Accounts != null && Properties.Settings.Default.Accounts.Contains(store.DisplayName))
                {
                    Outlook.Folder todoFolder = store.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderToDo) as Outlook.Folder;
                    tasks.AddRange(this.RetrieveTasksForFolder(todoFolder));
                    // TODO: Shared calendars?
                }
            }

            if (!this.ShowCompletedTasks)
            {
                tasks = tasks.Where(t => !t.Completed).ToList();
            }

            // We need to sort them because they may come from different accounts already ordered
            // Applies sorting by due date
            tasks.Sort(CompareTasks);

            //Outlook.Folder todoFolder =
            //    Globals.ThisAddIn.Application.Session.GetDefaultFolder(
            //    Outlook.OlDefaultFolders.olFolderToDo)
            //    as Outlook.Folder;
            //this.RetrieveTasksForFolder(todoFolder);

            int sameDay = -1; // startRange.Day;

            List<ListViewItem> lstCol = new List<ListViewItem>();
            ListViewGroup grp = null;

            tasks.ForEach(t =>
            {
                if (t.DueDate.Day != sameDay)
                {
                    string groupHeaderText = t.DueDate.ToShortDateString();
                    if (t.DueDate.Year == Constants.NullYear) groupHeaderText = "No due date";
                    if (this.ShowFriendlyGroupHeaders)
                    {
                        int daysDiff = (int)(t.DueDate.Date - DateTime.Today).TotalDays;
                        switch (daysDiff)
                        {
                            case -1:
                                groupHeaderText = Constants.Yesterday + ": " + groupHeaderText;
                                break;
                            case 0:
                                groupHeaderText = Constants.Today + ": " + groupHeaderText;
                                break;
                            case 1:
                                groupHeaderText = Constants.Tomorrow + ": " + groupHeaderText;
                                break;
                            default:
                                break;
                        }
                    }
                    if (this.ShowDayNames && t.DueDate.Year != Constants.NullYear)
                    {
                        groupHeaderText += " (" + t.DueDate.ToString("dddd") + ")";
                    }
                    grp = new ListViewGroup(groupHeaderText, HorizontalAlignment.Left);
                    this.lstTasks.Groups.Add(grp);
                    sameDay = t.DueDate.Day;
                };

                ListViewItem current = new ListViewItem(new string[] { String.Format("{0} {1}", t.DueDate.ToShortTimeString(), t.TaskSubject), "***" });
                current.SubItems.Add(t.TaskSubject);

                // current.Font = new Font(this.Font, FontStyle.Bold);
                // current.UseItemStyleForSubItems = false;

                current.ToolTipText = String.Format("Task Subject: {0}", t.TaskSubject);
                if (t.StartDate.Year != Constants.NullYear)
                {
                    current.ToolTipText += Environment.NewLine + String.Format("Start Date: {0}", t.StartDate.ToShortDateString());
                }
                if (t.Reminder.Year != Constants.NullYear)
                {
                    current.ToolTipText += Environment.NewLine + String.Format("Reminder Time: {0}", t.Reminder.ToString());
                }
                if (t.DueDate.Year != Constants.NullYear)
                {
                    current.ToolTipText += Environment.NewLine + String.Format("Due Date: {0}", t.DueDate.ToShortDateString());
                }
                current.ToolTipText += Environment.NewLine + String.Format("In Folder: {0}", t.FolderName);

                t.Categories.ForEach(cat =>
                {
                    Outlook.Category c = Globals.ThisAddIn.Application.Session.Categories[cat] as Outlook.Category;
                    if (c != null)
                    {
                        current.ToolTipText += Environment.NewLine + " - " + c.Name;
                    }

                });

                current.Tag = t;
                current.Group = grp;
                lstCol.Add(current);
            });

            this.lstTasks.Items.Clear();
            this.lstTasks.Items.AddRange(lstCol.ToArray());
        }

        /// <summary>
        /// Retrieves to-do tasks for the folder on the specified store
        /// </summary>
        /// <param name="todoFolder">Outlook folder</param>
        private List<OLTaskItem> RetrieveTasksForFolder(Outlook.Folder todoFolder)
        {
            List<OLTaskItem> tasks = new List<OLTaskItem>();
            foreach (object item in todoFolder.Items)
            {
                try
                {
                    tasks.Add(new OLTaskItem(item));
                }
                catch
                {
                    // Do nothing, probably the task had already been deleted
                }
            }
            return tasks.Where(t => t.ValidTaskItem).ToList(); // Filter out invalid ones
        }

        /// <summary>
        /// Retrieve all appointments for the current configurations for all selected stores
        /// </summary>
        private void RetrieveAppointments()
        {
            List<Outlook.AppointmentItem> appts = new List<Outlook.AppointmentItem>();
            foreach (Outlook.Store store in Globals.ThisAddIn.Application.Session.Stores)
            {
                if (Properties.Settings.Default.Accounts != null && Properties.Settings.Default.Accounts.Contains(store.DisplayName))
                {
                    Outlook.Folder calFolder = store.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderCalendar) as Outlook.Folder;
                    appts.AddRange(this.RetrieveAppointmentsForFolder(calFolder));
                    // TODO: Shared calendars?
                }
            }
            // We need to sort them because they may come from different accounts already ordered
            appts.Sort(CompareAppointments);

            // Get the Outlook folder for the calendar to retrieve the appointments
            //Outlook.Folder calFolder =
            //    Globals.ThisAddIn.Application.Session.GetDefaultFolder(
            //    Outlook.OlDefaultFolders.olFolderCalendar)
            //    as Outlook.Folder;
            //List<Outlook.AppointmentItem> appts = this.RetrieveAppointmentsForFolder(calFolder);

            // Highlight dates with appointments in the current calendar
            this.apptCalendar.BoldedDates = appts.Select<Outlook.AppointmentItem, DateTime>(a => a.Start.Date).Distinct().ToArray();

            // Now display the actual appointments below the calendar
            DateTime startRange = this.apptCalendar.SelectedDate;
            if (!this.ShowPastAppointments && startRange.Date == DateTime.Today)
            {
                startRange = startRange.Add(DateTime.Now.TimeOfDay);
            }
            DateTime endRange = startRange.AddDays((int)this.NumDays);

            // Get items in range
            var lstItems = appts.Where(a => a.Start >= startRange && a.Start <= endRange);

            int sameDay = -1; // startRange.Day;

            List<ListViewItem> lstCol = new List<ListViewItem>();
            ListViewGroup grp = null;
            lstItems.ToList().ForEach(i =>
            {
                if (i.Start.Day != sameDay)
                {
                    string groupHeaderText = i.Start.ToShortDateString();
                    if (this.ShowFriendlyGroupHeaders)
                    {
                        int daysDiff = (int)(i.Start.Date - DateTime.Today).TotalDays;
                        switch (daysDiff)
                        {
                            case -1:
                                groupHeaderText = Constants.Yesterday + ": " + groupHeaderText;
                                break;
                            case 0:
                                groupHeaderText = Constants.Today + ": " + groupHeaderText;
                                break;
                            case 1:
                                groupHeaderText = Constants.Tomorrow + ": " + groupHeaderText;
                                break;
                            default:
                                break;
                        }
                    }
                    if (this.ShowDayNames)
                    {
                        groupHeaderText += " (" + i.Start.ToString("dddd") + ")";
                    }
                    grp = new ListViewGroup(groupHeaderText, HorizontalAlignment.Left);
                    this.lstAppointments.Groups.Add(grp);
                    sameDay = i.Start.Day;
                };
                string loc = "-"; // TODO: If no second line is specified, the tile is stretched to only one line
                if (!String.IsNullOrEmpty(i.Location)) loc = i.Location;
                ListViewItem current = new ListViewItem(new string[] { String.Format("{0} {1}", i.Start.ToShortTimeString(), i.Subject), loc });
                current.SubItems.Add(i.Subject);

                // current.Font = new Font(this.Font, FontStyle.Bold);
                // current.UseItemStyleForSubItems = false;

                current.ToolTipText = String.Format("{0} - {1}  {2}", i.Start.ToShortTimeString(), i.End.ToShortTimeString(), i.Subject);

                if (!String.IsNullOrEmpty(i.Categories))
                {
                    string[] allCats = i.Categories.Split(new char[] { ',' });
                    if (allCats != null && allCats.Length != 0)
                    {
                        List<string> cs = allCats.Select(cat => cat.Trim()).ToList();
                        cs.ForEach(cat =>
                        {
                            Outlook.Category c = Globals.ThisAddIn.Application.Session.Categories[cat] as Outlook.Category;
                            if (c != null)
                            {
                                current.ToolTipText += Environment.NewLine + " - " + c.Name;
                            }
                        });
                    }
                }

                current.Tag = i;
                current.Group = grp;
                switch (i.BusyStatus)
                {
                    case Outlook.OlBusyStatus.olBusy:
                        current.ForeColor = Color.Purple;
                        break;
                    case Outlook.OlBusyStatus.olFree:
                        break;
                    case Outlook.OlBusyStatus.olOutOfOffice:
                        current.ForeColor = Color.Brown;
                        break;
                    case Outlook.OlBusyStatus.olTentative:
                        break;
                    case Outlook.OlBusyStatus.olWorkingElsewhere:
                        break;
                    default:
                        break;
                }

                lstCol.Add(current);
            });

            this.lstAppointments.Items.Clear();
            this.lstAppointments.Items.AddRange(lstCol.ToArray());

            this.apptCalendar.ShowWeekNumbers = this.ShowWeekNumbers;
            this.apptCalendar.UpdateCalendar();
        }

        /// <summary>
        /// Comparer method to sort tasks based on due date/time
        /// </summary>
        /// <param name="x">First task</param>
        /// <param name="y">Second task</param>
        /// <returns></returns>
        private static int CompareTasks(OLTaskItem x, OLTaskItem y)
        {
            return x.DueDate.CompareTo(y.DueDate);
        }

        /// <summary>
        /// Comparer method to sort appointments based on start date/time
        /// </summary>
        /// <param name="x">First appointment</param>
        /// <param name="y">Second appointment</param>
        /// <returns></returns>
        private static int CompareAppointments(Outlook.AppointmentItem x, Outlook.AppointmentItem y)
        {
            return x.Start.CompareTo(y.Start);
        }

        /// <summary>
        /// Retrieve all appointments for the current configurations for a specific folder
        /// </summary>
        /// <param name="calFolder">Outlook folder</param>
        /// <returns>List of appointments</returns>
        private List<Outlook.AppointmentItem> RetrieveAppointmentsForFolder(Outlook.Folder calFolder)
        {
            int selectedMonth = this.apptCalendar.SelectedDate.Month;
            int selectedYear = this.apptCalendar.SelectedDate.Year;

            // To get all the appointments for the current month (so it displays nicely bolded even for past events)
            DateTime start = new DateTime(selectedYear, selectedMonth, 1); // MM-01-YYYY
            DateTime end = start.AddMonths(1).AddDays(-1); // Last day of the month
            end = end.AddDays((int)this.NumDays); // So we get appointments for the "possible" first days of the next month

            // Get all the appointments
            Outlook.Items rangeAppts = GetAppointmentsInRange(calFolder, start, end);

            // Get a more manageable list
            List<Outlook.AppointmentItem> appts = new List<Outlook.AppointmentItem>();
            if (rangeAppts != null)
            {
                foreach (Outlook.AppointmentItem appt in rangeAppts)
                {
                    appts.Add(appt);
                }
            }
            return appts;
        }

        /// <summary>
        /// Get recurring appointments in a date range.
        /// </summary>
        /// <param name="folder">Outlook folder</param>
        /// <param name="startTime">Start time</param>
        /// <param name="endTime">End time</param>
        /// <returns>Outlook.Items</returns>
        private Outlook.Items GetAppointmentsInRange(Outlook.Folder folder, DateTime startTime, DateTime endTime)
        {
            string filter = "[Start] >= '"
                + startTime.ToString("g")
                + "' AND [End] <= '"
                + endTime.ToString("g") + "'";

            try
            {
                Outlook.Items calItems = folder.Items;
                calItems.IncludeRecurrences = true;
                calItems.Sort("[Start]", Type.Missing);
                Outlook.Items restrictItems = calItems.Restrict(filter);
                if (restrictItems.Count > 0)
                {
                    return restrictItems;
                }
                else
                {
                    return null;
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Open the task
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void lstTasks_DoubleClick(object sender, EventArgs e)
        {
            if (this.lstTasks.SelectedIndices.Count != 0)
            {
                OLTaskItem task = this.lstTasks.SelectedItems[0].Tag as OLTaskItem;
                if (task != null)
                {
                    if (task.OriginalItem is Outlook.MailItem)
                    {
                        Outlook.MailItem mail = task.OriginalItem as Outlook.MailItem;
                        mail.Display(true);
                    }
                    else if (task.OriginalItem is Outlook.ContactItem)
                    {
                        Outlook.ContactItem contact = task.OriginalItem as Outlook.ContactItem;
                        contact.Display(true);
                    }
                    else if (task.OriginalItem is Outlook.TaskItem)
                    {
                        Outlook.TaskItem t = task.OriginalItem as Outlook.TaskItem;
                        t.Display(true);
                    }
                    else
                    {
                        // Do nothing
                    }
                    // At the end, synchronously "refresh" tasks in case they have changed
                    this.RetrieveTasks();
                }
             }
        }

        /// <summary>
        /// Open the appointment, having in mind it might be a recurring event
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void lstAppointments_DoubleClick(object sender, EventArgs e)
        {
            if (this.lstAppointments.SelectedIndices.Count != 0)
            {
                Outlook.AppointmentItem appt = this.lstAppointments.SelectedItems[0].Tag as Outlook.AppointmentItem;
                if (appt != null)
                {
                    if (appt.IsRecurring)
                    {
                        FormRecurringOpen f = new FormRecurringOpen();
                        f.Title = "Open Recurring Item";
                        f.Message = "This is one appointment in a series. What do you want to open?";
                        if (f.ShowDialog() == DialogResult.OK)
                        {
                            if (f.OpenRecurring)
                            {
                                // Open up the master appointment in a new window
                                // If we open the current instance then there is an error: "This item is no longer valid because it has been closed"
                                // One workaround is to refresh the appointments list to get new instances...
                                Outlook.AppointmentItem masterAppt = appt.Parent; // Get the master appointment item
                                masterAppt.Display(true); // Will modify ALL instances
                            }
                            else
                            {
                                // Open up the appointment in a new window
                                appt.Display(true); // Modal yes/no
                            }
                        }
                    }
                    else
                    {
                        // Open up the appointment in a new window
                        appt.Display(true); // Modal yes/no
                    }
                    // At the end, synchronously "refresh" appointments in case they have changed
                    this.RetrieveAppointments();
                }
            }
        }

        /// <summary>
        /// Creates a new mail item to reply all recipients of an appointment (except the current user)
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void mnuItemReplyAllEmail_Click(object sender, EventArgs e)
        {
            if (this.lstAppointments.SelectedIndices.Count != 0)
            {
                Outlook.AppointmentItem appt = this.lstAppointments.SelectedItems[0].Tag as Outlook.AppointmentItem;
                if (appt != null)
                {
                    Outlook.MailItem mail = Globals.ThisAddIn.Application.CreateItem(Outlook.OlItemType.olMailItem) as Outlook.MailItem;
                    string curUserAddress = OutlookHelper.GetEmailAddress(Globals.ThisAddIn.Application.Session.CurrentUser);
                    foreach (Outlook.Recipient rcpt in appt.Recipients)
                    {
                        string smtpAddress = OutlookHelper.GetEmailAddress(rcpt);
                        if (curUserAddress != smtpAddress)
                        {
                            mail.Recipients.Add(smtpAddress);
                        }
                    }
                    mail.Body = Environment.NewLine + Environment.NewLine + appt.Body;
                    mail.Subject = Constants.SubjectRE + ": " + appt.Subject;
                    mail.Display();
                }
            }
        }

        /// <summary>
        /// Allows to delete the selected appointment (whether it's a recurring one or not)
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void mnuItemDeleteAppointment_Click(object sender, EventArgs e)
        {
            if (this.lstAppointments.SelectedIndices.Count != 0)
            {
                Outlook.AppointmentItem appt = this.lstAppointments.SelectedItems[0].Tag as Outlook.AppointmentItem;
                if (appt != null)
                {
                    if (appt.IsRecurring)
                    {
                        FormRecurringOpen f = new FormRecurringOpen();
                        f.Title = "Warning: Delete Recurring Item";
                        f.Message = "This is one appointment in a series. What do you want to delete?";
                        if (f.ShowDialog() == DialogResult.OK)
                        {
                            if (f.OpenRecurring)
                            {
                                Outlook.AppointmentItem masterAppt = appt.Parent; // Get the master appointment item
                                masterAppt.Delete(); // Will delete ALL instances
                            }
                            else
                            {
                                appt.Delete(); // Delete just this instance
                            }
                        }
                    }
                    else
                    {
                        if (MessageBox.Show("Are you sure you want to delete this appointment?", "Delete appointment", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            appt.Delete(); // Delete just this instance
                        }
                    }
                    // At the end, synchronously "refresh" appointments in case they have changed
                    this.RetrieveAppointments();
                }
            }
        }

        /// <summary>
        /// Allows to delete the selected task
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void mnuItemDeleteTask_Click(object sender, EventArgs e)
        {
            if (this.lstTasks.SelectedIndices.Count != 0)
            {
                OLTaskItem task = this.lstTasks.SelectedItems[0].Tag as OLTaskItem;
                if (task != null)
                {
                    if (MessageBox.Show("Are you sure you want to delete this task?", "Delete task", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        if (task.OriginalItem is Outlook.MailItem)
                        {
                            Outlook.MailItem mail = task.OriginalItem as Outlook.MailItem;
                            mail.Delete();
                        }
                        else if (task.OriginalItem is Outlook.ContactItem)
                        {
                            Outlook.ContactItem contact = task.OriginalItem as Outlook.ContactItem;
                            contact.Delete();
                        }
                        else if (task.OriginalItem is Outlook.TaskItem)
                        {
                            Outlook.TaskItem t = task.OriginalItem as Outlook.TaskItem;
                            t.Delete();
                        }
                        else
                        {
                            // Do nothing
                        }
                    }
                    // At the end, synchronously "refresh" tasks in case they have changed
                    this.RetrieveTasks();
                }
            }
        }

        /// <summary>
        /// Allows to mark the selected task as completed (clear task flag for regular items)
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void mnuItemMarkComplete_Click(object sender, EventArgs e)
        {
            if (this.lstTasks.SelectedIndices.Count != 0)
            {
                OLTaskItem task = this.lstTasks.SelectedItems[0].Tag as OLTaskItem;
                if (task != null && !task.Completed) // Attempting to complete an already completed task throws an exception
                {
                    if (MessageBox.Show("Are you sure you want to complete this task?", "Mark task as completed/Clear task flag", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        if (task.OriginalItem is Outlook.MailItem)
                        {
                            Outlook.MailItem mail = task.OriginalItem as Outlook.MailItem;
                            mail.ClearTaskFlag();
                        }
                        else if (task.OriginalItem is Outlook.ContactItem)
                        {
                            Outlook.ContactItem contact = task.OriginalItem as Outlook.ContactItem;
                            contact.ClearTaskFlag();
                        }
                        else if (task.OriginalItem is Outlook.TaskItem)
                        {
                            Outlook.TaskItem t = task.OriginalItem as Outlook.TaskItem;
                            t.MarkComplete();
                        }
                        else
                        {
                            // Do nothing
                        }
                    }
                    // At the end, synchronously "refresh" tasks in case they have changed
                    this.RetrieveTasks();
                }
            }
        }

        /// <summary>
        /// Switch to the calendar view when double-clicking a date
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void apptCalendar_CellDoubleClick(object sender, EventArgs e)
        {
            // Clicking in days outside of the current month will cause the calendar to refresh to that day
            // reposition all days and select the wrong one
            Outlook.Folder f = Globals.ThisAddIn.Application.Session.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderCalendar) as Outlook.Folder;
            Globals.ThisAddIn.Application.ActiveExplorer().CurrentFolder = f;
            Outlook.CalendarView cv = (Outlook.CalendarView)(Globals.ThisAddIn.Application.ActiveExplorer().CurrentView);
            cv.CalendarViewMode = Outlook.OlCalendarViewMode.olCalendarViewDay;
            cv.GoToDate(this.apptCalendar.SelectedDate);
        }

        /// <summary>
        /// New method to show the configuration form
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void apptCalendar_ConfigurationButtonClicked(object sender, EventArgs e)
        {
            using (FormConfiguration cfg = new FormConfiguration())
            {
                if (cfg.ShowDialog() == DialogResult.OK)
                {
                    this.NumDays = cfg.NumDays;
                    this.MailAlertsEnabled = cfg.MailAlertsEnabled;
                    this.ShowPastAppointments = cfg.ShowPastAppointments;
                    this.Accounts = cfg.Accounts;
                    this.ShowFriendlyGroupHeaders = cfg.ShowFriendlyGroupHeaders;
                    this.ShowDayNames = cfg.ShowDayNames;
                    this.ShowWeekNumbers = cfg.ShowWeekNumbers;
                    this.ShowTasks = cfg.ShowTasks;
                    this.ShowCompletedTasks = cfg.ShowCompletedTasks;
                    this.FirstDayOfWeek = cfg.FirstDayOfWeek;
                    this.RetrieveData();
                }
            }
        }

        private void lstTasks_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawBackground(); // To avoid repainting (making font "grow")
            OLTaskItem task = e.Item.Tag as OLTaskItem;

            // Color catColor = Color.Empty;
            List<Color> catColors = new List<Color>();

            Font itemFont = this.Font;
            task.Categories.ForEach(cat =>
            {
                Outlook.Category c = Globals.ThisAddIn.Application.Session.Categories[cat] as Outlook.Category;
                if (c != null)
                {
                    catColors.Add(TranslateCategoryColor(c.Color));
                }
            });
            int categoriesRectangleWidth = 30;
            int horizontalSpacing = 2;

            Rectangle totalRectangle = e.Bounds;
            Rectangle subjectRectangle = totalRectangle; subjectRectangle.Height = this.FontHeight;
            Rectangle categoriesRectangle = totalRectangle; categoriesRectangle.Width = categoriesRectangleWidth; categoriesRectangle.Height = this.FontHeight; categoriesRectangle.Offset(10, this.FontHeight);
            Rectangle reminderRectangle = totalRectangle; reminderRectangle.Height = this.FontHeight; reminderRectangle.Offset(0, this.FontHeight);
            if (task.Categories.Count != 0) reminderRectangle.Offset(categoriesRectangleWidth + horizontalSpacing * 4, 0);

            bool selected = e.State.HasFlag(ListViewItemStates.Selected);
            Color back = Color.Empty;
            if (selected) back = Color.LightCyan;
            using (Brush br = new SolidBrush(back))
                e.Graphics.FillRectangle(br, totalRectangle);

            StringFormat rightFormat = new StringFormat();
            rightFormat.Alignment = StringAlignment.Far;
            rightFormat.LineAlignment = StringAlignment.Near;
            StringFormat leftFormat = new StringFormat();
            leftFormat.Alignment = StringAlignment.Near;
            leftFormat.LineAlignment = StringAlignment.Near;

            Brush colorBrush = new SolidBrush(this.ForeColor);

            if (catColors.Count != 0)
            {
                int catWidth = categoriesRectangle.Width / catColors.Count;
                // int catWidth = categoriesRectangle.Width / 3; // TODO: This looks nicer, but more than 3 won't fit
                Rectangle catRect = categoriesRectangle; catRect.Width = catWidth;
                // catColors.ForEach(cc =>
                catColors.Take(3).ToList().ForEach(cc =>
                {
                    // e.Graphics.FillEllipse(new SolidBrush(cc), catRect);
                    e.Graphics.FillRectangle(new SolidBrush(cc), catRect);
                    // e.Graphics.FillRectangle(new SolidBrush(cc), catRect.Left, catRect.Top, catWidth, this.FontHeight);
                    catRect.Offset(catWidth, 0);
                });

            }
            Font mainFont = new Font(this.Font, FontStyle.Bold);
            Font subFont = this.Font;
            if (task.Completed)
            {
                mainFont = new Font(this.Font, FontStyle.Bold | FontStyle.Strikeout);
                subFont = new Font(this.Font, FontStyle.Strikeout);
            }
            e.Graphics.DrawString(task.TaskSubject, mainFont, colorBrush, subjectRectangle, leftFormat);
            if (task.Reminder.Year != Constants.NullYear)
            {
                e.Graphics.DrawImage(Properties.Resources.Alert_16xSM, reminderRectangle.Left, reminderRectangle.Top);
                e.Graphics.DrawString(task.Reminder.ToString(), subFont, colorBrush, reminderRectangle.Left + Properties.Resources.Alert_16xSM.Width + horizontalSpacing, reminderRectangle.Top, leftFormat);
            }
            else if (task.StartDate.Year != Constants.NullYear)
            {
                e.Graphics.DrawImage(Properties.Resources.CurrentRow_15x14, reminderRectangle.Left, reminderRectangle.Top);
                e.Graphics.DrawString(task.StartDate.ToShortDateString(), subFont, colorBrush, reminderRectangle.Left + Properties.Resources.CurrentRow_15x14.Width + horizontalSpacing, reminderRectangle.Top, leftFormat);
            }
        }

        /// <summary>
        /// Method to custom draw the list items
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">DrawListViewItemEventArgs</param>
        private void lstAppointments_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawBackground(); // To avoid repainting (making font "grow")
            Outlook.AppointmentItem appt = e.Item.Tag as Outlook.AppointmentItem;

            // Color catColor = Color.Empty;
            List<Color> catColors = new List<Color>();

            Font itemFont = this.Font;
            if (!String.IsNullOrEmpty(appt.Categories))
            {
                string[] allCats = appt.Categories.Split(new char[] { ',' });
                if (allCats != null && allCats.Length != 0)
                {
                    List<string> cs = allCats.Select(cat => cat.Trim()).ToList();
                    cs.ForEach(cat =>
                    {
                        Outlook.Category c = Globals.ThisAddIn.Application.Session.Categories[cat] as Outlook.Category;
                        if (c != null)
                        {
                            catColors.Add(TranslateCategoryColor(c.Color));
                        }
                    });
                }
            }
            int startRectangleWidth = 65;
            int categoriesRectangleWidth = 55;
            int horizontalSpacing = 5;

            Rectangle totalRectangle = e.Bounds;
            Rectangle startRectangle = totalRectangle; startRectangle.Width = startRectangleWidth;
            Rectangle statusRectangle = totalRectangle; statusRectangle.Width = horizontalSpacing * 2; statusRectangle.Offset(startRectangleWidth + horizontalSpacing, 0);
            Rectangle subjectRectangle = totalRectangle; subjectRectangle.Height = this.FontHeight; subjectRectangle.Offset(startRectangleWidth + horizontalSpacing * 4, 0);
            Rectangle categoriesRectangle = totalRectangle; categoriesRectangle.Width = categoriesRectangleWidth; categoriesRectangle.Height = this.FontHeight; categoriesRectangle.Offset(10, this.FontHeight);
            Rectangle locationRectangle = totalRectangle; locationRectangle.Height = this.FontHeight; locationRectangle.Offset(startRectangleWidth + horizontalSpacing * 4, this.FontHeight);
            bool selected = e.State.HasFlag(ListViewItemStates.Selected);
            Color back = Color.Empty;
            if (selected) back = Color.LightCyan;
            using (Brush br = new SolidBrush(back))
                e.Graphics.FillRectangle(br, totalRectangle);

            StringFormat rightFormat = new StringFormat();
            rightFormat.Alignment = StringAlignment.Far;
            rightFormat.LineAlignment = StringAlignment.Near;
            StringFormat leftFormat = new StringFormat();
            leftFormat.Alignment = StringAlignment.Near;
            leftFormat.LineAlignment = StringAlignment.Near;

            Brush colorBrush = new SolidBrush(this.ForeColor);
            e.Graphics.DrawString(appt.Start.ToShortTimeString(), this.Font, colorBrush, startRectangle, rightFormat);

            Color statusColor = Color.LightBlue;
            Brush statusBrush = new SolidBrush(Color.Transparent);
            switch (appt.BusyStatus)
            {
                case Outlook.OlBusyStatus.olBusy:
                    statusBrush = new SolidBrush(statusColor);
                    break;
                case Outlook.OlBusyStatus.olFree:
                    break;
                case Outlook.OlBusyStatus.olOutOfOffice:
                    statusBrush = new SolidBrush(Color.Purple); // TODO: Figure this out
                    break;
                case Outlook.OlBusyStatus.olTentative:
                    statusBrush = new HatchBrush(HatchStyle.BackwardDiagonal, statusColor, this.BackColor);
                    break;
                case Outlook.OlBusyStatus.olWorkingElsewhere:
                    statusBrush = new HatchBrush(HatchStyle.DottedDiamond, statusColor, this.BackColor);
                    break;
                default:
                    break;
            }
            // Let's draw the status with a custom brush
            e.Graphics.FillRectangle(statusBrush, statusRectangle);
            e.Graphics.DrawRectangle(new Pen(statusColor), statusRectangle);

            if (catColors.Count != 0)
            {
                // int catWidth = categoriesRectangle.Width / catColors.Count;
                int catWidth = categoriesRectangle.Width / 3; // TODO: This looks nicer, but more than 3 won't fit
                Rectangle catRect = categoriesRectangle; catRect.Width = catWidth;
                // catColors.ForEach(cc =>
                catColors.Take(3).ToList().ForEach(cc =>
                {
                    e.Graphics.FillEllipse(new SolidBrush(cc), catRect);
                    catRect.Offset(catWidth, 0);
                });
                
            }

            //e.Graphics.FillRectangle(new SolidBrush(catColor), subjectRectangle);
            e.Graphics.DrawString(appt.Subject, new Font(this.Font, FontStyle.Bold), colorBrush, subjectRectangle, leftFormat);
            //e.Graphics.FillRectangle(new SolidBrush(catColor), locationRectangle);
            e.Graphics.DrawString(appt.Location, this.Font, colorBrush, locationRectangle, leftFormat);
        }

        /// <summary>
        /// The color scheme for Outlook is not the same as of Windows Forms, so this method tries to do a best effor in matching them
        /// </summary>
        /// <param name="col">Color of the category</param>
        /// <returns>Resulting color to be displayed</returns>
        private Color TranslateCategoryColor(Outlook.OlCategoryColor col)
        {
            Color result = Color.Black;
            switch (col)
            {
                case Outlook.OlCategoryColor.olCategoryColorNone:
                    // Nothing
                    break;
                case Outlook.OlCategoryColor.olCategoryColorRed:
                    result = Color.Red;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorOrange:
                    result = Color.Orange;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorPeach:
                    result = Color.PeachPuff;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorYellow:
                    result = Color.Yellow;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorGreen:
                    result = Color.Green;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorTeal:
                    result = Color.Teal;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorOlive:
                    result = Color.Olive;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorBlue:
                    result = Color.Blue;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorPurple:
                    result = Color.Purple;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorMaroon:
                    result = Color.Maroon;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorSteel:
                    result = Color.LightSteelBlue;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkSteel:
                    result = Color.SteelBlue;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorGray:
                    result = Color.Gray;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkGray:
                    result = Color.DarkGray;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorBlack:
                    result = Color.Black;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkRed:
                    result = Color.DarkRed;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkOrange:
                    result = Color.DarkOrange;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkPeach:
                    result = Color.DarkSalmon;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkYellow:
                    result = Color.DarkGoldenrod;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkGreen:
                    result = Color.DarkGreen;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkTeal:
                    result = Color.DarkCyan;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkOlive:
                    result = Color.DarkOliveGreen;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkBlue:
                    result = Color.DarkBlue;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkPurple:
                    result = Color.DarkViolet;
                    break;
                case Outlook.OlCategoryColor.olCategoryColorDarkMaroon:
                    result = Color.DarkKhaki;
                    break;
                default:
                    break;
            }
            return result;
        }

        /// <summary>
        /// Save the splitter distance to restore upon reloading the plugin
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">SplitterEventArgs</param>
        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            Properties.Settings.Default.SplitterDistance = this.splitContainer1.SplitterDistance;
        }

        #endregion "Methods"
    }
}