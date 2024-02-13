using RansomGuard.Properties;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RansomGuard.Helpers;
using System.Web.Services.Protocols;

namespace RansomGuard
{
    class RamsomGuardAppContext : ApplicationContext  
    {
        NotifyIcon notifyIcon;

        //menu dialogs
        FileExtsDialog fileDlg = null;
        Preferences prefDlg = null;
        AboutDialog aboutDlg = null;
        FolderLockDialog flockDlg = null;
        PasswordDialog pwDlg = null;
        NetPreferences netPrefDlg = null;

        //file utils
        ShredderForm shredderDlg = null;
        TrashBinDialog trashBinDlg = null;
        CryptForm cryptDlg = null;
        SafecaseForm safecaseDlg = null;
        SafecaseSetForm safecaseSetDlg = null;

        const string defaultTooltip = "Ransom Guard";

        public RamsomGuardAppContext()
        {
            initializeContext();
        }

        private void initializeContext()
        {
            initializeNotifyIcon();

            Program.lm.AddLog(LogData.LogType.ProgramStart);

            LangManger.SetLanguage(Properties.Settings.Default.LangSet);

            //update the rgupdate file.
            Utilities.UpdateRgUpdater(new Version(Program.rgUpdaterVer), Program.rgUpdaterName);

            //init shell context menu
            Program.cm.initShellContextMenu();

            //open safecase if it's needed when start with the program, if no file found, remove the settings.
            string sfPath = Properties.Settings.Default.SafeCaseImage;
            if (!string.IsNullOrEmpty(sfPath))
            {
                if (File.Exists(sfPath))
                    Task.Factory.StartNew(() => Program.sm.AttachSafeCase(sfPath, null, false));
                else
                    Properties.Settings.Default.SafeCaseImage = "";
            }
        }

        private void initializeNotifyIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            notifyIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            notifyIcon.Text = defaultTooltip;
            notifyIcon.Visible = true;

            notifyIcon.ContextMenuStrip.Opening += new CancelEventHandler(onContextMenuStripOpening);
            notifyIcon.Click += new EventHandler(onNotifyIconClick);
        }

        public NotifyIcon GetNotifyIcon()
        {
            return notifyIcon;
        }

        //****************************************************************
        // tray icon menu
        //****************************************************************

