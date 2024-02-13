using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;


namespace RansomGuard
{
    public partial class FileExtsDialog : Form
    {
        // additional extension allowing count
        const int allowedCountAddedExt = 100;

        const string docsExts = "doc,docx,docm,pdf,pages,rtf,ppt,pptm,pptx,psd,pst,ptx,rtf";
        const string dataExts = "xlk,xls,xlsb,xlsm,xlsx,csv,rgsf";
        const string audioExts = "mp3,avi,mpg,mpeg,xvid,mov,jpe,jpg";
        const string dbExts = "accdb,dbf,mdb,pdb,sql";
        const string programExts = "sys,dll,exe";
        const string shellScriptrExts = "vbs,cmd,bat,wsf,ps1";
        const string webExts = "asp,asp,cer,cfm,csr,css,htm,html,js,jsp,php,rss,xhtm";
        const string devExts = "c,class,cpp,cs,dtd,fla,h,java,lua,pl,py,sh,sln,swift,vcxproj,xcodeproj";

        //additinoal and remove exts
        public static string addedFileExts = "";
        public static string removedFileExts = "";

        const string exampleText = "eg)txt,zip,srt";
        bool changedSettings;

        public FileExtsDialog()
        {
            InitializeComponent();
            initTexts();

            //get settings
            addedFileExts = Properties.Settings.Default.AddedExts;

            InitStyle();
            FileExtsToShow();
        }
    
        void initTexts()
        {
            label1.Text = Properties.Resources.F_Exts_Label1;
            label2.Text = Properties.Resources.F_Exts_Label2;

            button1.Text = Properties.Resources.F_Exts_Btn_Add;
            button2.Text = Properties.Resources.F_Exts_Btn_Remove;
            button3.Text = Properties.Resources.F_Exts_Btn_Close;
        }

        private void InitStyle()
        {
            changedSettings = false;
            textBox1.Text = exampleText;
            textBox1.ForeColor = System.Drawing.SystemColors.InactiveCaption;
            this.AcceptButton = button3;
        }

        // adjust and close
        private void button3_Click(object sender, EventArgs e)
        {
            if (changedSettings)
            {
                Properties.Settings.Default.AddedExts = addedFileExts;
                Properties.Settings.Default.Save();

                Program.lm.AddLog(LogData.LogType.SetExtentions);   

                if (!Program.fm.Start())
                {
                    MessageBox.Show(Properties.Resources.FilterConnectionError);
                    return;
                }
            }

            this.Close();
        }

        public void FileExtsToShow()
        {
            string fileExtsShow;

            fileExtsShow = Properties.Resources.F_Exts_Doc + " : " + docsExts + "\r\n";
            fileExtsShow += Properties.Resources.F_Exts_Data + " : " + dataExts + "\r\n";
            fileExtsShow += Properties.Resources.F_Exts_Media + " : " + audioExts + "\r\n";
            fileExtsShow += Properties.Resources.F_Exts_Db + " : " + dbExts + "\r\n";
            fileExtsShow += Properties.Resources.F_Exts_Program + " : " + programExts + "\r\n";
            fileExtsShow += Properties.Resources.F_Exts_Script + " : " + shellScriptrExts + "\r\n";
            fileExtsShow += Properties.Resources.F_Exts_Web + " :  " + webExts + "\r\n";
            fileExtsShow += Properties.Resources.F_Exts_Src + " : " + devExts + "\r\n\r\n";

            //additional exts
            fileExtsShow += Properties.Resources.F_Exts_Added + " : " + addedFileExts + "\r\n";

            exts.Text = fileExtsShow;
        }

        public static string GetFileExts()
        {
            string fileExts = "";
            string addedExts = Properties.Settings.Default.AddedExts;

            fileExts += docsExts;
            fileExts += "," + dataExts;
            fileExts += "," + audioExts;
            fileExts += "," + dbExts;
            fileExts += "," + programExts;
            fileExts += "," + shellScriptrExts;
            fileExts += "," + webExts;
            fileExts += "," + devExts;

            if (!String.IsNullOrEmpty(addedExts))
                fileExts += "," + addedExts;

            return fileExts;
        }

