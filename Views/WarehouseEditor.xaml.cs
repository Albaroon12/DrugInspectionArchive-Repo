using DrugInspectionArchive.Core;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DrugInspectionArchive.Views;

public partial class WarehouseEditor : UserControl
{
    private readonly string _group = "Accent" + Guid.NewGuid().ToString("N");

    public WarehouseEditor()
    {
        InitializeComponent();

        BuildSwatches();
        UpdatePreview();
    }

    public string WarehouseName => NameBox.Text.Trim();

    public string AccentKey { get; private set; } = AccentPalette.DefaultKey;

    // شعار جديد اختاره المستخدم ولم يُحفظ بعد
    public string? NewLogoPath { get; private set; }

    public bool LogoRemoved { get; private set; }

    public void Load(WarehouseProfile? profile)
    {
        NameBox.Text = profile?.Name ?? string.Empty;
        AccentKey = AccentPalette.Find(profile?.AccentKey).Key;

        NewLogoPath = null;
        LogoRemoved = false;

        foreach (var rb in SwatchPanel.Children.OfType<RadioButton>())
            rb.IsChecked = (rb.Tag as AccentOption)?.Key == AccentKey;

        UpdatePreview();
    }

    public void FocusName() => NameBox.Focus();

    private void BuildSwatches()
    {
        var style = (Style)FindResource("AccentSwatch");

        for (int i = 0; i < AccentPalette.All.Count; i++)
        {
            var option = AccentPalette.All[i];

            var rb = new RadioButton
            {
                Tag = option,
                GroupName = _group,
                Style = style,
                ToolTip = option.Name,
                IsChecked = option.Key == AccentKey,
                Margin = new Thickness(i == 0 ? 0 : 10, 0, 0, 0)
            };

            rb.Click += Swatch_Click;

            SwatchPanel.Children.Add(rb);
        }
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: AccentOption option })
        {
            AccentKey = option.Key;

            // معاينة مباشرة للون
            AccentPalette.Apply(option.Key);
        }
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdatePreview();

    private void PickLogo_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);

        var dialog = new OpenFileDialog
        {
            Title = "اختيار شعار المذخر",
            Filter = "الصور|*.png;*.jpg;*.jpeg;*.bmp"
        };

        if (dialog.ShowDialog(owner) != true)
            return;

        if (WarehouseService.LoadImage(dialog.FileName) is null)
        {
            MessageDialog.Show(
                owner!, "تعذر قراءة الصورة المختارة",
                "اختيار الشعار", DialogKind.Warning);
            return;
        }

        NewLogoPath = dialog.FileName;
        LogoRemoved = false;

        UpdatePreview();
    }

    private void RemoveLogo_Click(object sender, RoutedEventArgs e)
    {
        NewLogoPath = null;
        LogoRemoved = true;

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        ImageSource? image =
            NewLogoPath is not null
                ? WarehouseService.LoadImage(NewLogoPath)
                : LogoRemoved
                    ? null
                    : WarehouseService.LoadLogo();

        LogoImage.Source = image;

        LogoBadge.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        InitialBadge.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
        RemoveLogoButton.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;

        InitialText.Text = WarehouseService.InitialOf(NameBox.Text);
    }
}