        void onContextMenuStripOpening(object sender, CancelEventArgs e)
        {
            RgDebug.WriteLine(RgDebug.DebugType.TrayMenu, "onContextMenuStripOpening Start");

            //set menu context
            bool isFolderLockSet = Properties.Settings.Default.FolderLockSet;
            bool isSafeCaseOpened = Program.sm.isSafeCaseAttached();

            notifyIcon.ContextMenuStrip.Items.Clear();

            //folder lock or unlock
            if (isFolderLockSet)
            {
                notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.FolderLockFree, Properties.Resources.unLock, onLockUnLockMenuClick);
            }
            else
            {
                notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.FolderLockSet, Properties.Resources.folderLock, onLockUnLockMenuClick);
            }

            //file extensions
            notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.ProtectFileExt, Properties.Resources.exts, onSetExtsMenuClick);

            //spyware
            notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.MenuSpyware, Properties.Resources.spyware, onSpywareMenuClick);

            //Show safecase menu
            if (isSafeCaseOpened)
            {
                var safeCase = new ToolStripMenuItem(Properties.Resources.MenuSafeCaseInUse, Properties.Resources.safecase);
                safeCase.DropDownItems.Add(Properties.Resources.MenuSafeCaseOpen, null, onSafeCaseFolderOpenClick);
                safeCase.DropDownItems.Add(Properties.Resources.MenuSafeCaseSet, null, onSafeCaseSettingsClick);
                safeCase.DropDownItems.Add(Properties.Resources.MenuSafeCaseLog, null, null).Enabled = false;
                safeCase.DropDownItems.Add(Properties.Resources.MenuSafeCaseClose, null, onSafeCaseCloseClick);
                notifyIcon.ContextMenuStrip.Items.Add(safeCase);
            }
            else
            {
                var safeCase = new ToolStripMenuItem(Properties.Resources.MenuSafeCase, Properties.Resources.safecase);
                safeCase.DropDownItems.Add(Properties.Resources.MenuSafeCaseOpen, null, onSafeCaseOpenClick);
                safeCase.DropDownItems.Add(Properties.Resources.MenuSafeCaseCreate, null, onSafeCaseCreateClick);
                notifyIcon.ContextMenuStrip.Items.Add(safeCase);
            }

            //file utilities
            var utilsMenu = new ToolStripMenuItem(Properties.Resources.MenuUtils, Properties.Resources.utils);
            utilsMenu.DropDownItems.Add(Properties.Resources.MenuUtilsShredder, Properties.Resources.shredder, onShredderMenuClick);
            utilsMenu.DropDownItems.Add(Properties.Resources.MenuUtilsTrashBin, Properties.Resources.trashBin, onTrashBinMenuClick);
            utilsMenu.DropDownItems.Add(Properties.Resources.MenuUtilsCrypt, Properties.Resources.encryption, onCryptMenuClick);
            utilsMenu.DropDownItems.Add(Properties.Resources.MenuUtilsBackup, Properties.Resources.backup, null).Enabled = false;
            notifyIcon.ContextMenuStrip.Items.Add(utilsMenu);

            //logs
            notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.MenuLogs, Properties.Resources.logs, onLogMenuClick);

            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

            //preferences
            notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.MenuPreferences, Properties.Resources.settings, onPreferencesMenuClick);

            //reset
            if (!isFolderLockSet)
            {
                notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.ResetSettings, Properties.Resources.reset, onResetClick);
            }

            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

            //help
            notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.MenuHelp, Properties.Resources.help, onHelpMenuClick);

            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

            //about
            notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.MenuAbout, Properties.Resources.info, onAboutMenuClick);

            //exit
            if(!isFolderLockSet)
            {
                notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.MenuExit, Properties.Resources.exit, onExitMenuClick);
            }

            RgDebug.WriteLine(RgDebug.DebugType.TrayMenu, "Create end");

            e.Cancel = false;
        }


        //****************************************************************
        // Reset Data to default values
        //****************************************************************
        void dataReset()
        {
            Program.pm.Reset();
            Program.nm.Reset();
            Program.sm.Reset();

            //file extension data
            Properties.Settings.Default.AddedExts = "";

            //folder lock data
            Properties.Settings.Default.FolderLockPath = "";
            Properties.Settings.Default.LockPassword = "";
            Properties.Settings.Default.UsePassword = false;
            Properties.Settings.Default.FolderLockSet = false;

            //Net and pr data
            Properties.Settings.Default.ProcessList = "";
            Properties.Settings.Default.NetList = "";
            Properties.Settings.Default.NetShowTrustedPr = false;

            //pref
            Properties.Settings.Default.LangSet = 0;
            Properties.Settings.Default.WarningLevel = 0;
            Properties.Settings.Default.SpyWarningLevel = 0;

            Properties.Settings.Default.Save();
        }

        //****************************************************************
        // Menu event handlers
        //****************************************************************
        void onNotifyIconClick(object sender, EventArgs e)
        {
            if ((e as MouseEventArgs).Button == MouseButtons.Right)
            {
                if (notifyIcon.ContextMenuStrip != null)
                {
                    notifyIcon.ContextMenuStrip.Show(Cursor.Position);
                }
            }
        }

        private void onLockUnLockMenuClick(object sender, EventArgs e)
        {
            RgDebug.WriteLine(RgDebug.DebugType.TrayMenu, "onLockUnLockMenuClick");

            // set folder lock menu text
            if (Properties.Settings.Default.FolderLockSet)
            {
                //if user didn't use password
                if(Properties.Settings.Default.UsePassword == false)
                {
                    Properties.Settings.Default.FolderLockSet = false;
                    Properties.Settings.Default.Save();

                    if (!Program.fm.Start())
                    {
                        MessageBox.Show(Properties.Resources.FilterConnectionError);
                        return;
                    }

                    Program.lm.AddLog(LogData.LogType.ReleaseFolderLock);
                    MessageBox.Show(Properties.Resources.FolderLockFreeMsg);
                }
                else
                {
                    if (pwDlg == null)
                    {
                        pwDlg = new PasswordDialog();
                        pwDlg.ShowDialog();
                        pwDlg = null;
                    }
                }
            }
            else
            {
                if (flockDlg == null)
                {
                    flockDlg = new FolderLockDialog();
                    flockDlg.ShowDialog();
                    flockDlg = null;
                }    
            }
        }

        private void onAboutMenuClick(object sender, EventArgs e)
        {
            RgDebug.WriteLine(RgDebug.DebugType.TrayMenu, "onAboutMenuClick");

            if(aboutDlg == null)
            {
                aboutDlg = new AboutDialog();
                aboutDlg.ShowDialog();
                aboutDlg = null;
            }
        }

        private void onHelpMenuClick(object sender, EventArgs e)
        {
            RgDebug.WriteLine(RgDebug.DebugType.TrayMenu, "onHelpMenuClick");

            System.Diagnostics.Process.Start(Program.rgWebHelpLink);
        }

        private void onPreferencesMenuClick(object sender, EventArgs e)
        {
            RgDebug.WriteLine(RgDebug.DebugType.TrayMenu, "onPreferencesMenuClick");

            if (prefDlg == null)
            {
                prefDlg = new Preferences();
                prefDlg.ShowDialog();
                prefDlg = null;
            }
        }

        private void onLogMenuClick(object sender, EventArgs e)
        {
            RgDebug.WriteLine(RgDebug.DebugType.TrayMenu, "onLogMenuClick - thread");

            //create thread for the reason of delay.
            Task.Factory.StartNew(() => loadLog());
        }

        private void loadLog()
        {
            const string viewerFile = "rgLogs.html";

            string viewerPath = Directory.GetParent(
                Directory.GetParent(Program.logRootPath).FullName).FullName;

            string path = Path.Combine(viewerPath, viewerFile);
            File.WriteAllBytes(path, Properties.Resources.rgLogs);

            //replace text
            Program.lm.ReplaceTexts(path);

            //run browser
            System.Diagnostics.Process.Start(path);
        }

        private void onSetExtsMenuClick(object sender, EventArgs e)
        {
            RgDebug.WriteLine(RgDebug.DebugType.TrayMenu, "onSetExtsMenuClick");

            // file ext setting dlg
            if (fileDlg == null)
            {
                fileDlg = new FileExtsDialog();
                fileDlg.ShowDialog();
                fileDlg = null;
            }
        }

        private void onResetClick(object sender, EventArgs e)
        {
            RgDebug.WriteLine(RgDebug.DebugType.TrayMenu, "onResetClick");

            DialogResult rt = MessageBox.Show(Properties.Resources.ResetWarninigMsg, Program.processName, MessageBoxButtons.YesNo);
            if (rt == DialogResult.Yes)
            {
                FormCollection fc = Application.OpenForms;
                //one is for main which is always loaded.
                if (fc.Count >= 2)
                {
                    MessageBox.Show(Properties.Resources.CloseOpenedForm);                 
                    return;
                }

                //reset auto allow
                Program.pm.DisableAutoAllow();

                //setting reset
                dataReset();
                Program.lm.AddLog(LogData.LogType.Reset);   

                //filter restart
                if (!Program.fm.Start())
                {
                    MessageBox.Show(Properties.Resources.FilterConnectionError);
                    return;
                }

                MessageBox.Show(Properties.Resources.ResetDoneMsg);
            }
        }

        private void onSpywareMenuClick(object sender, EventArgs e)
        {
            RgDebug.WriteLine(RgDebug.DebugType.TrayMenu, "onSpywareMenuClick - thread");

            //create thread for the reason of delay.
            var thread = new Thread(new ThreadStart(loadSpyWare));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void loadSpyWare()
        {
            //show tooltip if firewall is turned off
            if (!Program.nm.IsFirewallTurnedOn())
            {
                Utilities.showWarningTooltip(Program.GetNotifyIcon(),
                                              Properties.Resources.BalloonTip_FirewallTitle,
                                              Properties.Resources.BalloonTip_FirewallText);
            }

            if (netPrefDlg == null)
            {
                netPrefDlg = new NetPreferences();
                netPrefDlg.ShowDialog();
                netPrefDlg = null;
            }     
        }

        private void onExitMenuClick(object sender, EventArgs e)
        {
            DialogResult rt = MessageBox.Show(Properties.Resources.MenuExitWarning, Program.processName, MessageBoxButtons.YesNo);
            if (rt == DialogResult.Yes)
            {
                //remove shell context menu
                Program.cm.uninitShellContextMenu();

                //don't restart in this case.
                //((MainForm)this.MainForm).AppRestartDisable();

                //safecase detach
                if (Program.sm.isSafeCaseAttached())
                    Program.sm.DetachLoadedSafeCase();

                Application.Exit();
            }
        }

        //****************************************************************
        // SafeCase menu event handlers
        //****************************************************************
        private void onSafeCaseCreateClick(object sender, EventArgs e)
        {   
            if (safecaseDlg == null)
            {
                safecaseDlg = new SafecaseForm();
                safecaseDlg.ShowDialog();
                safecaseDlg = null;
            }
        }

        private void onSafeCaseFolderOpenClick(object sender, EventArgs e)
        {
            //open folder
            Process.Start(Program.sm.GetLoadedDrvLetter() + @":\");
        }

        private void onSafeCaseOpenClick(object sender, EventArgs e)
        {
            var fbrowse = new OpenFileDialog();
            string ext = SafeCaseManager.safecaseFileExt;
            fbrowse.Filter = "SafeCase(*"+ ext + ")|*" + ext;

            if (fbrowse.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //mount vhd image
                if (!Program.sm.AttachSafeCase(fbrowse.FileName))
                {
                    MessageBox.Show(Properties.Resources.FailedOpenSafeCase);
                }
            }
        }

        private void onSafeCaseSettingsClick(object sender, EventArgs e)
        {
            if (safecaseSetDlg == null)
            {
                safecaseSetDlg = new SafecaseSetForm(Program.sm.GetLoadedHeader());
                safecaseSetDlg.ShowDialog();
                safecaseSetDlg = null;
            }
        }

        private void onSafeCaseCloseClick(object sender, EventArgs e)
        {
            Program.sm.DetachLoadedSafeCase();

            //show complete message
            MessageBox.Show(Properties.Resources.CloseSfComplete);
        }

        //****************************************************************
        // File Utils Sub Menu event handlers
        //****************************************************************
        private void onShredderMenuClick(object sender, EventArgs e)
        {
            loadShredder();
        }

        private void onCryptMenuClick(object sender, EventArgs e)
        {
            loadCrypt();
        }

        private void onTrashBinMenuClick(object sender, EventArgs e)
        {
            if (trashBinDlg == null)
            {
                trashBinDlg = new TrashBinDialog();
                trashBinDlg.ShowDialog();
                trashBinDlg = null;
            }
        }

        private void loadShredder()
        {
            if (shredderDlg == null)
            {
                shredderDlg = new ShredderForm();
                shredderDlg.ShowDialog();
                shredderDlg = null;
            }
        }

        private void loadCrypt()
        {
            if (cryptDlg == null)
            {
                cryptDlg = new CryptForm();
                cryptDlg.ShowDialog();
                cryptDlg = null;
            }
        }

        //****************************************************************
        // command actions it will be called in command manager
        //****************************************************************
        private volatile bool isCryptDlgCreated = false;
        private volatile bool isShrederDlgCreated = false;

        public void addCryptFile(string filePath)
        {
            RgDebug.WriteLine(RgDebug.DebugType.CommandLog, "addCryptFile");

            //we need to use isCryptDlgCreated flag to avoid to run creation routine again.
            if (isCryptDlgCreated == false && cryptDlg == null)
            {
                isCryptDlgCreated = true;
                cryptDlg = new CryptForm();
                cryptDlg.Load += (s, e1) =>
                {
                    addFileToCryptDlg(filePath);
                };

                //create thread for the reason of delay.                               
                cryptDlg.ShowDialog();
                cryptDlg = null;
                isCryptDlgCreated = false;
            }
            else                
            {
                if (waitFormLoading<CryptForm>(ref cryptDlg, 1000))
                {
                    cryptDlg.Invoke(new Action(() => addFileToCryptDlg(filePath)));
                }
            }
        }


        public void addDeleteFile(string filePath)
        {
            RgDebug.WriteLine(RgDebug.DebugType.CommandLog, "addDeleteFile");

            //we need to use isShrederDlgCreated flag to avoid to run creation routine again.
            if (isShrederDlgCreated == false && shredderDlg == null)
            {
                isShrederDlgCreated = true;
                shredderDlg = new ShredderForm();
                shredderDlg.Load += (s, e1) =>
                {
                    addDeleteFileDlg(filePath);
                };

                //create thread for the reason of delay.                               
                shredderDlg.ShowDialog();
                shredderDlg = null;
                isShrederDlgCreated = false;
            }
            else
            {
                if (waitFormLoading<ShredderForm>(ref shredderDlg, 1000))
                {
                    shredderDlg.Invoke(new Action(() => addDeleteFileDlg(filePath)));
                }
            }
        }

        public void openSafeCase(string filePath)
        {
            RgDebug.WriteLine(RgDebug.DebugType.CommandLog, "openSafeCase");

            //in case of alread loaded
            if (Program.sm.isSafeCaseAttached())
            {
                if (Program.sm.GetLoadedImage() == filePath)
                {
                    Process.Start(Program.sm.GetLoadedDrvLetter() + @":\");
                }
                else
                {
                    MessageBox.Show(Properties.Resources.SafeCaseAlreadyInUse, Program.processName, MessageBoxButtons.OK);
                }
            }
            else
            {
                if (!Program.sm.AttachSafeCase(filePath))
                {
                    MessageBox.Show(Properties.Resources.FailedOpenSafeCase, Program.processName, MessageBoxButtons.OK);
                }
            }
        }

        public void addFileToCryptDlg(string filePath)
        {
            if (cryptDlg == null)
                return;

            cryptDlg.addFile(filePath);

            //show on top
            cryptDlg.TopMost = true;
            cryptDlg.Focus();
            cryptDlg.BringToFront();

            //set it back....why just bringToFront is not work :(
            cryptDlg.TopMost = false;
        }

        public void addDeleteFileDlg(string filePath)
        {
            if (shredderDlg == null)
                return;

            shredderDlg.addFile(filePath);

            //show on top
            shredderDlg.TopMost = true;
            shredderDlg.Focus();
            shredderDlg.BringToFront();

            //set it back....why just bringToFront is not work :(
            shredderDlg.TopMost = false;
        }

        //to wait for the instance creation. timeout * 10 /1000 sec
        public bool waitFormLoading<T>(ref T obj, int timeout)
        {
            for (int i = 0; i < timeout; i++)
            {
                Thread.Sleep(10);

                if (obj != null)
                    return true;
            }

            return false;
        }

        //****************************************************************
        // Destory or clean routines
        //****************************************************************

        protected override void ExitThreadCore()
        {
            if (notifyIcon != null)
                notifyIcon.Dispose();

            base.ExitThreadCore();
        }

#if false
        protected override void OnMainFormClosed(object sender, EventArgs e)
        {
            bool isRestartEnabled = ((MainForm)this.MainForm).IsAppRestartEnabled();

            Program.lm.AddLog(LogData.LogType.ProgramExit);
            Program.pm.SaveProcessListBeforeExit();
            Program.lm.DoLogWriteBeforeExit();

            if (isRestartEnabled)
            {
               Application.Exit();
            }
            else
            {
               base.OnMainFormClosed(sender, e);
            }
        }
#endif
    }
}