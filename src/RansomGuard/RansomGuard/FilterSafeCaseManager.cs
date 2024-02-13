using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Concurrent;
using RansomGuard.Helpers;
using System.Security.Cryptography;

namespace RansomGuard
{
    class FilterSafeCaseManager : IDisposable
    {
        private string portName = "\\rgCryptPort";
        private string processName = Program.processName;
        private IntPtr portPtr = IntPtr.Zero;
        private IntPtr completionPort = IntPtr.Zero;

        SC_SERVICE_DATA serviceData;
        SC_REPLY_MESSAGE replyMessage;

        //task realted variables
        Task[] tasks;
        const int maxTask = 1;

        internal FilterSafeCaseManager()
        {
            tasks = new Task[maxTask];
            serviceData = new SC_SERVICE_DATA();
            replyMessage = new SC_REPLY_MESSAGE();
        }

        internal bool Start(SafeCaseHeader sfHeader, string password, string loadedDrvLetter)
        {
            Stop();

            //init safecase service data
            filterDataInit(sfHeader, password, loadedDrvLetter);

            if (isNeedFilterLoad(serviceData))
            {
                //attach filter
                if (!connect())
                {
                    RgDebug.WriteLine(RgDebug.DebugType.FilterLog, "SafeCase Filter connection failed");
                    return false;
                }

                completionPort = SafeCaseNativeMethods.CreateIoCompletionPort(portPtr, IntPtr.Zero, UIntPtr.Zero, 0);
                if (completionPort == IntPtr.Zero)
                    return false;

                RgDebug.WriteLine(RgDebug.DebugType.FilterLog, "SafeCase Filter is Started");

                //create enough threads to handle messages from the filter dirver.
                for (int i = 0; i < maxTask; i++)
                {
                    tasks[i] = Task.Factory.StartNew(() => worker());
                    if (tasks[i] == null)
                        return false;
                }
            }

            return true;
        }

        internal void Stop()
        {
            if (portPtr != IntPtr.Zero)
            {
                SafeCaseNativeMethods.CloseHandle(portPtr);
                portPtr = IntPtr.Zero;
            }
            if (completionPort != IntPtr.Zero)
            {
                SafeCaseNativeMethods.CloseHandle(completionPort);
                completionPort = IntPtr.Zero;
            }
        }

        private bool isNeedFilterLoad(SC_SERVICE_DATA svData)
        {
            return (svData.CopyActionNotifyEnable == 1 ||
                    svData.CopyProtectEnable == 1 ||
                    svData.ReadOnlyEnable == 1 ||
                    svData.EncryptionType != (int)SafeCaseManager.EncryptionMethod.NONE);
        }

        private void filterDataInit(SafeCaseHeader sfHeader, string password, string loadedDrvLetter)
        {
            const int keyLength = 32;

            serviceData.Init();
            replyMessage.Reply.Init();

            // Auto encryption and decryption
            if (sfHeader.md == (int)SafeCaseManager.EncryptionMethod.NONE)
            {
                //no need to encryption or decryption
                serviceData.ReadDecryptionEnable = 0;
                serviceData.WriteEncryptionEnable = 0;
            }
            else
            {
                serviceData.ReadDecryptionEnable = 1;
                serviceData.WriteEncryptionEnable = 1;
            }

            // Copy protection by file name in security folder
            serviceData.CopyProtectEnable = 0;

            // Readonly Eanbled.
            serviceData.ReadOnlyEnable = (byte)sfHeader.rd;

            // Filter protect from unloading it during the running application(filter connected). 
            serviceData.FilterProtectEnable = 0;

            // Send file copy action info. to the application in case of the secured folder. 
            serviceData.CopyActionNotifyEnable = (byte)sfHeader.lg;

            // Predefined encryption method,  0: simple encryption with XOR:0, AES256:1, None:2
            serviceData.EncryptionType = (byte)sfHeader.md;

            // Set count of the secured folder.
            serviceData.SecurityPathCount = 1;

            // Accessed app's PID.
            serviceData.AppPid = getProcessId(processName);

            // Encryption Key based on password.
            var sha256 = SHA256.Create();
            byte[] PasswordHash = sha256.ComputeHash(Encoding.ASCII.GetBytes(password));

            for (int i = 0; i < keyLength; i++)
                serviceData.EncryptionKey[i] = PasswordHash[i];

            // Security paths.
            serviceData.SecurityPath[0] = driveLetterToVolume(loadedDrvLetter + @":");
        }

