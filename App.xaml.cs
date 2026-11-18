using DrugInspectionArchive.Core;
using DrugInspectionArchive.Services;
using DrugInspectionArchive.Views;
using System.Windows;

namespace DrugInspectionArchive;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // حتى لا يُغلق البرنامج عند إغلاق شاشة الإعداد الأولى
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            // لون المذخر المحفوظ
            AccentPalette.Apply(WarehouseService.Current?.AccentKey);

            // أول تشغيل: إنشاء المذخر
            if (!WarehouseService.Exists)
            {
                var setup = new WarehouseSetupWindow();

                if (setup.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            // تجهيز قاعدة البيانات قبل فتح الواجهة
            await DatabaseInitializer.InitializeAsync();

            var mainWindow = new MainWindow();

            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;

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