using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Management;
using System.Security.Cryptography;
using Microsoft.Win32;
using System.Windows.Forms;
using RansomGuard.Helpers;

namespace RansomGuard
{
    class ProcessManager : IDisposable
    {
        const int saveInterval = 30000;
        const string serviceHostName = "svchost.exe"; 

        // All process data to allow or disallow are saved in here.
        public List<ProcessData> prList;
        public List<ProcessData> prMalwareList;
        public List<ProcessWatchData> prWatchList;
        public List<ProcessTerminateData> prTerminateList;

        private bool prNeedToSaveFlag = false;

        private System.Windows.Forms.Timer SaveListTimer;

        // process auto allow for an hour
        private bool prAutoAllowFlag = false;
        private DateTime prAutoAllowSetTime;
        private const int prAutoAllowInterval = 1;   //1 hour

        //this is for saving pre file name for wathing to avoid to add already backed up file.
        ProcessWatchData prePrWatchData;

        public ProcessManager()
        {
            // load list
            prList = LoadProcessList();
            if (prList == null)
            {
                prList = new List<ProcessData>();
            }
            else
            {
                //fix processlist data
                FixProcessList(prList);
            }

            // for process watch list.
            prWatchList = new List<ProcessWatchData>();

            // for malware process list.
            prMalwareList = new List<ProcessData>();

            // to terminate the blocked process
            prTerminateList = new List<ProcessTerminateData>();

            // to watch process
            prePrWatchData = new ProcessWatchData();

            // set timer to save process Info
            SaveListTimer = new System.Windows.Forms.Timer();
            SaveListTimer.Tick += new EventHandler(OnTimedEvent);
            SaveListTimer.Interval = saveInterval;
            SaveListTimer.Enabled = true;
        }

        public void AddPr(ProcessData prData, int permission)
        {
            if (!IsExistData(prData.processName, prData.processHashCode))
            {
                ProcessData newPrData = new ProcessData();

                newPrData.processId = prData.processId;
                newPrData.processName = prData.processName;
                newPrData.permision = permission;
                newPrData.ExecutablePath = prData.ExecutablePath;
                newPrData.processHashCode = prData.processHashCode;

                //add data to allowed list
                prList.Add(newPrData);

                //set process save flag on
                prNeedToSaveFlag = true;

                RgDebug.WriteLine(RgDebug.DebugType.ProcessLog,
                    "process added, {0}, {1}", newPrData.processName, newPrData.processHashCode);
            }
        }

        public void AddMalwarePr(ProcessData prData, int permission)
        {
            if (!IsExistData(prData.processName, prData.processHashCode))
            {
                ProcessData newPrData = new ProcessData();

                newPrData.processId = prData.processId;
                newPrData.processName = prData.processName;
                newPrData.permision = permission;
                newPrData.ExecutablePath = prData.ExecutablePath;
                newPrData.processHashCode = prData.processHashCode;
                newPrData.processDesFromWeb = prData.processDesFromWeb;

                //add data to allowed list
                prMalwareList.Add(newPrData);
                
                RgDebug.WriteLine(RgDebug.DebugType.ProcessLog,
                    "malware Added {0}, {1}", newPrData.processName, newPrData.processHashCode);
            }
        }

        public bool IsExistData(string prName, int hashCode)
        {
            //get hashcode :: haschcode is not reliable to compare as unique value, we need to compare the process name togather.
            ProcessData foundData = prList.Find(data => (data.processHashCode == hashCode) && (data.processName == prName));
            return ((foundData == null) ? false : true);  
        }

        public ProcessData GetPrData(string prName, int hashCode)
        {
            //get hashcode :: haschcode is not reliable to compare as unique value, we need to compare the process name togather.
            ProcessData foundData = prList.Find(data => (data.processHashCode == hashCode) && (data.processName == prName));
            return foundData;
        }

        public int GetCurrentPrPermission(string prName, int hashCode, string filePath)
        {
            ProcessData foundData = prList.Find(data => (data.processHashCode == hashCode) && (data.processName == prName));
            return ((foundData == null) ? 0 : foundData.permision);
        }

        public int GetQuickHashCode(string fileInfo)
        {
            return fileInfo.GetHashCode();
        }

        public ProcessData GetNewPrData(int pid)
        {
            ProcessData data = new ProcessData();

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "Select * From Win32_Process Where ProcessId=" + pid.ToString());

