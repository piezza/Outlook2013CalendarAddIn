﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Outlook = Microsoft.Office.Interop.Outlook;
using System.Globalization;

namespace Outlook2013TodoAddIn
{
    /// <summary>
    /// Custom calendar control with more flexibility and better skinning control
    /// </summary>
    public partial class CustomCalendar : UserControl
    {
        #region "Variables"

        /// <summary>
        /// Used to keep track of the currently selected Date
        /// </summary>
        DateTime _selectedDate = DateTime.Today;

        #endregion "Variables"

        #region "Properties"

        /// <summary>
        /// Gets/sets the current date
        /// </summary>
        public DateTime SelectedDate
        {
            get
            {
                return _selectedDate;
            }
            set
            {
                // Check if the day is the same we're trying to set
                //if (_selectedDate != value)
                //{
                // Commented out, because if the user clicks on the TODAY link to refresh today's view it wouldn't work
                    _selectedDate = value;
                    // this.UpdateCalendar();
                    this.OnSelectedDateChanged(EventArgs.Empty);
                //}
            }
        }

        /// <summary>
        /// Gets/sets the first day of week
        /// </summary>
        public DayOfWeek FirstDayOfWeek { get; set; }

        /// <summary>
        /// Set of dates to display in BOLD
        /// </summary>
        public DateTime[] BoldedDates { get; set; }

        /// <summary>
        /// Gets/sets font color of dates for current month
        /// </summary>
        public Color CurrentMonthForeColor { get; set; }

        /// <summary>
        /// Gets/sets font color of dates for other months (previous/next)
        /// </summary>
        public Color OtherMonthForeColor { get; set; }

        /// <summary>
        /// Gets/sets font color for today's date
        /// </summary>
        public Color TodayForeColor { get; set; }

        /// <summary>
        /// Gets/sets back color for today's date
        /// </summary>
        public Color TodayBackColor { get; set; }

        /// <summary>
        /// Gets/sets font color for selected's date
        /// </summary>
        public Color SelectedForeColor { get; set; }

        /// <summary>
        /// Gets/sets back color for selected's date
        /// </summary>
        public Color SelectedBackColor { get; set; }

        /// <summary>
        /// Gets/sets font color when mouse over a cell
        /// </summary>
        public Color HoverForeColor { get; set; }

        /// <summary>
        /// Gets/sets back color when mouse over a cell
        /// </summary>
        public Color HoverBackColor { get; set; }

        /// <summary>
        /// Gets/sets whether to show week numbers
        /// </summary>
        public bool ShowWeekNumbers { get; set; }

        #endregion "Properties"

        #region "Methods"

        /// <summary>
        /// Default constructor
        /// </summary>
        public CustomCalendar()
        {
            InitializeComponent();
            this.FirstDayOfWeek = DayOfWeek.Sunday;
            //TODO: Thread.CurrentThread.CurrentCulture.DateTimeFormat.FirstDayOfWeek
            this.CurrentMonthForeColor = Color.Black;
            this.OtherMonthForeColor = Color.LightGray;
            this.TodayForeColor = Color.White;
            this.TodayBackColor = Color.Blue;

            this.SelectedForeColor = Color.Blue;
            this.SelectedBackColor = Color.LightBlue;

            this.HoverForeColor = Color.Black;
            this.HoverBackColor = Color.LightCyan;
            this.CreateTableControls();
        }

        /// <summary>
        /// Paint the month upon initial load
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void CustomCalendar_Load(object sender, EventArgs e)
        {
            // TODO: check initial double firing
            // TODO: Mark dates with colors from outlook
            // this.UpdateCalendar();
        }

