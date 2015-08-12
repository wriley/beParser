using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
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

            // RCON
            tbRCONHostname.Text = parentForm.rconHostname;
            tbRCONPort.Text = parentForm.rconPort;
            tbRCONPassword.Text = parentForm.rconPassword;
            cbRCONConnect.Checked = parentForm.rconConnect;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog(this);
            if(folderBrowserDialog1.SelectedPath.Length > 0)
            {
                tbGeneralPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // General
            parentForm.rewindOn = cbGeneralRewindOn.Checked;
            parentForm.basePath = tbGeneralPath.Text;

            // RCON
            parentForm.rconHostname = tbRCONHostname.Text;
            parentForm.rconPort = tbRCONPort.Text;
            parentForm.rconPassword = tbRCONPassword.Text;
            parentForm.rconConnect = cbRCONConnect.Checked;

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
