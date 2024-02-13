using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RansomGuard
{
    static class RgDebug
    {
        public enum DebugType : int
        {
            None        = 0x00000000,
            Error       = 0x00000001,
            FilterLog   = 0x00000002,
            TrayMenu    = 0x00000004,
            WarningLog  = 0x00000008,
            TraceLog    = 0x00000010,
            TrashBinLog = 0x00000020,
            SpywareLog  = 0x00000040,
            CryptLog    = 0x00000080,
            CommandLog  = 0x00000100,
            CloudLog    = 0x00000200,
            ProcessLog  = 0x00000400,
            SafeCaseLog = 0x00000800,
            DebugLog    = 0x00001000
        }

        static DebugType debugType = DebugType.None;
        static bool delDebugFile = true;
        static bool useDebugTime = true;

        static public void SetDebugMode(string path, string filename, string[] args, 
            DebugType debugLogType = DebugType.None)
        {
            //do not need to set debug when type is 0
            if(debugLogType == DebugType.None)
                return;

            string debugPath = Path.Combine(path, filename);

            //do not delete debug file in case of restart
            if (args.Contains("-restart"))
                delDebugFile = false;

            //delete debug file.
            if (delDebugFile)
            {
                try
                {
                    File.Delete(debugPath);
                }
                catch { }
            }

            TextWriterTraceListener[] listeners = new TextWriterTraceListener[] {
                                                    new TextWriterTraceListener(debugPath)
                                                    //new TextWriterTraceListener(Console.Out)
                                                    };
            Trace.AutoFlush = true;
            Trace.Listeners.AddRange(listeners);

            SetDebugLogType(debugLogType);
        }

        static public void SetDebugLogType(DebugType debugLogType = DebugType.None)
        {
            debugType = debugLogType;

            Trace.WriteLine( string.Format("[Debug Display Type] : 0x{0:X}", debugType));
        }

        static public void WriteLine(DebugType debugLogType, string message)
        {
            if (((int)debugType & (int)debugLogType) != 0)
            {
                string debugTime = "";

                //set time if it is enabled
                if (useDebugTime)
                    debugTime = DateTime.Now.ToString("HH:mm:ss") + " : ";

                Trace.WriteLine(debugTime + "[" + debugLogType.ToString() + "] " + message);
            }
        }

        static public void WriteLine(DebugType debugLogType, string message, params object[] args)
        {
            if (((int)debugType & (int)debugLogType) != 0)
            {
                string debugTime = "";

                //set time if it is enabled
                if (useDebugTime)
                    debugTime = DateTime.Now.ToString("HH:mm:ss") + " : ";

                Trace.WriteLine(String.Format(debugTime + "[" + debugLogType.ToString() + "] " + message, args));
            }
        }
    }
}