        private string driveLetterToVolume(string path)
        {
            StringBuilder volume = new StringBuilder();
            SafeCaseNativeMethods.QueryDosDevice(path.Substring(0, 2), volume, 260);
            return volume + path.Substring(2, path.Length - 2);
        }

        private int getProcessId(string processName)
        {
            Process[] prs = Process.GetProcesses();
            foreach (Process pr in prs)
            {
                if (pr.ProcessName == processName)
                {
                    return pr.Id;
                }
            }

            return 0;
        }

        private bool connect()
        {
            int status;

            status = SafeCaseNativeMethods.FilterConnectCommunicationPort(
                                    portName,
                                    0,
                                    ref serviceData,
                                    Convert.ToUInt16(Marshal.SizeOf(typeof(SC_SERVICE_DATA))),
                                    IntPtr.Zero,
                                    ref portPtr);

            return (status) == 0;
        }

        private void worker()
        {
            bool isError = false;

            //send file path and pid.
            RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "Start SfFilter Worker Thread");

            var notifyMessage = new SC_NOTIFICATION_MESSAGE();

            int ovlpOffset = Marshal.OffsetOf(typeof(SC_NOTIFICATION_MESSAGE), "Ovlp").ToInt32();

            IntPtr messagePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SC_NOTIFICATION_MESSAGE)));
            Marshal.StructureToPtr(notifyMessage, messagePtr, false); // memset to 0

            IntPtr replyMessagePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SC_REPLY_MESSAGE)));
            Marshal.StructureToPtr(replyMessage, replyMessagePtr, false); // memset to 0

            try
            {
                while (true)
                {
                    uint numOfBytes = 0;
                    UIntPtr completionKeyPtr = UIntPtr.Zero;
                    IntPtr overlappedPtr = IntPtr.Zero;

                    //filter message it has own queue buffer inside, so we don't need to worry about it.
                    long res = SafeCaseNativeMethods.FilterGetMessage(
                                                portPtr,
                                                messagePtr,
                                                ovlpOffset,
                                                IntPtr.Add(messagePtr, ovlpOffset));

                    HRESULT hr = new HRESULT((uint)res);
                    if (hr != Win32Error.ERROR_IO_PENDING.ToHRESULT())
                    {
                        RgDebug.WriteLine(RgDebug.DebugType.Error, "FilterGetMessage");
                        isError = true;
                        break;
                    }

                    if (!SafeCaseNativeMethods.GetQueuedCompletionStatus(completionPort,
                                                                 ref numOfBytes,
                                                                 ref completionKeyPtr,
                                                                 ref overlappedPtr,
                                                                 int.MaxValue))
                    {
                        RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "GetQueuedCompletionStatus");
                        break;
                    }

                    var message = (SC_NOTIFICATION_MESSAGE)Marshal.PtrToStructure(overlappedPtr - ovlpOffset,
                                                                               typeof(SC_NOTIFICATION_MESSAGE));

                    RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog,
                        "Message Get Path:{0},  Pid: {1}, Type: {2}", message.Notification.Contents.Command, 
                                                                      message.Notification.Contents.Pid, 
                                                                      message.Notification.Contents.FilePath);

                    replyMessage.ReplyHeader.Status = 0;
                    replyMessage.ReplyHeader.MessageId = message.MessageHeader.MessageId;
                    replyMessage.Reply.Pid = message.Notification.Contents.Pid;
                    replyMessage.Reply.Command = onMessageHandler(message);

                    //use sizeof(FILTER_REPLY_HEADER) + sizeof(MY_STRUCT) instead of sizeof(REPLY_STRUCT)
                    res = SafeCaseNativeMethods.FilterReplyMessage(portPtr,
                                                           ref replyMessage,
                                                           Marshal.SizeOf(typeof(SC_FILTER_REPLY_HEADER)) + Marshal.SizeOf(typeof(SC_REPLY_DATA)));

                    hr = new HRESULT((uint)res);
                    if (!hr.Succeeded)
                    {
                        RgDebug.WriteLine(RgDebug.DebugType.Error, "Failed to reply");
                        isError = true;
                    }
                }
            }
            catch
            {
                RgDebug.WriteLine(RgDebug.DebugType.Error, "SfFilter Messeage Exception");
                isError = true;
            }
            finally
            {
                Marshal.FreeHGlobal(messagePtr);
                Marshal.FreeHGlobal(replyMessagePtr);

                messagePtr = IntPtr.Zero;
                replyMessagePtr = IntPtr.Zero;

                RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "SfFilter worker terminated");

                if (isError)
                {
                    RgDebug.WriteLine(RgDebug.DebugType.Error, "Restart filter caused by errors");

                    //create thread for wait to terminate all threads and restart filter.
                    //filterRestart();
                }
            }
        }

        //Process inspection code , compare process with the list
        private int onMessageHandler(SC_NOTIFICATION_MESSAGE msg)
        {
            return 0;
        }

        public void Dispose()
        {
            //close filter handle
            Stop();
        }
    }

    #region structs
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SC_SERVICE_DATA
    {
        internal void Init()
        {
            this.EncryptionKey = new byte[512];
            this.SecurityPath = new StringWithMaxSize260[10];
        }

        // Auto decryption when file read
        public byte ReadDecryptionEnable;

        // Auto encryption when file read
        public byte WriteEncryptionEnable;

        // Copy protection by file name in security folder
        public byte CopyProtectEnable;

        // Readonly Eanbled.
        public byte ReadOnlyEnable;

        // Filter protect from unloading it during the running application(filter connected). 
        public byte FilterProtectEnable;

        // Send file copy action info. to the application in case of the secured folder. 
        public byte CopyActionNotifyEnable;

        // Predefined encryption method,  0: simple encryption with XOR, 1: AES256
        public int EncryptionType;

        // Set count of the secured folder.
        public int SecurityPathCount;

        // Accessed app's PID.
        public int AppPid;

        // Encryption Key.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public byte[] EncryptionKey;

        // Security paths.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public StringWithMaxSize260[] SecurityPath;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct StringWithMaxSize260
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            private string Value;

            public static implicit operator string(StringWithMaxSize260 source)
            {
                return source.Value;
            }

            public static implicit operator StringWithMaxSize260(string source)
            {
                return new StringWithMaxSize260 { Value = source };
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SC_NOTIFICATION_MESSAGE
    {
        //  Required structure header.
        public SC_FILTER_MESSAGE_HEADER MessageHeader;

        //  Private fields begin here.
        public SC_NOTIFICATION Notification;

        //  Overlapped structure: this is not really part of the message
        //  However we embed it instead of using a separately allocated overlap structure
        public SC_OVERLAPPED Ovlp;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SC_NOTIFICATION
    {
        public uint BytesToScan;
        // for quad-word alignement of the Contents structure
        public uint Reserved;

        public int NotifyType;

        public SC_MSG_SEND_DATA Contents;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SC_MSG_SEND_DATA
    {
        //comand for the actions 1: block, 2:allow.
        public int Command;

        //process ID.
        public int Pid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string FilePath;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string Contents_Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SC_FILTER_MESSAGE_HEADER
    {
        public ulong ReplyLength; // uint
        public ulong MessageId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SC_FILTER_REPLY_HEADER
    {
        public int Status; // int Status;
        public ulong MessageId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SC_REPLY_MESSAGE
    {
        public SC_FILTER_REPLY_HEADER ReplyHeader;
        public SC_REPLY_DATA Reply;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SC_REPLY_DATA
    {
        internal void Init()
        {
            this.Options = new byte[256];
            this.Reserved = new byte[256];
        }

        //comand for the actions 1: block, 2:allow.
        public int Command;

        //process ID.
        public int Pid;

        //Not use for now
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] Options;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SC_OVERLAPPED
    {
        public int Internal;
        public int InternalHigh;
        public uType u;
        public int hEvent;

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public struct uType
        {
            [FieldOffset(8)]
            public sType s;
            [FieldOffset(16)]
            public int Pointer;

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct sType
            {
                public int Offset;
                public int OffsetHigh;
            }
        }
    }

    #endregion
}
