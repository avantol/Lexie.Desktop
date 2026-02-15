using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Lexie.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
}
