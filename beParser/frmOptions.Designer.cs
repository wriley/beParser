namespace beParser
{
    partial class frmOptions
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnSave = new System.Windows.Forms.Button();
            this.lblGeneralPath = new System.Windows.Forms.Label();
            this.tbGeneralPath = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.btnGeneralBrowse = new System.Windows.Forms.Button();
            this.cbGeneralRewindOn = new System.Windows.Forms.CheckBox();
            this.gbGeneral = new System.Windows.Forms.GroupBox();
            this.gbRCON = new System.Windows.Forms.GroupBox();
            this.cbRCONConnect = new System.Windows.Forms.CheckBox();
            this.tbRCONPassword = new System.Windows.Forms.TextBox();
            this.lblRCONPassword = new System.Windows.Forms.Label();
            this.tbRCONPort = new System.Windows.Forms.TextBox();
            this.lblRCONPort = new System.Windows.Forms.Label();
            this.tbRCONHostname = new System.Windows.Forms.TextBox();
            this.lblRCONHostname = new System.Windows.Forms.Label();
            this.cbRCONServerConsole = new System.Windows.Forms.CheckBox();
            this.gbGeneral.SuspendLayout();
            this.gbRCON.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(162, 319);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // lblGeneralPath
            // 
            this.lblGeneralPath.AutoSize = true;
            this.lblGeneralPath.Location = new System.Drawing.Point(11, 22);
            this.lblGeneralPath.Name = "lblGeneralPath";
            this.lblGeneralPath.Size = new System.Drawing.Size(71, 13);
            this.lblGeneralPath.TabIndex = 1;
            this.lblGeneralPath.Text = "BattlEye Path";
            // 
            // tbGeneralPath
            // 
            this.tbGeneralPath.Location = new System.Drawing.Point(88, 19);
            this.tbGeneralPath.Name = "tbGeneralPath";
            this.tbGeneralPath.Size = new System.Drawing.Size(268, 20);
            this.tbGeneralPath.TabIndex = 2;
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(243, 319);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnGeneralBrowse
            // 
            this.btnGeneralBrowse.Location = new System.Drawing.Point(362, 17);
            this.btnGeneralBrowse.Name = "btnGeneralBrowse";
            this.btnGeneralBrowse.Size = new System.Drawing.Size(75, 23);
            this.btnGeneralBrowse.TabIndex = 4;
            this.btnGeneralBrowse.Text = "Browse...";
            this.btnGeneralBrowse.UseVisualStyleBackColor = true;
            this.btnGeneralBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // cbGeneralRewindOn
            // 
            this.cbGeneralRewindOn.AutoSize = true;
            this.cbGeneralRewindOn.Location = new System.Drawing.Point(14, 56);
            this.cbGeneralRewindOn.Name = "cbGeneralRewindOn";
            this.cbGeneralRewindOn.Size = new System.Drawing.Size(233, 17);
            this.cbGeneralRewindOn.TabIndex = 5;
            this.cbGeneralRewindOn.Text = "Always rewind (start at beginning of log files)";
            this.cbGeneralRewindOn.UseVisualStyleBackColor = true;
            // 
            // gbGeneral
            // 
            this.gbGeneral.Controls.Add(this.tbGeneralPath);
            this.gbGeneral.Controls.Add(this.cbGeneralRewindOn);
            this.gbGeneral.Controls.Add(this.lblGeneralPath);
            this.gbGeneral.Controls.Add(this.btnGeneralBrowse);
            this.gbGeneral.Location = new System.Drawing.Point(12, 12);
            this.gbGeneral.Name = "gbGeneral";
            this.gbGeneral.Size = new System.Drawing.Size(447, 131);
            this.gbGeneral.TabIndex = 6;
            this.gbGeneral.TabStop = false;
            this.gbGeneral.Text = "General";
            // 
            // gbRCON
            // 
            this.gbRCON.Controls.Add(this.cbRCONServerConsole);
            this.gbRCON.Controls.Add(this.cbRCONConnect);
            this.gbRCON.Controls.Add(this.tbRCONPassword);
            this.gbRCON.Controls.Add(this.lblRCONPassword);
            this.gbRCON.Controls.Add(this.tbRCONPort);
            this.gbRCON.Controls.Add(this.lblRCONPort);
            this.gbRCON.Controls.Add(this.tbRCONHostname);
            this.gbRCON.Controls.Add(this.lblRCONHostname);
            this.gbRCON.Location = new System.Drawing.Point(12, 149);
            this.gbRCON.Name = "gbRCON";
            this.gbRCON.Size = new System.Drawing.Size(447, 152);
            this.gbRCON.TabIndex = 6;
            this.gbRCON.TabStop = false;
            this.gbRCON.Text = "RCON";
            // 
            // cbRCONConnect
            // 
            this.cbRCONConnect.AutoSize = true;
            this.cbRCONConnect.Location = new System.Drawing.Point(88, 101);
            this.cbRCONConnect.Name = "cbRCONConnect";
            this.cbRCONConnect.Size = new System.Drawing.Size(145, 17);
            this.cbRCONConnect.TabIndex = 6;
            this.cbRCONConnect.Text = "Connect on program start";
            this.cbRCONConnect.UseVisualStyleBackColor = true;
            // 
            // tbRCONPassword
            // 
            this.tbRCONPassword.Location = new System.Drawing.Point(88, 75);
            this.tbRCONPassword.Name = "tbRCONPassword";
            this.tbRCONPassword.PasswordChar = '*';
            this.tbRCONPassword.Size = new System.Drawing.Size(128, 20);
            this.tbRCONPassword.TabIndex = 11;
            // 
            // lblRCONPassword
            // 
            this.lblRCONPassword.AutoSize = true;
            this.lblRCONPassword.Location = new System.Drawing.Point(28, 78);
            this.lblRCONPassword.Name = "lblRCONPassword";
            this.lblRCONPassword.Size = new System.Drawing.Size(53, 13);
            this.lblRCONPassword.TabIndex = 10;
            this.lblRCONPassword.Text = "Password";
            // 
            // tbRCONPort
            // 
            this.tbRCONPort.Location = new System.Drawing.Point(88, 49);
            this.tbRCONPort.Name = "tbRCONPort";
            this.tbRCONPort.Size = new System.Drawing.Size(72, 20);
            this.tbRCONPort.TabIndex = 9;
            // 
            // lblRCONPort
            // 
            this.lblRCONPort.AutoSize = true;
            this.lblRCONPort.Location = new System.Drawing.Point(55, 52);
            this.lblRCONPort.Name = "lblRCONPort";
            this.lblRCONPort.Size = new System.Drawing.Size(26, 13);
            this.lblRCONPort.TabIndex = 8;
            this.lblRCONPort.Text = "Port";
            // 
            // tbRCONHostname
            // 
            this.tbRCONHostname.Location = new System.Drawing.Point(87, 23);
            this.tbRCONHostname.Name = "tbRCONHostname";
            this.tbRCONHostname.Size = new System.Drawing.Size(268, 20);
            this.tbRCONHostname.TabIndex = 7;
            // 
            // lblRCONHostname
            // 
            this.lblRCONHostname.AutoSize = true;
            this.lblRCONHostname.Location = new System.Drawing.Point(11, 26);
            this.lblRCONHostname.Name = "lblRCONHostname";
            this.lblRCONHostname.Size = new System.Drawing.Size(70, 13);
            this.lblRCONHostname.TabIndex = 6;
            this.lblRCONHostname.Text = "Hostname/IP";
            // 
            // cbRCONServerConsole
            // 
            this.cbRCONServerConsole.AutoSize = true;
            this.cbRCONServerConsole.Location = new System.Drawing.Point(87, 124);
            this.cbRCONServerConsole.Name = "cbRCONServerConsole";
            this.cbRCONServerConsole.Size = new System.Drawing.Size(183, 17);
            this.cbRCONServerConsole.TabIndex = 12;
            this.cbRCONServerConsole.Text = "Use RCON for server console log";
            this.cbRCONServerConsole.UseVisualStyleBackColor = true;
            // 
            // frmOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(471, 354);
            this.Controls.Add(this.gbRCON);
            this.Controls.Add(this.gbGeneral);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Name = "frmOptions";
            this.Text = "Options";
            this.Load += new System.EventHandler(this.frmOptions_Load);
            this.gbGeneral.ResumeLayout(false);
            this.gbGeneral.PerformLayout();
            this.gbRCON.ResumeLayout(false);
            this.gbRCON.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Label lblGeneralPath;
        private System.Windows.Forms.TextBox tbGeneralPath;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Button btnGeneralBrowse;
        private System.Windows.Forms.CheckBox cbGeneralRewindOn;
        private System.Windows.Forms.GroupBox gbGeneral;
        private System.Windows.Forms.GroupBox gbRCON;
        private System.Windows.Forms.CheckBox cbRCONConnect;
        private System.Windows.Forms.TextBox tbRCONPassword;
        private System.Windows.Forms.Label lblRCONPassword;
        private System.Windows.Forms.TextBox tbRCONPort;
        private System.Windows.Forms.Label lblRCONPort;
        private System.Windows.Forms.TextBox tbRCONHostname;
        private System.Windows.Forms.Label lblRCONHostname;
        private System.Windows.Forms.CheckBox cbRCONServerConsole;
    }
}