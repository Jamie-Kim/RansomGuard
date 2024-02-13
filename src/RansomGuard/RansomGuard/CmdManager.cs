using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO.Pipes;
using System.IO;
using System.Windows.Forms;
using RansomGuard.Helpers;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RansomGuard
{
    class CmdManager : IDisposable
    {
        //shell context menu key
        const string shellContextMenuCryptKey = "rgCrypt";
        const string shellContextMenuDelKey = "rgDelete";

        //commands
        const string cmd_restart = "-restart";
        const string cmd_crypt = "-rgct";
        const string cmd_delete = "-rgdel";
        const string cmd_safecase = "-rgsf";

        //seperator for command and data
        const char data_seperator = '&';

        //pipe name
        const string rgPipe = "RgPipe";

        //client pipe connection waiting timeout
        const int connectionTimeout = 1000; //1 sec

        //pipe and listener thread
        NamedPipeServerStream pipe = null;
        Thread msgListener = null;

        //thread stop flag
        volatile bool stopListener = false;

        //****************************************************************
        // send command : program will be exited when return value is true 
        //****************************************************************
        public bool runCommand(string[] args)
        {
            //exit the program after the command
            bool needToExit = true;

            //if it is not valid command params, don't exit the program.
            if (!isValidCommand(args))
                return false;

            //command
            string command = args[0];

            //send a message based on command
            if(command == cmd_crypt)
            {
                sendCrypt(args);
            }
            else if (command == cmd_delete)
            {
                sendDelete(args);
            }
            else if (command == cmd_safecase)
            {
                sendSafeCase(args);
            }
            else
            {
                //do nothing
            }

            return needToExit;
        }

        public bool isValidCommand(string[] args)
        {
            if (args.Count() == 0)
                return false;

            //send message based on command
            if (args[0] == cmd_crypt)
            {
                //crypt data : cmd,filePath
                if (args.Count() != 2)
                    return false;
            }
            else if (args[0] == cmd_delete)
            {
                //del data : cmd,filePath
                if (args.Count() != 2)
                    return false;
            }
            else if (args[0] == cmd_safecase)
            {
                //del data : cmd,filePath
                if (args.Count() != 2)
                    return false;
            }
            else
            {
                //unknown command param
                return false;
            }

            return true;
        }

        public void sendCrypt(string[] args)
        {
            //set filepath
            string filePath = args[1];
            if (File.Exists(filePath))
            {
                SendMessage(cmd_crypt, filePath);
            }
        }

        private void sendDelete(string[] args)
        {
            string filePath = args[1];
            if (File.Exists(filePath))
            {
                SendMessage(cmd_delete, filePath);
            }
        }

        private void sendSafeCase(string[] args)
        {
            string filePath = args[1];
            if (File.Exists(filePath))
            {
                SendMessage(cmd_safecase, filePath);
            }
        }

        //****************************************************************
        // pipe listener message handler, will be run in main app
        //****************************************************************
        public async void MsgHandlerAsync(string revData)
        {
            string[] dataArray = revData.Split(data_seperator);

            if (string.IsNullOrEmpty(dataArray[0]) || string.IsNullOrEmpty(dataArray[1]) )
                return;

            //get command
            string command = dataArray[0];
            string param = dataArray[1];

            RgDebug.WriteLine(RgDebug.DebugType.CommandLog, "received data : {0}", revData);

            //received message
            await doCommandWork(command, param);

            RgDebug.WriteLine(RgDebug.DebugType.CommandLog, "doCommandWork done data : {0}", revData);
        }

        public Task doCommandWork(string command, string param)
        {
            return Task.Factory.StartNew(() =>
            {
                //received message
                if (command == cmd_crypt)
                {
                    doCrypt(param);
                }
                else if (command == cmd_delete)
                {
                    doDelete(param);
                }
                else if (command == cmd_safecase)
                {
                    doSafeCaseOpen(param);
                }
                else
                {
                    //unknown command
                    RgDebug.WriteLine(RgDebug.DebugType.Error, "Unknown command recevied : {0}", command);
                }
            });
        }

        public void doCrypt(string filePath)
        {
            Program.context.addCryptFile(filePath);
        }

        private void doDelete(string filePath)
        {
            Program.context.addDeleteFile(filePath);
        }

        private void doSafeCaseOpen(string filePath)
        {
            Program.context.openSafeCase(filePath);
        }

        //****************************************************************
        // Context menu add or remove
        //****************************************************************
        public void initShellContextMenu()
        {
            //ShellContextState  0: use menu, 1: no menu
            if (Properties.Settings.Default.ShellContextState == 0)
                addShellContextMenu();

            //add file association for the .rgct
            addFileAssociation();
        }

        public void uninitShellContextMenu()
        {
            if (Properties.Settings.Default.ShellContextState == 0)
                removeShellContextMenu();
        }

        public void setShellContextMenu(int state)
        {
            if(state == 0)
                addShellContextMenu();
            else
                removeShellContextMenu();
        }

        private void addShellContextMenu()
        {
            //get current app path 
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            string cryptCommand = string.Format("\"{0}\" {1} \"%L\"", appPath, cmd_crypt);
            Utilities.SetShellContextMenu("*", shellContextMenuCryptKey, Properties.Resources.MenuUtilsCrypt,
                cryptCommand, Properties.Resources.encryption);

            //set delete menu
            string delCommand = string.Format("\"{0}\" {1} \"%L\"", appPath, cmd_delete);
            Utilities.SetShellContextMenu("*", shellContextMenuDelKey, Properties.Resources.MenuUtilsShredder,
                delCommand, Properties.Resources.shredder);
        }

        private void removeShellContextMenu()
        {
            //unset all menus
            Utilities.UnSetShellContextMenu("*", shellContextMenuCryptKey);
            Utilities.UnSetShellContextMenu("*", shellContextMenuDelKey);
        }

        private void addFileAssociation()
        {
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            bool isUpdatedEnExt = false;
            bool isUpdatedSfExt = false;

            //associaltion for crypt file
            string cryptCommand = string.Format("\"{0}\" {1} \"%L\"", appPath, cmd_crypt);
            isUpdatedEnExt = Utilities.AddFileAssociation(FileCrypto.encFileExtension, cryptCommand, appPath, 1);

            //associaltion for safecase file
            string safecaseCommand = string.Format("\"{0}\" {1} \"%L\"", appPath, cmd_safecase);
            isUpdatedSfExt = Utilities.AddFileAssociation(SafeCaseManager.safecaseFileExt, safecaseCommand, appPath, 2);

            if(isUpdatedEnExt || isUpdatedSfExt)
            {
                //refresh icon
                long SHCNE_ASSOCCHANGED = 0x08000000;
                uint SHCNF_IDLIST = 0x0000;
                NativeMethods.SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
        }

        //****************************************************************
        // pipe related functions
        //****************************************************************
        public void CreatePipListenerProc()
        {
            msgListener = new Thread(new ThreadStart(ReadPipeProc));
        }

        public void CreatePipe(string name)
        {
            if (stopListener)
                return;

            //set server pipe UAC to allow everyone
            PipeSecurity ps = new PipeSecurity(); 
            ps.SetAccessRule(new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, AccessControlType.Allow));

            pipe = new NamedPipeServerStream(rgPipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, 
                PipeOptions.Asynchronous, 1024, 1024, ps);

            // ecursively wait for the connection again and again....
            pipe.BeginWaitForConnection( new AsyncCallback(AsyncPipeCallback), pipe);
        }

        public void AsyncPipeCallback(IAsyncResult Result)
        {
            var instream = new StreamReader(pipe, Encoding.UTF8);

            pipe.EndWaitForConnection(Result);
            string revData = instream.ReadLine();

            MsgHandlerAsync(revData);
            pipe.Close();

            // kill original sever and create new wait server
            CreatePipe(rgPipe);
        }

        public void SendMessage(string cmd, string param)
        {
            try
            {
                string sendData;
                var rgClient = new NamedPipeClientStream(rgPipe);
                rgClient.Connect(connectionTimeout);

                StreamWriter writer = new StreamWriter(rgClient);

                //set data and flush
                sendData = cmd + data_seperator + param;
                writer.WriteLine(sendData);
                writer.Flush();
            }
            catch { }
        }

        public void ReadPipeProc()
        {
            try
            {
                CreatePipe(rgPipe);
            }
            catch
            {
                StopMsgListener();
            }
        }

        public void StopMsgListener()
        {
            stopListener = true;

            if (pipe != null)
            {
                if (pipe.IsConnected)
                    pipe.Disconnect();
            }
        }

        public void StartMsgListener()
        {
            //create listener
            if (msgListener == null)
                CreatePipListenerProc();

            //set falg as default
            stopListener = false;
         
            //start listener
            if (!msgListener.IsAlive)
                msgListener.Start();
        }

        public void Dispose()
        {
            StopMsgListener();
        }
    }
}
