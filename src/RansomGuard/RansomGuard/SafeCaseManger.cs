using RansomGuard.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Management.Automation;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace RansomGuard
{
    class SafeCaseManager : IDisposable
    {
        public enum EncryptionMethod : int
        {
            RC4 = 0,
            AES256 = 1,
            NONE = 2
        }

        //default defined values
        const string diskpartName = "diskpart.exe";
        public const string vhdFileExt = ".vhd";
        public const string safecaseFileExt = ".rgsf";
        public const string passwordRule = @"^(?=.*[a-zA-Z])((?=.*\d)|(?=.*\W)).{8,32}$";
        public const string namePathRule = @"^[a-zA-Z0-9 ()\\_:.\-]*$";

        //safecase header
        const string headerKey = "ABm1yS817I5vsOZK9We4jcby8lkjx74x";
        const string headerIV = "an67414VM870DCYW";
        const string aesIV = "tc67105VA312aCxT";
        const string keySalt = "tc1d055Vc321aTxO";
        const string rgsfIdentifier = "safecase"; // it should be 8 length

        //vhd footer info
        const string vhdIdentifier = "cxsparse";
        const int vhdIdentifierLen = 8;
        const int vhdHeaderSize = 512; //or 512 vhd file header size
        const int checksumOffset = 64; //checksum offset in vhd footer
        const int checksumSize = 4; //checksum size in vhd footer

        //safecase header info
        const int headerSize = 256; //max limit is 400
        const int cipherHeaderSize = headerSize + 16; //header paading after the encryption
        const int headerVersion = 2;

        //loaded safecase info
        string diskpartPath;
        string loadedImagePath;
        string loadedDriveLetter;
        string loadedPassword;

        SafeCaseHeader loadedHeader;

        //saved passwords for safecase.
        List<SafeCasePw> savedPwList;

        //safecase filter manager
        FilterSafeCaseManager sfFilterManager;

        public SafeCaseManager()
        {
            //set diskpart program path
            diskpartPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                diskpartName);

            savedPwList = LoadSafeCasePwList();
            if (savedPwList == null)
            {
                savedPwList = new List<SafeCasePw>();
            }

            sfFilterManager = new FilterSafeCaseManager();

            RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, 
                "SafeCaseManager is Created with {0} pw data", savedPwList.Count);
        }

        //****************************************************************
        // safecase basic functions
        //****************************************************************
        //maxSize in mb
        public bool CreateSafeCase(WaitingForm wf, SafeCaseHeader header, 
            string vhdPath, string orgPath, string maxSize, string label, string pw)
        {
            bool res = false;

            RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog,
                "Run CreateSafeCase {0} - {1}", label, orgPath);

            //error if there is not diskpart program or original file already exist
            if (!File.Exists(diskpartPath) || File.Exists(orgPath))
            {
                wf.Invoke((MethodInvoker)(() => wf.SafeCaseCreatingDone(res, null)));
                return res;
            }

            //create With init
            VhdCreateAndInit(vhdPath, maxSize, label);

            //detach vdisk
            DetachSafeCase(vhdPath);

            RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "detached vdisk");

            //rename and mount as original filename
            if (File.Exists(vhdPath))
            {
                try
                {
                    //rename to original path
                    File.Move(vhdPath, orgPath);

                    //write safecase settings
                    SetSafeCaseHeader(header, orgPath);

                    //mount vhd image
                    AttachSafeCaseWhenCreate(orgPath, pw);

                    res = true;
                }
                catch 
                {
                    RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "Creating header and mounting exceptions");
                }
            }

            //to show complete UI in waiting form, need to use invoke for safe update
            wf.Invoke((MethodInvoker)(() =>wf.SafeCaseCreatingDone(res, loadedDriveLetter)));

            return res;
        }

        //this routine will be used only when safecase created.
        public bool AttachSafeCaseWhenCreate(string vhdPath, string password)
        {
            if (!File.Exists(vhdPath))
                return false;

            //get safecase header
            SafeCaseHeader header = null; 
            try
            {
                header = GetSafeCaseHeader(vhdPath);
            }
            catch 
            { 
                //file open error, most caese are file is in use.
                return false; 
            }

            //replace file identifier to vhd to mount
            ReplaceIdentifier(vhdPath, false);

            //set password in use.
            SetLoadedPassword(password);

            //mount safecase
            mountSafeCase(vhdPath, header);

            //save header to loaded header
            loadedHeader = header;

            return true;
        }

        //safecase attach routine for existing file.
        public bool AttachSafeCase(string vhdPath, string password = null, bool openDrive = true)
        {
            bool res = false;
            string actualPassword = password;

            SafeCaseHeader header = null;

            if (!File.Exists(vhdPath))
                return res;

            //detach first if case of some errors, the file can be mounted already.
            unmountSafeCase(vhdPath);

            if (isSafeCaseFile(vhdPath) == false)
                return res;

            //get safecase header
            try
            {
                header = GetSafeCaseHeader(vhdPath);
            }
            catch
            {
                //file open error, most caese are file is in use.
                return false;
            }

            //check saved password is there ,if it is set, check if it is correct.
            bool isSavedPwCorrect = false;
            var pwData = GetPwData(header.gh);

            if (pwData != null)
            {
                string _pwHash = pwData.passsword.GetHashCode().ToString();

                //get actual password for sending to filter
                actualPassword = pwData.passsword;

                RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog,
                    "found saved password data - pwData {0} , {1}", pwData.guidHash, pwData.passsword);

                if (_pwHash == header.ph)
                {
                    isSavedPwCorrect = true;
                }
            }

            //open directly if the password is correct or show the password dialog.
            if (isSavedPwCorrect)
            {
                //replace file identifier to vhd to mount
                ReplaceIdentifier(vhdPath, false);

                //set password in use.
                SetLoadedPassword(actualPassword);

                //mount safecase               
                mountSafeCase(vhdPath, header);

                // opens the folder in explorer
                if (openDrive)
                {
                    Process.Start(loadedDriveLetter + @":\");
                }

                res = true;
            }
            else
            {
                //show password dialog
                using (var pwForm = new SafecasePwForm(header.nm, header.ph, header.ht, header.gh))
                {
                    if (pwForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        //replace file identifier to vhd to mount
                        ReplaceIdentifier(vhdPath, false);

                        //password will be set in SafecasePwForm.
                        //So we don't need to set the loaded password.
                        //mount safecase
                        mountSafeCase(vhdPath, header);

                        // opens the folder in explorer
                        if (openDrive)
                        {
                            Process.Start(loadedDriveLetter + @":\");
                        }

                        res = true;
                    }
                }
            }

            //save header to loaded header
            if(res)
            {
                loadedHeader = header;
            }

            return res;
        }

        public bool DetachSafeCase(string vhdPath)
        {
            if (!File.Exists(vhdPath))
                return false;

            //unmount safecase
            unmountSafeCase(vhdPath);

            return true;
        }

        public bool DetachLoadedSafeCase()
        {
            string targetPath = loadedImagePath;

            //unmount safecase
            unmountSafeCase(targetPath);

            //replace file identifier to safecase for the protection
            ReplaceIdentifier(targetPath, true);

            return true;
        }

        public bool SetSafeCaseHeader(SafeCaseHeader header, string targetPath)
        {
            //creater file and write encryted data           
            using (var fsInOut = File.Open(targetPath, FileMode.Open, FileAccess.ReadWrite))
            {    
                //set office to save the header in vhd header.
                SerializeHeader(header, fsInOut);

                //set checkum of the vhd header
                SetVhdCheckSum(fsInOut);
            }

            return true;
        }

        public SafeCaseHeader GetSafeCaseHeader(string targetPath)
        {
            SafeCaseHeader sfHeader;

            //creater file and write encryted data           
            using (var fsInOut = File.Open(targetPath, FileMode.Open, FileAccess.Read))
            {
                //set offset
                int headOffset = vhdHeaderSize - cipherHeaderSize;

                //write to copy of footer
                fsInOut.Seek(headOffset, SeekOrigin.Begin);

                //set office to save the header in vhd header.
                sfHeader = DeserializeHeader(fsInOut);
            }

            return sfHeader;
        }

        //****************************************************************
        // safecase filter related functions
        //****************************************************************
        private void StartSfFilter(SafeCaseHeader sfHeader)
        {
#if false
            bool result = sfFilterManager.Start(sfHeader, GetLoadedPassword(), GetLoadedDrvLetter());
            if(!result)
            {
                //show error message to user
                MessageBox.Show(Properties.Resources.FilterConnectionError);
            }
#endif
        }

        private void StopSfFilter()
        {
#if false
            sfFilterManager.Stop();
#endif
        }

        //****************************************************************
        // get safecase mounted status
        //****************************************************************
        public string GetLoadedImage()
        {
            return loadedImagePath;
        }

        public string GetLoadedDrvLetter()
        {
            return loadedDriveLetter;
        }

        public SafeCaseHeader GetLoadedHeader()
        {
            return loadedHeader;
        }

        public bool isSafeCaseAttached()
        {
            return !string.IsNullOrEmpty(loadedImagePath);
        }

        public void SetSafeCaseAttached(string imagePath)
        {
            loadedImagePath = imagePath;

            //get drive letter
            loadedDriveLetter = GetDriveLetter();
        }

        public void UnSafeCaseAttached(string imagePath)
        {
            if (loadedImagePath == imagePath)
            {
                loadedImagePath = null;
                loadedDriveLetter = null;
            }
        }

        public string GetLoadedPassword()
        {
            return loadedPassword;
        }

        public void SetLoadedPassword(string password)
        {
            loadedPassword = password;
        }

        //****************************************************************
        // Header related functions
        //****************************************************************
        public SafeCaseHeader CreateHeader(int _enMethod, int _useLog, string _name, string _hint, string _pw, string _guidHash)
        {
            SafeCaseHeader header = new SafeCaseHeader
            {
                gh = _guidHash,
                md = _enMethod,
                vr = headerVersion,
                lg = _useLog,
                nm = _name,
                ht = _hint,
                ph = _pw.GetHashCode().ToString(),
                dt = DateTime.UtcNow,

                //additional options
                id = Program.pdInfo.GetComputerId(),
                cp = 0,
                sp = 0,
                ue = 0
            };

            return header;
        }

        private bool isSafeCaseFile(string filePath)
        {
            return true;
        }

        private void SerializeHeader(SafeCaseHeader header, FileStream fsOutput)
        {
            string headerStr = ToJson<SafeCaseHeader>(header);

            byte[] data = Encoding.UTF8.GetBytes(headerStr);

            RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog,
               "SerializeHeader - data: {0} , byte length {1} - limit {2}", headerStr, data.Length, headerSize);           

            Array.Resize(ref data, headerSize);

            var enData = CryptHeader(data, true);

            //offset
            int headOffset = vhdHeaderSize - cipherHeaderSize;

            //write to copy of footer
            fsOutput.Seek(headOffset, SeekOrigin.Begin);
            fsOutput.Write(enData, 0, enData.Length);

            //write to footer
            fsOutput.Seek(headOffset - vhdHeaderSize, SeekOrigin.End);
            fsOutput.Write(enData, 0, enData.Length);
        }

        private void ReplaceIdentifier(string targetPath, bool isSafeCase)
        {
            using (var fsInOut = File.Open(targetPath, FileMode.Open, FileAccess.ReadWrite))
            {
                //write safecase identifier, 8 bytes
                byte[] identifierBytes;
                string identifierStr;

                //set identifier for VHD file
                if (isSafeCase)
                    identifierStr = rgsfIdentifier;
                else
                    identifierStr = vhdIdentifier;

                identifierBytes = Encoding.UTF8.GetBytes(identifierStr);

                //length should be 8.
                if (identifierBytes.Length == vhdIdentifierLen)
                {
                    fsInOut.Seek(vhdHeaderSize, SeekOrigin.Begin);
                    fsInOut.Write(identifierBytes, 0, identifierBytes.Length);
                }
            }
        }

        private string GetIdentifier(Stream stream)
        {
            var identifier = new byte[vhdIdentifierLen];

            //set offset
            stream.Seek(vhdHeaderSize, SeekOrigin.Begin);
            stream.Read(identifier, 0, identifier.Length);

            return System.Text.Encoding.UTF8.GetString(identifier);
        }

        private SafeCaseHeader DeserializeHeader(Stream stream)
        {
            var encData = new byte[cipherHeaderSize];

            stream.Read(encData, 0, encData.Length);

            var DecData = CryptHeader(encData, false);

            string dataStr = System.Text.Encoding.UTF8.GetString(DecData);

            //cut empty data
            int index = dataStr.IndexOf("}");
            if (index > 0)
                dataStr = dataStr.Substring(0, index + 1);

            var header = FromJson<SafeCaseHeader>(dataStr);

            RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "DeserializeHeader - data: {0}", dataStr);           

            return header;
        }

        private byte[] CryptHeader(byte[] sourceData, bool encryption)
        {
            using (var ms = new MemoryStream())
            {
                using (var aes = Rijndael.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(headerKey);
                    aes.IV = Encoding.UTF8.GetBytes(headerIV);

                    using (var cs = new CryptoStream(ms,
                                            encryption ? aes.CreateEncryptor() : aes.CreateDecryptor(),
                                            CryptoStreamMode.Write))
                    {
                        cs.Write(sourceData, 0, sourceData.Length);
                    }

                    return ms.ToArray();
                }
            }
        }

        public void SetVhdCheckSum(FileStream fs)
        {
            int sfChecksum = 0;
            int checksum = 0;
            var vhdHeader = new byte[vhdHeaderSize];

            //get checksum
            fs.Seek(0, SeekOrigin.Begin);
            fs.Read(vhdHeader, 0, vhdHeader.Length);
            
            for (int counter = 0; counter < vhdHeader.Length; counter++)
            {
                //skip checksum field
                if (counter >= checksumOffset &&
                    counter < checksumOffset + checksumSize)
                {
                    continue;
                }

                checksum += vhdHeader[counter];
            }

            //get safecase header checksum with vhd checksum.
            sfChecksum = ~checksum;

            //get checksum byte array
            byte[] chBytes = BitConverter.GetBytes(sfChecksum);

            //4-byte integer with bytes ordered in a big-endian way.
            if (BitConverter.IsLittleEndian)
                Array.Reverse(chBytes);

            //write checksum to copy of footer
            fs.Seek(64, SeekOrigin.Begin);
            fs.Write(chBytes, 0, chBytes.Length);

            //write checksum to footer
            fs.Seek(-vhdHeaderSize + 64, SeekOrigin.End);
            fs.Write(chBytes, 0, chBytes.Length);
        }

        //****************************************************************
        // powershell commands
        //****************************************************************
        public void unmountSafeCase(string imagePath)
        {
            using (var ps = PowerShell.Create())
            {
                ps.AddCommand("Dismount-DiskImage")
                    .AddParameter("ImagePath", imagePath)
                    .AddParameter("StorageType", "VHD")
                    .Invoke();
            }

            //set vhd mounted status
            UnSafeCaseAttached(imagePath);

            StopSfFilter();
        }

        public void mountSafeCase(string imagePath, SafeCaseHeader sfHeader)
        {
            using (var ps = PowerShell.Create())
            {
                var res = ps.AddCommand("Mount-DiskImage")
                  .AddParameter("ImagePath", imagePath)
                  .AddParameter("StorageType", "VHD")
                  .Invoke();
            }

            //set vhd mounted status
            SetSafeCaseAttached(imagePath);

            //start filter
            StartSfFilter(sfHeader);
        }

        public string GetDriveLetter()
        {
            string deviceNumber = "";
            string drvLetter = "";

            if (!isSafeCaseAttached())
                return drvLetter;
            try
            {
                using (var ps = PowerShell.Create())
                {
                    var res_gd = ps.AddCommand("Get-DiskImage")
                      .AddParameter("ImagePath", loadedImagePath)
                      .AddParameter("StorageType", "VHD")
                      .Invoke();

                    deviceNumber = res_gd[0].Members["Number"].Value.ToString();
                }

                //get drive letter using device number
                using (var ps = PowerShell.Create())
                {
                    var res_gp = ps.AddCommand("Get-Partition")
                        .AddParameter("-DiskNumber", deviceNumber)
                        .Invoke();

                    drvLetter = res_gp[0].Members["DriveLetter"].Value.ToString();

                    RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "GetDriveLetter - drvLetter : {0}", drvLetter);
                }
            }
            catch { }

            return drvLetter;
        }

        //****************************************************************
        // Diskpart commands
        //****************************************************************
        ProcessStartInfo getDiskpartStartInfo(string prPath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();

            //set propertise to run diskpart as backgound process
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardInput = true;
            startInfo.FileName = prPath;

            return startInfo;
        }

        void DiskpartEnd(Process diskpart)
        {
            diskpart.StandardInput.WriteLine("exit");
        }

        void VhdCreateAndInit(string vhdPath, string maxSize, string label)
        {
            //disk part start
            using (var diskpart = Process.Start(getDiskpartStartInfo(diskpartPath)))
            {
                //create vhd image
                diskpart.StandardInput.WriteLine("create vdisk file=\"" + vhdPath + "\"" +
                    " maximum=" + maxSize +
                    " type=expandable");

                //init vhd image
                diskpart.StandardInput.WriteLine("select vdisk file=\"" + vhdPath + "\"");
                diskpart.StandardInput.WriteLine("attach vdisk");
                diskpart.StandardInput.WriteLine("create partition primary");
                diskpart.StandardInput.WriteLine("format fs=ntfs" + " quick" + " label=\"" + label + "\"");
                diskpart.StandardInput.WriteLine("assign");

                //disk part end
                DiskpartEnd(diskpart);

                //wait until finish
                diskpart.WaitForExit();
            }
        }

        //****************************************************************
        // Saving password for the safecase
        //****************************************************************
        public SafeCasePw GetPwData(string hashCode)
        {
            var foundData = savedPwList.Find(data => (data.guidHash == hashCode));
            return foundData;
        }

        public bool IsExistPwData(string hashCode)
        {
            var foundData = savedPwList.Find(data => (data.guidHash == hashCode));
            return ((foundData == null) ? false : true);
        }

        public void RemovePassword(string hashCode)
        {
            var foundData = savedPwList.Find(data => (data.guidHash == hashCode));

            if (foundData != null)
            {
                savedPwList.Remove(foundData);

                RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "Password Removed hash : {0}", hashCode);
            }
        }

        public void AddPassword(string pw, string hashCode, bool needToSave = false)
        {
            if (!IsExistPwData(hashCode))
            {
                var newPwData = new SafeCasePw();

                newPwData.guidHash = hashCode;
                newPwData.passsword = pw;

                //add data to allowed list
                savedPwList.Add(newPwData);

                //save the list to the setting file if it is needed.
                if (needToSave)
                    SavePwList();

                RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog,
                    "password added, hash : {0},  pw : {1}", newPwData.guidHash, newPwData.passsword);
            }
        }

        public void SavePwList()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                var bf = new BinaryFormatter();
                bf.Serialize(ms, savedPwList);
                ms.Position = 0;
                byte[] buffer = new byte[(int)ms.Length];
                ms.Read(buffer, 0, buffer.Length);
                Properties.Settings.Default.SafeCasePwList = Convert.ToBase64String(buffer);
                Properties.Settings.Default.Save();

                RgDebug.WriteLine(RgDebug.DebugType.SafeCaseLog, "SavePwList count {0}", savedPwList.Count);
            }
        }

        public List<SafeCasePw> LoadSafeCasePwList()
        {
            if (String.IsNullOrEmpty(Properties.Settings.Default.SafeCasePwList))
                return null;

            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(Properties.Settings.Default.SafeCasePwList)))
            {
                BinaryFormatter bf = new BinaryFormatter();
                return (List<SafeCasePw>)bf.Deserialize(ms);
            }
        }

        //****************************************************************
        // Utils
        //****************************************************************
        private string ToJson<T>(T instance)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var tempStream = new MemoryStream())
            {
                serializer.WriteObject(tempStream, instance);
                return Encoding.UTF8.GetString(tempStream.ToArray());
            }
        }

        private T FromJson<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var tempStream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)serializer.ReadObject(tempStream);
            }
        }

        //****************************************************************
        // Destory
        //****************************************************************
        public void Reset()
        {
            savedPwList.Clear();
        }

        public void Dispose()
        {
            savedPwList.Clear();
        }
    }

    //************************************************************************************
    // SafeCase Header(size limit is 256 bytes), will be placed in vhd header
    // last 256 + 16 bytes in 512
    // variable name should be short becase the name will be saved also in 256 bytes.
    // header will be saved as JSON type.
    // need to be tested the max length of the json length, and it should be less than 256
    //************************************************************************************
    [DataContract]
    public class SafeCaseHeader
    {
        //encryption method
        [DataMember]
        public int md { get; set; }

        //version
        [DataMember]
        public int vr { get; set; }

        //use log
        [DataMember]
        public int lg { get; set; }

        //use readonly
        [DataMember]
        public int rd { get; set; }

        //safecase name
        [DataMember]
        public string nm { get; set; }

        //password hint
        [DataMember]
        public string ht { get; set; }

        //GUID hashcode to distinguish the unique safecase.
        [DataMember]
        public string gh { get; set; }

        //password hash code to check the password is correct or not
        [DataMember]
        public string ph { get; set; }

        //date of expiry or creation date if it is not set the expiry date.
        [DataMember]
        public DateTime dt { get; set; }

        //pc id.
        [DataMember]
        public string id { get; set; }

        //copy protection
        [DataMember]
        public int cp { get; set; }

        //setting on created PC.
        [DataMember]
        public int sp { get; set; }

        //use expiry date.
        [DataMember]
        public int ue { get; set; }
    }

    //password list to open safecase automatically.
    [Serializable()]
    public class SafeCasePw
    {
        public string guidHash { get; set; }
        public string passsword { get; set; }
    }

}