        /// <summary>
        /// Creates all inner labels once to be updated when the calendar changes
        /// </summary>
        private void CreateTableControls()
        {
            this.tableLayoutPanel1.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
            for (int row = 0; row < this.tableLayoutPanel1.RowCount; row++)
            {
                for (int col = 0; col < this.tableLayoutPanel1.ColumnCount; col++)
                {
                    Label lblCtrl = new Label() { Text = "xx" };
                    lblCtrl.Name = String.Format("lbl_{0}_{1}", col.ToString(), row.ToString());
                    lblCtrl.Dock = DockStyle.Fill;
                    lblCtrl.TextAlign = ContentAlignment.MiddleCenter;
                    lblCtrl.Margin = Padding.Empty;
                    lblCtrl.Padding = Padding.Empty;
                    lblCtrl.FlatStyle = FlatStyle.Flat;
                    if (row != 0)
                    {
                        lblCtrl.MouseEnter += lblCtrl_MouseEnter;
                        lblCtrl.MouseLeave += lblCtrl_MouseLeave;
                        lblCtrl.Click += lblCtrl_Click; // TODO: If we disable this, then we can't select a date...
                        lblCtrl.DoubleClick += lblCtrl_DoubleClick;
                        lblCtrl.AllowDrop = true;
                        lblCtrl.DragEnter += lblCtrl_DragEnter;
                        lblCtrl.DragDrop += lblCtrl_DragDrop;
                    }
                    this.tableLayoutPanel1.Controls.Add(lblCtrl);
                    this.tableLayoutPanel1.SetCellPosition(lblCtrl, new TableLayoutPanelCellPosition(col, row));
                }
            }

            // This piece controls the week numbers
            this.tableLayoutPanel2.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
            for (int row = 0; row < this.tableLayoutPanel2.RowCount; row++)
            {
                Label lblCtrl = new Label() { Text = "#" };
                lblCtrl.Name = String.Format("lbl_{0}", row.ToString());
                lblCtrl.Dock = DockStyle.Fill;
                lblCtrl.TextAlign = ContentAlignment.MiddleRight;
                lblCtrl.Margin = Padding.Empty;
                lblCtrl.Padding = Padding.Empty;
                lblCtrl.Font = new Font(this.Font.FontFamily, this.Font.Size-2, FontStyle.Italic);
                lblCtrl.FlatStyle = FlatStyle.Flat;
                this.tableLayoutPanel2.Controls.Add(lblCtrl);
                this.tableLayoutPanel2.SetCellPosition(lblCtrl, new TableLayoutPanelCellPosition(0, row));
            }

            this.btnPrevious.FlatAppearance.MouseOverBackColor = this.HoverBackColor;
            this.btnNext.FlatAppearance.MouseOverBackColor = this.HoverBackColor;
            this.btnConfig.FlatAppearance.MouseOverBackColor = this.HoverBackColor;
        }

