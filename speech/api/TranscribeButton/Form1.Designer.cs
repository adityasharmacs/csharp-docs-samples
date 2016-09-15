namespace TranscribeButton
{
    partial class TranscribeForm
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
            this.Transcription = new System.Windows.Forms.TextBox();
            this.DeviceList = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // Transcription
            // 
            this.Transcription.Enabled = false;
            this.Transcription.Location = new System.Drawing.Point(12, 138);
            this.Transcription.Multiline = true;
            this.Transcription.Name = "Transcription";
            this.Transcription.Size = new System.Drawing.Size(265, 243);
            this.Transcription.TabIndex = 1;
            // 
            // DeviceList
            // 
            this.DeviceList.FormattingEnabled = true;
            this.DeviceList.Location = new System.Drawing.Point(12, 17);
            this.DeviceList.Name = "DeviceList";
            this.DeviceList.Size = new System.Drawing.Size(264, 95);
            this.DeviceList.TabIndex = 2;
            this.DeviceList.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnKeyDown);
            this.DeviceList.KeyUp += new System.Windows.Forms.KeyEventHandler(this.OnKeyUp);
            // 
            // TranscribeForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(291, 397);
            this.Controls.Add(this.DeviceList);
            this.Controls.Add(this.Transcription);
            this.Name = "TranscribeForm";
            this.Text = "Transcribe Demo";
            this.Load += new System.EventHandler(this.OnLoad);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnKeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.OnKeyUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox Transcription;
        private System.Windows.Forms.ListBox DeviceList;
    }
}

