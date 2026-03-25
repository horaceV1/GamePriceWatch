using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
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
    private ObservableCollection<StorePrice> _selectedGamePrices = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingPrices;

    [ObservableProperty]
    private bool _showDetail;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _lastUpdated = "Never";

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMax = 50;

    [ObservableProperty]
    private string _selectedGameCoverUrl = "";

    [ObservableProperty]
    private string _selectedGamePriceRange = "";

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

    partial void OnSelectedGameChanged(GameInfo? value)
    {
        if (value != null)
        {
            ShowDetail = true;
            _ = LoadGamePricesAsync(value);
        }
        else
        {
            ShowDetail = false;
            SelectedGamePrices.Clear();
            SelectedGameCoverUrl = "";
            SelectedGamePriceRange = "";
        }
    }

    [RelayCommand]
    private async Task LoadGamesAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusText = "Fetching AllKeyShop TOP 50 Popular...";
        ProgressValue = 0;
        SelectedGame = null;
        ShowDetail = false;

        try
        {
            var progress = new Progress<(int current, int total, string message)>(p =>
            {
                ProgressValue = p.current;
                ProgressMax = p.total;
                StatusText = p.message;
            });

            var games = await _scraperService.GetTop50GamesAsync(progress);

            Games.Clear();
            foreach (var game in games)
            {
                Games.Add(game);
            }

            LastUpdated = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy");
            StatusText = $"Loaded {games.Count} games from TOP 50 Popular";
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

    private async Task LoadGamePricesAsync(GameInfo game)
    {
        if (game.HasLoadedPrices)
        {
            SelectedGamePrices.Clear();
            foreach (var p in game.StorePrices)
                SelectedGamePrices.Add(p);
            SelectedGameCoverUrl = game.CoverImageUrl;
            SelectedGamePriceRange = game.PriceRange;
            StatusText = $"Showing {game.StorePrices.Count} best offers for {game.Name}";
            return;
        }

        IsLoadingPrices = true;
        SelectedGamePrices.Clear();
        SelectedGameCoverUrl = "";
        SelectedGamePriceRange = "";
        StatusText = $"Loading store prices for {game.Name}...";

        try
        {
            var detail = await _scraperService.GetGameDetailAsync(game.PageUrl);
            game.StorePrices = detail.Prices;
            game.CoverImageUrl = detail.CoverImageUrl;
            game.PriceRange = detail.PriceRange;
            game.HasLoadedPrices = true;

            // Only update if this game is still selected
            if (SelectedGame == game)
            {
                foreach (var p in detail.Prices)
                    SelectedGamePrices.Add(p);
                SelectedGameCoverUrl = detail.CoverImageUrl;
                SelectedGamePriceRange = detail.PriceRange;

                if (!string.IsNullOrEmpty(detail.ErrorMessage))
                    StatusText = $"Error: {detail.ErrorMessage}";
                else
                    StatusText = $"Found {detail.Prices.Count} offers for {game.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading prices: {ex.Message}";
        }
        finally
        {
            IsLoadingPrices = false;
        }
    }

    [RelayCommand]
    private void BackToList()
    {
        SelectedGame = null;
        ShowDetail = false;
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

    [RelayCommand]
    private void CopyCoupon(string? couponCode)
    {
        if (!string.IsNullOrEmpty(couponCode))
        {
            try
            {
                Clipboard.SetText(couponCode);
                StatusText = $"Copied coupon code: {couponCode}";
            }
            catch
            {
                StatusText = "Failed to copy coupon code";
            }
        }
    }
}
