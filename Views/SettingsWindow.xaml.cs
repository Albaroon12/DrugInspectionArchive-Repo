using DrugInspectionArchive.Core;
using DrugInspectionArchive.Enums;
using DrugInspectionArchive.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DrugInspectionArchive.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        foreach (var rb in ModeChips.Children.OfType<RadioButton>())
            rb.IsChecked = (string?)rb.Tag == AppSettings.DefaultMode.ToString();

        foreach (var rb in SortChips.Children.OfType<RadioButton>())
            rb.IsChecked = (string?)rb.Tag == AppSettings.DefaultSort.ToString();

        DataPathText.Text = AppPaths.RootDirectory;

        Loaded += async (_, _) => await ShowSizeAsync();
    }

    private async Task ShowSizeAsync()
    {
        long bytes = await Task.Run(() =>
        {
            try
            {
                return Directory.Exists(AppPaths.RootDirectory)
                    ? new DirectoryInfo(AppPaths.RootDirectory)
                        .EnumerateFiles("*", SearchOption.AllDirectories)
                        .Sum(f => f.Length)
                    : 0L;
            }
            catch
            {
                return 0L;
            }
        });

        SizeText.Text = "الحجم الكلي للبيانات: " +
            (bytes >= 1_048_576
                ? $"{bytes / 1_048_576.0:0.#} MB"
                : $"{bytes / 1024.0:0.#} KB");
    }

    private void Mode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } &&
            Enum.TryParse(tag, out SearchMode mode))
            AppSettings.DefaultMode = mode;
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } &&
            Enum.TryParse(tag, out BookSort sort))
            AppSettings.DefaultSort = sort;
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message, "تعذر فتح المجلد", DialogKind.Error);
        }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(AppPaths.RootDirectory);

    private void OpenBackups_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(AppPaths.BackupsDirectory);

    private async void Normalize_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await new TrashService().NormalizeArchiveAsync();

            MessageDialog.Show(
                this,
                "تم فحص الأرشيف وتوحيد أسماء الملفات بصيغة Inspection Drug#رقم الكتاب",
                "اكتملت الصيانة",
                DialogKind.Success);
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message, "تعذرت الصيانة", DialogKind.Error);
        }
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}