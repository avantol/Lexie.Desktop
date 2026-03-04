using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lexie.Models;
using Lexie.Services;

namespace Lexie.ViewModels;

public enum SortOption { Name, Spots, Snr, Distance }

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly WsjtxParser _parser;
    private readonly DxccLookupService _dxcc;
    private readonly CallsignExtractor _extractor;
    private readonly WsjtxUdpService _udp;
    private readonly AnnouncementService _announcer;
    private readonly GridSquareService _gridService;
    private readonly AdiLogService _adiLog;

    private static readonly string UserSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lexie", "settings.json");

    private readonly Dictionary<string, CountryCardViewModel> _countryMap = new();
    private readonly HashSet<string> _allCallsigns = new();
    private CancellationTokenSource? _cts;
    private string? _myGrid;
    private string? _myCountry;
    private DateTime _discardUntil = DateTime.MinValue;

    public ObservableCollection<CountryCardViewModel> Countries { get; } = [];
    public ObservableCollection<FeedEntryViewModel> FeedEntries { get; } = [];

    [ObservableProperty] private int _countryCount;
    [ObservableProperty] private int _callsignCount;
    [ObservableProperty] private int _messageCount;
    [ObservableProperty] private VoiceMode _voiceMode = VoiceMode.Off;
    [ObservableProperty] private string _statusText = "Starting...";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private SortOption _sortOption = SortOption.Name;
    [ObservableProperty] private bool _sortAscending = true;
    [ObservableProperty] private bool _useMiles = true;
    [ObservableProperty] private string _currentMode = "";
    [ObservableProperty] private string _currentBand = "";
    [ObservableProperty] private string _currentFrequency = "";
    [ObservableProperty] private string _myGridDisplay = "";

    public MainWindowViewModel(WsjtxParser parser, DxccLookupService dxcc,
        CallsignExtractor extractor, WsjtxUdpService udp, AnnouncementService announcer,
        GridSquareService gridService, AdiLogService adiLog)
    {
        _parser = parser;
        _dxcc = dxcc;
        _extractor = extractor;
        _udp = udp;
        _announcer = announcer;
        _gridService = gridService;
        _adiLog = adiLog;
        LoadUserSettings();
    }

    partial void OnSortOptionChanged(SortOption value)
    {
        ResortCountries();
        OnPropertyChanged(nameof(AscendingLabel));
        OnPropertyChanged(nameof(DescendingLabel));
        SaveUserSettings();
    }

    partial void OnSortAscendingChanged(bool value)
    {
        ResortCountries();
        SaveUserSettings();
    }

    public string AscendingLabel => SortOption switch
    {
        SortOption.Name => "A\u2192Z",
        SortOption.Distance => "Near",
        SortOption.Spots => "Few",
        SortOption.Snr => "Low",
        _ => "\u25B2"
    };

    public string DescendingLabel => SortOption switch
    {
        SortOption.Name => "Z\u2192A",
        SortOption.Distance => "Far",
        SortOption.Spots => "Many",
        SortOption.Snr => "High",
        _ => "\u25BC"
    };

    [RelayCommand]
    private void SortAsc() => SortAscending = true;

    [RelayCommand]
    private void SortDesc() => SortAscending = false;

    [RelayCommand]
    private void UseKm() => UseMiles = false;

    [RelayCommand]
    private void UseMi() => UseMiles = true;

    partial void OnUseMilesChanged(bool value)
    {
        foreach (var card in Countries)
            card.SetDistanceUnit(value);
        SaveUserSettings();
    }

    [RelayCommand]
    private void TestVoice()
    {
        _announcer.Test(VoiceMode);
    }

    [RelayCommand]
    private void Clear()
    {
        Countries.Clear();
        FeedEntries.Clear();
        _countryMap.Clear();
        _allCallsigns.Clear();
        CountryCount = 0;
        CallsignCount = 0;
        MessageCount = 0;
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();

        // Load CTY.DAT entity data (must happen before ADI log parsing)
        await Dispatcher.UIThread.InvokeAsync(() => StatusText = "Loading entity data...");
        await Task.Run(() => _dxcc.LoadCtyDat());

        // Load ADI logs on background thread so UI stays responsive
        await Dispatcher.UIThread.InvokeAsync(() => StatusText = "Loading log files...");
        await Task.Run(() => _adiLog.LoadLogs());

        // Show load results transiently
        var allResults = _dxcc.LoadResults.Concat(_adiLog.LoadResults);
        foreach (var result in allResults)
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusText = result);
            await Task.Delay(2000);
        }

        // Start UDP listener on background thread
        _ = Task.Run(() => _udp.StartAsync(2237, _cts.Token), _cts.Token);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusText = "Listening for UDP multicast on 239.255.0.0 port 2237";
        });

        // Process messages from channel
        try
        {
            await foreach (var data in _udp.Messages.ReadAllAsync(_cts.Token))
            {
                var message = _parser.Parse(data);
                if (message == null) continue;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!IsConnected)
                    {
                        IsConnected = true;
                        StatusText = "Receiving";
                    }
                    ProcessMessage(message);
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private void ProcessMessage(WsjtxMessage message)
    {
        var callsigns = new List<string>();
        string? messageText = null;
        int? snr = null;
        string? grid = null;

        switch (message)
        {
            case DecodeMessage decode:
                if (DateTime.UtcNow < _discardUntil) return;
                messageText = decode.Message;
                callsigns = _extractor.Extract(decode.Message);
                snr = decode.Snr;
                grid = _extractor.ExtractGrid(decode.Message);
                break;
            case StatusMessage status:
                HandleStatusUpdate(status);
                return;
            case QsoLoggedMessage qso when !string.IsNullOrEmpty(qso.DxCall):
                callsigns.Add(qso.DxCall);
                if (!string.IsNullOrEmpty(qso.DxGrid))
                    grid = qso.DxGrid;
                break;
            case WsprDecodeMessage wspr when !string.IsNullOrEmpty(wspr.Callsign):
                callsigns.Add(wspr.Callsign);
                snr = wspr.Snr;
                if (!string.IsNullOrEmpty(wspr.Grid))
                    grid = wspr.Grid;
                break;
        }

        if (callsigns.Count == 0) return;

        MessageCount++;
        var needsResort = false;

        foreach (var call in callsigns)
        {
            var country = _dxcc.Lookup(call);
            if (country == null) continue;

            // Compute distance from entity center (single lookup, same entity object)
            int? entityDistance = null;
            if (!string.IsNullOrEmpty(_myGrid))
            {
                var result = _dxcc.LookupWithDistance(call, _myGrid);
                if (result != null)
                    entityDistance = result.Value.DistanceKm;
            }

            _allCallsigns.Add(call);
            var isNewCountry = !_countryMap.ContainsKey(country);

            if (_countryMap.TryGetValue(country, out var card))
            {
                card.AddCallsign(call);
                card.Highlight();
                // Fill distance if card was created before _myGrid was available
                if (!card.DistanceKm.HasValue && entityDistance.HasValue)
                {
                    card.DistanceKm = entityDistance;
                    card.SetDistanceUnit(UseMiles);
                }
            }
            else
            {
                var isNew = _adiLog.IsNewCountry(country);
                var isNewOnBand = !isNew && _adiLog.IsNewOnBand(country, CurrentBand);
                var newLabel = isNew ? "New:" : isNewOnBand ? "New on band:" : "";
                card = new CountryCardViewModel
                {
                    Name = country,
                    IsHomeCountry = country == _myCountry,
                    IsNewCountry = isNew || isNewOnBand,
                    NewLabel = newLabel,
                    NameDisplay = country
                };
                card.SetDistanceUnit(UseMiles);
                card.AddCallsign(call);
                card.Highlight();
                _countryMap[country] = card;

                // Set distance (skip home country — no meaningful distance)
                if (!card.IsHomeCountry && entityDistance.HasValue)
                    card.DistanceKm = entityDistance;
                card.SetDistanceUnit(UseMiles);

                InsertSorted(card);
            }

            // Update SNR
            if (snr.HasValue)
            {
                var oldSnr = card.BestSnr;
                card.UpdateSnr(snr.Value);
                if (card.BestSnr != oldSnr && SortOption == SortOption.Snr)
                    needsResort = true;
            }

            // Grid-based distance from decoded messages (disabled — using entity center only)
            // if (!string.IsNullOrEmpty(grid))
            // {
            //     card.UpdateGrid(grid, _myGrid, _gridService);
            //     if (SortOption == SortOption.Distance)
            //         needsResort = true;
            // }

            // Spots changed — may need resort
            if (!isNewCountry && SortOption == SortOption.Spots)
                needsResort = true;

            // Add feed entry (skip duplicates)
            if (FeedEntries.Count == 0 || FeedEntries[0].Callsign != call.Replace('0', '\u00D8'))
            {
                FeedEntries.Insert(0, new FeedEntryViewModel
                {
                    TimeDisplay = DateTime.Now.ToString("HH:mm:ss"),
                    Callsign = call.Replace('0', '\u00D8'),
                    Country = country,
                    RawMessage = messageText,
                });
            }

            // Cap feed at 200
            while (FeedEntries.Count > 200)
                FeedEntries.RemoveAt(FeedEntries.Count - 1);

            // Announce new countries
            if (isNewCountry)
                _announcer.Announce(country, call, VoiceMode);
        }

        if (needsResort)
            ResortCountries();

        CountryCount = _countryMap.Count;
        CallsignCount = _allCallsigns.Count;
    }

    private void HandleStatusUpdate(StatusMessage status)
    {
        if (!string.IsNullOrEmpty(status.DeGrid))
        {
            var gridChanged = _myGrid != status.DeGrid;
            _myGrid = status.DeGrid;
            MyGridDisplay = _myGrid;

            // Fill distances for any cards that don't have one yet
            var filled = false;
            foreach (var card in _countryMap.Values)
            {
                if (card.DistanceKm.HasValue || card.IsHomeCountry) continue;
                if (card.FirstCallsign != null)
                {
                    var result = _dxcc.LookupWithDistance(card.FirstCallsign, _myGrid);
                    if (result != null)
                    {
                        card.DistanceKm = result.Value.DistanceKm;
                        card.SetDistanceUnit(UseMiles);
                        filled = true;
                    }
                }
            }
            if (filled && SortOption == SortOption.Distance)
                ResortCountries();
        }

        if (!string.IsNullOrEmpty(status.DeCall))
        {
            var homeCountry = _dxcc.Lookup(status.DeCall);
            if (homeCountry != null && homeCountry != _myCountry)
            {
                _myCountry = homeCountry;
                if (_countryMap.TryGetValue(homeCountry, out var homeCard))
                {
                    homeCard.IsHomeCountry = true;
                    homeCard.DistanceKm = null;
                    homeCard.SetDistanceUnit(UseMiles);
                    if (SortOption == SortOption.Distance)
                        ResortCountries();
                }
            }
        }

        // Update mode/band/frequency and auto-clear on band or mode change
        var newMode = status.Mode ?? "";
        var newBand = FrequencyToBand(status.DialFrequency);
        var freqMhz = status.DialFrequency / 1_000_000.0;
        CurrentFrequency = freqMhz >= 1.0 ? $"{freqMhz:F3} MHz" : "";

        var bandChanged = !string.IsNullOrEmpty(CurrentBand) && !string.IsNullOrEmpty(newBand) && newBand != CurrentBand;
        var modeChanged = !string.IsNullOrEmpty(CurrentMode) && !string.IsNullOrEmpty(newMode) && newMode != CurrentMode;

        if (bandChanged || modeChanged)
        {
            Clear();
            _discardUntil = DateTime.UtcNow.AddSeconds(15);
        }

        if (!string.IsNullOrEmpty(newMode))
            CurrentMode = newMode;
        if (!string.IsNullOrEmpty(newBand))
            CurrentBand = newBand;
    }

    private static string FrequencyToBand(ulong freqHz)
    {
        var mhz = freqHz / 1_000_000.0;
        return mhz switch
        {
            >= 1.8 and < 2.0 => "160m",
            >= 3.5 and < 4.0 => "80m",
            >= 5.3 and < 5.5 => "60m",
            >= 7.0 and < 7.3 => "40m",
            >= 10.1 and < 10.15 => "30m",
            >= 14.0 and < 14.35 => "20m",
            >= 18.068 and < 18.168 => "17m",
            >= 21.0 and < 21.45 => "15m",
            >= 24.89 and < 24.99 => "12m",
            >= 28.0 and < 29.7 => "10m",
            >= 50.0 and < 54.0 => "6m",
            >= 144.0 and < 148.0 => "2m",
            >= 420.0 and < 450.0 => "70cm",
            _ => "",
        };
    }

    private void InsertSorted(CountryCardViewModel card)
    {
        var index = 0;
        while (index < Countries.Count && CompareCards(Countries[index], card) < 0)
            index++;
        Countries.Insert(index, card);
    }

    private int CompareCards(CountryCardViewModel a, CountryCardViewModel b)
    {
        if (SortOption == SortOption.Distance)
        {
            // Cards without distance always go to the end, regardless of direction
            if (!a.DistanceKm.HasValue && !b.DistanceKm.HasValue) return 0;
            if (!a.DistanceKm.HasValue) return 1;
            if (!b.DistanceKm.HasValue) return -1;
            var dCmp = a.DistanceKm.Value.CompareTo(b.DistanceKm.Value);
            return SortAscending ? dCmp : -dCmp;
        }

        var cmp = SortOption switch
        {
            SortOption.Name => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
            SortOption.Spots => a.Count.CompareTo(b.Count),
            SortOption.Snr => CompareSnr(a, b),
            _ => 0,
        };
        return SortAscending ? cmp : -cmp;
    }

    private static int CompareSnr(CountryCardViewModel a, CountryCardViewModel b)
    {
        // Cards without SNR data always go to the end
        var aHas = a.BestSnr > int.MinValue;
        var bHas = b.BestSnr > int.MinValue;
        if (!aHas && !bHas) return 0;
        if (!aHas) return 1;
        if (!bHas) return -1;
        return a.BestSnr.CompareTo(b.BestSnr);
    }

    private void ResortCountries()
    {
        var sorted = Countries.OrderBy(c => c, Comparer<CountryCardViewModel>.Create(CompareCards)).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var currentIndex = Countries.IndexOf(sorted[i]);
            if (currentIndex != i)
                Countries.Move(currentIndex, i);
        }
    }

    private bool _loadingSettings;

    private void LoadUserSettings()
    {
        try
        {
            if (!File.Exists(UserSettingsPath)) return;
            var json = File.ReadAllText(UserSettingsPath);
            var s = JsonSerializer.Deserialize<UserSettings>(json);
            if (s == null) return;
            _loadingSettings = true;
            SortOption = (SortOption)s.SortOption;
            SortAscending = s.SortAscending;
            UseMiles = s.UseMiles;
            _loadingSettings = false;
        }
        catch { _loadingSettings = false; }
    }

    private void SaveUserSettings()
    {
        if (_loadingSettings) return;
        try
        {
            var s = new UserSettings
            {
                SortOption = (int)SortOption,
                SortAscending = SortAscending,
                UseMiles = UseMiles
            };
            Directory.CreateDirectory(Path.GetDirectoryName(UserSettingsPath)!);
            File.WriteAllText(UserSettingsPath, JsonSerializer.Serialize(s));
        }
        catch { /* ignore save errors */ }
    }

    private class UserSettings
    {
        public int SortOption { get; set; }
        public bool SortAscending { get; set; } = true;
        public bool UseMiles { get; set; } = true;
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        _udp.Dispose();
        _announcer.Dispose();
        _cts?.Dispose();
    }
}