        /// <summary>
        /// To enable/disable drop operations on every day of the calendar
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">DragEventArgs</param>
        private void lblCtrl_DragEnter(object sender, DragEventArgs e)
        {
            List<string> outlookRequiredFormats = new List<string>() { "RenPrivateSourceFolder", "RenPrivateMessages", "RenPrivateItem", "FileGroupDescriptor", "FileGroupDescriptorW", "FileContents", "Object Descriptor" };
            if (outlookRequiredFormats.All(r => e.Data.GetDataPresent(r)))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        /// <summary>
        /// When the user "drops" one or more email items into the calendar
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">DragEventArgs</param>
        private void lblCtrl_DragDrop(object sender, DragEventArgs e)
        {
            Label lblDay = sender as Label;
            if (sender != null)
            {
                Outlook.Explorer mailExpl = Globals.ThisAddIn.Application.ActiveExplorer();
                List<string> attendees = new List<string>();
                string curUserAddress = OutlookHelper.GetEmailAddress(Globals.ThisAddIn.Application.Session.CurrentUser);
                string body = String.Empty;
                string subject = String.Empty;
                foreach (object obj in mailExpl.Selection)
                {
                    Outlook.MailItem mail = obj as Outlook.MailItem;
                    if (mail != null)
                    {
                        subject = mail.Subject;
                        body = mail.Body;
                        if (mail.SenderEmailAddress != curUserAddress && !attendees.Contains(mail.SenderEmailAddress))
                        {
                            attendees.Add(mail.SenderEmailAddress);
                        }
                        attendees.AddRange(OutlookHelper.GetRecipentsEmailAddresses(mail.Recipients, curUserAddress));
                    }
                    else // It's not an email, let's see if it's a meeting instead
                    {
                        Outlook.MeetingItem meeting = obj as Outlook.MeetingItem;
                        if (meeting != null)
                        {
                            subject = meeting.Subject;
                            body = meeting.Body;
                            if (meeting.SenderEmailAddress != curUserAddress && !attendees.Contains(meeting.SenderEmailAddress))
                            {
                                attendees.Add(meeting.SenderEmailAddress);
                            }
                            attendees.AddRange(OutlookHelper.GetRecipentsEmailAddresses(meeting.Recipients, curUserAddress));
                        }
                        else // It wasn't a meeting either, let's try with an appointment
                        {
                            Outlook.AppointmentItem appointment = obj as Outlook.AppointmentItem;
                            if (appointment != null)
                            {
                                subject = appointment.Subject;
                                body = appointment.Body;
                                if (appointment.Organizer != curUserAddress && !attendees.Contains(appointment.Organizer))
                                {
                                    attendees.Add(appointment.Organizer);
                                }
                                attendees.AddRange(OutlookHelper.GetRecipentsEmailAddresses(appointment.Recipients, curUserAddress));
                            }
                        }
                    }
                }
                Outlook.AppointmentItem appt = Globals.ThisAddIn.Application.CreateItem(Outlook.OlItemType.olAppointmentItem) as Outlook.AppointmentItem;
                attendees.ForEach(a => appt.Recipients.Add(a));
                appt.Body = Environment.NewLine + Environment.NewLine + body;
                appt.Subject = subject;
                DateTime day = (DateTime)lblDay.Tag;
                DateTime now = DateTime.Now;
                appt.Start = OutlookHelper.RoundUp(new DateTime(day.Year, day.Month, day.Day, now.Hour, now.Minute, now.Second), TimeSpan.FromMinutes(15));
                appt.Display();
            }
        }

        /// <summary>
        /// Changes labels according to the currently displayed month
        /// </summary>
        public void UpdateCalendar()
        {
            // All controls are previously created, just need to update labels, etc...
            this.lnkCurrentRange.Text = this.SelectedDate.ToString("MMM yyyy");
            this.lnkToday.Text = Constants.Today + ": " + DateTime.Today.ToShortDateString();

            string[] daysOfWeek = Enum.GetNames(typeof(DayOfWeek));
            string sFirstDayOfWeek = Enum.GetName(typeof(DayOfWeek), this.FirstDayOfWeek);
            List<string> sortedDays = new List<string>();
            sortedDays.AddRange(daysOfWeek.SkipWhile(ow => ow != sFirstDayOfWeek));
            sortedDays.AddRange(daysOfWeek.TakeWhile(ow => ow != sFirstDayOfWeek));

            int dayCurrent = 0;
            int firstIndex = 0;
            DateTime firstOfCurrentMonth = new DateTime(this.SelectedDate.Year, this.SelectedDate.Month, 1);
            DateTime previousMonth = firstOfCurrentMonth.AddMonths(-1);
            DateTime nextMonth = firstOfCurrentMonth.AddMonths(1);
            int daysInPreviousMonth = DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month);
            int daysInCurrentMonth = DateTime.DaysInMonth(this.SelectedDate.Year, this.SelectedDate.Month);

            for (int col = 0; col < this.tableLayoutPanel1.ColumnCount; col++)
            {
                if (sortedDays[col] == Enum.GetName(typeof(DayOfWeek), firstOfCurrentMonth.DayOfWeek))
                {
                    firstIndex = col; // Get the position of day 1 of the current month
                }
                Label lblDay = this.tableLayoutPanel1.GetControlFromPosition(col, 0) as Label;
                lblDay.Text = sortedDays[col].Substring(0, 2).ToUpper();
            }

            dayCurrent = daysInPreviousMonth - firstIndex + 1;
            if (dayCurrent > daysInPreviousMonth)
            {
                dayCurrent = daysInPreviousMonth - 6;
            }
            bool previousMonthVisible = (dayCurrent != 1);
            bool nextMonthVisible = false;

            if (this.ShowWeekNumbers)
            {
                this.tableLayoutPanel1.Left = this.tableLayoutPanel2.Left + this.tableLayoutPanel2.Width;
                this.tableLayoutPanel1.Width = this.btnNext.Left - this.btnPrevious.Left - this.tableLayoutPanel2.Width + this.btnNext.Width;
                this.tableLayoutPanel2.Visible = true;
            }
            else
            {
                this.tableLayoutPanel1.Left = this.btnPrevious.Left;
                this.tableLayoutPanel1.Width = this.btnNext.Left - this.btnPrevious.Left + this.btnNext.Width;
                this.tableLayoutPanel2.Visible = false;
            }            

            // Row 0 is for days of week
            for (int row = 1; row < this.tableLayoutPanel1.RowCount; row++)
            {
                if (this.ShowWeekNumbers)
                {
                    Label lblDoW = this.tableLayoutPanel2.GetControlFromPosition(0, row) as Label;
                    DateTime dateForWeek;
                    if (previousMonthVisible)
                    {
                        dateForWeek = new DateTime(previousMonth.Year, previousMonth.Month, dayCurrent);
                    }
                    else if (nextMonthVisible)
                    {
                        dateForWeek = new DateTime(nextMonth.Year, nextMonth.Month, dayCurrent);
                    }
                    else
                    {
                        dateForWeek = new DateTime(this.SelectedDate.Year, this.SelectedDate.Month, dayCurrent);
                    }
                    lblDoW.Text = GetWeekForDate(dateForWeek).ToString();
                }
                
                for (int col = 0; col < this.tableLayoutPanel1.ColumnCount; col++)
                {
                    Label lblCtrl = this.tableLayoutPanel1.GetControlFromPosition(col, row) as Label;
                    lblCtrl.Text = dayCurrent.ToString();

                    DateTime embeddedDate;
                    Font displayFont;
                    Color foreColor;
                    Color backColor = this.BackColor;
                    BorderStyle borderStyle = BorderStyle.None;

                    // Customize the days
                    if (previousMonthVisible)
                    {
                        embeddedDate = new DateTime(previousMonth.Year, previousMonth.Month, dayCurrent);
                        displayFont = this.Font;
                        foreColor = this.OtherMonthForeColor;
                    }
                    else if (nextMonthVisible)
                    {
                        embeddedDate = new DateTime(nextMonth.Year, nextMonth.Month, dayCurrent);
                        displayFont = this.Font;
                        foreColor = this.OtherMonthForeColor;
                    }
                    else // Current month
                    {
                        embeddedDate = new DateTime(this.SelectedDate.Year, this.SelectedDate.Month, dayCurrent);
                        displayFont = this.Font;
                        foreColor = this.CurrentMonthForeColor;
                    }

                    if (this.BoldedDates != null && this.BoldedDates.Contains(embeddedDate))
                    {
                        displayFont = new Font(this.Font, FontStyle.Bold);
                    }

                    if (embeddedDate == DateTime.Today)
                    {
                        foreColor = this.TodayForeColor;
                        backColor = this.TodayBackColor;
                    }
                    else if (embeddedDate == this.SelectedDate)
                    {
                        borderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                        foreColor = this.SelectedForeColor;
                        backColor = this.SelectedBackColor;
                    }

                    lblCtrl.Tag = embeddedDate;
                    lblCtrl.Font = displayFont;
                    lblCtrl.ForeColor = foreColor;
                    lblCtrl.BackColor = backColor;
                    lblCtrl.BorderStyle = borderStyle;

                    dayCurrent++;

                    if (previousMonthVisible && dayCurrent > daysInPreviousMonth)
                    {
                        dayCurrent = 1; // Start current month
                        previousMonthVisible = false;
                    }
                    if (!previousMonthVisible && dayCurrent > daysInCurrentMonth)
                    {
                        dayCurrent = 1; // Start next month
                        nextMonthVisible = true;
                    }
                }
            }
        }

