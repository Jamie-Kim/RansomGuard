using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RansomGuard
{
    public partial class AboutDialog : Form
    {
        public AboutDialog()
        {
            InitializeComponent();
            link.Text = @"Visit this website https://www.ransomguard.ca";
            link.Links.Add(19, 31, "https://www.ransomguard.ca");

            lbVersion.Text = "Version " + Application.ProductVersion;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {

        }

        private void link_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Link.LinkData.ToString());
        }
    }
}
