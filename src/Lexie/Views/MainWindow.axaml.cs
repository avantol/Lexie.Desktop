using System.Globalization;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Lexie.Views;

public partial class MainWindow : Window
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lexie", "window.json");

    public MainWindow()
    {
        InitializeComponent();
        RestoreWindowPosition();
        Opened += (_, _) => ValidatePosition();
        PositionChanged += (_, _) => SaveWindowPosition();
        SizeChanged += (_, _) => SaveWindowPosition();
    }

    private void RestoreWindowPosition()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            var pos = JsonSerializer.Deserialize<WindowPosition>(json);
            if (pos == null) return;

            Width = pos.Width;
            Height = pos.Height;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(pos.X, pos.Y);
            if (pos.IsMaximized)
                WindowState = WindowState.Maximized;
        }
        catch { /* ignore corrupt settings */ }
    }

    private void ValidatePosition()
    {
        // Ensure window is visible on a connected screen
        var screens = Screens;
        if (screens.All.Count == 0) return;

        var pos = Position;
        var onScreen = screens.All.Any(s =>
            pos.X >= s.Bounds.X - 50 &&
            pos.Y >= s.Bounds.Y - 50 &&
            pos.X < s.Bounds.X + s.Bounds.Width - 100 &&
            pos.Y < s.Bounds.Y + s.Bounds.Height - 100);

        if (!onScreen)
        {
            // Move to primary screen center
            var primary = screens.Primary ?? screens.All[0];
            Position = new PixelPoint(
                primary.Bounds.X + (primary.Bounds.Width - (int)Width) / 2,
                primary.Bounds.Y + (primary.Bounds.Height - (int)Height) / 2);
        }
    }

    private void SaveWindowPosition()
    {
        try
        {
            var pos = new WindowPosition
            {
                X = Position.X,
                Y = Position.Y,
                Width = (int)Width,
                Height = (int)Height,
                IsMaximized = WindowState == WindowState.Maximized
            };
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(pos));
        }
        catch { /* ignore save errors */ }
    }

    private class WindowPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsMaximized { get; set; }
    }

    // Converter: bool IsConnected → status dot color
    public static readonly FuncValueConverter<bool, IBrush> StatusColorConverter =
        new(connected => connected
            ? new SolidColorBrush(Color.Parse("#44ff88"))
            : new SolidColorBrush(Color.Parse("#ff4444")));

    // Converter: bool IsHighlighted → card border brush
    public static readonly FuncValueConverter<bool, IBrush> HighlightBorderConverter =
        new(highlighted => highlighted
            ? new SolidColorBrush(Color.Parse("#4a8aff"))
            : new SolidColorBrush(Color.Parse("#2a3a5c")));

    // Converter: bool isActive → toggle segment background
    public static readonly FuncValueConverter<bool, IBrush> ActiveToggleBgConverter =
        new(active => active
            ? new SolidColorBrush(Color.Parse("#3a5a8c"))
            : SolidColorBrush.Parse("Transparent"));

}
