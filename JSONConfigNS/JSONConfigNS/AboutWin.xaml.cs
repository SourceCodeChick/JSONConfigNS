using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace JSONConfigNS
{
    /// <summary>
    /// Interaction logic for AboutWin.xaml
    /// </summary>
    public partial class AboutWin : Window
    {
        public AboutWin()
        {
            InitializeComponent();

            // Dynamically update info on this window based on the assembly itself

            string AppName = Assembly.GetExecutingAssembly().GetName().Name;
            Title = "About " + AppName;
            ApplicationNameLbl.Content = AppName;

            DateTime buildDate = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).LastWriteTime;

            BuildDateLbl.Content = buildDate.ToString();
            string VersionInfo = typeof(NSJSONConfigMakerWPF.MainWindow).Assembly.GetName().Version.ToString();
            VersionLbl.Content = VersionInfo;

            Assembly currentAssem = typeof(NSJSONConfigMakerWPF.MainWindow).Assembly;
            object[] attribs = currentAssem.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), true);
            if (attribs.Length > 0)
            {
                CopyrightLbl.Content = ((AssemblyCopyrightAttribute)attribs[0]).Copyright;
            }

            attribs = currentAssem.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), true);
            if (attribs.Length > 0)
            {
                DescriptionTextBox.Text = ((AssemblyDescriptionAttribute)attribs[0]).Description;
            }
        }

        private void OKBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
