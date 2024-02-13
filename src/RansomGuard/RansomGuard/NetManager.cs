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

using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using System.Net;
using System.Collections;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using NetFwTypeLib;

namespace RansomGuard
{
    class NetManager : IDisposable
    {
        const int interval = 5000;
        const string firewallPrefix = "rgInUse_";
        const string trustedProcessStr = "Trustworthy";
        private bool netNeedToSaveFlag = false;
        bool netStatusEnable;

        public List<NetData> netList;
        System.Windows.Forms.Timer netTimer;

        public NetManager()
        {
            netStatusEnable = false;

            // load list
            netList = LoadNetList();
            if (netList == null)
            {
                netList = new List<NetData>();
            }
            else
            {
                //fix data
                FixNetList(netList);
            }

            // set timer to save process Info
            netTimer = new System.Windows.Forms.Timer();
            netTimer.Tick += new EventHandler(OnTimedEvent);
            netTimer.Interval = interval;
            netTimer.Enabled = true;
        }
     
        public string GettrustedProcessStr()
        {
            return trustedProcessStr;
        }

        //****************************************************************
        // Timer Event
        //****************************************************************
        private void OnTimedEvent(object sender, System.EventArgs e)
        {
            netTimer.Stop();

            //query netstat
            if (netStatusEnable)
            {
                try
                {
                    GetNetState();
                }
                catch
                {
                    Debug.WriteLine("GetNetState Exception!");
                }
            }

            netTimer.Start();
        }

        public void EnableNetState(int timeInterval)
        {
            FixNetList(netList);

            netStatusEnable = true;

            netTimer.Stop();
            netTimer.Interval = timeInterval;
            netTimer.Start();
        }

        public void DisableNetState()
        {
            netStatusEnable = false;

            netTimer.Stop();
            netTimer.Interval = interval;
        }

        //****************************************************************
        // Net state
        //****************************************************************
        public void GetNetState()
        {
            NetData currNetData = null;
            TcpTable tcpList = null;

            Debug.Write("--- GetNetState Start---");

            try 
            {   
                //get netstat for tcp.
                tcpList = ManagedIpHelper.GetExtendedTcpTable(true);
            }
            catch
            {
                Debug.WriteLine("--- GetExtendedTcpTable Exception End---");
                return;
            }

            if (tcpList == null)
            {
                Debug.WriteLine("--- null End---");
                return;
            }

            foreach (var tcpRow in tcpList)
            {
                if (tcpRow == null)
                    continue;

                NetData netData = new NetData();

                //set tcpdata
                netData.tcpData = tcpRow;

                //we don't need to get prData here.
                netData.prData = null;

                //get netId
                netData.netId = getNetID(netData);
                if (netData.netId == 0)
                    continue;

                //get net data with simple id
                currNetData = GetExistData(netData.netId);

                //add or update net data
                if (currNetData != null)
                {
                    TcpDataUpdate(currNetData, netData);
                }
                else
                {
                    //get process data
                    netData.prData = Program.pm.GetNewPrData(tcpRow.ProcessId);
                    if (netData.prData != null)
                    {
                        currNetData = GetExistPrData(netData.prData.processHashCode);
                        if (currNetData != null)
                        {
                            TcpDataUpdate(currNetData, netData);
                        }
                        else
                        {
                            NetDataAdd(netData);
                        }
                    }
                }
            }

            Debug.WriteLine("--- Correct End---");
        }

        //****************************************************************
        // List operation
        //****************************************************************
        public void SaveNetListBeforeExit()
        {
            netTimer.Enabled = false;

            if (netNeedToSaveFlag)
            {
                SaveNetList(netList);
                netNeedToSaveFlag = false;
            }
        }

        public void SaveForceNetList()
        {
            SaveNetList(netList);
        }

