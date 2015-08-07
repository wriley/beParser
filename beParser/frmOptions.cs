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
            tbPath.Text = Properties.Settings.Default.basePath;
            cbRewindOn.Checked = Properties.Settings.Default.rewindOn;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog(this);
            if(folderBrowserDialog1.SelectedPath.Length > 0)
            {
                tbPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            parentForm.rewindOn = cbRewindOn.Checked;
            parentForm.basePath = tbPath.Text;
            parentForm.SaveSettings();
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
