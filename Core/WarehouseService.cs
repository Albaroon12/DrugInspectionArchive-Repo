using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DrugInspectionArchive.Core;

public sealed class WarehouseProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string AccentKey { get; set; } = AccentPalette.DefaultKey;
    public bool HasLogo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// هوية المذخر: الاسم والشعار واللون
public static class WarehouseService
{
    private const int LogoMaxSize = 256;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ProfilePath =>
        Path.Combine(AppPaths.RootDirectory, "warehouse.json");

    public static string LogoPath =>
        Path.Combine(AppPaths.RootDirectory, "warehouse-logo.png");

    public static WarehouseProfile? Current { get; private set; } = Load();

    public static bool Exists => !string.IsNullOrWhiteSpace(Current?.Name);

    // يُرفع عند أي تغيير في بيانات المذخر
    public static event Action? Changed;

    // أول حرف من الاسم (يظهر إذا لم يكن هناك شعار)
    public static string InitialOf(string? name)
    {
        char c = (name ?? string.Empty).FirstOrDefault(char.IsLetterOrDigit);

        return c == default ? string.Empty : char.ToUpperInvariant(c).ToString();
    }

    public static ImageSource? LoadLogo()
    {
        if (Current is not { HasLogo: true } || !File.Exists(LogoPath))
            return null;

        return LoadImage(LogoPath);
    }

    // قراءة صورة دون قفل الملف
    public static ImageSource? LoadImage(string path)
    {
        try
        {
            var image = new BitmapImage();

            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(
        string name, string accentKey,
        string? newLogoPath = null, bool removeLogo = false)
    {
        name = name.Trim();

        if (name.Length == 0)
            throw new ArgumentException("أدخل اسم المذخر");

        Directory.CreateDirectory(AppPaths.RootDirectory);

        var profile = Current ?? new WarehouseProfile();

        profile.Name = name;
        profile.AccentKey = AccentPalette.Find(accentKey).Key;

        if (!string.IsNullOrEmpty(newLogoPath))
        {
            SaveLogo(newLogoPath);
            profile.HasLogo = true;
        }
        else if (removeLogo)
        {
            try
            {
                if (File.Exists(LogoPath))
                    File.Delete(LogoPath);
            }
            catch
            {
            }

            profile.HasLogo = false;
        }

        string temp = ProfilePath + ".tmp";

        File.WriteAllText(temp, JsonSerializer.Serialize(profile, JsonOptions));
        File.Move(temp, ProfilePath, overwrite: true);

        Current = profile;
        Changed?.Invoke();
    }

    // حفظ الشعار بصيغة PNG وبحجم أقصى 256 بكسل
    private static void SaveLogo(string sourcePath)
    {
        BitmapSource frame = BitmapFrame.Create(
            new Uri(sourcePath, UriKind.Absolute),
            BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.OnLoad);

        double scale = Math.Min(
            1.0, LogoMaxSize / (double)Math.Max(frame.PixelWidth, frame.PixelHeight));

        if (scale < 1.0)
            frame = new TransformedBitmap(frame, new ScaleTransform(scale, scale));

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));

        string temp = LogoPath + ".tmp";

        using (var stream = File.Create(temp))
            encoder.Save(stream);

        File.Move(temp, LogoPath, overwrite: true);
    }

    private static WarehouseProfile? Load()
    {
        try
        {
            if (File.Exists(ProfilePath))
                return JsonSerializer.Deserialize<WarehouseProfile>(
                    File.ReadAllText(ProfilePath));
        }
        catch
        {
        }

        return null;
    }
}