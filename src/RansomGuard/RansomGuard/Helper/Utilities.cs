﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32;
using System.Drawing;
using System.Threading;
using System.Runtime.Serialization.Json;

namespace RansomGuard.Helpers
{
    static class Utilities
    {
        //****************************************************************
        // Get debug type
        //****************************************************************
        public static RgDebug.DebugType GetDebugType(string path, string filename)
        {
            const string debugTypeName = "DebugType:";
            string debugTypePath = Path.Combine(path, filename);
            int debugType = 0;

            try 
            {
                // Read the file by line.
                System.IO.StreamReader file = new System.IO.StreamReader(debugTypePath);

                var line = file.ReadLine();
                file.Close();

                if (line.Contains(debugTypeName))
                {
                    var debugTypeStr = line.Replace(debugTypeName, "");
                    debugType = Convert.ToInt32(debugTypeStr);  
                }
            }
            catch 
            {
                debugType = 0;
            }

            return (RgDebug.DebugType)debugType;
        }

        //****************************************************************
        // Registering file association
        //****************************************************************
        public static bool AddFileAssociation(string fileType, string menuCommand, string imageFilePath, int iconIndex)
        {
            //updated flag to refresh the icon
            bool isUpdated = false;

            try
            {
                //open and comapre with menuCommand
                string verifyKeyPath = string.Format(@"Software\Classes\{0}\Shell\Open\Command",fileType);
                RegistryKey verifyKey = Registry.CurrentUser.OpenSubKey(verifyKeyPath);
                if(verifyKey != null)
                {
                    var verifyCmd = verifyKey.GetValue(null);

                    //if it is same data, then return it.
                    if (menuCommand == verifyCmd.ToString())
                        return isUpdated;
                }

                //regist file association reg.
                RegistryKey root = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + fileType);

                using (RegistryKey DeIconkey = root.CreateSubKey("DefaultIcon"))
                {
                    DeIconkey.SetValue(null, string.Format("\"{0}\",{1}", imageFilePath, iconIndex));
                }

                using (RegistryKey Shellkey = root.CreateSubKey("Shell"))
                {
                    using (RegistryKey OpenKey = Shellkey.CreateSubKey("Open"))
                    {
                        using (RegistryKey CommandKey = OpenKey.CreateSubKey("Command"))
                        {
                            CommandKey.SetValue(null, menuCommand);
                            isUpdated = true;
                        }
                    }
                }


            }
            catch {}

            return isUpdated;
        }

        //****************************************************************
        // Autoplay disable or enable
        //****************************************************************

