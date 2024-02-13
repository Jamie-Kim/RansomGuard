using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RansomGuard.Helpers
{
    class NativeMethods
    {
        //****************************************************************
        // Win32 Messages
        //****************************************************************
        public const uint WM_CLOSE = 0x0010;
        public const uint WM_QUIT = 0x0012;
        public const uint WM_SYSCOMMAND = 0x0112;
        public const uint WM_MOUSEMOVE = 0x0200;
        public const uint WM_USER = 0x0400; //~0x7FFF
        public const uint SC_CLOSE = 0xF060;

        //****************************************************************
        // Filter and spyware related Win32 functions
        //****************************************************************
        [DllImport("fltlib", SetLastError = false)]
        public extern static int FilterConnectCommunicationPort(
                    [MarshalAs(UnmanagedType.LPWStr)]
                    string lpPortName,
                    uint dwOptions,
                    ref SERVICE_DATA lpContext,
                    uint wSizeOfContext,
                    IntPtr lpSecurityAttributes,
                    ref IntPtr hPort);

        [DllImport("fltLib.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public extern static int FilterGetMessage(
            IntPtr hPort,
            IntPtr lpMessageBuffer,
            int dwMessageBufferSize,
            IntPtr lpOverlapped);

        [DllImport("fltLib.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public extern static int FilterReplyMessage(
            IntPtr hPort,
            ref REPLY_MESSAGE lpReplyBuffer,
            int dwReplyBufferSize);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public extern static IntPtr CreateIoCompletionPort(
            IntPtr FileHandle,
            IntPtr ExistingCompletionPort,
            UIntPtr CompletionKey,
            uint NumberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool GetQueuedCompletionStatus(
            IntPtr CompletionPort,
            ref uint lpNumberOfBytes,
            ref UIntPtr lpCompletionKey,
            ref IntPtr lpOverlapped,
            int dwMilliseconds);

        [DllImport("kernel32")]
        static public extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);
        [DllImport("kernel32")]
        static public extern uint GetVolumePathName(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

        [DllImport("kernel32", SetLastError = true)]
        public extern static bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", EntryPoint = "RtlFillMemory", SetLastError = false)]
        public extern static void FillMemory(IntPtr destination, uint length, byte fill);

        [DllImport("shlwapi.dll")]
        public extern static bool PathIsNetworkPath(string pszPath);


        //****************************************************************
        // Command related Win32 functions
        //****************************************************************
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public extern static IntPtr SendMessage(IntPtr handle, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint SendMessage(IntPtr hWnd, UInt32 MSG, IntPtr zero, byte[] text); 

        public delegate bool EnumWindowsCallBack(IntPtr handle, int lParam);

        [DllImport("user32")]
        public extern static int EnumWindows(EnumWindowsCallBack ewcb, int lParam);

        [DllImport("user32.dll")]
        public extern static int GetWindowText(IntPtr handle, StringBuilder buffer, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("Shell32.dll")]
        public static extern void SHChangeNotify(long wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }

    class SafeCaseNativeMethods
    {
        //****************************************************************
        // SafeCase Filter related Win32 functions
        //****************************************************************
        [DllImport("fltlib", SetLastError = false)]
        public extern static int FilterAttach(
          [MarshalAs(UnmanagedType.LPWStr)]
          string lpFilterName,
          [MarshalAs(UnmanagedType.LPWStr)]
          string lpVolumeName,
          IntPtr lpInstanceName,
          uint dwCreatedInstanceNameLength,
          IntPtr lpCreatedInstanceName
        );

        [DllImport("fltlib", SetLastError = false)]
        public extern static int FilterConnectCommunicationPort(
                    [MarshalAs(UnmanagedType.LPWStr)]
                    string lpPortName,
                    uint dwOptions,
                    ref SC_SERVICE_DATA lpContext,
                    uint wSizeOfContext,
                    IntPtr lpSecurityAttributes,
                    ref IntPtr hPort);

        [DllImport("fltLib.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public extern static int FilterGetMessage(
            IntPtr hPort,
            IntPtr lpMessageBuffer,
            int dwMessageBufferSize,
            IntPtr lpOverlapped);

        [DllImport("fltLib.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public extern static int FilterReplyMessage(
            IntPtr hPort,
            ref SC_REPLY_MESSAGE lpReplyBuffer,
            int dwReplyBufferSize);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public extern static IntPtr CreateIoCompletionPort(
            IntPtr FileHandle,
            IntPtr ExistingCompletionPort,
            UIntPtr CompletionKey,
            uint NumberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool GetQueuedCompletionStatus(
            IntPtr CompletionPort,
            ref uint lpNumberOfBytes,
            ref UIntPtr lpCompletionKey,
            ref IntPtr lpOverlapped,
            int dwMilliseconds);

        [DllImport("kernel32")]
        static public extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);
        [DllImport("kernel32")]
        static public extern uint GetVolumePathName(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

        [DllImport("kernel32", SetLastError = true)]
        public extern static bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", EntryPoint = "RtlFillMemory", SetLastError = false)]
        public extern static void FillMemory(IntPtr destination, uint length, byte fill);

        [DllImport("shlwapi.dll")]
        public extern static bool PathIsNetworkPath(string pszPath);
    }
}
