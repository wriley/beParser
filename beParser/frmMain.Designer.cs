namespace beParser
{
    partial class frmMain
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
            this.rtbOutput = new System.Windows.Forms.RichTextBox();
            this.rtbDebug = new System.Windows.Forms.RichTextBox();
            this.btnRCON = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // rtbOutput
            // 
            this.rtbOutput.Location = new System.Drawing.Point(12, 41);
            this.rtbOutput.Name = "rtbOutput";
            this.rtbOutput.ReadOnly = true;
            this.rtbOutput.Size = new System.Drawing.Size(912, 510);
            this.rtbOutput.TabIndex = 0;
            this.rtbOutput.Text = "";
            // 
            // rtbDebug
            // 
            this.rtbDebug.Location = new System.Drawing.Point(12, 557);
            this.rtbDebug.Name = "rtbDebug";
            this.rtbDebug.ReadOnly = true;
            this.rtbDebug.Size = new System.Drawing.Size(912, 90);
            this.rtbDebug.TabIndex = 1;
            this.rtbDebug.Text = "";
            // 
            // btnRCON
            // 
            this.btnRCON.Location = new System.Drawing.Point(12, 12);
            this.btnRCON.Name = "btnRCON";
            this.btnRCON.Size = new System.Drawing.Size(75, 23);
            this.btnRCON.TabIndex = 2;
            this.btnRCON.Text = "RCON";
            this.btnRCON.UseVisualStyleBackColor = true;
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(936, 659);
            this.Controls.Add(this.btnRCON);
            this.Controls.Add(this.rtbDebug);
            this.Controls.Add(this.rtbOutput);
            this.Name = "frmMain";
            this.Text = "beParser";
            this.Load += new System.EventHandler(this.fmrMain_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RichTextBox rtbOutput;
        private System.Windows.Forms.RichTextBox rtbDebug;
        private System.Windows.Forms.Button btnRCON;
    }
}