        private bool IsValidInput(string inputText)
        {
            Regex FileExtRegex = new Regex(@"^[\w\-.]{1,8}$");
            Match match;
             
            string[] exts = inputText.Split(',');

            foreach(string ext in exts)
            {
               match = FileExtRegex.Match(ext);
               if (!match.Success)
                   return false;
            }

            return true;
        }

        private string RemoveFileExts(string currExts , string removeExts)
        {
            if (String.IsNullOrEmpty(currExts))
                return "";

            if(String.IsNullOrEmpty(removeExts))
                return currExts;

            string[] addedExts = currExts.Split(',');
            string[] rmExts = removeExts.Split(',');
            List<string> list = new List<string>(addedExts);
            bool result = false;

            foreach (string rmExt in rmExts)
            {
                result = list.Remove(rmExt); //remove specieifed item.
                if(result == false)
                    return currExts;
            }

            return string.Join(",", list.ToArray());
        }

        private bool IsAllowedCntExt(string defaultExts, int allowedCountAddedExt, string inputExts)
        {
            string[] deExts = defaultExts.Split(',');
            string[] inExts = inputExts.Split(',');

            if (allowedCountAddedExt < (deExts.Length + inExts.Length))
            {
                return false;
            }

            return true;
        }

        private bool IsAlreadySavedExt(string defaultExts, string addedExts, string inputExts)
        {
            string[] deExts = defaultExts.Split(',');
            string[] adExts = addedExts.Split(',');
            string[] inExts = inputExts.Split(',');

            List<string> deList = new List<string>(deExts);
            List<string> adList = new List<string>(adExts);

            //check in added list
            foreach (string inExt in inExts)
            {
                //check in default list
                if (deList.Contains(inExt))
                    return true;

                //check in default list
                if (adList.Contains(inExt))
                    return true;
            }

            return false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string savedExts = GetFileExts();

            if (String.IsNullOrEmpty(textBox1.Text) 
                || textBox1.Text == exampleText)
                return;

            //check input validation
            if (!IsValidInput(textBox1.Text)) {

                MessageBox.Show(Properties.Resources.FileExtValidError);
                return;
            }

            if (IsAlreadySavedExt(savedExts, addedFileExts, textBox1.Text))
            {
                MessageBox.Show(Properties.Resources.F_Exts_ExistExtsErrorMsg);
                return;
            }

            //check limit of the file extensions.
            if (!IsAllowedCntExt(savedExts, allowedCountAddedExt, textBox1.Text))
            {
                MessageBox.Show(Properties.Resources.F_Exts_MaxExtsErrorMsg1 + " " +
                    allowedCountAddedExt.ToString() + Properties.Resources.F_Exts_MaxExtsErrorMsg2);
                return;
            }

            if (!String.IsNullOrEmpty(addedFileExts))
            {
                if (addedFileExts.Last().ToString() != ",") 
                {
                    addedFileExts += ",";
                }
            }

            addedFileExts += textBox1.Text;
            textBox1.Text = "";
            FileExtsToShow();

            changedSettings = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(textBox1.Text)
                || textBox1.Text == exampleText)
                return;

            //check input validation
            if (!IsValidInput(textBox1.Text))
            {
                MessageBox.Show(Properties.Resources.FileExtValidError);
                return;
            }

            string newAddedExts = RemoveFileExts(addedFileExts, textBox1.Text);
            if (newAddedExts == addedFileExts) 
            {
                MessageBox.Show(Properties.Resources.F_Exts_FindErrorMsg);
            }
            else
            {
                addedFileExts = newAddedExts;
                textBox1.Text = "";
                FileExtsToShow();

                changedSettings = true;
            }
        }

        private void FileExtsDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            InitStyle();
        }

        public void ResetAddedExts()
        {
            addedFileExts = "";
            removedFileExts = "";
            FileExtsToShow();
        }

        private void textBox1_Enter(object sender, EventArgs e)
        {
            if (textBox1.Text == exampleText)
            {
                textBox1.Text = "";
                textBox1.ForeColor = System.Drawing.SystemColors.ControlText;
            }
        }

        private void textBox1_Leave(object sender, EventArgs e)
        {
            if(String.IsNullOrEmpty(textBox1.Text))
            {
                textBox1.Text = exampleText;
                textBox1.ForeColor = System.Drawing.SystemColors.InactiveCaption;
            }

        }
    }
}
