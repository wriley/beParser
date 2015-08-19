using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace beParser
{
    public partial class frmOptions : Form
    {
        frmMain parentForm;

        public frmOptions(frmMain frm)
        {
            InitializeComponent();
            parentForm = new frmMain();
            parentForm = frm;
        }

        private void frmOptions_Load(object sender, EventArgs e)
        {
            // General
            tbGeneralPath.Text = parentForm.basePath;
            cbGeneralRewindOn.Checked = parentForm.rewindOn;
            cbAppendLogs.Checked = parentForm.appendLogs;

            // RCON
            tbRCONHostname.Text = parentForm.GetLoginCredentials("Host");
            tbRCONPort.Text = parentForm.GetLoginCredentials("Port");
            tbRCONPassword.Text = parentForm.GetLoginCredentials("Password");
            cbRCONConnect.Checked = parentForm.rconConnect;
            cbRCONServerConsole.Checked = parentForm.rconServerConsole;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog(this);
            if(folderBrowserDialog1.SelectedPath.Length > 0)
            {
                tbGeneralPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            // General
            parentForm.rewindOn = cbGeneralRewindOn.Checked;
            parentForm.basePath = tbGeneralPath.Text;
            parentForm.appendLogs = cbAppendLogs.Checked;

            // RCON
            parentForm.SetLoginCredentials("Host", tbRCONHostname.Text);
            parentForm.SetLoginCredentials("Port", tbRCONPort.Text);
            parentForm.SetLoginCredentials("Password", tbRCONPassword.Text);
            parentForm.rconConnect = cbRCONConnect.Checked;
            parentForm.rconServerConsole = cbRCONServerConsole.Checked;

            // Save
            parentForm.SaveSettings();

            // Update parent form controls
            parentForm.LoadSettings();

            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
