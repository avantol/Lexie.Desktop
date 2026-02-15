using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lexie.Models;
using Lexie.Services;

namespace Lexie.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly WsjtxParser _parser;
    private readonly DxccLookupService _dxcc;
    private readonly CallsignExtractor _extractor;
    private readonly WsjtxUdpService _udp;
    private readonly AnnouncementService _announcer;

    private readonly Dictionary<string, CountryCardViewModel> _countryMap = new();
    private readonly HashSet<string> _allCallsigns = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<CountryCardViewModel> Countries { get; } = [];
    public ObservableCollection<FeedEntryViewModel> FeedEntries { get; } = [];

    [ObservableProperty] private int _countryCount;
    [ObservableProperty] private int _callsignCount;
    [ObservableProperty] private int _messageCount;
    [ObservableProperty] private VoiceMode _voiceMode = VoiceMode.Off;
    [ObservableProperty] private string _statusText = "Starting...";
    [ObservableProperty] private bool _isConnected;

    public MainWindowViewModel(WsjtxParser parser, DxccLookupService dxcc,
        CallsignExtractor extractor, WsjtxUdpService udp, AnnouncementService announcer)
    {
        _parser = parser;
        _dxcc = dxcc;
        _extractor = extractor;
        _udp = udp;
        _announcer = announcer;
    }

    [RelayCommand]
    private void TestVoice()
    {
        _announcer.Test(VoiceMode);
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();

        // Start UDP listener on background thread
        _ = Task.Run(() => _udp.StartAsync(2237, _cts.Token), _cts.Token);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusText = "Listening on UDP 2237";
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

        switch (message)
        {
            case DecodeMessage decode:
                messageText = decode.Message;
                callsigns = _extractor.Extract(decode.Message);
                break;
            case StatusMessage status when !string.IsNullOrEmpty(status.DxCall):
                callsigns.Add(status.DxCall);
                break;
            case QsoLoggedMessage qso when !string.IsNullOrEmpty(qso.DxCall):
                callsigns.Add(qso.DxCall);
                break;
            case WsprDecodeMessage wspr when !string.IsNullOrEmpty(wspr.Callsign):
                callsigns.Add(wspr.Callsign);
                break;
        }

        if (callsigns.Count == 0) return;

        MessageCount++;

        foreach (var call in callsigns)
        {
            var country = _dxcc.Lookup(call);
            if (country == null) continue;

            _allCallsigns.Add(call);
            var isNewCountry = !_countryMap.ContainsKey(country);

            if (_countryMap.TryGetValue(country, out var card))
            {
                card.AddCallsign(call);
                card.Highlight();
            }
            else
            {
                card = new CountryCardViewModel { Name = country };
                card.AddCallsign(call);
                card.Highlight();
                _countryMap[country] = card;

                // Insert alphabetically
                var index = 0;
                while (index < Countries.Count &&
                       string.Compare(Countries[index].Name, country, StringComparison.Ordinal) < 0)
                    index++;
                Countries.Insert(index, card);
            }

            // Add feed entry
            FeedEntries.Insert(0, new FeedEntryViewModel
            {
                TimeDisplay = DateTime.Now.ToString("HH:mm:ss"),
                Callsign = call,
                Country = country,
                RawMessage = messageText,
            });

            // Cap feed at 200
            while (FeedEntries.Count > 200)
                FeedEntries.RemoveAt(FeedEntries.Count - 1);

            // Announce new countries
            if (isNewCountry)
                _announcer.Announce(country, call, VoiceMode);
        }

        CountryCount = _countryMap.Count;
        CallsignCount = _allCallsigns.Count;
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        _udp.Dispose();
        _announcer.Dispose();
        _cts?.Dispose();
    }
}
