using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RansomGuard.Helpers
{
    class HardwareInfo
    {
        const int computerIdLength = 16;

        public string Model { get; private set; }
        public string Manufacturer { get; private set; }
        public string CpuId { get; private set; }
        public string Cpu { get; private set; }
        public string MacAddr { get; private set; }
        public string BisoVersion { get; private set; }
        public string OsVersion { get; private set; }
        public string UserName { get; private set; }
        public string ComputerId { get; private set; }

        public void LoadPcInfo()
        {
            Manufacturer = getManufacturer();
            Model = getModel();
            UserName = getUserName();
            BisoVersion = getBiosVersion();
            OsVersion = getOsVersion();
            Cpu = getCpu();
            MacAddr = getMacAddress();
            ComputerId = getComputerId();
        }

        public string getMacAddress()
        {
            if (string.IsNullOrEmpty(MacAddr))
            {
                const int MIN_MAC_ADDR_LENGTH = 12;
                string macAddress = string.Empty;
                long maxSpeed = -1;

                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string tempMac = string.Join(":", (from z in nic.GetPhysicalAddress().GetAddressBytes() select z.ToString("X2")).ToArray());
                    if (nic.Speed > maxSpeed &&
                        !string.IsNullOrEmpty(tempMac) &&
                        tempMac.Length >= MIN_MAC_ADDR_LENGTH)
                    {
                        maxSpeed = nic.Speed;
                        MacAddr = tempMac;
                    }
                }
            }

            return MacAddr;
        }

        public string getVolumeSerial(char driveLetter)
        {
            using (ManagementObject disk = new ManagementObject(@"win32_logicaldisk.deviceid=""" + driveLetter + @":"""))
            {
                disk.Get();
                return disk["VolumeSerialNumber"].ToString();
            }
        }

        public string getProcessorId()
        {
            if (string.IsNullOrEmpty(CpuId))
            {
                using (ManagementClass mc = new ManagementClass("win32_processor"))
                {
                    using (ManagementObjectCollection moc = mc.GetInstances())
                    {
                        // foreach automatically calls Dispose on IEnumerable if it is an IDisposable
                        foreach (ManagementObject mo in moc)
                        {
                            if (CpuId == "")
                            {
                                //Get only the first CPU's ID
                                CpuId = mo.Properties["processorID"].Value.ToString();
                                break;
                            }
                        }
                    }
                }
            }

            return CpuId;
        }

        public string getComputerId()
        {
            if(string.IsNullOrEmpty(ComputerId))
            {
                string id = getProcessorId() + getVolumeSerial('C');
                ComputerId = sha256_hash(id, computerIdLength);
            }

            return ComputerId;
        }

        public string getCpu()
        {
            if (string.IsNullOrEmpty(Cpu))
            {
                //get Win32_Processor
                var mos = new ManagementObjectSearcher("SELECT Name,processorID,UniqueId FROM Win32_Processor");
                foreach (ManagementObject mo in mos.Get())
                {
                    Cpu = (mo["Name"] ?? String.Empty).ToString();
                }
            }

            return Cpu;
        }

        public string getOsVersion()
        {
            if (string.IsNullOrEmpty(OsVersion))
            {
                var mos = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
                foreach (ManagementObject mo in mos.Get())
                {
                    OsVersion = (mo["Caption"] ?? String.Empty).ToString();
                }
            }

            return OsVersion;
        }

        public string getBiosVersion()
        {
            if (string.IsNullOrEmpty(BisoVersion))
            {
                var mos = new ManagementObjectSearcher("SELECT BIOSVersion FROM Win32_BIOS");
                foreach (ManagementObject mo in mos.Get())
                {
                    if (mo["BIOSVersion"] == null)
                    {
                        this.BisoVersion = "";
                    }
                    else
                    {
                        if (((string[])mo["BIOSVersion"]).Length > 1)
                        {
                            BisoVersion = ((string[])mo["BIOSVersion"])[0] + " - " + ((string[])mo["BIOSVersion"])[1];
                        }
                        else
                        {
                            BisoVersion = ((string[])mo["BIOSVersion"])[0];
                        }
                    }
                }
            }

            return BisoVersion;
        }

        public string getManufacturer()
        {
            if (string.IsNullOrEmpty(Manufacturer))
            {
                var mos = new ManagementObjectSearcher("SELECT Manufacturer,Model,UserName FROM Win32_ComputerSystem");
                foreach (ManagementObject mo in mos.Get())
                {
                    Manufacturer = (mo["Manufacturer"] ?? String.Empty).ToString();
                    Model = (mo["Model"] ?? String.Empty).ToString();
                    UserName = (mo["UserName"] ?? String.Empty).ToString();
                }
            }

            return Manufacturer;
        }

        public string getModel()
        {
            if (string.IsNullOrEmpty(Model))
            {
                var mos = new ManagementObjectSearcher("SELECT Manufacturer,Model,UserName FROM Win32_ComputerSystem");
                foreach (ManagementObject mo in mos.Get())
                {
                    Manufacturer = (mo["Manufacturer"] ?? String.Empty).ToString();
                    Model = (mo["Model"] ?? String.Empty).ToString();
                    UserName = (mo["UserName"] ?? String.Empty).ToString();
                }
            }

            return Model;
        }

        public string getUserName()
        {
            if (string.IsNullOrEmpty(UserName))
            {
                var mos = new ManagementObjectSearcher("SELECT Manufacturer,Model,UserName FROM Win32_ComputerSystem");
                foreach (ManagementObject mo in mos.Get())
                {
                    Manufacturer = (mo["Manufacturer"] ?? String.Empty).ToString();
                    Model = (mo["Model"] ?? String.Empty).ToString();
                    UserName = (mo["UserName"] ?? String.Empty).ToString();
                }
            }

            return UserName;
        } 

        public static String sha256_hash(String value, int length)
        {
            StringBuilder Sb = new StringBuilder();

            using (SHA256 hash = SHA256Managed.Create())
            {
                Encoding enc = Encoding.UTF8;
                Byte[] result = hash.ComputeHash(enc.GetBytes(value));

                foreach (Byte b in result)
                    Sb.Append(b.ToString("x2"));
            }

            return Sb.ToString().Substring(0, length);
        }
    }
}