                ManagementObjectCollection processList = searcher.Get();
                foreach (ManagementObject obj in processList)
                {
                    if (obj["Name"] == null || obj["ExecutablePath"] == null)
                    {
                        return null;
                    }

                    //must info
                    data.processName = obj["Name"].ToString();
                    data.ExecutablePath = obj["ExecutablePath"].ToString();

                    if (data.processName != serviceHostName)
                    {
                        //optional info
                        if (obj["Description"] != null)
                            data.processDescription = obj["Description"].ToString();

                        if (obj["InstallDate"] != null)
                            data.installDate = obj["InstallDate"].ToString();

                        if (obj["Caption"] != null)
                            data.caption = obj["Caption"].ToString();
                    }
                    else
                    {
                        // Set file name and service name in case of the service process.
                        SetServiceInfo(data, pid);
                    }

                    //weired file path i don't know why , some error path has \\?\
                    data.ExecutablePath = data.ExecutablePath.Replace(@"\\?\", "");

                    //get size
                    try
                    {
                        data.size = new System.IO.FileInfo(data.ExecutablePath).Length.ToString();
                    }
                    catch { }

                    //get hash code
                    data.processHashCode = GetQuickHashCode(data.ExecutablePath + data.size);

                    //get pid
                    data.processId = pid;
                }
            }
            catch
            {
                RgDebug.WriteLine(RgDebug.DebugType.Error,
                    "GetNewPrData , Can't get process information");

                data = null;
            }

            return data;
        }


        private void SetServiceInfo(ProcessData prData, int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_Service Where ProcessId=" + pid.ToString());

            ManagementObjectCollection serviceList = searcher.Get();
            foreach (ManagementObject obj in serviceList)
            {
                prData.processName = (obj["Name"] ?? String.Empty).ToString();
                prData.ExecutablePath = (obj["PathName"] ?? String.Empty).ToString();
                prData.processDescription = (obj["Description"] ?? String.Empty).ToString();
            }

            //check service program file. there are some cases of wroung path name.
            if (prData.ExecutablePath.Contains(serviceHostName))
            {
                string servicePath = getDirtyPathName(prData.processName);
                if(!string.IsNullOrEmpty(servicePath))
                {
                    prData.ExecutablePath = servicePath;
                }
            }
        }

        private string getDirtyPathName(string serviceName)
        {
            const string nodeName = "ServiceDll";
            string servicePath = null;
            string keypath = String.Format(@"SYSTEM\CurrentControlSet\services\{0}\Parameters", serviceName);

            try
            {
                using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(keypath))
                {
                    if (registryKey != null)
                    {
                        Object obj = registryKey.GetValue(nodeName);
                        if (obj != null)
                        {
                            servicePath = obj.ToString();
                        }
                    }
                }
            }
            catch{}

