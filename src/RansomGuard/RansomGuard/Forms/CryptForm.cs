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
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using RansomGuard.Helpers;

namespace RansomGuard
{
    public partial class CryptForm : Form
    {
        enum EncryptionMethod : int
        {
            AES256 = 0,
            RC4 = 1,

            MethodMaxCnt
        }

        const int minPasswordLen = 4;

        //state text color
        Color colorStateError = System.Drawing.Color.Red;
        Color colorStateCrypting = System.Drawing.Color.Orange;
        Color colorStateComplete = System.Drawing.Color.Blue;

        delegate void listViewDelegate();

        //crypting status
        private bool isCryptStarted = false;
        private bool isCrypting = false;
        private bool isCanceled = false;

        //settings
        private EncryptionMethod selectedMethod;
        private bool useOrgFileDel;
        private string password;
        private string hint;

        //crypto manager
        private FileCrypto fileCrypto = new FileCrypto();

        public CryptForm()
        {
            InitializeComponent();

            //get settings
            selectedMethod = (EncryptionMethod)Properties.Settings.Default.CryptMethod;
            useOrgFileDel = Properties.Settings.Default.UseOrgFileDel;
            password = Properties.Settings.Default.CryptionPw;
            hint = Properties.Settings.Default.CryptionHint;

            //text init
            initText();

            //set radio btn init
            initRadioBtn(selectedMethod);

            //list view init
            initListView();

            //init checkbox
            optionDelete.Checked = useOrgFileDel;

            //init textBox
            textBox_pw.Text = password;
            textBox_pw_confirm.Text = password;
            textBox_hint.Text = hint;
        }

        //****************************************************************
        // command event from outside
        //****************************************************************
        public void addFile(string file)
        {
            try
            {
                //get the fileInfo
                var fileInfo = new FileInfo(file);

                // clear list if there is encrypted file list.
                clearPreListData();

                //add file
                addListRow(listView1, fileInfo);

                //set cols size
                SizeLastColumn(listView1);
            }
            catch
            {
                MessageBox.Show(Properties.Resources.F_Del_Msg_Error_FileInfo);
            }
        }

        //****************************************************************
        // Initializations
        //****************************************************************
        private void initText()
        {
            //title
            title.Text = Properties.Resources.F_Crypt_Title;
            subTitle.Text = Properties.Resources.F_Crypt_SubTitle;

            //groupBox texts
            groupBox1.Text = Properties.Resources.F_Crypt_Method;
            groupBox2.Text = Properties.Resources.F_Crypt_SetPw;

            //radio texts
            radioButton1.Text = Properties.Resources.F_Crypt_Method_AES256;
            radioButton2.Text = Properties.Resources.F_Crypt_Method_RC4;

            //chekc box option
            optionDelete.Text = Properties.Resources.F_Crypt_UseDelete;

            //labels
            label_aes256.Text = Properties.Resources.F_Crypt_Label_AES256;
            label_rc4.Text = Properties.Resources.F_Crypt_Label_RC4;

            label1.Text = Properties.Resources.F_Crypt_Pw;
            label2.Text = Properties.Resources.F_Crypt_Confirm;
            label3.Text = Properties.Resources.F_Crypt_Hint;
            label4.Text = Properties.Resources.F_Crypt_Option;

            //buttons
            button_remove.Text = Properties.Resources.F_Crypt_Btn_RmItems;
            button_add.Text = Properties.Resources.F_Crypt_Btn_AddItems;
            button_crypt.Text = Properties.Resources.F_Crypt_Btn_Crypt;
            button_close.Text = Properties.Resources.F_Crypt_Btn_Close;
        }

        private void initListView()
        {
            //set list properties
            ListViewHelper.SetExtendedStyle(listView1, ListViewHelper.ListViewExtendedStyles.FullRowSelect);
            ListViewHelper.EnableDoubleBuffer(listView1);
            listView1.View = View.Details;

            //add cols
            listView1.Columns.Add("Name", 200, HorizontalAlignment.Left);
            listView1.Columns.Add("Size", 80, HorizontalAlignment.Left);
            listView1.Columns.Add("State", 120, HorizontalAlignment.Left);
            listView1.Columns.Add("Path", 250, HorizontalAlignment.Left);
        }

        private void initRadioBtn(EncryptionMethod selected)
        {
            switch(selected)
            {
                case EncryptionMethod.AES256:
                    radioButton1.Checked = true;
                    break;

                case EncryptionMethod.RC4:
                    radioButton2.Checked = true;
                    break;

                default:
                    radioButton1.Checked = true;
                    break;
            }
        }

        //****************************************************************
        // Form Load and Close Event Handlers
        //****************************************************************
        private void CryptForm_Load(object sender, EventArgs e)
        {
            SizeLastColumn(listView1);
        }

        private void CryptForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            //save settings
            Properties.Settings.Default.CryptMethod = (int)selectedMethod;
            Properties.Settings.Default.UseOrgFileDel = useOrgFileDel;
            Properties.Settings.Default.CryptionPw = textBox_pw.Text;
            Properties.Settings.Default.CryptionHint = textBox_hint.Text;

