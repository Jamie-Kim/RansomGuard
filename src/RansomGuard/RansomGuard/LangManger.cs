using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Windows.Forms;

namespace RansomGuard
{
    class LangManger
    {
        public enum Lang : int
        {
            OS = 0x00,
            Korean = 0x01,
            English = 0x02,

            LangMax
        }

        public const string OS = "Windows OS";
        public const string KOREAN = "한국어";
        public const string ENGLISH = "English";

        public static void SetLanguage(int langSet)
        {
            switch (langSet)
            {
                case (int)Lang.Korean:
                    Properties.Resources.Culture = new CultureInfo("ko-KR");
                    break;

                case (int)Lang.English:
                    Properties.Resources.Culture = new CultureInfo("en-US");
                    break;

                default:
                    Properties.Resources.Culture = new CultureInfo(CultureInfo.CurrentUICulture.Name);
                    break;
            }
        }

        public static string[] GetByArray()
        {
            return new string[] {OS, KOREAN ,ENGLISH};
        }
    }
}