            return servicePath;
        }

        // Timer routine to work realted to process like saving ,terminating etc.
        private void OnTimedEvent(object sender, System.EventArgs e)
        {
            //save the allowed or blocked process.
            if (prNeedToSaveFlag)
            {
                SaveProcessList(prList);
                prNeedToSaveFlag = false;
            }

            //kill the blocked process.
            PrKiller(prTerminateList);

            //fix if there are any malware processes
            MalwareFix(prMalwareList);
        }

        // Terminate Process
        public void AddTerminatePr(int pid)
        {
            ProcessTerminateData foundData = prTerminateList.Find(data => (data.pid == pid));
            if(foundData == null)
            {
                var terminatePrData = new ProcessTerminateData();
                terminatePrData.pid = pid;
                terminatePrData.didGentleTerminate = false;

                prTerminateList.Add(terminatePrData);
            }
        }

        private void PrKiller(List<ProcessTerminateData> list)
        {
            //ignore if list is empty
            if (list.Count <= 0)
                return;

            Process process;
            Process[] processlist = Process.GetProcesses();

            foreach (ProcessTerminateData data in list.ToList())
            {
                process = processlist.FirstOrDefault(pr => pr.Id == data.pid);
                if (process == null)
                {
                    list.Remove(data);
                    RgDebug.WriteLine(RgDebug.DebugType.ProcessLog, "null terminate {0}", data.pid);
                    break;
                }

                if (data.didGentleTerminate)
                {
                    //Forced kill if it is still alive.
                    process.Kill();
                    //remove the item
                    list.Remove(data);

                    Program.lm.AddLog(LogData.LogType.PrForcedKill, "", "",
                        process.ProcessName);

                    RgDebug.WriteLine(RgDebug.DebugType.ProcessLog, "forced kill {0}", process.ProcessName);
                }
                else
                {
                    //send WM_CLOSE message.
                    process.CloseMainWindow();
                    data.didGentleTerminate = true;

                    Program.lm.AddLog(LogData.LogType.PrGentleKill, "", "",
                        process.ProcessName);

                    RgDebug.WriteLine(RgDebug.DebugType.ProcessLog, "gentle kill {0}", process.ProcessName);
                }
            }
        }

        private void MalwareFix(List<ProcessData> list)
        {
            //ignore if list is empty
            if (list.Count <= 0)
                return;

            Process process;
            Process[] processlist = Process.GetProcesses();

            foreach (ProcessData data in list.ToList())
            {
                process = processlist.FirstOrDefault(pr => pr.Id == data.processId);
                if (process == null)
                {
                    bool isFixed = true;

                    RgDebug.WriteLine(RgDebug.DebugType.ProcessLog, "Malware was terminated {0}", data.processName);

                    try
                    {
                        //create folder
                        string desFolder = Path.Combine(Program.sanitizerRootPath, DateTime.Now.ToString("yyyyMMdd"));
                        string des = Path.Combine(desFolder, data.processName + Program.sanitizerExt);

                        //create directoty if it is not exist
                        System.IO.Directory.CreateDirectory(desFolder);

                        //do rename file and move to sanitizer path
                        File.Copy(data.ExecutablePath, des, true);

                        //delete src file
                        File.Delete(data.ExecutablePath);

                        Program.lm.AddLog(LogData.LogType.MalwareFix, "", data.processName, "", data.ExecutablePath);
                    }
                    catch 
                    {
                        RgDebug.WriteLine(RgDebug.DebugType.ProcessLog, "Failed to fix {0}", data.processName);
                        Program.lm.AddLog(LogData.LogType.MalwareFixFailed, "", data.processName, "", data.ExecutablePath);

                        //failed to copy or delete the file. we don't need to care of the reason.
                        isFixed = false;
                    }

                    //show successful or failed message
                    Utilities.showMalwareFixTooltip(Program.GetNotifyIcon(), data.processName
                        , data.processDesFromWeb, isFixed);

                    //in any cases just remove it.
                    list.Remove(data);

                    break;
                }
            }
        }

        //****************************************************************
        // process auto allowed when user checked it in warning message
        //****************************************************************

        public void EnableAutoAllow()
        {
            prAutoAllowFlag = true;
            prAutoAllowSetTime = DateTime.Now;
        }

        public void DisableAutoAllow()
        {
            prAutoAllowFlag = false;
        }

        public bool IsAutoAllowEnabled()
        {
            if (prAutoAllowFlag)
            {
                TimeSpan diff = DateTime.Now - prAutoAllowSetTime;
                double hours = diff.TotalHours;

                //if it is over 1 hour differ, then set false.
                if ((int)hours > prAutoAllowInterval)
                {
                    prAutoAllowFlag = false;
                }
            }

            return prAutoAllowFlag;
        }

        //****************************************************************
        //Related to watch process
        //****************************************************************

        public void AddToWatchList(string path, int hashCode)
        {
            var time = System.DateTime.Now.ToString("hh:mm:ss");

           if(prePrWatchData.modifedFilePath == path &&
               prePrWatchData.hashCode == hashCode &&
               prePrWatchData.time == time)
           {
               //we don't need to check it. it was already added.
               return;
           }
           else
           {
               var newPrWatchData = new ProcessWatchData();
               newPrWatchData.hashCode = hashCode;
               newPrWatchData.time = time;
               newPrWatchData.modifedFilePath = path;

               if (!newPrWatchData.Equals(prePrWatchData))
               {
                   prWatchList.Add(newPrWatchData);
#if !DEBUG
                   // don't run in debug mode since protected ID is not applied in debug mode.
                   if (File.Exists(path))
                       Program.pm.CopyFile(path, hashCode);
#endif
                   prePrWatchData = newPrWatchData;
               }
           }
        }

        public void CopyFile(string src, int hashCode)
        {
            string des;
            string desFolder = Program.restoreRootPath;
            desFolder += DateTime.UtcNow.ToString("yyyyMMdd") + hashCode.ToString() + @"\";
            des = desFolder + Path.GetFileName(src);

            try
            {
                //create directoty if it is not exist
                System.IO.Directory.CreateDirectory(desFolder);
                File.Copy(src, des, false);
            }
            catch (IOException e)
            {
                RgDebug.WriteLine(RgDebug.DebugType.Error, "CopyFile IOException source: {0}", e.Source);
            }
        }

        public List<ProcessWatchData> GetPrWatchList()
        {
            return prWatchList;
        }

        public void RemovePrWatchData(ProcessWatchData removeData)
        {
            prWatchList.Remove(removeData);
        }

        public void RemoveToWatchList(int hashCode)
        {
            List<ProcessWatchData> foundList = prWatchList.FindAll(data => (data.hashCode == hashCode));
            foreach(ProcessWatchData data in foundList)
            {
                prWatchList.Remove(data);
            }
        }

        public void RemovePrData(string prName, int hashCode)
        {
            ProcessData foundData = prList.Find(data => (data.processHashCode == hashCode) && (data.processName == prName));

            if(foundData != null)
                prList.Remove(foundData);
        }

        public int SetPrPermission(string prName, int hashCode, int cmd)
        {
            ProcessData foundData = prList.Find(data => (data.processHashCode == hashCode) && (data.processName == prName));
            foundData.permision = cmd;

            return ((foundData == null) ? 0 : foundData.permision);
        }

        //****************************************************************
        //get real hashcode of the process
        //****************************************************************

        private string GetHexCode(byte[] bytes, bool upperCase)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));

            return result.ToString();
        }

        public string GetFileHashCode(string path)
        {
            string hashStr;

            try
            {
                // if the build platform of this app is x86 use C:\windows\sysnative
                if (!Environment.Is64BitProcess)
                    path = path.ToLower().Replace(@"c:\windows\system32", @"c:\windows\sysnative");

                using (var sha256 = SHA256.Create())
                {
                    using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        // Process des
                        hashStr = GetHexCode(sha256.ComputeHash(stream), true);
                    }
                }
            }
            catch
            {
                return null;
            }

            return hashStr;
        }

        //****************************************************************
        // Save and load the list
        //****************************************************************

        public void SaveProcessListBeforeExit()
        {
            SaveListTimer.Enabled = false;

            if (prNeedToSaveFlag)
            {
                SaveProcessList(prList);
                prNeedToSaveFlag = false;
            }
        }

        public void SaveForceProcessList()
        {
            SaveProcessList(prList);
        }

        public void SaveProcessList(List<ProcessData> prData)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, prData);
                ms.Position = 0;
                byte[] buffer = new byte[(int)ms.Length];
                ms.Read(buffer, 0, buffer.Length);
                Properties.Settings.Default.ProcessList = Convert.ToBase64String(buffer);
                Properties.Settings.Default.Save();
            }
        }

        public List<ProcessData> LoadProcessList()
        {
            if (String.IsNullOrEmpty(Properties.Settings.Default.ProcessList))
                return null;

            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(Properties.Settings.Default.ProcessList)))
            {
                BinaryFormatter bf = new BinaryFormatter();
                return (List<ProcessData>)bf.Deserialize(ms);
            }
        }

        public void FixProcessList(List<ProcessData> list)
        {
            //delete watch set nodes.
            int removedCnt = list.RemoveAll(p => p.permision == (int)ProcessData.Permision.Watch);
            if (removedCnt > 0)
            {
                RgDebug.WriteLine(RgDebug.DebugType.Error, "FixProcessList {0}", removedCnt);
            }
        }

        //****************************************************************
        // Destory or clean the list
        //****************************************************************

        public void Reset()
        {
            prList.Clear();
            prTerminateList.Clear();
            prWatchList.Clear();

            SaveProcessList(prList);
        }

        public void Dispose()
        {
            SaveListTimer.Enabled = false;
            prList.Clear();
        }
    }

    //****************************************************************
    // Process info to save or compare or show
    //****************************************************************

    [Serializable()]
    public class ProcessData
    {
        public enum Permision : int
        {
            None  = 0x00,
            Allow = 0x01,
            Block = 0x02,
            Watch = 0x03,
            Malware = 0x04
        }

        public enum WarningLevel : int
        {
            UseCloudInfo = 0x00,
            NonCertificate = 0x01,
            All = 0x02
        }

        public int processId { get; set; }
        public string processName { get; set; }
        public string size { get; set; }
        public string installDate { get; set; }
        public string caption { get; set; }
        public string processDescription { get; set; }
        public string ExecutablePath { get; set; }

        public string certficate { get; set; }
        public string processHash { get; set; }
        public string processDesFromWeb { get; set; }

        // 1 : allow , 2 : block
        public int permision { get; set; }
        public int processHashCode { get; set; }
        public bool dataSent { get; set; }

        public int cloudLevel { get; set; }
        public int cloudInfo { get; set; }
    }

    public class ProcessWatchData
    {
        public int hashCode { get; set; }
        public string time { get; set; }
        public string modifedFilePath { get; set; }
    }

    public class ProcessTerminateData
    {
        public bool didGentleTerminate { get; set; }
        public int pid { get; set; }
    }
}
