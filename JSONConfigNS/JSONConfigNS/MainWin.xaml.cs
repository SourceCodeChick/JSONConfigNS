using Microsoft.Win32;
using SeaMonkey.JSONConfigNS;
using System;
using System.ComponentModel;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace NSJSONConfigMakerWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        BackgroundWorker worker = new BackgroundWorker();
        bool ConfigChanged { get; set; }
        bool TestingConnection { get; set; }
        bool CanConnect { get; set; }
        Action emptyDelegate = delegate { };
        public ConfigData Config { get; set; }
        public DispatcherTimer timer;
        private static readonly Regex _regex = new Regex("[^0-9.-]+"); //regex that matches disallowed text
        private static bool IsTextAllowed(string text)
        {
            return !_regex.IsMatch(text);
        }

        public MainWindow()
        {
            InitializeComponent();
            AES.Init(EncryptionType.AES_2020v1);
            PopulateEncryptionTypeCB();
            Config = new ConfigData();
        }

        private void PopulateEncryptionTypeCB()
        {
            EncryptionTypeCB.Items.Clear();
            foreach (EncryptionType eType in EncryptionType.GetValues(typeof(EncryptionType)))
            {
                EncryptionTypeCB.Items.Add(eType.ToString());
            }
            EncryptionTypeCB.SelectedIndex = (int)EncryptionType.None;
            /*EncryptionType TestEType = StrToEncryptionType("whatever");
            MessageBox.Show(TestEType.ToString());*/
        }

        private EncryptionType StrToEncryptionType(string str)
        {
            EncryptionType RetVal = EncryptionType.None;
            foreach (EncryptionType eType in EncryptionType.GetValues(typeof(EncryptionType)))
            {
                if (str.Equals(eType.ToString(), System.StringComparison.OrdinalIgnoreCase))
                {
                    RetVal = eType;
                    break;
                }
            }

            return RetVal;
        }

        private void CreateSampleConfigFile()
        {
            // Manually setting up a sample configuration
            Config = new ConfigData(
                    "add-server-name-here",
                    "add-database-name-here",
                    "add-username-here",
                    "add-password-here",
                    15,
                    AES.EncryptionKey.ToString()
            );
            // Saving the above to a file
            Config.Save(Config.SuggestFilename());

            // Reset configuration
            Config.Clear();
            ConfigChanged = false;
        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FileSelectBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenNewFile();
            ShowPassword(false);
        }

        private void OpenNewFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                DefaultExt = ".cfg",
                Filter = "Config File|*.cfg|Text File|*.txt|All Files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FilenameTextBox.Text = openFileDialog.FileName;
                OpenFile(openFileDialog.FileName);
            }
        }

        private string GetSaveAsFilename()
        {
            string CurrentFilename = FilenameTextBox.Text;
            try
            {
                CurrentFilename = Path.GetFileName(CurrentFilename);
            }
            catch
            {
            }

            if (CurrentFilename.Length == 0)
            {
                CurrentFilename = "Untitled.cfg";
            }
            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                DefaultExt = ".cfg",
                FileName = CurrentFilename,
                Filter = "Config File|*.cfg|Text File|*.txt|All Files|*.*"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                CurrentFilename = saveFileDialog.FileName;
            }
            else
            {
                CurrentFilename = "";
            }
            return CurrentFilename;
        }

        private void OpenFile(string Filename)
        {
            if (File.Exists(Filename))
            {
                bool Loaded = Config.Load(Filename);
                if (Loaded)
                {
                    ServerTextBox.Text = Config.DB.Server;
                    DatabaseTextBox.Text = Config.DB.Database;
                    UserIDTextBox.Text = Config.DB.Username;
                    PasswordBox.Password = Config.DB.Password;
                    TimeoutTextBox.Text = Config.DB.ConnectTimeout.ToString();
                    IntegratedSecurityCB.IsChecked = Config.DB.IntegratedSecurity;
                    PersistSecurityInfoCB.IsChecked = Config.DB.PersistSecurityInfo;
                    FilenameTextBox.Text = Filename;
                    try
                    {
                        int Index = (int)StrToEncryptionType(Config.EncryptionType);
                        if (Index < EncryptionTypeCB.Items.Count)
                        {
                            EncryptionTypeCB.SelectedIndex = Index;
                        }
                        ConfigChanged = false;
                        Output(Config.ToStringEncrypted(), false, 0);
                    }
                    catch
                    {
                        EncryptionTypeCB.SelectedIndex = (int)EncryptionType.None;
                    }
                }
                else
                {
                    ConfigChanged = false;
                    MessageBox.Show("Unable to load file", "File Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            UpdateUI();
        }

        private void Reset()
        {
            Config.Clear();
            FilenameTextBox.Clear();
            ServerTextBox.Clear();
            DatabaseTextBox.Clear();

            UserIDLbl.IsEnabled = true;
            UserIDTextBox.Clear();
            UserIDTextBox.IsEnabled = true;

            ShowPasswordLbl.Visibility = Visibility.Hidden;
            PasswordBox.Clear();
            PasswordBox.IsEnabled = true;
            PasswordLbl.IsEnabled = true;

            OutputTextBox.Text = "";
            TimeoutTextBox.Text = Config.DB.ConnectTimeout.ToString();
            IntegratedSecurityCB.IsChecked = Config.DB.IntegratedSecurity;
            PersistSecurityInfoCB.IsChecked = Config.DB.PersistSecurityInfo;
            EncryptionTypeCB.SelectedIndex = (int)EncryptionType.None;

            CanConnect = false;
            TestingConnection = false;
            ConnectionTestRect.Visibility = Visibility.Hidden;
            ConnectionStringLinkLbl.Visibility = Visibility.Hidden;
            JSONStringLinkLbl.Visibility = Visibility.Hidden;
            ShowPassword(false);
            UpdateUI();
            ConfigChanged = false;
        }

        private void ShowPassword(bool Flag)
        {
            if (Flag)
            {
                var Resource = TryFindResource("ShowPW");
                if (Resource != null && Resource.GetType().Name.Equals("Image", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Show the password
                    PasswordVisibilityToggleBtn.Content = Resource;
                    ShowPasswordLbl.Visibility = Visibility.Visible;
                    ShowPasswordLbl.Content = PasswordBox.Password;
                }
            }
            else
            {
                var Resource = TryFindResource("HidePW");
                if (Resource != null && Resource.GetType().Name.Equals("Image", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Hide the password
                    PasswordVisibilityToggleBtn.Content = Resource;
                    ShowPasswordLbl.Visibility = Visibility.Hidden;
                    ShowPasswordLbl.Content = "";
                }
            }
        }

        private void PasswordVisibilityToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            var CurrentResource = PasswordVisibilityToggleBtn.Content;
            if (CurrentResource != null)
            {
                System.Windows.Controls.Image BtnImage = (System.Windows.Controls.Image)CurrentResource;
                var Current = BtnImage.Source.ToString();
                ShowPassword(!Current.ToLower().Contains("show"));
            }
        }

        private void OrigPasswordVisibilityToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            var CurrentResource = PasswordVisibilityToggleBtn.Content;
            if (CurrentResource != null)
            {
                System.Windows.Controls.Image BtnImage = (System.Windows.Controls.Image)CurrentResource;
                var Current = BtnImage.Source.ToString();
                if (Current.ToLower().Contains("show"))
                {
                    var Resource = TryFindResource("HidePW");
                    if (Resource != null && Resource.GetType().Name.Equals("Image", System.StringComparison.OrdinalIgnoreCase))
                    {
                        PasswordVisibilityToggleBtn.Content = Resource;
                        ShowPasswordLbl.Visibility = Visibility.Hidden;
                        ShowPasswordLbl.Content = "";
                    }
                }
                else
                {
                    var Resource = TryFindResource("ShowPW");
                    if (Resource != null && Resource.GetType().Name.Equals("Image", System.StringComparison.OrdinalIgnoreCase))
                    {
                        PasswordVisibilityToggleBtn.Content = Resource;
                        ShowPasswordLbl.Visibility = Visibility.Visible;
                        ShowPasswordLbl.Content = PasswordBox.Password;
                    }
                }
            }
            PasswordBox.Focus();
        }

        private void PasswordBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ShowPasswordLbl.Visibility == Visibility.Visible)
            {
                ShowPasswordLbl.Content = PasswordBox.Password;
            }
            Config.DB.Password = PasswordBox.Password.Trim();
            ConfigChanged = true;
            UpdateUI();
        }

        private void AttemptFileDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Assuming you have one file that you care about, pass it off to whatever
                // handling code you have defined.
                FilenameTextBox.Text = files[0];
                OpenFile(files[0]);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            AttemptFileDrop(e);
        }

        private void IntegratedSecurityCB_Checked(object sender, RoutedEventArgs e)
        {
            PasswordBox.Tag = PasswordBox.Password;
            UserIDTextBox.Tag = UserIDTextBox.Text;
            PasswordBox.Clear();
            ShowPassword(false);
            UserIDTextBox.Clear();
            PasswordBox.IsEnabled = false;
            UserIDTextBox.IsEnabled = false;
            PasswordLbl.IsEnabled = false;
            UserIDLbl.IsEnabled = false;
            PasswordVisibilityToggleBtn.Visibility = Visibility.Hidden;

            Config.DB.Username = "";
            Config.DB.Password = "";
            Config.DB.IntegratedSecurity = true;
            ConfigChanged = true;

            UpdateUI();
        }

        private void IntegratedSecurityCB_Unchecked(object sender, RoutedEventArgs e)
        {
            PasswordBox.Password = (string)PasswordBox.Tag;
            UserIDTextBox.Text = (string)UserIDTextBox.Tag;
            ShowPassword(false);
            PasswordBox.IsEnabled = true;
            UserIDTextBox.IsEnabled = true;
            PasswordLbl.IsEnabled = true;
            UserIDLbl.IsEnabled = true;
            PasswordVisibilityToggleBtn.Visibility = Visibility.Visible;

            Config.DB.Username = UserIDTextBox.Text;
            Config.DB.Password = PasswordBox.Password;
            Config.DB.IntegratedSecurity = false;
            ConfigChanged = true;

            UpdateUI();
        }

        private void OpenMenu_Click(object sender, RoutedEventArgs e)
        {
            OpenNewFile();
            ShowPassword(false);
        }

        private void NewMenu_Click(object sender, RoutedEventArgs e)
        {
            Reset();
            ServerTextBox.Focus();
        }

        private void TimeoutTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text) || TimeoutTextBox.Text.Length > 2;
        }

        // Use the DataObject.Pasting Handler - prevent all pasting of content into this field
        // This is to ensure content can only be numeric since this is the connection timeout value (int)
        private void TimeoutPastingHandler(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            Save(FilenameTextBox.Text.Trim());
        }

        private void Output(string Msg, bool Append=false, int ClearAfter=5)
        {
            if (timer != null)
            {
                try
                {
                    KillTimer();
                }
                catch
                {
                }
            }
            if (!Append)
            {
                try
                {
                    OutputTextBox.Text = Msg;
                }
                catch
                {

                }
            }
            else
            {
                try
                {
                    if (OutputTextBox.Text.Length > 0)
                    {
                        OutputTextBox.Text = OutputTextBox.Text + Environment.NewLine;
                    }
                    OutputTextBox.Text = OutputTextBox.Text + Msg;
                }
                catch
                {

                }
            }
            if (ClearAfter > 0)
            {
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(ClearAfter);
                timer.Tick += timer_Tick;
                timer.Start();
            }
            ForceRefresh();
        }

        void timer_Tick(object sender, EventArgs e)
        {
            KillTimer();
            Output(Config.ToStringEncrypted(), false, 0);
        }

        void KillTimer()
        {
            timer.Stop();
            ConnectionTestRect.Visibility = Visibility.Hidden;
            timer = null;
        }

        void ForceRefresh()
        {
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
        }

        // Set up backgroundworker to run SQL DB connection test asynchronously
        private void TestConnection()
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += worker_DoWork;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        // Asynchronous background function for testing connection to DB server
        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                SqlConnection Conn = new SqlConnection(Config.DB.ConnectionString);
                Conn.Open();
                Conn.Close();
                CanConnect = true;
            }
            catch (Exception ex)
            {
                CanConnect = false;
                Output(ex.Message);
            }
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Communicate w/rest of application as needed
            OutputTextBox.Text = DateTime.Now.ToString();
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TestingConnection = false;
        }

        private void CancelTestConnection()
        {
            TestConnectionBtn.Content = "Test Connection";
            try
            {
                worker.CancelAsync();
                while(worker.CancellationPending)
                {
                    System.Threading.Thread.Sleep(100);
                }
                worker.Dispose();
            }
            catch
            {
            }

            TestConnProgressBar.Visibility = Visibility.Hidden;
            TestConnProgressBar.IsIndeterminate = false;
            ConnectionTestRect.Visibility = Visibility.Hidden;
            Output("Test connection cancelled.");

            UpdateUI();
        }

        private void TestConnectionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TestConnectionBtn.Content.Equals("Cancel"))
            {
                CancelTestConnection();
                return;
            }
            ConnectionTestRect.Visibility = Visibility.Visible;
            ConnectionTestRect.Fill = new SolidColorBrush(Colors.Gray);
            TestingConnection = true;
            TestConnectionBtn.Content = "Cancel";
            CanConnect = false;
            TestConnProgressBar.Visibility = Visibility.Visible;
            TestConnProgressBar.IsIndeterminate = true;
            ForceRefresh();
            System.Threading.Thread.Sleep(500);
            Output("Testing connection to database server ...", false, 0);

            // Start connection test asynchronously
            TestConnection();

            //TestConnectionBtn.IsEnabled = false;
            SaveBtn.IsEnabled = false;

            // Wait for result without freezing UI
            while (TestingConnection)
            {
                System.Threading.Thread.Sleep(500);
                ForceRefresh();
            }

            TestConnProgressBar.Visibility = Visibility.Hidden;
            TestConnProgressBar.IsIndeterminate = false;
            UpdateUI();

            if (CanConnect)
            {
                Output("Connection Test:  Success");
                ConnectionTestRect.Fill = new SolidColorBrush(Colors.Green);
            }
            else
            {
                Output("Connection Test:  Failed");
                ConnectionTestRect.Fill = new SolidColorBrush(Colors.Red);
            }
            TestConnectionBtn.Content = "Test Connection";
        }

        private void UpdateUI()
        {
            bool ConfigViable = (Config==null) ? false : Config.Viable();

            TestConnectionBtn.IsEnabled = ConfigViable;
            SaveBtn.IsEnabled = ConfigViable;
            if (ConfigViable)
            {
                ConnectionStringLinkLbl.Visibility = Visibility.Visible;
                JSONStringLinkLbl.Visibility = Visibility.Visible;
                Output(Config.ToStringEncrypted(), false, 0);
            }
            else
            {
                ConnectionStringLinkLbl.Visibility = Visibility.Hidden;
                JSONStringLinkLbl.Visibility = Visibility.Hidden;
                OutputTextBox.Clear();
            }
        }

        private void ServerTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Config.DB.Server = ServerTextBox.Text.Trim();
            ConfigChanged = true;
            UpdateUI();
        }

        private void DatabaseTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Config.DB.Database = DatabaseTextBox.Text.Trim();
            ConfigChanged = true;
            UpdateUI();

        }

        private void UserIDTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Config.DB.Username = UserIDTextBox.Text.Trim();
            ConfigChanged = true;
            UpdateUI();
        }

        private void TimeoutTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                Config.DB.ConnectTimeout = Convert.ToInt32(TimeoutTextBox.Text.Trim());
            }
            catch
            {
                TimeoutTextBox.Text = Convert.ToInt32(Config.DB.ConnectTimeout.ToString()).ToString();
            }
            ConfigChanged = true;
            UpdateUI();
        }

        private void EncryptionTypeCB_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            //   Output(EncryptionTypeCB.Items[EncryptionTypeCB.SelectedIndex].ToString());
            if (Config != null && EncryptionTypeCB.SelectedIndex > -1)
            {
                Config.EncryptionType = EncryptionTypeCB.Items[EncryptionTypeCB.SelectedIndex].ToString();
            }
            UpdateUI();
        }

        private bool Save(string Filename)
        {
            bool FileExists = false;

            if (Filename.Length == 0)
            {
                // get filename
                Filename=GetSaveAsFilename();
            }

            if (Filename.Length == 0)
            {
                Output("Save canceled", false, 12);
                return false;
            }

            try
            {
                Config.Save(Filename);
                FileExists = (File.Exists(Filename));
            }
            catch
            {
                return false;
            }

            if (FileExists)
            {
                // Reload file (validates the file contents are loadable)
                Reset();
                OpenFile(Filename);
                Output(Config.ToStringEncrypted(), false, 0);
                MessageBox.Show("Configuration saved", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            else
            {
                Output("There was a problem saving the configuration file to the selected path:  " + FilenameTextBox.Text, false, 10);
                return false;
            }
        }

        private void SaveMenu_Click(object sender, RoutedEventArgs e)
        {
            Save(FilenameTextBox.Text.Trim());
        }

        private void SaveAsMenu_Click(object sender, RoutedEventArgs e)
        {
            bool Saved = false;
            string OrigFN = FilenameTextBox.Text.Trim();
            string Filename = GetSaveAsFilename();

            if (Filename.Length > 0)
            {
                Saved = Save(Filename);
            }

            if (!Saved && OrigFN.Length > 0  && File.Exists(OrigFN))
            {
                Reset();
                OpenFile(OrigFN);
            }
        }

        private void TimeoutTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Config.DB.ConnectTimeout < 3)
            {
                Config.DB.ConnectTimeout = 3;
                TimeoutTextBox.Text = "3";
            }
            else
            {
                if (Config.DB.ConnectTimeout > 30)
                {
                    Config.DB.ConnectTimeout = 30;
                    TimeoutTextBox.Text = "30";
                }
            }
        }

        private void ConnectionStringLinkLbl_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Output(Config.DB.ConnectionString, false, 0);
        }

        private void JSONStringLinkLbl_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Output(Config.ToStringEncrypted(), false, 0);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Reset();
            ServerTextBox.Focus();
        }

        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            Opacity = 0.5;
            JSONConfigNS.AboutWin aboutWin = new JSONConfigNS.AboutWin();
            aboutWin.ShowDialog();
            Opacity = 1.0;
        }

        private void PersistSecurityInfoCB_Checked(object sender, RoutedEventArgs e)
        {
            Config.DB.PersistSecurityInfo = true;
            UpdateUI();
        }

        private void PersistSecurityInfoCB_Unchecked(object sender, RoutedEventArgs e)
        {
            Config.DB.PersistSecurityInfo = false;
            UpdateUI();
        }
    }
}
