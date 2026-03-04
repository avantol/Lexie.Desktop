using CommunityToolkit.Mvvm.ComponentModel;

namespace Lexie.ViewModels;

public partial class FeedEntryViewModel : ObservableObject
{
    [ObservableProperty] private string _timeDisplay = "";
    [ObservableProperty] private string _callsign = "";
    [ObservableProperty] private string _country = "";
    [ObservableProperty] private string? _rawMessage;
}
