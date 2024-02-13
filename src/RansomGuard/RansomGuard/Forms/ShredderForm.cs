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
using RansomGuard.Helpers;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RansomGuard
{
    public partial class ShredderForm : Form
    {
        const string rgDefaultPath = Program.rgDeletePath;

        enum EraseMethod : int
        {
            SSD = 0,
            SetZero = 1,
            DoD3 = 2,
            DoD7 = 3,

            MethodMaxCnt
        }

        //state text color
        Color colorStateError = System.Drawing.Color.Red;
        Color colorStateErasing = System.Drawing.Color.Orange;
        Color colorStateComplete = System.Drawing.Color.Blue;

        delegate void listViewDelegate();
        private EraseMethod selectedMethod;
        private bool isEraseStarted = false;
        private bool isErasing = false;
        private bool isCanceled = false;

        public ShredderForm()
        {
            InitializeComponent();

            //get settings
            selectedMethod = (EraseMethod)Properties.Settings.Default.EraseMethod;

            //text init
            initText();

            //set scheduled erase controls
            initScheCtrls();

            //set radio btn init
            initRadioBtn(selectedMethod);

            //list view init
            initListView();
        }

        //****************************************************************
        // Initializations
        //****************************************************************
        private void initText()
        {
            title.Text = Properties.Resources.F_Del_Title;
            subTitle.Text = Properties.Resources.F_Del_Title_Sub;

            groupBox1.Text = Properties.Resources.F_Del_Box_Methods;
            groupBox2.Text = Properties.Resources.F_Del_Box_ScheDel;

            useScheCheckbox.Text = Properties.Resources.F_Del_Btn_UseSche;
            button_sche_cancle.Text = Properties.Resources.F_Del_Btn_CancelSche;
            button_remove.Text = Properties.Resources.F_Del_Btn_RmItems;

            button_add_folder.Text = Properties.Resources.F_Del_Btn_AddFolder;
            button_add.Text = Properties.Resources.F_Del_Btn_AddFiles;
            button_wipe.Text = Properties.Resources.F_Del_Btn_DelFiles;
            button_close.Text = Properties.Resources.F_Del_Btn_StopClose;
        }

        private void initScheCtrls()
        {
            dateTimePicker1.Format = DateTimePickerFormat.Short;
            dateTimePicker2.Format = DateTimePickerFormat.Custom;
            dateTimePicker2.CustomFormat = "HH:mm";
            dateTimePicker2.ShowUpDown = true;

            setScheBtns(false);
        }

        private void initListView()
        {
            //set list properties
            ListViewHelper.SetExtendedStyle(listView1, ListViewHelper.ListViewExtendedStyles.FullRowSelect);
            ListViewHelper.EnableDoubleBuffer(listView1);
            listView1.View = View.Details;

            //add cols
            listView1.Columns.Add("Name", 200, HorizontalAlignment.Left);
            listView1.Columns.Add("Type", 80, HorizontalAlignment.Left);
            listView1.Columns.Add("State", 120, HorizontalAlignment.Left);
            listView1.Columns.Add("Path", 250, HorizontalAlignment.Left);
        }

        private void initRadioBtn(EraseMethod selected)
        {
            switch(selected)
            {
                case EraseMethod.SSD:
                    radioButton1.Checked = true;
                    break;

                case EraseMethod.SetZero:
                    radioButton2.Checked = true;
                    break;

                case EraseMethod.DoD3:
                    radioButton3.Checked = true;
                    break;

                case EraseMethod.DoD7:
                    radioButton4.Checked = true;
                    break;

                default:
                    radioButton1.Checked = true;
                    break;
            }
        }

        //****************************************************************
        // Form Load and Close Event Handlers
        //****************************************************************
        private void ShredderForm_Load(object sender, EventArgs e)
        {
            SizeLastColumn(listView1);
        }

        private void ShredderForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            //save
            Properties.Settings.Default.EraseMethod = (int)selectedMethod;

            listView1.Clear();
            ListViewHelper.DisableDoubleBuffer(listView1);
            listView1.Dispose();
        }

        //****************************************************************
        // ListView related functions
        //****************************************************************
        private void addListRow(ListView lView, FileInfo fileInfo)
        {
            //do not allow same path
            if(listView1.FindItemWithText(fileInfo.FullName) != null)
                return;

            //Create list item with fiel Name
            var lvi = new ListViewItem(fileInfo.Name);

            //type
            FileAttributes attr = File.GetAttributes(fileInfo.FullName);

            if (attr.HasFlag(FileAttributes.Directory))
                lvi.SubItems.Add(Properties.Resources.F_Del_type_Dir);
            else
                lvi.SubItems.Add(Properties.Resources.F_Del_type_File);

            //state
            lvi.SubItems.Add(Properties.Resources.F_Del_State_Ready);

            //path
            lvi.SubItems.Add(fileInfo.FullName);

            //skip it
            if (string.IsNullOrEmpty(fileInfo.Name))
            {
                MessageBox.Show(Properties.Resources.F_Del_Msg_NotType_Supported);
                return;
            }

            //add item
            lView.Items.Add(lvi);
        }

        private void SizeLastColumn(ListView lv)
        {
            lv.Columns[lv.Columns.Count - 1].Width =- 2;
        }

        private void listView1_Resize(object sender, EventArgs e)
        {
            listView1.BeginUpdate();
            SizeLastColumn((ListView)sender);
            listView1.EndUpdate();
        }

        //****************************************************************
        // button Event Handlers
        //****************************************************************
        private void button_remove_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                item.Remove();
            }
        }

        private void button_add_folder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbrowse = new FolderBrowserDialog();
            if (fbrowse.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                listView1.BeginUpdate();

                // clear list if there is erased file list.
                clearPreListData();

                //get the fileInfo
                try
                {
                    var fileInfo = new FileInfo(fbrowse.SelectedPath);
                    addListRow(listView1, fileInfo);
                }
                catch
                {
                    MessageBox.Show(Properties.Resources.F_Del_Msg_Error_FileInfo);
                }

                SizeLastColumn(listView1);
                listView1.EndUpdate();
            }
        }

        private void button_add_Click(object sender, EventArgs e)
        {
            var fbrowse = new OpenFileDialog();
            fbrowse.Multiselect = true;

            if (fbrowse.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                listView1.BeginUpdate();

                // clear list if there is erased file list.
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

        private void button_wipe_Click(object sender, EventArgs e)
        {
            string rgDelPath = getRgDeletePath();

            // clear list if there is erased file list.
            clearPreListData();

            //check validation
            if (!eraseValidation(listView1.Items.Count, rgDelPath))
                return;

            //confirm message
            DialogResult rt = MessageBox.Show(Properties.Resources.F_Del_Msg_Start_Warn, Program.processName, MessageBoxButtons.YesNo);
            if (rt != DialogResult.Yes)
                return;

            //set erase start flag
            isEraseStarted = true;
            isErasing = true;

            //prepare to erase, disable buttons.
            button_remove.Enabled = false;
            button_add.Enabled = false;
            button_wipe.Enabled = false;

            //init prograss bar
            progressBar1.Maximum = listView1.Items.Count + 1; // +1 is to show the process of starting.
            progressBar1.Step = 1;
            progressBar1.PerformStep();                

            //do erase task
            Task.Factory.StartNew(() => doEraseInList(rgDelPath, listView1, selectedMethod, 
                progressBar1, useScheCheckbox.Checked));

        }

        private void button_close_Click(object sender, EventArgs e)
        {
            if(isErasing)
            {
                DialogResult rt = MessageBox.Show(Properties.Resources.F_Del_Msg_Stop_Warn, Program.processName, MessageBoxButtons.YesNo);
                if (rt == DialogResult.Yes)
                {
                    isCanceled = true;
                }
            }
            else
            {
                this.Close();
            }
        }

        private void useScheCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            setScheBtns(useScheCheckbox.Checked);
        }

        private void button_sche_cancle_Click(object sender, EventArgs e)
        {
            //confirm message
            DialogResult rt = MessageBox.Show(Properties.Resources.F_Del_Msg_Sche_Cancel_Warn, Program.processName, MessageBoxButtons.YesNo);
            if (rt != DialogResult.Yes)
                return;

            cancelScheduledErase();
            MessageBox.Show(Properties.Resources.F_Del_Msg_Sche_Canceled);
        }

        //****************************************************************
        // Radio buttons Event Handlers
        //****************************************************************
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            selectedMethod = EraseMethod.SSD;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            selectedMethod = EraseMethod.SetZero;
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            selectedMethod = EraseMethod.DoD3;
        }

        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            selectedMethod = EraseMethod.DoD7;
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
        // Utils
        //****************************************************************
        private void clearPreListData()
        {
            if (isEraseStarted)
            {
                isEraseStarted = false;
                listView1.Items.Clear();
                progressBar1.Value = 0;
            }
        }

        private void setScheBtns(bool isChecked)
        {
            if (isChecked)
            {
                dateTimePicker1.Enabled = true;
                dateTimePicker2.Enabled = true;
                button_sche_cancle.Enabled = true;
            }
            else
            {
                dateTimePicker1.Enabled = false;
                dateTimePicker2.Enabled = false;
                button_sche_cancle.Enabled = false;
            }
        }

        private void doEraseInList(string rgDelPath, ListView lv, EraseMethod method, ProgressBar pb, bool isSchErase = false)
        {
            for (int i = 0; i < lv.Items.Count; i++)
            {
                //cancle was fired.
                if (isCanceled)
                {
                    MessageBox.Show(Properties.Resources.F_Del_Msg_Stopped);
                    break;
                }

                string filePath = "";

                //start UI
                this.Invoke(new Action(() =>
                {
                    filePath = lv.Items[i].SubItems[3].Text;

                    //start erasing..
                    lv.Items[i].SubItems[2].ForeColor = colorStateErasing;
                    lv.Items[i].SubItems[2].Text = Properties.Resources.F_Del_State_Erasing;
                    lv.Items[i].ForeColor = colorStateErasing;
                }));

                //actual start
                bool isErased = singleErase(rgDelPath, filePath, method, isSchErase);

                //complete UI
                this.Invoke(new Action(() =>
                {
                    if (isErased)
                    {
                        //erasing complete
                        lv.Items[i].SubItems[2].Text = Properties.Resources.F_Del_State_Complete;
                        lv.Items[i].ForeColor = colorStateComplete;
                    }
                    else
                    {
                        //erasing error
                        lv.Items[i].SubItems[2].Text = Properties.Resources.F_Del_State_Fail;
                        lv.Items[i].ForeColor = colorStateError;
                    }

                    pb.PerformStep();
                }));
            }

            //complete UI
            this.Invoke(new Action(() =>
            {
                //finish deleting, enable buttons.
                button_remove.Enabled = true;
                button_add.Enabled = true;
                button_wipe.Enabled = true;

                isErasing = false;
                isCanceled = false;
            }));
        }

        private bool eraseValidation(int itemCnt, string rgDelPath)
        {
            try
            {
                if (itemCnt == 0)
                {
                    MessageBox.Show(Properties.Resources.F_Del_Msg_Add_Files);
                    return false; 
                }

                if (!File.Exists(rgDelPath))
                {
                    MessageBox.Show(Properties.Resources.F_Del_Msg_Error_Program);
                    return false;
                }
            }
            catch 
            {
                return false;
            }

            return true;
        }

        private string getRgDeletePath()
        {
            string rgDeletePath = "";

            try
            {
                //first path 
                string rgPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                rgDeletePath = Path.Combine(rgPath, rgDefaultPath);
            }
            catch { }

            return rgDeletePath;
        }

        private bool singleErase(string rgDeletePath, string delPath, EraseMethod delMethod, bool isScheErase = false)
        {
            bool isErased = false;

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = rgDeletePath;
            startInfo.Arguments = @"/s RgDelete /p """ + delPath + @"""";

            // /m : del_method , 0: fast, 1:random, 2:dod3 , 3:dod7, 4:32overwrite
            switch (delMethod)
            {
                case EraseMethod.SSD:
                    startInfo.Arguments += " /m 0";
                    break;

                case EraseMethod.SetZero:
                    startInfo.Arguments += " /m 0";
                    break;

                case EraseMethod.DoD3:
                    startInfo.Arguments += " /m 2";
                    break;

                case EraseMethod.DoD7:
                    startInfo.Arguments += " /m 7";
                    break;

                default:
                    startInfo.Arguments += " /m 0";
                    break;
            }

            //set scheduled erase options
            if(isScheErase)
            {
                //get date and time (ex /d 2015-07-28 /t 12:00 )
                string date = dateTimePicker1.Value.ToString("yyyy-MM-dd");
                string time = dateTimePicker2.Value.ToString("HH:mm");

                startInfo.Arguments += " /d " + date + " /t " + time;
            }

            //start and wait the process to end.
            var pr = Process.Start(startInfo);
            pr.WaitForExit();

            //set erase success
            isErased = true;

            return isErased;
        }

        private void cancelScheduledErase()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = getRgDeletePath();
            startInfo.Arguments = @"/s RgDelete /dt y";

            //start and wait the process to end.
            var pr = Process.Start(startInfo);
            pr.WaitForExit(); 
        }
    }
}
