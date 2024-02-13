using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace RansomGuard
{
    public partial class FolderLockDialog : Form
    {
        const int minPasswordLen = 4;

        public FolderLockDialog()
        {
            InitializeComponent();
            initTexts();

            //get settings
            tbPassword.Text = Properties.Settings.Default.LockPassword;
            tbPassword1.Text = tbPassword.Text;
            tbSourceFolder.Text = Properties.Settings.Default.FolderLockPath;

            checkBox1.Checked = Properties.Settings.Default.UsePassword;
            if (checkBox1.Checked)
            {
                tbPassword.Enabled = true;
                tbPassword1.Enabled = true;
            }
            else
            {
                tbPassword.Enabled = false;
                tbPassword1.Enabled = false;
            }
        }

        void initTexts()
        {
            label1.Text = Properties.Resources.F_LockSet_Label1;
            checkBox1.Text = Properties.Resources.F_LockSet_Check1;

            button3.Text = Properties.Resources.F_LockSet_Btn_Browse;
            button1.Text = Properties.Resources.F_LockSet_Btn_Lock;
            button2.Text = Properties.Resources.F_LockSet_Btn_Close;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        //folder set
        private void button3_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderDlg = new FolderBrowserDialog();
            folderDlg.ShowNewFolderButton = true;

            DialogResult result = folderDlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                if (!isSpecialFolder(folderDlg.SelectedPath))
                {
                    tbSourceFolder.Text = folderDlg.SelectedPath;
                    Environment.SpecialFolder root = folderDlg.RootFolder;
                }
                else
                {
                    MessageBox.Show(Properties.Resources.FolderSetErrorMsg);
                }
            }
        }

        //folder lock
        private void button1_Click(object sender, EventArgs e)
        {
            if (validate())
            {
                Properties.Settings.Default.UsePassword = checkBox1.Checked;
                Properties.Settings.Default.LockPassword = tbPassword.Text;
                Properties.Settings.Default.FolderLockPath = tbSourceFolder.Text;
                Properties.Settings.Default.FolderLockSet = true;
                Properties.Settings.Default.Save();

                if (!Program.fm.Start())
                {
                    MessageBox.Show(Properties.Resources.FilterConnectionError);
                    return;
                }

                Program.lm.AddLog(LogData.LogType.SetFolderLock);

                MessageBox.Show(Properties.Resources.FolderLockOkMsg);
                this.Close();
            }
        }

        private bool isSpecialFolder(string folder)
        {
            bool result = false;

            //check if it is system drive.
            string lowerFolder = folder.ToLower();

            string userDoc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
            if (lowerFolder.Contains(systemRoot.ToLower()))
            {
                //allow doc and desktop sub folder in system drive.
                if (lowerFolder.Contains(userDoc.ToLower()) || lowerFolder.Contains(userDesktop.ToLower()))
                    result = false;
                else
                    result = true;
            }

            //check if it is special folder
            if (result == false)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(lowerFolder);
                foreach (Environment.SpecialFolder suit in Enum.GetValues(typeof(Environment.SpecialFolder)))
                {
                    if (directoryInfo.FullName == Environment.GetFolderPath(suit).ToLower())
                    {
                        result = true;
                        break;
                    }
                }
            }

            if (result == false)
            {
                //check if it is ransom guard doc path.
                string currDoc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Program.processName);
                if (lowerFolder.Contains(currDoc.ToLower()))
                    result = true;
            }

            return result;
        }

        public string browseForFolder(string title, string rootPath)
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            var shell = Activator.CreateInstance(shellType);
            var folder = shellType.InvokeMember("BrowseForFolder", 
                BindingFlags.InvokeMethod, null, shell, new object[] { 0, title, 0, rootPath });
            if (folder == null)
                return null; // User clicked cancel

            var folderSelf = folder.GetType().InvokeMember("Self", BindingFlags.GetProperty, null, folder, null);

            return folderSelf.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, folderSelf, null) as string;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            // Change the ThreeState and CheckAlign properties on every other click.
            if (checkBox1.Checked)
            {
               tbPassword.Enabled = true;
               tbPassword1.Enabled = true;
            }
            else
            {
               tbPassword.Enabled = false;
               tbPassword1.Enabled = false;
            }
        }

        bool validate()
        {
            if(String.IsNullOrEmpty(tbSourceFolder.Text))
            {
                MessageBox.Show(Properties.Resources.EmptyFolderText);
                return false;
            }

            string rootPath = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                rootPath = Directory.GetParent(rootPath).ToString();
            }

            //check folder ... user folder itself will not be locked.
            if (tbSourceFolder.Text == rootPath)
            {
                MessageBox.Show(Properties.Resources.FolderSetErrorMsg);
                return false;
            }

            //check password
            //length should be longer than 4 digit.
            if (checkBox1.Checked)
            {
                if (tbPassword1.Text != tbPassword.Text)
                {
                    MessageBox.Show(Properties.Resources.PasswordErrorMsg);
                    return false;
                }

                if (tbPassword.Text.Length < minPasswordLen)
                {
                    MessageBox.Show(Properties.Resources.PasswordErrorMsg);
                    return false;
                }
            }

            return true;
        }
    }
}
