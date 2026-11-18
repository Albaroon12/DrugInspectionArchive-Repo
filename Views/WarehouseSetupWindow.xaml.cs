using DrugInspectionArchive.Core;
using System.Windows;

namespace DrugInspectionArchive.Views;

public partial class WarehouseSetupWindow : Window
{
    public WarehouseSetupWindow()
    {
        InitializeComponent();

        Loaded += (_, _) => Editor.FocusName();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.WarehouseName.Length == 0)
        {
            MessageDialog.Show(
                this, "أدخل اسم المذخر", "إنشاء المذخر", DialogKind.Warning);
            return;
        }

        try
        {
            WarehouseService.Save(
                Editor.WarehouseName,
                Editor.AccentKey,
                Editor.NewLogoPath,
                Editor.LogoRemoved);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message, "تعذر حفظ بيانات المذخر", DialogKind.Error);
        }
    }
}