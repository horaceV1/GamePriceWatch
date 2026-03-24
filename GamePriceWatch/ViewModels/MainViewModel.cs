using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamePriceWatch.Models;
using GamePriceWatch.Services;

namespace GamePriceWatch.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ScraperService _scraperService;
    private readonly DispatcherTimer _autoRefreshTimer;

    [ObservableProperty]
    private ObservableCollection<GameInfo> _games = new();

    [ObservableProperty]
    private GameInfo? _selectedGame;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _lastUpdated = "Never";

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMax = 20;

    public MainViewModel()
    {
        _scraperService = new ScraperService();

        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromHours(1)
        };
        _autoRefreshTimer.Tick += async (s, e) => await LoadGamesAsync();
        _autoRefreshTimer.Start();
    }

    [RelayCommand]
    private async Task LoadGamesAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusText = "Fetching latest game releases...";
        ProgressValue = 0;

        try
        {
            var progress = new Progress<(int current, int total, string message)>(p =>
            {
                ProgressValue = p.current;
                ProgressMax = p.total;
                StatusText = p.message;
            });

            var games = await _scraperService.GetLatestGamesWithPricesAsync(progress);

            Games.Clear();
            foreach (var game in games)
            {
                Games.Add(game);
            }

            LastUpdated = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy");
            StatusText = $"Loaded {games.Count} games successfully";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenGameUrl()
    {
        if (SelectedGame?.PageUrl is { Length: > 0 } url)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }
    }
}
