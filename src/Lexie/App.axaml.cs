using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lexie.Services;
using Lexie.ViewModels;
using Lexie.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Lexie;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<WsjtxParser>();
        services.AddSingleton<DxccLookupService>();
        services.AddSingleton<CallsignExtractor>();
        services.AddSingleton<WsjtxUdpService>();
        services.AddSingleton<SpeechService>();
        services.AddSingleton<NvdaService>();
        services.AddSingleton<AnnouncementService>();
        services.AddSingleton<MainWindowViewModel>();
        _services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = _services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;

            // Start listening after window is shown
            window.Opened += async (_, _) =>
            {
                try
                {
                    await vm.StartAsync();
                }
                catch (Exception ex)
                {
                    vm.StatusText = $"Error: {ex.Message}";
                }
            };

            desktop.ShutdownRequested += (_, _) =>
            {
                vm.Dispose();
                _services.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
