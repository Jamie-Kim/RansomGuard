using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Security.Cryptography.X509Certificates;

namespace rgUpdater
{
    static class Program
    {
        public const string processName = "RansomGuard";
        public const string rgExeFile = "RansomGuard.exe";


        public const string defaultUrl = "https://www.ransomguard.ca";
        public const string castPath = "/app_update/app.version.xml";

        public static string baseUrl;

        public static string rgExePath;
        public static string srcPath;
        public static string targetPath;

        public static Version version;

        [STAThread]
        static void Main(string[] args)
        {
            targetPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            srcPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                + @"\" + processName + @"\Tmp";
            rgExePath = targetPath + "\\" + rgExeFile;

            if(args.Length > 1)
            {
                if (!String.IsNullOrEmpty(args[1]))
                    baseUrl = args[1];
            }

            if (args.Contains("-update"))
            {
                //
                // check internet connection.
                //

                FileVersionInfo versionInfo;
                try
                {
                    versionInfo = FileVersionInfo.GetVersionInfo(rgExePath);
                    version = new Version(versionInfo.ProductVersion);
                }
                catch
                {
                    version = new Version(0,0,0,0);
                }

                //update the application
                DoAutoAppUpdate();
            }

            RunRansomGuard();
        }

        static bool DoAutoAppUpdate()
        {
            var updater = new AutoAppUpdate(version, srcPath, targetPath, GetBaseUrl());
            return updater.run();
        }

        static void RunRansomGuard()
        {
            //program restart without perameters
            if (File.Exists(rgExePath))
            {
                //check certification for the case of fraud program.


#if false
                if (hasCertificate(rgExePath))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = rgExePath;
                    Process.Start(startInfo);
                }
                else
                {
                    MessageBox.Show(rgExePath + " is modified or not verified program.");
                }
#else 
                //do not check file certificate
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = rgExePath;
                Process.Start(startInfo);
#endif
            }
        }

        static string GetBaseUrl()
        {
            if(String.IsNullOrEmpty(baseUrl))
            {
                baseUrl = defaultUrl;
            }
            baseUrl += castPath;

            return baseUrl;
        }

        static bool hasCertificate(string path)
        {
            X509Certificate2 certificate;
            bool result = false;

            //default texts
            string certificateIssuer = "";
            string strCertificate = "";

            try
            {
                X509Certificate theSigner = X509Certificate.CreateFromSignedFile(path);
                certificate = new X509Certificate2(theSigner);

                bool chainIsValid = false;
                var theCertificateChain = new X509Chain();

                theCertificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;

                /*
                * 
                * Using .Online here means that the validation WILL CALL OUT TO THE INTERNET
                * to check the revocation status of the certificate. Change to .Offline if you
                * don't want that to happen.
                */

                theCertificateChain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                theCertificateChain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                theCertificateChain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                chainIsValid = theCertificateChain.Build(certificate);
                if (chainIsValid)
                {
                    Debug.WriteLine("Publisher Information : " + certificate.SubjectName.Name);
                    Debug.WriteLine("Valid From: " + certificate.GetEffectiveDateString());
                    Debug.WriteLine("Valid To: " + certificate.GetExpirationDateString());
                    Debug.WriteLine("Issued By: " + certificate.Issuer);

                    strCertificate = certificate.SubjectName.Name;
                    certificateIssuer = certificate.Issuer;

                    result = true;
                }
            }
            catch { }

            return result;
        }

    }
}

