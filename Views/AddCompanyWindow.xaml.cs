using DrugInspectionArchive.Services;
using System.Windows;
using System.Windows.Controls;

namespace DrugInspectionArchive.Views;

public partial class AddCompanyWindow : Window
{
    private readonly CompanyService _companyService = new();

    public string? CreatedCompanyName { get; private set; }

    public AddCompanyWindow() : this(null)
    {
    }

    // initialName: اسم مقترح (مثلاً المقروء من الكتاب)
    public AddCompanyWindow(string? initialName)
    {
        InitializeComponent();

        CompanyNameTextBox.Text = initialName?.Trim() ?? string.Empty;

        Loaded += (_, _) =>
        {
            CompanyNameTextBox.Focus();
            CompanyNameTextBox.SelectAll();
        };
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = CompanyNameTextBox.Text.Trim();

        if (name.Length == 0)
        {
            ShowError("اكتب اسم الشركة");
            return;
        }

        SaveButton.IsEnabled = false;

        try
        {
            await _companyService.CreateAsync(name);

            CreatedCompanyName = name;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            SaveButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CompanyName_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (ErrorText is not null)
            ErrorText.Visibility = Visibility.Collapsed;
    }

    // عرض الخطأ داخل النافذة (بدون نقطة بالنهاية)
    private void ShowError(string message)
    {
        ErrorText.Text = message.TrimEnd('.', ' ');
        ErrorText.Visibility = Visibility.Visible;
    }
}