        //disable all autorun using NoDriveTypeAutoRun, if it is not exist return -1
        public static int disableAutorunForAllDevices()
        {
            const string rootKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
            const string autorunKey = "NoDriveTypeAutoRun";
            const int keyValueToDisable = 0xff;

            int orgValue = -1;

            try
            {
                // add context menu to the registry
                using (RegistryKey root = Registry.CurrentUser.CreateSubKey(rootKey))
                {
                    orgValue = Convert.ToInt32(root.GetValue(autorunKey, -1));
                    root.SetValue(autorunKey, keyValueToDisable, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch { }

            RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "disableAutorunForAllDevices , orgValue :{0}", orgValue);

            return orgValue;
        }

        public static void enableAutorun(int value)
        {
            const string rootKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
            const string autorunKey = "NoDriveTypeAutoRun";
            try
            {
                // add context menu to the registry
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(rootKey, true))
                {
                    if (value == -1)
                        root.DeleteValue(autorunKey);
                    else
                        root.SetValue(autorunKey, value, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch { }

            RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "enableAutorun , value :{0}", value);
        }

        //****************************************************************
        // Registering mouse right click context menu
        //****************************************************************
        public static bool UnSetShellContextMenu(string fileType, string shellKeyName)
        {
            // remove context menu from the registry
            if (!string.IsNullOrEmpty(shellKeyName))
            {
                try
                {
                    //check if it is exist

                    //delete key
                    string regPath = string.Format(@"Software\Classes\{0}\shell\{1}", fileType, shellKeyName);

                    // remove context menu from the registry
                    Registry.CurrentUser.DeleteSubKeyTree(regPath);
                }
                catch 
                {
                    return false;
                }
            }

            return true;
        }

        public static bool SetShellContextMenu(string fileType, string shellKeyName, 
            string menuText, string menuCommand, Bitmap icon)
        {
            try
            {
                RegistryKey root = Registry.CurrentUser.OpenSubKey(@"Software\Classes", true);

                // create path to registry location
                string regPath = string.Format(@"{0}\shell\{1}", fileType, shellKeyName);

                // add context menu to the registry
                using (RegistryKey key = root.CreateSubKey(regPath))
                {
                    key.SetValue(null, menuText);
                    //key.SetValue("icon", icon);
                }

                // add command that is invoked to the registry
                using (RegistryKey key = root.CreateSubKey(
                                string.Format(@"{0}\command", regPath)))
                {
                    key.SetValue(null, menuCommand);
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        //****************************************************************
        // file related utils
        //****************************************************************
        public static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        //****************************************************************
        // replace rgupdate at runtime
        //****************************************************************
        public static void UpdateRgUpdater(Version updaterVer, string updaterName)
        {
            try
            {
                //get rgupdater path
                string currFolder =  Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string path = Path.Combine(currFolder, updaterName);

                //check rgupdater version.
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(path);
                var currVersion = new Version(versionInfo.ProductVersion);

                if (updaterVer > currVersion)
                {
                    //write file.
                    File.WriteAllBytes(path, Properties.Resources.rgUpdater);
                }

            } catch{ }
        }

        //****************************************************************
        // show tooltip
        //****************************************************************
        public static void showMalwareFixTooltip(NotifyIcon notifyIcon, string malware, string text, bool isFixed)
        {
            string balloonTip = "";

            //set balloon texts
            if (isFixed)
            {
                notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                balloonTip += Properties.Resources.BalloonTip_MalwareFixed + Environment.NewLine;
            }
            else
            {
                notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
                balloonTip += Properties.Resources.BalloonTip_MalwareFixFailed + Environment.NewLine;
            }

            balloonTip += text + Environment.NewLine;

            //set balloon properties
            notifyIcon.BalloonTipTitle = malware;
            notifyIcon.BalloonTipText = balloonTip;

            const int timemout = 10000;
            notifyIcon.Visible = true;

            //show balloontip
            notifyIcon.ShowBalloonTip(timemout);
        }

        public static void showWarningTooltip(NotifyIcon notifyIcon, string title, string text)
        {
            string balloonTip = "";

            //set balloon texts
            notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
            balloonTip += text + Environment.NewLine;

            //set balloon properties
            notifyIcon.BalloonTipTitle = title;
            notifyIcon.BalloonTipText = balloonTip;

            const int timemout = 10000;
            notifyIcon.Visible = true;

            //show balloontip
            notifyIcon.ShowBalloonTip(timemout);
        }
    }

    /// <summary>
    /// Contains helper methods to change extended styles on ListView, including enabling double buffering.
    /// Based on Giovanni Montrone's article on <see cref="http://www.codeproject.com/KB/list/listviewxp.aspx"/>
    /// </summary>
    public class ListViewHelper
    {
        public enum ListViewExtendedStyles : int
        {
            /// <summary>
            /// LVS_EX_GRIDLINES
            /// </summary>
            GridLines = 0x00000001,
            /// <summary>
            /// LVS_EX_SUBITEMIMAGES
            /// </summary>
            SubItemImages = 0x00000002,
            /// <summary>
            /// LVS_EX_CHECKBOXES
            /// </summary>
            CheckBoxes = 0x00000004,
            /// <summary>
            /// LVS_EX_TRACKSELECT
            /// </summary>
            TrackSelect = 0x00000008,
            /// <summary>
            /// LVS_EX_HEADERDRAGDROP
            /// </summary>
            HeaderDragDrop = 0x00000010,
            /// <summary>
            /// LVS_EX_FULLROWSELECT
            /// </summary>
            FullRowSelect = 0x00000020,
            /// <summary>
            /// LVS_EX_ONECLICKACTIVATE
            /// </summary>
            OneClickActivate = 0x00000040,
            /// <summary>
            /// LVS_EX_TWOCLICKACTIVATE
            /// </summary>
            TwoClickActivate = 0x00000080,
            /// <summary>
            /// LVS_EX_FLATSB
            /// </summary>
            FlatsB = 0x00000100,
            /// <summary>
            /// LVS_EX_REGIONAL
            /// </summary>
            Regional = 0x00000200,
            /// <summary>
            /// LVS_EX_INFOTIP
            /// </summary>
            InfoTip = 0x00000400,
            /// <summary>
            /// LVS_EX_UNDERLINEHOT
            /// </summary>
            UnderlineHot = 0x00000800,
            /// <summary>
            /// LVS_EX_UNDERLINECOLD
            /// </summary>
            UnderlineCold = 0x00001000,
            /// <summary>
            /// LVS_EX_MULTIWORKAREAS
            /// </summary>
            MultilWorkAreas = 0x00002000,
            /// <summary>
            /// LVS_EX_LABELTIP
            /// </summary>
            LabelTip = 0x00004000,
            /// <summary>
            /// LVS_EX_BORDERSELECT
            /// </summary>
            BorderSelect = 0x00008000,
            /// <summary>
            /// LVS_EX_DOUBLEBUFFER
            /// </summary>
            DoubleBuffer = 0x00010000,
            /// <summary>
            /// LVS_EX_HIDELABELS
            /// </summary>
            HideLabels = 0x00020000,
            /// <summary>
            /// LVS_EX_SINGLEROW
            /// </summary>
            SingleRow = 0x00040000,
            /// <summary>
            /// LVS_EX_SNAPTOGRID
            /// </summary>
            SnapToGrid = 0x00080000,
            /// <summary>
            /// LVS_EX_SIMPLESELECT
            /// </summary>
            SimpleSelect = 0x00100000
        }

        public enum ListViewMessages : int
        {
            First = 0x1000,
            SetExtendedStyle = (First + 54),
            GetExtendedStyle = (First + 55),
        }

        public ListViewHelper()
        {
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr handle, int messg, int wparam, int lparam);

        public static void SetExtendedStyle(Control control, ListViewExtendedStyles exStyle)
        {
            ListViewExtendedStyles styles;
            styles = (ListViewExtendedStyles)SendMessage(control.Handle, (int)ListViewMessages.GetExtendedStyle, 0, 0);
            styles |= exStyle;
            SendMessage(control.Handle, (int)ListViewMessages.SetExtendedStyle, 0, (int)styles);
        }

        public static void EnableDoubleBuffer(Control control)
        {
            ListViewExtendedStyles styles;

            // read current style
            styles = (ListViewExtendedStyles)SendMessage(control.Handle, (int)ListViewMessages.GetExtendedStyle, 0, 0);

            // enable double buffer and border select
            styles |= ListViewExtendedStyles.DoubleBuffer | ListViewExtendedStyles.BorderSelect;

            // write new style
            SendMessage(control.Handle, (int)ListViewMessages.SetExtendedStyle, 0, (int)styles);
        }

        public static void DisableDoubleBuffer(Control control)
        {
            ListViewExtendedStyles styles;

            // read current style
            styles = (ListViewExtendedStyles)SendMessage(control.Handle, (int)ListViewMessages.GetExtendedStyle, 0, 0);

            // disable double buffer and border select
            styles -= styles & ListViewExtendedStyles.DoubleBuffer;
            styles -= styles & ListViewExtendedStyles.BorderSelect;

            // write new style
            SendMessage(control.Handle, (int)ListViewMessages.SetExtendedStyle, 0, (int)styles);
        }
    }
}