        private int GetWeekForDate(DateTime time)
        {
            // Add 6 days since we want the date at the "last" column of the calendar to be more precise
            int weekNumber = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time.AddDays(6), CalendarWeekRule.FirstDay, this.FirstDayOfWeek);
            return weekNumber;
        } 

        /// <summary>
        /// Returns to the previous month
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void btnPrevious_Click(object sender, EventArgs e)
        {
            this.SelectedDate = this.SelectedDate.AddMonths(-1);
            //this.UpdateCalendar();
        }

        /// <summary>
        /// Advances to the next month
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void btnNext_Click(object sender, EventArgs e)
        {
            this.SelectedDate = this.SelectedDate.AddMonths(1);
            //this.UpdateCalendar();
        }

        /// <summary>
        /// Change background color of the label
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void lblCtrl_MouseEnter(object sender, EventArgs e)
        {
            Label lblCtrl = sender as Label;
            DateTime curDate = (DateTime)lblCtrl.Tag;
            if (curDate.Month == this.SelectedDate.Month && curDate.Year == this.SelectedDate.Year)
            {
                lblCtrl.ForeColor = this.HoverForeColor;
            }
            else
            {
                lblCtrl.ForeColor = this.OtherMonthForeColor;
            }
            //lblCtrl.ForeColor = this.HoverForeColor;
            lblCtrl.BackColor = this.HoverBackColor;
        }

