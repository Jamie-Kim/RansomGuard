using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using System.Threading;
using System.Reflection;
using System.Net;
using System.Net.Cache;
using System.IO;
using System.Xml;
using System.ComponentModel;

namespace rgUpdater
{
    class AutoAppUpdate
    {
        //URL of update info
        string appCastUrl;

        //XML node names
        const string appNodeName = "appname";
        const string versionNodeName = "version";
        const string urlNodeName = "url";
        const string componentsNodeName = "components";
        const string fileNodeName = "file";
        const string filenameAttName = "filename";
        const string locationAttName = "location";
        const string fileTypeAttName = "type";
        const string fileVersionAttName = "version";
        const string driverType = "driver";

        //temp file extension
        const string tempFileExt = ".tmp";
        const string doneFileExt = ".done";

        List<string> fileNameList;
        string downloadUrl;
        Version currentVersion;
        Version installedVersion;
        WebClient webClient;
        XmlDocument appCastDocument;
        string tempPath;
        string updatePath;

        bool isDriverUpdate;
        string drvFileName;

        public AutoAppUpdate(Version version, string srcPath, string targetPath, string baseUrl)
        {
            appCastUrl = baseUrl;

            installedVersion = version;
            tempPath = srcPath;
            updatePath = targetPath;

            isDriverUpdate = false;
        }

        public bool run()
        {
            bool result = false;

            try
            {
                if (IsUpdaterAvailable())
                {
                    //get update file list
                    fileNameList = getFileUrlList();

                    //download all files to temp folder
                    DownloadFiles(fileNameList);

                    //replace files
                    FileOverwrite(fileNameList);

                    //delete temp folder
                    DeleteFolder(tempPath);

                    //driver update
                    if (isDriverUpdate)
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo();
                        startInfo.FileName = updatePath + drvFileName;
                        startInfo.Arguments = @"-install";
                        Process.Start(startInfo).WaitForExit();
                    }

                    result = true;
                }
            }
            catch{ }

            return result;
        }

        List<string> getFileUrlList()
        {
            //download files
            XmlNode appCastItems = appCastDocument.SelectSingleNode(appNodeName);
            XmlNode components = appCastItems.SelectSingleNode(componentsNodeName);
            XmlNodeList fileItems = components.SelectNodes(fileNodeName);

            var list = new List<string>();

            foreach (XmlNode fileNode in fileItems)
            {
                if (fileNode != null)
                {
                    string filename = fileNode.Attributes[filenameAttName].Value;
                    string location = fileNode.Attributes[locationAttName].Value;
                    string type = fileNode.Attributes[fileTypeAttName].Value;
                    string version = fileNode.Attributes[fileVersionAttName].Value;

                    if(type == driverType)
                    {
                        isDriverUpdate = true;
                        drvFileName = location + filename;
                    }

                    if (installedVersion < new Version(version))
                    {
                        list.Add(location + filename);
                    }
                }
                else
                    continue;
            }

            return list;
        }

        bool IsUpdaterAvailable()
        {
            //try several times to connect max 30 sec
            for (int i = 0; i < 10; i++)
            {
                appCastDocument = GetXmlUpdateInfo();

                if (appCastDocument != null)
                    break;
                else
                    Thread.Sleep(3000);
            }

            //return error if internet is not connected.
            if (appCastDocument == null)
                return false;

            XmlNode appCastItem = appCastDocument.SelectSingleNode(appNodeName);

            //set download path
            XmlNode appCastUrl = appCastItem.SelectSingleNode(urlNodeName);
            if(appCastUrl != null)
                downloadUrl = appCastUrl.InnerText;

            //check driver update
            XmlNode drvUpdate = appCastItem.SelectSingleNode(urlNodeName);
            if (appCastUrl != null)
                downloadUrl = appCastUrl.InnerText;

            //check version
            XmlNode appCastVersion = appCastItem.SelectSingleNode(versionNodeName);
            if (appCastVersion != null)
            {
                String appVersion = appCastVersion.InnerText;
                currentVersion = new Version(appVersion);

                if (currentVersion > installedVersion)
                    return true;
            }

            return false;
        }

        void DownloadFiles(List<string> fileList)
        {
            // Loop through List with foreach.
            foreach (string filename in fileNameList) 
            {
                webClient = new WebClient();
                var uri = new Uri(downloadUrl + filename);
                string fullPath = tempPath + filename + tempFileExt;

                try
                {
                    //create folder if it is not exist
                    var directory = Path.GetDirectoryName(fullPath);
                    Directory.CreateDirectory(directory);

                    webClient.DownloadFile(uri, fullPath);
                    System.IO.File.Move(fullPath, System.IO.Path.ChangeExtension(fullPath, null));
                }
                catch{}
            }
        }

        bool AcceptAllCertifications(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        XmlDocument GetXmlUpdateInfo()
        {
            var uri = new Uri(appCastUrl);
            if (uri.Scheme == "https")
            {
                ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(AcceptAllCertifications);
            }

            WebRequest webRequest = WebRequest.Create(uri.ToString());
            webRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            WebResponse webResponse;
            try
            {
                webResponse = webRequest.GetResponse();
            }
            catch (Exception)
            {
                return null;
            }

            Stream appCastStream = webResponse.GetResponseStream();
            var receivedAppCastDocument = new XmlDocument();
            if (appCastStream != null)
            {
                receivedAppCastDocument.Load(appCastStream);
            }
            else
            {
                return null;
            }

            return receivedAppCastDocument;
        }

        void FileOverwrite(List<string> fileList)
        {
            foreach(string filename in fileList)
            {
                FileCopy(tempPath, updatePath, filename);
            }
        }

        bool FileCopy(string src, string target, string fName)
        {
            string fileName = fName;
            string sourcePath = src;
            string targetPath = target;

            string sourceFile = sourcePath + fileName;
            string destFile = targetPath + fileName;

            try
            {
                //create folder if is not exist
                var directory = Path.GetDirectoryName(destFile);
                Directory.CreateDirectory(directory);

                System.IO.File.Copy(sourceFile, destFile, true);
            }
            catch
            {
                return false;
            }

            return true;
        }

        void DeleteFolder(string FolderName)
        {
            try
            {
                //verify folder path to avoid mistake
                if (FolderName.Contains(Program.processName))
                {
                    //check folder name
                    Directory.Delete(FolderName, true);
                }
            }
            catch { }
        }
    }
}
