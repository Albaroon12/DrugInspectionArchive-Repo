using DrugInspectionArchive.Core;
using System.Windows;

namespace DrugInspectionArchive.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        NameText.Text = AppInfo.Name;
        VersionText.Text = "الإصدار " + AppInfo.Version;
        PathText.Text = AppPaths.RootDirectory;
        ReleasesList.ItemsSource = AppInfo.Releases;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}