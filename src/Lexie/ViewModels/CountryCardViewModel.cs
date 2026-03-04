using CommunityToolkit.Mvvm.ComponentModel;
using Lexie.Services;

namespace Lexie.ViewModels;

public partial class CountryCardViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _count;
    [ObservableProperty] private string _callsignsDisplay = "";
    [ObservableProperty] private bool _isHighlighted;
    [ObservableProperty] private string _automationName = "";
    [ObservableProperty] private int _bestSnr = int.MinValue;
    [ObservableProperty] private string? _grid;
    [ObservableProperty] private int? _distanceKm;
    [ObservableProperty] private string _snrDisplay = "";
    [ObservableProperty] private string _distanceDisplay = "";
    [ObservableProperty] private bool _isHomeCountry;
    [ObservableProperty] private bool _isNewCountry;
    [ObservableProperty] private string _newLabel = "";
    [ObservableProperty] private string _nameDisplay = "";

    private bool _useMiles;
    private readonly List<string> _callsigns = [];
    private System.Timers.Timer? _highlightTimer;

    public string? FirstCallsign => _callsigns.Count > 0 ? _callsigns[0] : null;

    public void AddCallsign(string callsign)
    {
        if (_callsigns.Contains(callsign)) return;
        _callsigns.Add(callsign);
        Count = _callsigns.Count;
        CallsignsDisplay = string.Join(", ", _callsigns.TakeLast(5)).Replace('0', '\u00D8');
        // Refresh SNR label in case callsign count crossed the 1→2 threshold
        if (BestSnr > int.MinValue)
        {
            var label = _callsigns.Count > 1 ? "Best SNR:" : "SNR:";
            SnrDisplay = $"{label} {BestSnr}";
        }
        UpdateAutomationName();
    }

    public void UpdateSnr(int snr)
    {
        if (snr > BestSnr)
        {
            BestSnr = snr;
            UpdateAutomationName();
        }
        var label = _callsigns.Count > 1 ? "Best SNR:" : "SNR:";
        SnrDisplay = $"{label} {BestSnr}";
    }

    public void UpdateGrid(string grid, string? myGrid, GridSquareService gridService)
    {
        Grid = grid;
        if (!string.IsNullOrEmpty(myGrid))
        {
            DistanceKm = gridService.DistanceKm(myGrid, grid);
            RefreshDistanceDisplay();
        }
    }

    public void SetDistanceUnit(bool useMiles)
    {
        _useMiles = useMiles;
        RefreshDistanceDisplay();
    }

    private void RefreshDistanceDisplay()
    {
        if (!DistanceKm.HasValue || IsHomeCountry)
        {
            DistanceDisplay = "";
            return;
        }

        int value;
        string unit;
        if (_useMiles)
        {
            value = (int)Math.Round(DistanceKm.Value * 0.621371);
            unit = "mi";
        }
        else
        {
            value = DistanceKm.Value;
            unit = "km";
        }

        var prefix = value < 200 ? "~" : "";
        DistanceDisplay = $"{prefix}{value:N0} {unit}";
        UpdateAutomationName();
    }

    private void UpdateAutomationName()
    {
        var parts = $"{Name}, {Count} signals, callsigns: {CallsignsDisplay}";
        if (BestSnr > int.MinValue) parts += $", best SNR: {BestSnr}";
        if (!string.IsNullOrEmpty(DistanceDisplay)) parts += $", distance: {DistanceDisplay}";
        AutomationName = parts;
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
