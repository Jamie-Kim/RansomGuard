using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace RansomGuard
{
    public class LogManager : IDisposable
    {
        //log file will be saved like rgLogs_yyyyMMdd.json in log root folder
        const string logPrefix = "rgLogs_";
        const string log_ext = ".json";

        List<LogData> logList;
        string logFilePath;

        bool lgNeedToSaveFlag = false;
        Timer SaveListTimer;
        const int saveInterval = 10000;

        delegate void LogerWriterDelegate();

        public LogManager()
        {
            //set log file path
            logFilePath = Program.logRootPath + logPrefix + DateTime.UtcNow.ToString("yyyyMMdd") + log_ext; 

            logList = new List<LogData>();

            // set timer to write log
            SaveListTimer = new Timer();
            SaveListTimer.Tick += new EventHandler(OnTimedEvent);
            SaveListTimer.Interval = saveInterval;

            SaveListTimer.Enabled = true;
        }

        // Timer routine to work realted to save log.
        private void OnTimedEvent(object sender, System.EventArgs e)
        {
            if (lgNeedToSaveFlag)
            {
                try
                {
                    DoLogWrite(logFilePath, logList);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine(ex.ToString());
                }

                //clear the list after writing.
                logList.Clear();

                lgNeedToSaveFlag = false;
            }
        }

        public void AddLog(LogData.LogType type, string filename = "", string proName = "", 
            string modPath = "", string prPath = "", string msg = "")
        {
            logList.Add(new LogData()
            {
                logType = (int)type,
                time = DateTime.Now.ToString(),
                logTypeStr = GetTypeStr(type),
                prName = proName,
                fileName = filename,
                prPath = prPath,
                modPath = modPath,
                logMsg = msg
            });

            lgNeedToSaveFlag = true;
        }

        private string GetTypeStr(LogData.LogType type)
        {
            string strType = "";

            switch (type)
            {
                case LogData.LogType.PrAllow:
                    strType = Properties.Resources.LogPrAllow;
                    break;
                case LogData.LogType.PrBlock:
                    strType = Properties.Resources.LogPrBlock;
                    break;
                case LogData.LogType.PrForcedKill:
                    strType = Properties.Resources.LogPrForcedKill;
                    break;
                case LogData.LogType.PrGentleKill:
                    strType = Properties.Resources.LogPrGentleKill;
                    break;
                case LogData.LogType.ProgramExit:
                    strType = Properties.Resources.LogProgramExit;
                    break;
                case LogData.LogType.ProgramStart:
                    strType = Properties.Resources.LogProgramStart;
                    break;
                case LogData.LogType.PrWatch:
                    strType = Properties.Resources.LogPrWatch;
                    break;
                case LogData.LogType.PrWriteAllow:
                    strType = Properties.Resources.LogPrWriteAllow;
                    break;
                case LogData.LogType.PrWriteBlock:
                    strType = Properties.Resources.LogPrWriteBlock;
                    break;
                case LogData.LogType.PrWriteWatch:
                    strType = Properties.Resources.LogPrWriteWatch;
                    break;                   
                case LogData.LogType.SetFolderLock:
                    strType = Properties.Resources.LogSetFolderLock;
                    break;
                case LogData.LogType.ReleaseFolderLock:
                    strType = Properties.Resources.LogReleaseFolderLock;
                    break;
                case LogData.LogType.Reset:
                    strType = Properties.Resources.LogReset;
                    break;
                case LogData.LogType.SetExtentions:
                    strType = Properties.Resources.LogSetExtentions;
                    break;
                case LogData.LogType.MalwareFix:
                    strType = Properties.Resources.LogMalwareFix;
                    break;
                case LogData.LogType.MalwareFixFailed:
                    strType = Properties.Resources.LogMalwareFixFailed;
                    break;
                case LogData.LogType.PrTrustedAllow:
                    strType = Properties.Resources.LogPrTrustedAllow;
                    break;
                case LogData.LogType.PrAutoAllow:
                    strType = Properties.Resources.LogPrUserAutoAllow;
                    break;

                default:
                    break;
            }

            return strType;
        }

        //save log before closing the program
        public void DoLogWriteBeforeExit()
        {
            SaveListTimer.Enabled = false;

            if (lgNeedToSaveFlag)
            {
                try
                {
                    DoLogWrite(logFilePath, logList);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }
        }

        // logwrite as json
        public void DoLogWrite(string path, List<LogData> logList)
        {
            MemoryStream stream1 = new MemoryStream();
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(List<LogData>));
            ser.WriteObject(stream1, logList);

            stream1.Position = 0;
            using (StreamReader sr = new StreamReader(stream1))
            {
                using (StreamWriter Writer = new StreamWriter(path, true, Encoding.UTF8))
                {
                    Writer.WriteLine(sr.ReadToEnd());
                }
            }
        }

        public void ReplaceTexts(string viewerPath)
        {
            string logviewerOrg = File.ReadAllText(viewerPath);

            var logviewerNew = logviewerOrg.Replace("{LogFolderPathAndText}", Properties.Resources.Log_Viewer_LogFolderPathAndText).
                                            Replace("{LogFolderPath}", Program.logRootPath).
                                            Replace("{LogContents}", Properties.Resources.Log_Viewer_LogContents).
                                            Replace("{ThDateTime}", Properties.Resources.Log_Viewer_ThDateTime).
                                            Replace("{ThLogType}", Properties.Resources.Log_Viewer_ThLogType).
                                            Replace("{ThProcess}", Properties.Resources.Log_Viewer_ThProcess).
                                            Replace("{ThFilePath}", Properties.Resources.Log_Viewer_ThFilePath).
                                            Replace("{ThProcessPath}", Properties.Resources.Log_Viewer_ThProcessPath).
                                            Replace("{ThEtc}", Properties.Resources.Log_Viewer_ThEtc);

            //write new text to the file
            File.WriteAllText(viewerPath, logviewerNew);
        }

        public void Dispose()
        {
            SaveListTimer.Enabled = false;
            logList.Clear();
        }
    }

    [Serializable]
    public class LogData
    {
        public enum LogType : int
        {
            PrAllow = 1,
            PrBlock,
            PrWatch,
            Reset,
            SetExtentions,
            SetFolderLock,
            ReleaseFolderLock,
            ProgramExit,
            ProgramStart,
            PrForcedKill,
            PrGentleKill,
            PrWriteBlock,
            PrWriteAllow,
            PrWriteWatch,
            MalwareFix,
            MalwareFixFailed,
            PrTrustedAllow,
            PrAutoAllow,

            MaxLogType
        }

        public int logType;
        public string time;
        public string logTypeStr;
        public string prName;
        public string fileName;
        public string prPath;
        public string modPath;
        public string logMsg;
    }
}
