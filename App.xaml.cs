
using DrugInspectionArchive.Services;
using System.Windows;

namespace DrugInspectionArchive;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // تجهيز قاعدة البيانات قبل فتح الواجهة
            await DatabaseInitializer.InitializeAsync();

            // فتح النافذة الرئيسية
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"حدث خطأ أثناء تجهيز قاعدة البيانات:\n\n{ex.Message}",
                "خطأ في تشغيل البرنامج",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }
}
