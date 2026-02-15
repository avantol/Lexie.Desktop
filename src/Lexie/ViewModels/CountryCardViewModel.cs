using CommunityToolkit.Mvvm.ComponentModel;

namespace Lexie.ViewModels;

public partial class CountryCardViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _count;
    [ObservableProperty] private string _callsignsDisplay = "";
    [ObservableProperty] private bool _isHighlighted;
    [ObservableProperty] private string _automationName = "";

    private readonly List<string> _callsigns = [];
    private System.Timers.Timer? _highlightTimer;

    public void AddCallsign(string callsign)
    {
        if (!_callsigns.Contains(callsign))
            _callsigns.Add(callsign);
        Count++;
        CallsignsDisplay = string.Join(", ", _callsigns.TakeLast(5));
        AutomationName = $"{Name}, {Count} signals, callsigns: {CallsignsDisplay}";
    }

    public void Highlight()
    {
        IsHighlighted = true;
        _highlightTimer?.Stop();
        _highlightTimer?.Dispose();
        _highlightTimer = new System.Timers.Timer(2000) { AutoReset = false };
        _highlightTimer.Elapsed += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsHighlighted = false);
        };
        _highlightTimer.Start();
    }
}
