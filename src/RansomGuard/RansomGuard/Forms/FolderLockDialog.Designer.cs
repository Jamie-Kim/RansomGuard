namespace RansomGuard
{
    partial class FolderLockDialog
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
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.tbPassword1 = new System.Windows.Forms.TextBox();
            this.tbPassword = new System.Windows.Forms.TextBox();
            this.button3 = new System.Windows.Forms.Button();
            this.tbSourceFolder = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.button1.Location = new System.Drawing.Point(252, 18);
            this.button1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(96, 30);
            this.button1.TabIndex = 0;
            this.button1.Text = "폴더 잠그기";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.button2.Location = new System.Drawing.Point(362, 18);
            this.button2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(96, 30);
            this.button2.TabIndex = 1;
            this.button2.Text = "닫기";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold);
            this.checkBox1.Location = new System.Drawing.Point(33, 127);
            this.checkBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(75, 20);
            this.checkBox1.TabIndex = 1;
            this.checkBox1.Text = "암호 설정";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // tbPassword1
            // 
            this.tbPassword1.Location = new System.Drawing.Point(33, 159);
            this.tbPassword1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbPassword1.MaxLength = 12;
            this.tbPassword1.Name = "tbPassword1";
            this.tbPassword1.Size = new System.Drawing.Size(205, 20);
            this.tbPassword1.TabIndex = 2;
            this.tbPassword1.UseSystemPasswordChar = true;
            // 
            // tbPassword
            // 
            this.tbPassword.Location = new System.Drawing.Point(33, 191);
            this.tbPassword.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbPassword.MaxLength = 12;
            this.tbPassword.Name = "tbPassword";
            this.tbPassword.Size = new System.Drawing.Size(205, 20);
            this.tbPassword.TabIndex = 3;
            this.tbPassword.UseSystemPasswordChar = true;
            // 
            // button3
            // 
            this.button3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.button3.Location = new System.Drawing.Point(357, 75);
            this.button3.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(96, 30);
            this.button3.TabIndex = 0;
            this.button3.Text = "폴더 설정";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // tbSourceFolder
            // 
            this.tbSourceFolder.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.tbSourceFolder.Location = new System.Drawing.Point(33, 79);
            this.tbSourceFolder.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbSourceFolder.Name = "tbSourceFolder";
            this.tbSourceFolder.ReadOnly = true;
            this.tbSourceFolder.Size = new System.Drawing.Size(310, 20);
            this.tbSourceFolder.TabIndex = 5;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold);
            this.label1.Location = new System.Drawing.Point(29, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(237, 32);
            this.label1.TabIndex = 6;
            this.label1.Text = "잠금을 원하는 폴더를 선택하고 \r\n비밀번호입력 후에 잠그기 버튼을 눌러주세요.";
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.SystemColors.Control;
            this.panel1.Controls.Add(this.button1);
            this.panel1.Controls.Add(this.button2);
            this.panel1.Location = new System.Drawing.Point(-2, 239);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(485, 61);
            this.panel1.TabIndex = 4;
            // 
            // FolderLockDialog
            // 
            this.AcceptButton = this.button1;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(482, 299);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.tbSourceFolder);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.tbPassword);
            this.Controls.Add(this.tbPassword1);
            this.Controls.Add(this.checkBox1);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FolderLockDialog";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Folder Lock";
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.TextBox tbPassword1;
        private System.Windows.Forms.TextBox tbPassword;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.TextBox tbSourceFolder;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel1;
    }
}