            listView1.Clear();
            ListViewHelper.DisableDoubleBuffer(listView1);
            listView1.Dispose();
        }

        //****************************************************************
        // ListView related functions
        //****************************************************************
        private void addListRow(ListView lView, FileInfo fileInfo)
        {
            bool isEncryptedFile = fileCrypto.isEncryptedFile(fileInfo.FullName);

            //do not allow same path
            if (listView1.FindItemWithText(fileInfo.FullName) != null)
                return;

            //Create list item with fiel Name
            var lvi = new ListViewItem(fileInfo.Name);

            //size
            lvi.SubItems.Add(Utilities.BytesToString(fileInfo.Length));

            //state
            if (isEncryptedFile)
                lvi.SubItems.Add(Properties.Resources.F_Crypt_DeWaiting);
            else
                lvi.SubItems.Add(Properties.Resources.F_Crypt_EnWaiting);

            //path
            lvi.SubItems.Add(fileInfo.FullName);

            //add item
            lView.Items.Add(lvi);
        }

        private void SizeLastColumn(ListView lv)
        {
            lv.Columns[lv.Columns.Count - 1].Width = -2;
        }

        private void listView1_Resize(object sender, EventArgs e)
        {
            listView1.BeginUpdate();
            SizeLastColumn((ListView)sender);
            listView1.EndUpdate();
        }

