namespace RansomGuard
{
    partial class CryptForm
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
            this.listView1 = new System.Windows.Forms.ListView();
            this.title = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.button_add = new System.Windows.Forms.Button();
            this.button_remove = new System.Windows.Forms.Button();
            this.button_close = new System.Windows.Forms.Button();
            this.button_crypt = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label_rc4 = new System.Windows.Forms.Label();
            this.label_aes256 = new System.Windows.Forms.Label();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.radioButton1 = new System.Windows.Forms.RadioButton();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textBox_hint = new System.Windows.Forms.TextBox();
            this.optionDelete = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox_pw_confirm = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.textBox_pw = new System.Windows.Forms.TextBox();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.subTitle = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // listView1
            // 
            this.listView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listView1.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listView1.Location = new System.Drawing.Point(14, 78);
            this.listView1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(680, 195);
            this.listView1.TabIndex = 0;
            this.listView1.UseCompatibleStateImageBehavior = false;
            // 
            // title
            // 
            this.title.AutoSize = true;
            this.title.Font = new System.Drawing.Font("Meiryo", 9.75F, System.Drawing.FontStyle.Bold);
            this.title.Location = new System.Drawing.Point(10, 18);
            this.title.Name = "title";
            this.title.Size = new System.Drawing.Size(183, 20);
            this.title.TabIndex = 1;
            this.title.Text = "파일 및 폴더 암호화/복호화";
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.SystemColors.Control;
            this.panel1.Controls.Add(this.button_add);
            this.panel1.Controls.Add(this.button_remove);
            this.panel1.Controls.Add(this.button_close);
            this.panel1.Controls.Add(this.button_crypt);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 462);
            this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(708, 59);
            this.panel1.TabIndex = 2;
            // 
            // button_add
            // 
            this.button_add.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button_add.Location = new System.Drawing.Point(144, 12);
            this.button_add.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button_add.Name = "button_add";
            this.button_add.Size = new System.Drawing.Size(124, 34);
            this.button_add.TabIndex = 1;
            this.button_add.Text = "파일 추가";
            this.button_add.UseVisualStyleBackColor = true;
            this.button_add.Click += new System.EventHandler(this.button_add_Click);
            // 
            // button_remove
            // 
            this.button_remove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button_remove.Location = new System.Drawing.Point(14, 12);
            this.button_remove.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button_remove.Name = "button_remove";
            this.button_remove.Size = new System.Drawing.Size(124, 34);
            this.button_remove.TabIndex = 0;
            this.button_remove.Text = "선택 삭제";
            this.button_remove.UseVisualStyleBackColor = true;
            this.button_remove.Click += new System.EventHandler(this.button_remove_Click);
            // 
            // button_close
            // 
            this.button_close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button_close.Location = new System.Drawing.Point(570, 12);
            this.button_close.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button_close.Name = "button_close";
            this.button_close.Size = new System.Drawing.Size(124, 34);
            this.button_close.TabIndex = 3;
            this.button_close.Text = "삭제 중지 && 닫기";
            this.button_close.UseVisualStyleBackColor = true;
            this.button_close.Click += new System.EventHandler(this.button_close_Click);
            // 
            // button_crypt
            // 
            this.button_crypt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button_crypt.Location = new System.Drawing.Point(440, 12);
            this.button_crypt.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button_crypt.Name = "button_crypt";
            this.button_crypt.Size = new System.Drawing.Size(124, 34);
            this.button_crypt.TabIndex = 2;
            this.button_crypt.Text = "암호화/복호화";
            this.button_crypt.UseVisualStyleBackColor = true;
            this.button_crypt.Click += new System.EventHandler(this.button_crypt_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.label_rc4);
            this.groupBox1.Controls.Add(this.label_aes256);
            this.groupBox1.Controls.Add(this.radioButton2);
            this.groupBox1.Controls.Add(this.radioButton1);
            this.groupBox1.Location = new System.Drawing.Point(13, 291);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Size = new System.Drawing.Size(329, 153);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "암호화 방식";
            // 
            // label_rc4
            // 
            this.label_rc4.AutoSize = true;
            this.label_rc4.Font = new System.Drawing.Font("Meiryo", 8F);
            this.label_rc4.ForeColor = System.Drawing.SystemColors.AppWorkspace;
            this.label_rc4.Location = new System.Drawing.Point(28, 113);
            this.label_rc4.Name = "label_rc4";
            this.label_rc4.Size = new System.Drawing.Size(242, 17);
            this.label_rc4.TabIndex = 9;
            this.label_rc4.Text = "미디어 파일에 적합, 빠른 암호화 방식";
            // 
            // label_aes256
            // 
            this.label_aes256.AutoSize = true;
            this.label_aes256.Font = new System.Drawing.Font("Meiryo", 8F);
            this.label_aes256.ForeColor = System.Drawing.SystemColors.AppWorkspace;
            this.label_aes256.Location = new System.Drawing.Point(28, 55);
            this.label_aes256.Name = "label_aes256";
            this.label_aes256.Size = new System.Drawing.Size(242, 17);
            this.label_aes256.TabIndex = 8;
            this.label_aes256.Text = "문서 파일에 적합, 안전한 암호화 방식";
            // 
            // radioButton2
            // 
            this.radioButton2.AutoSize = true;
            this.radioButton2.Enabled = false;
            this.radioButton2.Location = new System.Drawing.Point(16, 91);
            this.radioButton2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.radioButton2.Name = "radioButton2";
            this.radioButton2.Size = new System.Drawing.Size(48, 21);
            this.radioButton2.TabIndex = 1;
            this.radioButton2.TabStop = true;
            this.radioButton2.Text = "RC4";
            this.radioButton2.UseVisualStyleBackColor = true;
            this.radioButton2.CheckedChanged += new System.EventHandler(this.radioButton2_CheckedChanged);
            // 
            // radioButton1
            // 
            this.radioButton1.AutoSize = true;
            this.radioButton1.Location = new System.Drawing.Point(16, 33);
            this.radioButton1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.radioButton1.Name = "radioButton1";
            this.radioButton1.Size = new System.Drawing.Size(68, 21);
            this.radioButton1.TabIndex = 0;
            this.radioButton1.TabStop = true;
            this.radioButton1.Text = "AES256";
            this.radioButton1.UseVisualStyleBackColor = true;
            this.radioButton1.CheckedChanged += new System.EventHandler(this.radioButton1_CheckedChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.textBox_hint);
            this.groupBox2.Controls.Add(this.optionDelete);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.textBox_pw_confirm);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.textBox_pw);
            this.groupBox2.Location = new System.Drawing.Point(348, 291);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox2.Size = new System.Drawing.Size(346, 153);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "암호 설정";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(255, 121);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(46, 17);
            this.label4.TabIndex = 7;
            this.label4.Text = "(옵션)";
            // 
            // textBox_hint
            // 
            this.textBox_hint.Location = new System.Drawing.Point(79, 117);
            this.textBox_hint.MaxLength = 256;
            this.textBox_hint.Name = "textBox_hint";
            this.textBox_hint.Size = new System.Drawing.Size(168, 24);
            this.textBox_hint.TabIndex = 6;
            // 
            // optionDelete
            // 
            this.optionDelete.AutoSize = true;
            this.optionDelete.ForeColor = System.Drawing.SystemColors.ActiveCaption;
            this.optionDelete.Location = new System.Drawing.Point(21, 26);
            this.optionDelete.Name = "optionDelete";
            this.optionDelete.Size = new System.Drawing.Size(276, 21);
            this.optionDelete.TabIndex = 0;
            this.optionDelete.Text = "파일 암호화/복호화 뒤에 원본 파일 삭제";
            this.optionDelete.UseVisualStyleBackColor = true;
            this.optionDelete.CheckedChanged += new System.EventHandler(this.optionDelete_CheckedChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(18, 122);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(36, 17);
            this.label3.TabIndex = 5;
            this.label3.Text = "힌트";
            // 
            // textBox_pw_confirm
            // 
            this.textBox_pw_confirm.Location = new System.Drawing.Point(79, 86);
            this.textBox_pw_confirm.MaxLength = 32;
            this.textBox_pw_confirm.Name = "textBox_pw_confirm";
            this.textBox_pw_confirm.Size = new System.Drawing.Size(168, 24);
            this.textBox_pw_confirm.TabIndex = 4;
            this.textBox_pw_confirm.UseSystemPasswordChar = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(18, 89);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(36, 17);
            this.label2.TabIndex = 3;
            this.label2.Text = "확인";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(18, 60);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(36, 17);
            this.label1.TabIndex = 2;
            this.label1.Text = "암호";
            // 
            // textBox_pw
            // 
            this.textBox_pw.Location = new System.Drawing.Point(79, 55);
            this.textBox_pw.MaxLength = 32;
            this.textBox_pw.Name = "textBox_pw";
            this.textBox_pw.Size = new System.Drawing.Size(168, 24);
            this.textBox_pw.TabIndex = 1;
            this.textBox_pw.UseSystemPasswordChar = true;
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.BackColor = System.Drawing.Color.White;
            this.progressBar1.ForeColor = System.Drawing.SystemColors.MenuHighlight;
            this.progressBar1.Location = new System.Drawing.Point(14, 273);
            this.progressBar1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(680, 7);
            this.progressBar1.TabIndex = 9;
            // 
            // subTitle
            // 
            this.subTitle.AutoSize = true;
            this.subTitle.Font = new System.Drawing.Font("Meiryo", 9F);
            this.subTitle.ForeColor = System.Drawing.SystemColors.ButtonShadow;
            this.subTitle.Location = new System.Drawing.Point(12, 46);
            this.subTitle.Name = "subTitle";
            this.subTitle.Size = new System.Drawing.Size(519, 18);
            this.subTitle.TabIndex = 10;
            this.subTitle.Text = "파일이나 폴더를 추가 해주세요, 한번 암호화된 파일은 암호 없이는 복구 할 수 없습니다.";
            // 
            // CryptForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(708, 521);
            this.Controls.Add(this.subTitle);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.title);
            this.Controls.Add(this.listView1);
            this.Font = new System.Drawing.Font("Meiryo", 8.25F);
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "CryptForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Encryption and Decryption for  Files";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.CryptForm_FormClosed);
            this.Load += new System.EventHandler(this.CryptForm_Load);
            this.panel1.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.Label title;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button button_add;
        private System.Windows.Forms.Button button_remove;
        private System.Windows.Forms.Button button_close;
        private System.Windows.Forms.Button button_crypt;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton radioButton2;
        private System.Windows.Forms.RadioButton radioButton1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label subTitle;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBox_hint;
        private System.Windows.Forms.CheckBox optionDelete;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox_pw_confirm;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox_pw;
        private System.Windows.Forms.Label label_rc4;
        private System.Windows.Forms.Label label_aes256;
    }
}