        public void SaveNetList(List<NetData> list)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, list);
                ms.Position = 0;
                byte[] buffer = new byte[(int)ms.Length];
                ms.Read(buffer, 0, buffer.Length);
                Properties.Settings.Default.NetList = Convert.ToBase64String(buffer);
                Properties.Settings.Default.Save();
            }
        }

        public List<NetData> LoadNetList()
        {
            if (String.IsNullOrEmpty(Properties.Settings.Default.NetList))
                return null;

            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(Properties.Settings.Default.NetList)))
            {
                BinaryFormatter bf = new BinaryFormatter();
                return (List<NetData>)bf.Deserialize(ms);
            }
        }

        public void FixNetList(List<NetData> list)
        {
            //delet removed process node.
            try
            {
                int removedCnt = list.RemoveAll(p => File.Exists(p.prData.ExecutablePath) == false);
            }
            catch { }
        }

        public NetData GetExistData(int netId)
        {
            NetData foundData = netList.Find(data => (data.netId == netId));
            return foundData;
        }

        public NetData GetExistPrData(int hashCode)
        {
            NetData foundData = netList.Find(data => (data.prData.processHashCode == hashCode));
            return foundData;
        }

        public NetData GetNetDataByPath(string path)
        {
            NetData foundData = netList.Find(data => (data.prData.ExecutablePath == path));
            return foundData;
        }

        //****************************************************************
        // Net Data handling
        //****************************************************************

        public void TcpDataUpdate(NetData currNetData, NetData netData)
        {
            currNetData.tcpData = DeepClone<TcpRow>(netData.tcpData);

            //get access time if it is established
            if (netData.tcpData.State == TcpState.Established)
            {
                currNetData.lastAccesstTime = DateTime.Now;
                currNetData.lastAccessIp = netData.tcpData.RemoteEndPoint.ToString();
            }

            currNetData.lastUpdateTime = DateTime.Now;
        }

        public void PrDataUpdate(NetData currNetData, NetData netData)
        {
            currNetData.prData = DeepClone<ProcessData>(netData.prData);

            //get access time if it is established
            if (netData.tcpData.State == TcpState.Established)
            {
                currNetData.lastAccesstTime = DateTime.Now;
            }

            currNetData.lastUpdateTime = DateTime.Now;
        }

        public void NetDataAdd(NetData netData)
        {
            //get process data
            netData.prData = Program.pm.GetNewPrData(netData.tcpData.ProcessId);
            if (netData.prData != null)
            {
                //get cloud info
                SetCloudInfo(netData.prData);

                //get dns name
                //newNetData.dns = GetDnsName(newNetData.tcpData.RemoteEndPoint.Address);

                //add to list
                netData.lastUpdateTime = DateTime.Now;

                netList.Add(netData);
            }
        }

        //****************************************************************
        // Utils
        //****************************************************************

        public void FirewallBlockApp(string exPath, string name)
        {
            string strCmd = "/c netsh advfirewall firewall add rule name=\"" + firewallPrefix + name + "\" dir=out action=block program=\"" + exPath + "\" enable=yes";

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = strCmd;
            Process.Start(startInfo);

            GetNetDataByPath(exPath).netPermision = NetData.NetPermision.Blocked;
        }

        public void FirewallAllowApp(string exPath, string name)
        {
            string strCmd = "/c netsh advfirewall firewall delete rule name=\"" + firewallPrefix + name + "\"";

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = strCmd;
            Process.Start(startInfo);

            GetNetDataByPath(exPath).netPermision = NetData.NetPermision.Allowed;
        }

        public void FirewallRemoveApp(string exPath, string name)
        {
            string strCmd = "/c netsh advfirewall firewall delet rule name=\"" + firewallPrefix + name + "\"";

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = strCmd;
            Process.Start(startInfo);

            netList.Remove(GetNetDataByPath(exPath));
        }

        public void FirewallAdvReset()
        {
            foreach (var netData in netList)
            {
                if(netData.netPermision ==  NetData.NetPermision.Blocked)
                {
                    string strCmd = "/c netsh advfirewall firewall delet rule name=\"" + firewallPrefix + netData.prData.processName + "\"";

                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = strCmd;
                    Process.Start(startInfo);
                }       
            }
        }

        public static T DeepClone<T>(T obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                return (T)formatter.Deserialize(ms);
            }
        }

        public int getNetID(NetData net)
        {
            int netId = 0;
            string hashStr;

            try
            {
                Process process = Process.GetProcessById(net.tcpData.ProcessId);
                hashStr = process.ProcessName + net.tcpData.RemoteEndPoint.ToString();
                netId = hashStr.GetHashCode();
            }
            catch
            {
                Debug.WriteLine("--- GetProcessById Exception ---");
            }

            return netId;
        }

        public string GetDnsName(IPAddress ipAddr)
        {
            string dnsName = "";

            try
            {
                IPHostEntry hostInfo = Dns.GetHostEntry(ipAddr);
                Console.WriteLine("Host name : " + hostInfo.HostName);
                dnsName = hostInfo.HostName;
            }
            catch
            {
                Console.WriteLine("error getting dns name");
            }

            return dnsName;
        }

        public bool IsFirewallTurnedOn()
        {
            Type NetFwMgrType = Type.GetTypeFromProgID("HNetCfg.FwMgr", false);
            INetFwMgr mgr = (INetFwMgr)Activator.CreateInstance(NetFwMgrType);
            bool Firewallenabled = mgr.LocalPolicy.CurrentProfile.FirewallEnabled;

            return Firewallenabled;
        }

        //****************************************************************
        //Get Cloud Info
        //****************************************************************
        public void RefreshCloudInfo()
        {
            if (Properties.Settings.Default.WarningLevel != (int)ProcessData.WarningLevel.UseCloudInfo)
                return;

            Debug.WriteLine("RefreshCloudInfo Start");

            var netListCopy = new List<NetData>(netList);
            foreach (var netData in netListCopy)
            {
                SetTrustedProcess(netData.prData);
            }

            Debug.WriteLine("RefreshCloudInfo End");
        }

        public void SetCloudInfo(ProcessData prData)
        {
            if (Properties.Settings.Default.WarningLevel != (int)ProcessData.WarningLevel.UseCloudInfo)
                return;

            prData.certficate = getCertificate(prData.ExecutablePath);
            SetTrustedProcess(prData);
        }

        public string getCertificate(string path)
        {
            //default texts
            string strCertificate = "None";

            try
            {
                X509Certificate theSigner = X509Certificate.CreateFromSignedFile(path);
                X509Certificate2 certificate = new X509Certificate2(theSigner);

                bool chainIsValid = false;
                var theCertificateChain = new X509Chain();

                theCertificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                theCertificateChain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                theCertificateChain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                theCertificateChain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                chainIsValid = theCertificateChain.Build(certificate);
                if (chainIsValid)
                {
                    strCertificate = certificate.SubjectName.Name;
                }
            }
            catch { }

            return strCertificate;
        }

        //it need to connect to internet,so use it carefully.
        public void SetTrustedProcess(ProcessData prData)
        {
            int cloudLevel = 0;
            int cloudInfo = 0;
            const string trustedStr = trustedProcessStr;

            if (prData.certficate.Contains("Microsoft") ||
                prData.certficate.Contains("Orient Computer") ||
                prData.certficate.Contains("Google"))
            {
                prData.caption = trustedStr;
                prData.cloudLevel = (int)CloudInfo.Level.Safe;
                prData.cloudInfo = (int)CloudInfo.Info.KnownPubliher;
                return;
            }

            //check cloud info
            if (Properties.Settings.Default.WarningLevel == (int)ProcessData.WarningLevel.UseCloudInfo)
            {
                string hashCode = Program.pm.GetFileHashCode(prData.ExecutablePath);
                if (!string.IsNullOrEmpty(hashCode))
                {
                    prData.processHash = hashCode;

                    var cls = ApiHelper.getFileInfo(prData.processHash);
                    if (cls != null)
                    {
                        cloudLevel = Int32.Parse(cls.level);
                        cloudInfo = Int32.Parse(cls.info);

                        if (cloudLevel == (int)CloudInfo.Level.Safe)
                        {
                            prData.caption = trustedStr;
                        }

                        prData.cloudLevel = cloudLevel;
                        prData.cloudInfo = cloudInfo;
                    }
                }
            }
        }

        //****************************************************************
        // Destory or clean the list
        //****************************************************************
        public void Reset()
        {
            FirewallAdvReset();
            netList.Clear();
            SaveNetList(netList);
        }

        public void Dispose()
        {
            netTimer.Enabled = false;
            netList.Clear();
        }
    }

    [Serializable()]
    public class NetData
    {
        public enum NetPermision : int
        {
            Allowed = 0x00,
            Blocked = 0x01,
            Spyware = 0x10
        }

        public int netId;

        //process
        public ProcessData prData { get; set; }

        //tcp info
        public TcpRow tcpData { get; set; }

        //additional info
        public NetPermision netPermision { get; set; }
        public DateTime lastAccesstTime { get; set; }
        public DateTime lastUpdateTime { get; set; }

        public string lastAccessIp { get; set; }
        public string dns { get; set; }
        public int cloudLevel { get; set; }
        public int cloudInfo { get; set; }
    }

    #region Managed IP Helper API

    public class TcpTable : IEnumerable<TcpRow>
    {
        private IEnumerable<TcpRow> tcpRows;

        public TcpTable(IEnumerable<TcpRow> tcpRows)
        {
            this.tcpRows = tcpRows;
        }

        public IEnumerable<TcpRow> Rows
        {
            get { return this.tcpRows; }
        }

        public IEnumerator<TcpRow> GetEnumerator()
        {
            return this.tcpRows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.tcpRows.GetEnumerator();
        }
    }

    [Serializable()]
    public class TcpRow
    {
        private IPEndPoint localEndPoint;
        private IPEndPoint remoteEndPoint;
        private TcpState state;
        public int processId;

        public TcpRow(IpHelper.TcpRow tcpRow)
        {
            this.state = tcpRow.state;
            this.processId = tcpRow.owningPid;

            int localPort = (tcpRow.localPort1 << 8) + (tcpRow.localPort2) + (tcpRow.localPort3 << 24) + (tcpRow.localPort4 << 16);
            long localAddress = tcpRow.localAddr;
            this.localEndPoint = new IPEndPoint(localAddress, localPort);

            int remotePort = (tcpRow.remotePort1 << 8) + (tcpRow.remotePort2) + (tcpRow.remotePort3 << 24) + (tcpRow.remotePort4 << 16);
            long remoteAddress = tcpRow.remoteAddr;
            this.remoteEndPoint = new IPEndPoint(remoteAddress, remotePort);
        }

        public IPEndPoint LocalEndPoint
        {
            get { return this.localEndPoint; }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return this.remoteEndPoint; }
        }

        public TcpState State
        {
            get { return this.state; }
        }

        public int ProcessId
        {
            get { return this.processId; }
        }
    }

    public static class ManagedIpHelper
    {
        public static TcpTable GetExtendedTcpTable(bool sorted)
        {
            List<TcpRow> tcpRows = new List<TcpRow>();

            IntPtr tcpTable = IntPtr.Zero;
            int tcpTableLength = 0;

            if (IpHelper.GetExtendedTcpTable(tcpTable, ref tcpTableLength, sorted, IpHelper.AfInet, IpHelper.TcpTableType.OwnerPidAll, 0) != 0)
            {
                try
                {
                    tcpTable = Marshal.AllocHGlobal(tcpTableLength);
                    if (IpHelper.GetExtendedTcpTable(tcpTable, ref tcpTableLength, true, IpHelper.AfInet, IpHelper.TcpTableType.OwnerPidAll, 0) == 0)
                    {
                        IpHelper.TcpTable table = (IpHelper.TcpTable)Marshal.PtrToStructure(tcpTable, typeof(IpHelper.TcpTable));

                        IntPtr rowPtr = (IntPtr)((long)tcpTable + Marshal.SizeOf(table.length));
                        for (int i = 0; i < table.length; ++i)
                        {
                            tcpRows.Add(new TcpRow((IpHelper.TcpRow)Marshal.PtrToStructure(rowPtr, typeof(IpHelper.TcpRow))));
                            rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(typeof(IpHelper.TcpRow)));
                        }
                    }
                }
                finally
                {
                    if (tcpTable != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(tcpTable);
                    }
                }
            }

            return new TcpTable(tcpRows);
        }
    }

    #endregion

    #region P/Invoke IP Helper API

    /// <summary>
    /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366073.aspx"/>
    /// </summary>
    public static class IpHelper
    {
        public const string DllName = "iphlpapi.dll";
        public const int AfInet = 2;

        /// <summary>
        /// <see cref="http://msdn2.microsoft.com/en-us/library/aa365928.aspx"/>
        /// </summary>
        [DllImport(IpHelper.DllName, SetLastError = true)]
        public static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int tcpTableLength, bool sort, int ipVersion, TcpTableType tcpTableType, int reserved);

        /// <summary>
        /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366386.aspx"/>
        /// </summary>
        public enum TcpTableType
        {
            BasicListener,
            BasicConnections,
            BasicAll,
            OwnerPidListener,
            OwnerPidConnections,
            OwnerPidAll,
            OwnerModuleListener,
            OwnerModuleConnections,
            OwnerModuleAll,
        }

        /// <summary>
        /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366921.aspx"/>
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct TcpTable
        {
            public uint length;
            public TcpRow row;
        }

        /// <summary>
        /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366913.aspx"/>
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct TcpRow
        {
            public TcpState state;
            public uint localAddr;
            public byte localPort1;
            public byte localPort2;
            public byte localPort3;
            public byte localPort4;
            public uint remoteAddr;
            public byte remotePort1;
            public byte remotePort2;
            public byte remotePort3;
            public byte remotePort4;
            public int owningPid;
        }
    }

    #endregion
}