        //****************************************************************
        // Radio buttons Event Handlers
        //****************************************************************
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            selectedMethod = EncryptionMethod.AES256;

        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            selectedMethod = EncryptionMethod.RC4;
        }

        //****************************************************************
        // button Event Handlers
        //****************************************************************
        private void optionDelete_CheckedChanged(object sender, EventArgs e)
        {
            useOrgFileDel = optionDelete.Checked;
        }

        private void button_remove_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                item.Remove();
            }
        }

        private void button_add_Click(object sender, EventArgs e)
        {
            var fbrowse = new OpenFileDialog();
            fbrowse.Multiselect = true;

            if (fbrowse.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                listView1.BeginUpdate();

                // clear list if there is encrypted file list.
                clearPreListData();

                //get the fileInfo
                foreach (String file in fbrowse.FileNames)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        addListRow(listView1, fileInfo);
                    }
                    catch
                    {
                        MessageBox.Show(Properties.Resources.F_Del_Msg_Error_FileInfo);
                        break;
                    }
                }

                SizeLastColumn(listView1);
                listView1.EndUpdate();
            }
        }

        private void button_crypt_Click(object sender, EventArgs e)
        {
            //isvalidate
            if (!cryptValidation(listView1.Items.Count))
                return;

            //confirm message
            DialogResult rt = MessageBox.Show(Properties.Resources.F_Crypt_Msg_StartCrypt, 
                Program.processName, MessageBoxButtons.YesNo);

            if (rt != DialogResult.Yes)
                return;

            //set crypt start flag
            isCryptStarted = true;
            isCrypting = true;

            //prepare to crypt, disable buttons.
            enableCtrlsForCrypt(false);

            //init prograss bar
            progressBar1.Maximum = listView1.Items.Count + 1; // +1 is to show the process of starting.
            progressBar1.Step = 1;
            progressBar1.PerformStep();

            //do the task
            Task.Factory.StartNew(() => doCryptInList(listView1, selectedMethod,progressBar1));
        }

        private void button_close_Click(object sender, EventArgs e)
        {
            if (isCrypting)
            {
                DialogResult rt = MessageBox.Show(Properties.Resources.F_Crypt_Msg_StopCrypt, 
                    Program.processName, MessageBoxButtons.YesNo);

                if (rt == DialogResult.Yes)
                {
                    //stop current crypting.
                    fileCrypto.StopCrypting();

                    //don't crypt next file.
                    isCanceled = true;
                }
            }
            else
            {
                this.Close();
            }
        }

        //****************************************************************
        // Crypt related
        //****************************************************************
        private void doCryptInList(ListView lv, EncryptionMethod method, ProgressBar pb)
        {
            FileCrypto.CryptError res = FileCrypto.CryptError.None;
            bool isEncrptedFile = false;

            //start encrypt or decrypt one by one
            for (int i = 0; i < listView1.Items.Count; i++)
            {

                //cancel was fired.
                if (isCanceled)
                    break;

                string filePath = "";

                //start UI
                this.Invoke(new Action(() =>
                {
                    filePath = lv.Items[i].SubItems[3].Text;

                    //start erasing..
                    lv.Items[i].SubItems[2].ForeColor = colorStateCrypting;
                    lv.Items[i].SubItems[2].Text = Properties.Resources.F_Crypt_Crypting;
                    lv.Items[i].ForeColor = colorStateCrypting;
                }));

                //check the file is exist
                if (File.Exists(filePath))
                {
                    //do encryption or decryption
                    isEncrptedFile = fileCrypto.isEncryptedFile(filePath);
                    if (isEncrptedFile)
                    {
                        string targetFolder = Directory.GetParent(filePath).ToString();
                        res = fileCrypto.DecryptFile(false, filePath, targetFolder, textBox_pw.Text, (int)selectedMethod);
                    }
                    else
                    {
                        res = fileCrypto.EncryptFile(false, filePath, textBox_pw.Text, textBox_hint.Text, (int)selectedMethod);
                    }
                }
                else
                {
                    res = FileCrypto.CryptError.FileMissing;
                }

                //complete UI
                this.Invoke(new Action(() =>
                {
                    if (res == FileCrypto.CryptError.None)
                    {
                        //erasing complete
                        if (isEncrptedFile)
                            lv.Items[i].SubItems[2].Text = Properties.Resources.F_Crypt_DeComplete;
                        else
                            lv.Items[i].SubItems[2].Text = Properties.Resources.F_Crypt_EnComplete;


                        lv.Items[i].ForeColor = colorStateComplete;
                    }
                    else
                    {
                        //erasing error
                        if (isEncrptedFile)
                            lv.Items[i].SubItems[2].Text = Properties.Resources.F_Crypt_DeFailed;
                        else
                            lv.Items[i].SubItems[2].Text = Properties.Resources.F_Crypt_EnFailed;

                        lv.Items[i].ForeColor = colorStateError;
                    }

                    pb.PerformStep();
                }));


                //result of the process
                if(res == FileCrypto.CryptError.None)
                {
                    //successfully crypted
                    if (useOrgFileDel)
                    {
                        //delete file
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch { }
                    }
                }
                else
                {
                    //show error mssage if it is true, then cancel the jobs.
                    if (showMessageBox(res, filePath))
                        break;
                }
            }

            //complete UI
            this.Invoke(new Action(() =>
            {
                //finish cryptin, enable buttons.
                enableCtrlsForCrypt(true);

                isCrypting = false;
                isCanceled = false;
            }));
        }

        //****************************************************************
        // Utils
        //****************************************************************
        private void enableCtrlsForCrypt(bool enabled)
        {
            button_crypt.Enabled = enabled;
            button_remove.Enabled = enabled;
            button_add.Enabled = enabled;
            optionDelete.Enabled = enabled;
            textBox_pw.Enabled = enabled;
            textBox_pw_confirm.Enabled = enabled;
            textBox_hint.Enabled = enabled;
        }

        private void clearPreListData()
        {
            if (isCryptStarted)
            {
                isCryptStarted = false;
                listView1.Items.Clear();
                progressBar1.Value = 0;
            }
        }

        bool cryptValidation(int itemCnt)
        {
            if (itemCnt <= 0)
            {
                MessageBox.Show(Properties.Resources.F_Crypt_Msg_SelFiles);
                return false;
            }

            if (String.IsNullOrEmpty(textBox_pw.Text))
            {
                MessageBox.Show(Properties.Resources.F_Crypt_Msg_InputPw);
                return false;
            }

            //check password
            //length should be longer than 4 digit.
            if (textBox_pw.Text.Length < minPasswordLen)
            {
                MessageBox.Show(Properties.Resources.F_Crypt_Msg_PwLength);
                return false;
            }

            if (textBox_pw.Text != textBox_pw_confirm.Text)
            {
                MessageBox.Show(Properties.Resources.F_Crypt_Msg_PwConfirm);
                return false;
            }

            return true;
        }

        bool showMessageBox(FileCrypto.CryptError state, string filePath)
        {
            bool stopAllCrypting = false;
            string fileName = Path.GetFileName(filePath);

            switch (state)
            {
                case FileCrypto.CryptError.InvalidPassword:
                    MessageBox.Show(fileName + "\n" + Properties.Resources.F_Crypt_Msg_PwError + "\n\n" + 
                        Properties.Resources.F_Crypt_Msg_PwHint + " : " + 
                        fileCrypto.getPasswordHint(filePath));

                    stopAllCrypting = true;
                    break;

                case FileCrypto.CryptError.TargetFileExist:
                    MessageBox.Show(fileName + "\n" + Properties.Resources.F_Crypt_Msg_FileExist);

                    stopAllCrypting = true;
                    break;

                case FileCrypto.CryptError.JobCanceled:
                    MessageBox.Show(fileName + "\n" + Properties.Resources.F_Crypt_Msg_Cancled);

                    stopAllCrypting = true;
                    break;

                case FileCrypto.CryptError.FileMissing:
                    MessageBox.Show(fileName + "\n" + Properties.Resources.F_Crypt_Msg_FileMissing);

                    stopAllCrypting = true;
                    break;

                default:
                    MessageBox.Show(fileName + "\n" + Properties.Resources.F_Crypt_Msg_UnknownError);

                    stopAllCrypting = true;
                    break;
            }

            return stopAllCrypting;
        }

    }
}
