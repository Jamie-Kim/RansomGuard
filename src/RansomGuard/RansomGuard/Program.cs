using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using RansomGuard.Helpers;

namespace RansomGuard
{
    static class Program
    {
        public static Guid guid = new Guid("98ef108c-4ff6-421b-8b6c-b74d03fed075");
        public const string programId = "Ransom.Guard";
        public const string processName = "RansomGuard";
        public const string sanitizerExt = ".malware";

        //rg delete application path
        public const string rgWebHelpLink = "https://www.ransomguard.ca/web/index.php/howtouse/";
        public const string rgApiUrl = "https://www.ransomguard.ca/leo/api/";
        public const string rgGeoLocationUrl = "http://ip-api.com/json/";

        //then included updater version
        public const string rgUpdaterVer = "1.0.0.3";
        public const string rgUpdaterName = "rgUpdater.exe";

        //debug filename
        public const string debugLogName = "debug.log";
        public const string debugTypeName = "debug.type";

        //rg delete application path
        public const string rgDeletePath = @"Tools\RgDelete.exe";

        public static CmdManager cm;
        public static ProcessManager pm;
        public static NetManager nm;
        public static FilterDriverManager fm;
        public static SafeCaseManager sm;
        public static LogManager lm;
        public static ProductInfo pdInfo;
        public static RamsomGuardAppContext context;
        //public static MainForm mainForm;

        public static string sanitizerRootPath;
        public static string restoreRootPath;
        public static string logRootPath;
        public static string configPath;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            //set configuration pathes (creating folders etc)
            SetPath();

            //set debug type manually for debug mode otherwise it will be determined by debug type file. 
            RgDebug.DebugType debugShowType =  //RgDebug.DebugType.Error     | 
                                               //RgDebug.DebugType.FilterLog   | 
                                               //RgDebug.DebugType.TrayMenu    | 
                                               //RgDebug.DebugType.WarningLog  | 
                                               //RgDebug.DebugType.TraceLog    | 
                                               //RgDebug.DebugType.TrashBinLog | 
                                               //RgDebug.DebugType.SpywareLog  |
                                               //RgDebug.DebugType.CryptLog    |
                                               //RgDebug.DebugType.CommandLog  |
                                               RgDebug.DebugType.CloudLog    |
                                               //RgDebug.DebugType.ProcessLog  |                                                                                                                    
                                               //RgDebug.DebugType.SafeCaseLog |
                                               //RgDebug.DebugType.DebugLog    |
                                               RgDebug.DebugType.None;

            // do not overwrite if it is already set.
            if (debugShowType == RgDebug.DebugType.None)
            {
                //(DebugType:8191(1FFF)) is full debug message
                debugShowType = Utilities.GetDebugType(logRootPath, debugTypeName);
            }

            //set debug mode, if type is 0, then nothing will happen.
            RgDebug.SetDebugMode(logRootPath, debugLogName, args, debugShowType);

            //run command to access the program if there is not valid commands, then start app with cmd listener.
            cm = new CmdManager();
            if (cm.runCommand(args))
                return;

            //send product information to the server
            pdInfo = new ProductInfo();
            pdInfo.SendBasicInfoAsync();

            //create main form to receive windows message.
            using (lm = new LogManager())
            {
                using (pm = new ProcessManager())
                {
                    using (nm = new NetManager())
                    {
                        using (sm = new SafeCaseManager())
                        {
                            using (fm = new FilterDriverManager())
                            {
                                if (!fm.Start())
                                {
                                    MessageBox.Show(Properties.Resources.FilterConnectionError);
                                    return;
                                }
                                
                                //create context of app
                                context = new RamsomGuardAppContext();

                                //create and start command listener
                                cm.StartMsgListener();

                                //run the context
                                Application.Run(context);

                                //stop command listener
                                cm.StopMsgListener();
                            }
                        }
                    }
                }
            }
        }

        static void SetPath()
        {
            try
            {
                //set sanitizer folder path and create dir if it is not exist
                sanitizerRootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
                   Path.DirectorySeparatorChar + processName +
                   Path.DirectorySeparatorChar + "Sanitizer" +
                   Path.DirectorySeparatorChar;
                System.IO.Directory.CreateDirectory(sanitizerRootPath);            

                //set restore folder path and create dir if it is not exist
                restoreRootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
                   Path.DirectorySeparatorChar + processName +
                   Path.DirectorySeparatorChar + "RestoreFiles" +
                   Path.DirectorySeparatorChar;
                System.IO.Directory.CreateDirectory(restoreRootPath);

                //set log folder path
                logRootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
                    Path.DirectorySeparatorChar + processName +
                    Path.DirectorySeparatorChar + "Logs" +
                    Path.DirectorySeparatorChar + "LogFiles" +
                    Path.DirectorySeparatorChar;
                System.IO.Directory.CreateDirectory(logRootPath);

                //set config folder path
                configPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) +
                    Path.DirectorySeparatorChar + processName +
                    Path.DirectorySeparatorChar + "Config" +
                    Path.DirectorySeparatorChar;
                System.IO.Directory.CreateDirectory(configPath);
            }
            catch (IOException ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        static public NotifyIcon GetNotifyIcon()
        {
            return context.GetNotifyIcon();
        }
    }
}
