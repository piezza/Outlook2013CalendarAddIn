﻿using Microsoft.Win32;
using Outlook2013TodoAddIn.Forms;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Office = Microsoft.Office.Core;

namespace Outlook2013TodoAddIn
{
    /// <summary>
    /// Class for the add-in
    /// </summary>
    public partial class ThisAddIn
    {
        #region "Properties"

        /// <summary>
        /// Control with calendar, etc...
        /// </summary>
        public AppointmentsControl AppControl { get; set; }

        /// <summary>
        /// Custom task pane
        /// </summary>
        public Microsoft.Office.Tools.CustomTaskPane ToDoTaskPane { get; set; }

        #endregion "Properties"

        #region "Methods"

        /// <summary>
        /// Initialize settings upon add-in startup
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            try
            {

                if (Properties.Settings.Default.CallUpgrade)
                {
                    Properties.Settings.Default.Upgrade();
                    Properties.Settings.Default.CallUpgrade = false;
                }

                Globals.ThisAddIn.Application.NewMailEx += Application_NewMailEx;
                this.AddRegistryNotification();

                this.AppControl = new AppointmentsControl();
                this.AppControl.MailAlertsEnabled = Properties.Settings.Default.MailAlertsEnabled;
                this.AppControl.ShowPastAppointments = Properties.Settings.Default.ShowPastAppointments;
                this.AppControl.Accounts = Properties.Settings.Default.Accounts;
                this.AppControl.ShowFriendlyGroupHeaders = Properties.Settings.Default.ShowFriendlyGroupHeaders;
                this.AppControl.ShowDayNames = Properties.Settings.Default.ShowDayNames;
                this.AppControl.ShowWeekNumbers = Properties.Settings.Default.ShowWeekNumbers;
                this.AppControl.ShowTasks = Properties.Settings.Default.ShowTasks;
                this.AppControl.ShowCompletedTasks = Properties.Settings.Default.ShowCompletedTasks;
                this.AppControl.FirstDayOfWeek = Properties.Settings.Default.FirstDayOfWeek;
                this.AppControl.NumDays = Properties.Settings.Default.NumDays; // Setting the value will load the appointments

                ToDoTaskPane = this.CustomTaskPanes.Add(this.AppControl, "Appointments");
                ToDoTaskPane.Visible = Properties.Settings.Default.Visible;
                ToDoTaskPane.Width = Properties.Settings.Default.Width;
                ToDoTaskPane.DockPosition = Office.MsoCTPDockPosition.msoCTPDockPositionRight;
                ToDoTaskPane.DockPositionRestrict = Office.MsoCTPDockPositionRestrict.msoCTPDockPositionRestrictNoHorizontal;
                ToDoTaskPane.VisibleChanged += ToDoTaskPane_VisibleChanged;
                this.AppControl.SizeChanged += appControl_SizeChanged;

                // Selecting the date will retrieve the appointments
                // Otherwise it'll take the one used when the designer changed!
                this.AppControl.SelectedDate = DateTime.Today;
                // this.AppControl.RetrieveData();

                Globals.ThisAddIn.Application.ActiveExplorer().Deactivate += ThisAddIn_Deactivate;

                // TODO: Make sure there are no memory leaks (dispose COM objects)
            }
            catch (Exception exc)
            {
                MessageBox.Show(String.Format("Error starting Calendar AddIn: {0}", exc.ToString()));
                throw exc;
            }
        }

        /// <summary>
        /// Process new email
        /// </summary>
        /// <param name="EntryIDCollection">ID of the email</param>
        private void Application_NewMailEx(string EntryIDCollection)
        {
            if (Properties.Settings.Default.MailAlertsEnabled)
            {

                Microsoft.Office.Interop.Outlook.MailItem newMail = Globals.ThisAddIn.Application.Session.GetItemFromID(EntryIDCollection) as Microsoft.Office.Interop.Outlook.MailItem;
                if (newMail != null)
                {
                    NewMailAlert nm = new NewMailAlert(newMail, Properties.Settings.Default.DisplayTimeOut);
                    // Show the popup without stealing focus
                    SoundHelper.sndPlaySoundW(SoundHelper.MailBeep, SoundHelper.SND_NODEFAULT);
                    nm.Show();
                }
            }
        }

        /// <summary>
        /// Store the new size setting upon resizing
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void appControl_SizeChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Width = ToDoTaskPane.Width;
        }

        /// <summary>
        /// Toggle ribbon button's status
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void ToDoTaskPane_VisibleChanged(object sender, EventArgs e)
        {
            // Can't save visibility here because this fires when closing Outlook, and by that time the pane is ALWAYS not visible
            // Properties.Settings.Default.Visible = ToDoTaskPane.Visible;
            TodoRibbonAddIn rbn = Globals.Ribbons.FirstOrDefault(r => r is TodoRibbonAddIn) as TodoRibbonAddIn;
            if (rbn != null)
            {
                rbn.toggleButton1.Checked = ToDoTaskPane.Visible;
            }
        }

        /// <summary>
        /// This is the alternative to capture the visibility of the pane when shutting down Outlook
        /// </summary>
        private void ThisAddIn_Deactivate()
        {
            Properties.Settings.Default.Visible = ToDoTaskPane.Visible;
        }

        /// <summary>
        /// This is not executed by default
        /// http://msdn.microsoft.com/en-us/library/office/ee720183.aspx#OL2010AdditionalShutdownChanges_AddinShutdownChangesinOL2010Beta
        /// We MANUALLY add notification to the registry of each user below
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private void ThisAddIn_Shutdown(object sender, System.EventArgs e)
        {
            // Can't call property setters such as: Properties.Settings.Default.NumDays = XXX because the pane is already disposed.
            // Settings will be set while the app is running and saved here.
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Implement shutdown notification for this particular add-in
        /// http://msdn.microsoft.com/en-us/library/office/ee720183.aspx#OL2010AdditionalShutdownChanges_AddinShutdownChangesinOL2010Beta
        /// HKEY_CURRENT_USER\Software\Microsoft\Office\Outlook\Addins\<ProgID>\[RequireShutdownNotification]=dword:0x1
        /// </summary>
        private void AddRegistryNotification()
        {
            // If the entry is not there when Outlook loads, it will NOT notify the add-in, so the first time won't save the results
            string subKey = @"Software\Microsoft\Office\Outlook\Addins\Outlook2013TodoAddIn";
            RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(subKey, true);
            if (rk == null)
            {
                rk = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(subKey);
            }
            if ((int)rk.GetValue("RequireShutdownNotification", 0) == 0)
            {
                rk.SetValue("RequireShutdownNotification", 1, RegistryValueKind.DWord); // "dword:0x1"
            }
        }

        #endregion "Methods"

        #region VSTO generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InternalStartup()
        {
            this.Startup += new System.EventHandler(ThisAddIn_Startup);
            this.Shutdown += new System.EventHandler(ThisAddIn_Shutdown);
        }

        #endregion VSTO generated code
    }
}