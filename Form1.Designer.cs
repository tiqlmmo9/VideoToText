using Mscc.GenerativeAI;
using System;
using System.Windows.Forms;

namespace VideoToText
{
    partial class Form1 : Form
    {
        private System.ComponentModel.IContainer components = null;
        private Button btnConvertVideoToText;
        private Button btnCancel;
        private TextBox logTextBox;
        private TextBox playlistIdTextBox;
        private TextBox outputPathTextBox;
        private Label playlistIdLabel;
        private Label outputPathLabel;
        private RadioButton radioPlaylist;
        private RadioButton radioVideo;
        private TextBox videoIdTextBox;
        private Label videoIdLabel;
        private FolderBrowserDialog folderBrowserDialog;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        public Form1()
        {
            InitializeComponent();
            LoadSettings();

            InitializeGenerativeAI();

            this.FormClosing += Form1_FormClosing;
        }

        private void LoadSettings()
        {
            // Restore CheckBox state
            payAsYouGoCheckBox.Checked = Properties.Settings.Default.IsPayAsYouGo;
            freeCheckBox.Checked = !Properties.Settings.Default.IsPayAsYouGo;

            playlistIdTextBox.Text = Properties.Settings.Default.PlaylistId;
            numericUpDownStart.Value = Properties.Settings.Default.StartIndex;
            numericUpDownEnd.Value = Properties.Settings.Default.EndIndex;

            outputPathTextBox.Text = Properties.Settings.Default.OutputPath;
            videoIdTextBox.Text = Properties.Settings.Default.VideoId;
            apiKeyTextBox.Text = Properties.Settings.Default.ApiKey;
            logTextBox.Text = Properties.Settings.Default.LogText;
            promptTextBox.Text = Properties.Settings.Default.Prompt;

            // Restore radio button state
            radioPlaylist.Checked = Properties.Settings.Default.IsPlaylistChecked;
            radioVideo.Checked = !Properties.Settings.Default.IsPlaylistChecked;

            // Restore ComboBox state
            modelComboBox.SelectedItem = Properties.Settings.Default.SelectedModel;

            // Update visibility based on radio button state
            radioPlaylist_CheckedChanged(null, null);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.PlaylistId = playlistIdTextBox.Text;

            Properties.Settings.Default.StartIndex = numericUpDownStart.Value;
            Properties.Settings.Default.EndIndex = numericUpDownEnd.Value;

            Properties.Settings.Default.OutputPath = outputPathTextBox.Text;
            Properties.Settings.Default.VideoId = videoIdTextBox.Text;
            Properties.Settings.Default.ApiKey = apiKeyTextBox.Text;
            Properties.Settings.Default.LogText = logTextBox.Text;
            Properties.Settings.Default.Prompt = promptTextBox.Text;
            Properties.Settings.Default.IsPlaylistChecked = radioPlaylist.Checked;

            Properties.Settings.Default.SelectedModel = modelComboBox.SelectedItem.ToString();
            // Save CheckBox state
            Properties.Settings.Default.IsPayAsYouGo = payAsYouGoCheckBox.Checked;

            Properties.Settings.Default.Save();
        }