        /// <summary>
        /// Change background color of the label
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void lblCtrl_MouseLeave(object sender, EventArgs e)
        {
            Label lblCtrl = sender as Label;
            DateTime curDate = (DateTime)lblCtrl.Tag;
            if (curDate == DateTime.Today)
            {
                lblCtrl.ForeColor = this.TodayForeColor;
                lblCtrl.BackColor = this.TodayBackColor;
            }
            else if (curDate == this.SelectedDate)
            {
                lblCtrl.ForeColor = this.SelectedForeColor;
                lblCtrl.BackColor = this.SelectedBackColor;
            }
            else
            {
                if (curDate.Month == this.SelectedDate.Month && curDate.Year == this.SelectedDate.Year)
                {
                    lblCtrl.ForeColor = this.CurrentMonthForeColor;
                }
                else
                {
                    lblCtrl.ForeColor = this.OtherMonthForeColor;
                }
                lblCtrl.BackColor = this.BackColor;
            }
        }

        /// <summary>
        /// Sets the currently selected day
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void lblCtrl_Click(object sender, EventArgs e)
        {
            this.SelectedDate = (DateTime)(sender as Label).Tag;
            //this.UpdateCalendar();
        }

        /// <summary>
        /// Event handler to subscribe to
        /// </summary>
        public event EventHandler CellDoubleClick;

        /// <summary>
        /// Fires the attached event handler
        /// </summary>
        /// <param name="e">EventArgs</param>
        protected virtual void OnCellDoubleClick(EventArgs e)
        {
            EventHandler handler = CellDoubleClick;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        /// <summary>
        /// Fires the double-click event on any given day label
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void lblCtrl_DoubleClick(object sender, EventArgs e)
        {
            this.SelectedDate = (DateTime)(sender as Label).Tag;
            OnCellDoubleClick(EventArgs.Empty);
        }

        /// <summary>
        /// Event handler to subscribe to
        /// </summary>
        public event EventHandler SelectedDateChanged;

        /// <summary>
        /// Fires the attached event handler
        /// </summary>
        /// <param name="e">EventArgs</param>
        protected virtual void OnSelectedDateChanged(EventArgs e)
        {
            EventHandler handler = SelectedDateChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        /// <summary>
        /// Event handler to subscribe to
        /// </summary>
        public event EventHandler ConfigurationButtonClicked;

        private void btnConfig_Click(object sender, EventArgs e)
        {
            EventHandler handler = ConfigurationButtonClicked;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        /// <summary>
        /// Select today's date in the calendar
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">LinkLabelLinkClickedEventArgs</param>
        private void lnkToday_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.SelectedDate = DateTime.Today;
            //this.UpdateCalendar();
        }

        #endregion "Methods"
    }
}