namespace NamingFix
{
    partial class CFormStatus
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
            this.lblText = new System.Windows.Forms.Label();
            this.pbMain = new System.Windows.Forms.ProgressBar();
            this.lblAction = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.pbSub = new System.Windows.Forms.ProgressBar();
            this.btnAbort = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblText
            // 
            this.lblText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblText.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblText.Location = new System.Drawing.Point(0, 109);
            this.lblText.Name = "lblText";
            this.lblText.Size = new System.Drawing.Size(492, 38);
            this.lblText.TabIndex = 0;
            this.lblText.Text = "Started";
            this.lblText.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblText.UseWaitCursor = true;
            // 
            // pbMain
            // 
            this.pbMain.Location = new System.Drawing.Point(36, 69);
            this.pbMain.Name = "pbMain";
            this.pbMain.Size = new System.Drawing.Size(430, 37);
            this.pbMain.TabIndex = 1;
            this.pbMain.UseWaitCursor = true;
            this.pbMain.Value = 50;
            // 
            // lblAction
            // 
            this.lblAction.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblAction.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.25F);
            this.lblAction.Location = new System.Drawing.Point(3, 9);
            this.lblAction.Name = "lblAction";
            this.lblAction.Size = new System.Drawing.Size(489, 18);
            this.lblAction.TabIndex = 2;
            this.lblAction.Text = "Applying given name sheme";
            this.lblAction.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblAction.UseWaitCursor = true;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F);
            this.label2.Location = new System.Drawing.Point(3, 37);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(489, 18);
            this.label2.TabIndex = 3;
            this.label2.Text = "Please Wait!";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label2.UseWaitCursor = true;
            // 
            // pbSub
            // 
            this.pbSub.Location = new System.Drawing.Point(36, 150);
            this.pbSub.Name = "pbSub";
            this.pbSub.Size = new System.Drawing.Size(430, 37);
            this.pbSub.TabIndex = 4;
            this.pbSub.UseWaitCursor = true;
            this.pbSub.Value = 50;
            // 
            // btnAbort
            // 
            this.btnAbort.Location = new System.Drawing.Point(210, 206);
            this.btnAbort.Name = "btnAbort";
            this.btnAbort.Size = new System.Drawing.Size(74, 33);
            this.btnAbort.TabIndex = 5;
            this.btnAbort.Text = "Abort";
            this.btnAbort.UseVisualStyleBackColor = true;
            this.btnAbort.UseWaitCursor = true;
            this.btnAbort.Click += new System.EventHandler(this._BtnAbortClick);
            // 
            // CFormStatus
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(492, 251);
            this.ControlBox = false;
            this.Controls.Add(this.btnAbort);
            this.Controls.Add(this.pbSub);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.lblAction);
            this.Controls.Add(this.pbMain);
            this.Controls.Add(this.lblText);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "CFormStatus";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Naming Fix";
            this.TopMost = true;
            this.UseWaitCursor = true;
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.Label lblText;
        public System.Windows.Forms.ProgressBar pbMain;
        private System.Windows.Forms.Label label2;
        public System.Windows.Forms.ProgressBar pbSub;
        private System.Windows.Forms.Button btnAbort;
        public System.Windows.Forms.Label lblAction;
    }
}