        private void InitializeComponent()
        {
            btnConvertVideoToText = new Button();
            btnCancel = new Button();
            logTextBox = new TextBox();
            playlistIdTextBox = new TextBox();
            outputPathTextBox = new TextBox();
            playlistIdLabel = new Label();
            outputPathLabel = new Label();
            radioPlaylist = new RadioButton();
            radioVideo = new RadioButton();
            videoIdTextBox = new TextBox();
            videoIdLabel = new Label();
            folderBrowserDialog = new FolderBrowserDialog();
            apiKeyTextBox = new TextBox();
            label1 = new Label();
            btnBrowse = new Button();
            promptTextBox = new TextBox();
            label2 = new Label();
            label3 = new Label();
            modelComboBox = new ComboBox();
            numericUpDownStart = new NumericUpDown();
            startIndexLabel = new Label();
            endIndexLabel = new Label();
            numericUpDownEnd = new NumericUpDown();
            payAsYouGoCheckBox = new CheckBox();
            freeCheckBox = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)numericUpDownStart).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownEnd).BeginInit();
            SuspendLayout();
            // 
            // btnConvertVideoToText
            // 
            btnConvertVideoToText.Location = new Point(12, 336);
            btnConvertVideoToText.Name = "btnConvertVideoToText";
            btnConvertVideoToText.Size = new Size(444, 23);
            btnConvertVideoToText.TabIndex = 0;
            btnConvertVideoToText.Text = "CONVERT VIDEO TO TEXT";
            btnConvertVideoToText.UseVisualStyleBackColor = true;
            btnConvertVideoToText.Click += btnConvertVideoToText_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCancel.Location = new Point(462, 336);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(117, 23);
            btnCancel.TabIndex = 9;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // logTextBox
            // 
            logTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            logTextBox.Location = new Point(12, 365);
            logTextBox.Multiline = true;
            logTextBox.Name = "logTextBox";
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.Size = new Size(567, 175);
            logTextBox.TabIndex = 1;
            // 
            // playlistIdTextBox
            // 
            playlistIdTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            playlistIdTextBox.Location = new Point(12, 113);
            playlistIdTextBox.Multiline = true;
            playlistIdTextBox.Name = "playlistIdTextBox";
            playlistIdTextBox.PlaceholderText = "Please enter Playlist URL(s)";
            playlistIdTextBox.ScrollBars = ScrollBars.Vertical;
            playlistIdTextBox.Size = new Size(256, 126);
            playlistIdTextBox.TabIndex = 2;
            playlistIdTextBox.TextChanged += playlistIdTextBox_TextChanged;
            // 
            // outputPathTextBox
            // 
            outputPathTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            outputPathTextBox.Location = new Point(14, 307);
            outputPathTextBox.Name = "outputPathTextBox";
            outputPathTextBox.PlaceholderText = "Please enter Output Path";
            outputPathTextBox.Size = new Size(442, 23);
            outputPathTextBox.TabIndex = 3;
            // 
            // playlistIdLabel
            // 
            playlistIdLabel.AutoSize = true;
            playlistIdLabel.Location = new Point(12, 95);
            playlistIdLabel.Name = "playlistIdLabel";
            playlistIdLabel.Size = new Size(84, 15);
            playlistIdLabel.TabIndex = 4;
            playlistIdLabel.Text = "Playlist URL(s):";
            // 
            // outputPathLabel
            // 
            outputPathLabel.AutoSize = true;
            outputPathLabel.Location = new Point(12, 289);
            outputPathLabel.Name = "outputPathLabel";
            outputPathLabel.Size = new Size(75, 15);
            outputPathLabel.TabIndex = 5;
            outputPathLabel.Text = "Output Path:";
            // 
            // radioPlaylist
            // 
            radioPlaylist.AutoSize = true;
            radioPlaylist.Checked = true;
            radioPlaylist.Location = new Point(12, 63);
            radioPlaylist.Name = "radioPlaylist";
            radioPlaylist.Size = new Size(62, 19);
            radioPlaylist.TabIndex = 6;
            radioPlaylist.TabStop = true;
            radioPlaylist.Text = "Playlist";
            radioPlaylist.UseVisualStyleBackColor = true;
            radioPlaylist.CheckedChanged += radioPlaylist_CheckedChanged;
            // 
            // radioVideo
            // 
            radioVideo.AutoSize = true;
            radioVideo.Location = new Point(81, 63);
            radioVideo.Name = "radioVideo";
            radioVideo.Size = new Size(55, 19);
            radioVideo.TabIndex = 7;
            radioVideo.Text = "Video";
            radioVideo.UseVisualStyleBackColor = true;
            radioVideo.CheckedChanged += radioVideo_CheckedChanged;
            // 
            // videoIdTextBox
            // 
            videoIdTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            videoIdTextBox.Location = new Point(12, 113);
            videoIdTextBox.Multiline = true;
            videoIdTextBox.Name = "videoIdTextBox";
            videoIdTextBox.PlaceholderText = "Please enter Video URL(s)";
            videoIdTextBox.ScrollBars = ScrollBars.Vertical;
            videoIdTextBox.Size = new Size(256, 126);
            videoIdTextBox.TabIndex = 8;
            // 
            // videoIdLabel
            // 
            videoIdLabel.AutoSize = true;
            videoIdLabel.Location = new Point(12, 95);
            videoIdLabel.Name = "videoIdLabel";
            videoIdLabel.Size = new Size(77, 15);
            videoIdLabel.TabIndex = 4;
            videoIdLabel.Text = "Video URL(s):";
            // 
            // apiKeyTextBox
            // 
            apiKeyTextBox.Location = new Point(68, 16);
            apiKeyTextBox.Name = "apiKeyTextBox";
            apiKeyTextBox.PlaceholderText = "Please enter API Key";
            apiKeyTextBox.Size = new Size(200, 23);
            apiKeyTextBox.TabIndex = 11;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 19);
            label1.Name = "label1";
            label1.Size = new Size(50, 15);
            label1.TabIndex = 12;
            label1.Text = "API Key:";
            // 
            // btnBrowse
            // 
            btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowse.Location = new Point(462, 306);
            btnBrowse.Name = "btnBrowse";
            btnBrowse.Size = new Size(117, 23);
            btnBrowse.TabIndex = 10;
            btnBrowse.Text = "Browse...";
            btnBrowse.UseVisualStyleBackColor = true;
            btnBrowse.Click += btnBrowse_Click;
            // 
            // promptTextBox
            // 
            promptTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            promptTextBox.Location = new Point(285, 113);
            promptTextBox.Multiline = true;
            promptTextBox.Name = "promptTextBox";
            promptTextBox.PlaceholderText = "Please enter prompt";
            promptTextBox.ScrollBars = ScrollBars.Vertical;
            promptTextBox.Size = new Size(294, 126);
            promptTextBox.TabIndex = 13;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(285, 95);
            label2.Name = "label2";
            label2.Size = new Size(50, 15);
            label2.TabIndex = 14;
            label2.Text = "Prompt:";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(285, 63);
            label3.Name = "label3";
            label3.Size = new Size(44, 15);
            label3.TabIndex = 16;
            label3.Text = "Model:";
            // 
            // modelComboBox
            // 
            modelComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            modelComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modelComboBox.FormattingEnabled = true;
            modelComboBox.Items.AddRange(new object[] { "gemini-1.5-flash-002", "gemini-1.5-pro-002" });
            modelComboBox.Location = new Point(341, 60);
            modelComboBox.Name = "modelComboBox";
            modelComboBox.Size = new Size(238, 23);
            modelComboBox.TabIndex = 15;
            modelComboBox.SelectedIndexChanged += modelComboBox_SelectedIndexChanged;
            // 
            // numericUpDownStart
            // 
            numericUpDownStart.Location = new Point(14, 260);
            numericUpDownStart.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numericUpDownStart.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericUpDownStart.Name = "numericUpDownStart";
            numericUpDownStart.Size = new Size(62, 23);
            numericUpDownStart.TabIndex = 18;
            numericUpDownStart.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // startIndexLabel
            // 
            startIndexLabel.AutoSize = true;
            startIndexLabel.Location = new Point(14, 242);
            startIndexLabel.Name = "startIndexLabel";
            startIndexLabel.Size = new Size(66, 15);
            startIndexLabel.TabIndex = 19;
            startIndexLabel.Text = "Start Index:";
            // 
            // endIndexLabel
            // 
            endIndexLabel.AutoSize = true;
            endIndexLabel.Location = new Point(95, 242);
            endIndexLabel.Name = "endIndexLabel";
            endIndexLabel.Size = new Size(62, 15);
            endIndexLabel.TabIndex = 21;
            endIndexLabel.Text = "End Index:";
            // 
            // numericUpDownEnd
            // 
            numericUpDownEnd.Location = new Point(95, 260);
            numericUpDownEnd.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numericUpDownEnd.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
            numericUpDownEnd.Name = "numericUpDownEnd";
            numericUpDownEnd.Size = new Size(62, 23);
            numericUpDownEnd.TabIndex = 20;
            numericUpDownEnd.Value = new decimal(new int[] { 2, 0, 0, 0 });
            // 
            // payAsYouGoCheckBox
            // 
            payAsYouGoCheckBox.AutoSize = true;
            payAsYouGoCheckBox.Checked = true;
            payAsYouGoCheckBox.CheckState = CheckState.Checked;
            payAsYouGoCheckBox.Location = new Point(341, 20);
            payAsYouGoCheckBox.Name = "payAsYouGoCheckBox";
            payAsYouGoCheckBox.Size = new Size(105, 19);
            payAsYouGoCheckBox.TabIndex = 22;
            payAsYouGoCheckBox.Text = "Pay-as-you-go";
            payAsYouGoCheckBox.UseVisualStyleBackColor = true;
            payAsYouGoCheckBox.CheckedChanged += payAsYouGoCheckBox_CheckedChanged;
            // 
            // freeCheckBox
            // 
            freeCheckBox.AutoSize = true;
            freeCheckBox.Checked = true;
            freeCheckBox.CheckState = CheckState.Checked;
            freeCheckBox.Location = new Point(285, 20);
            freeCheckBox.Name = "freeCheckBox";
            freeCheckBox.Size = new Size(48, 19);
            freeCheckBox.TabIndex = 23;
            freeCheckBox.Text = "Free";
            freeCheckBox.UseVisualStyleBackColor = true;
            freeCheckBox.CheckedChanged += freeCheckBox_CheckedChanged;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(595, 552);
            Controls.Add(freeCheckBox);
            Controls.Add(payAsYouGoCheckBox);
            Controls.Add(endIndexLabel);
            Controls.Add(numericUpDownEnd);
            Controls.Add(startIndexLabel);
            Controls.Add(numericUpDownStart);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(promptTextBox);
            Controls.Add(label1);
            Controls.Add(apiKeyTextBox);
            Controls.Add(radioVideo);
            Controls.Add(radioPlaylist);
            Controls.Add(outputPathLabel);
            Controls.Add(outputPathTextBox);
            Controls.Add(logTextBox);
            Controls.Add(btnConvertVideoToText);
            Controls.Add(btnCancel);
            Controls.Add(btnBrowse);
            Controls.Add(playlistIdTextBox);
            Controls.Add(modelComboBox);
            Controls.Add(playlistIdLabel);
            Controls.Add(videoIdTextBox);
            Controls.Add(videoIdLabel);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "Form1";
            Text = "Video To Text AI";
            ((System.ComponentModel.ISupportInitialize)numericUpDownStart).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownEnd).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private void radioPlaylist_CheckedChanged(object sender, EventArgs e)
        {
            if (radioPlaylist.Checked)
            {
                // Show playlist ID textbox and hide video ID textbox
                playlistIdLabel.Visible = true;
                playlistIdTextBox.Visible = true;
                videoIdTextBox.Visible = false;
                videoIdLabel.Visible = false;

                startIndexLabel.Visible = true;
                endIndexLabel.Visible = true;
                numericUpDownStart.Visible = true;
                numericUpDownEnd.Visible = true;
            }
        }

        private void radioVideo_CheckedChanged(object sender, EventArgs e)
        {
            if (radioVideo.Checked)
            {
                // Show video ID textbox and hide playlist ID textbox
                playlistIdLabel.Visible = false;
                playlistIdTextBox.Visible = false;
                videoIdTextBox.Visible = true;
                videoIdLabel.Visible = true;

                startIndexLabel.Visible = false;
                endIndexLabel.Visible = false;
                numericUpDownStart.Visible = false;
                numericUpDownEnd.Visible = false;
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                outputPathTextBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private TextBox apiKeyTextBox;
        private Label label1;
        private Button btnBrowse;
        private TextBox promptTextBox;
        private Label label2;
        private Label label3;
        private ComboBox modelComboBox;
        private NumericUpDown numericUpDownStart;
        private Label startIndexLabel;
        private Label endIndexLabel;
        private NumericUpDown numericUpDownEnd;
        private CheckBox payAsYouGoCheckBox;
        private CheckBox freeCheckBox;
    }
}
