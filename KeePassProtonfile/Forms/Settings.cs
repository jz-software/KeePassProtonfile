using System;
using System.Windows.Forms;
using KeePass.Plugins;
using KeePassLib;

namespace KeePassProtonfile
{
    public partial class Settings : Form
    {
        private IPluginHost m_host;
        private Configuration configuration;
        private bool Saved;

        public Settings(IPluginHost mHost)
        {
            InitializeComponent();
            m_host = mHost;
            configuration = new Configuration(m_host);
            configuration.init();
            this.Load += new EventHandler(Login_Load);
            button1.Click += new EventHandler(button1_Click);
            checkBox1.Click += new EventHandler(checkbox1_Clicked);

            if(configuration.getEntry("multipleBackups") == null)
            {
                configuration.setEntry("multipleBackups", "false");
            }

            var multipleBackups = configuration.getEntry("multipleBackups").Strings.Get(PwDefs.UserNameField).ReadString();
            checkBox1.Checked = multipleBackups == "true" ? true : false;

            checkbox1_Clicked();

            this.Icon = Properties.Resources.Protonfile_16x16_ico;
        }

        private void Login_Load(object sender, EventArgs e)
        {
            String login = configuration.getEntry("auth").Strings.Get(PwDefs.UserNameField).ReadString();
            String password = configuration.getEntry("auth").Strings.Get(PwDefs.PasswordField).ReadString();
            String destinationFolder = configuration.getEntry("destinationFolder").Strings.Get(PwDefs.UserNameField).ReadString();
            String filename = configuration.getEntry("filename").Strings.Get(PwDefs.UserNameField).ReadString();

            textBox1.Text = login;
            textBox2.Text = password;
            textBox3.Text = destinationFolder;
            textBox4.Text = filename;
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            String login = textBox1.Text;
            String password = textBox2.Text;
            String destinationFolder = textBox3.Text;
            String filename = textBox4.Text;

            configuration.setEntry("auth", login, password);
            configuration.setEntry("destinationFolder", destinationFolder);
            configuration.setEntry("filename", filename);
            configuration.setEntry("multipleBackups", checkBox1.Checked ? "true" : "false");
            configuration.setEntry("multipleBackupsNum", numericUpDown1.Value.ToString());

            Saved = true;

            Close();
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void checkbox1_Clicked(object sender, EventArgs e)
        {
            checkbox1_Clicked();
        }
        private void checkbox1_Clicked()
        {
            numericUpDown1.ReadOnly = !checkBox1.Checked;

            var entry = configuration.getEntry("multipleBackupsNum");
            if (entry == null) return;
            String multipleBackupsNum = entry.Strings.Get(PwDefs.UserNameField).ReadString();

            numericUpDown1.Value = Int32.Parse(multipleBackupsNum);
        }
        public bool isSaved()
        {
            var state = Saved;
            if (state) Saved = false;
            return state;
        }
    }
}
