using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using GamePriceWatch.Models;
using HtmlAgilityPack;

namespace GamePriceWatch.Services;

public class ScraperService
{
    private readonly HttpClient _httpClient;
    private const string NewReleasesUrl = "https://www.allkeyshop.com/blog/games/new-releases/";
    private const int MaxGames = 20;
    private const int MaxStoresPerGame = 5;

    public ScraperService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<List<GameInfo>> GetLatestGamesWithPricesAsync(IProgress<(int current, int total, string message)>? progress = null)
    {
        var games = new List<GameInfo>();

        progress?.Report((0, MaxGames, "Fetching new releases list..."));

        var gameLinks = await GetNewReleaseLinksAsync();

        if (gameLinks.Count == 0)
        {
            progress?.Report((0, MaxGames, "No games found. Site may be blocking requests."));
            return games;
        }

        var total = Math.Min(gameLinks.Count, MaxGames);
        progress?.Report((0, total, $"Found {gameLinks.Count} games, loading prices..."));

        for (int i = 0; i < total; i++)
        {
            var (name, url, releaseDate, platform, imageUrl, historicalLow) = gameLinks[i];
            progress?.Report((i + 1, total, $"Loading prices for {name}..."));

            try
            {
                var game = new GameInfo
                {
                    Name = name,
                    PageUrl = url,
                    ReleaseDate = releaseDate,
                    Platform = platform,
                    ImageUrl = imageUrl,
                    HistoricalLow = historicalLow
                };

                var prices = await GetGamePricesAsync(url);
                game.StorePrices = prices.Take(MaxStoresPerGame).ToList();
                games.Add(game);

                // Small delay to be polite to the server
                await Task.Delay(500);
            }
            catch (Exception)
            {
                // Skip games that fail to load
                continue;
            }
        }

        return games;
    }

    private async Task<List<(string name, string url, string releaseDate, string platform, string imageUrl, string historicalLow)>> GetNewReleaseLinksAsync()
    {
        var results = new List<(string name, string url, string releaseDate, string platform, string imageUrl, string historicalLow)>();

        try
        {
            var html = await _httpClient.GetStringAsync(NewReleasesUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // AllKeyShop uses product cards with links
            // Try multiple selectors for the game cards
            var gameCards = doc.DocumentNode.SelectNodes("//a[contains(@href, '/blog/buy-')]")
                           ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'product')]//a")
                           ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'search-results')]//a");

            if (gameCards == null) return results;

            var seenUrls = new HashSet<string>();

            foreach (var card in gameCards)
            {
                var href = card.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href) || !href.Contains("/blog/buy-")) continue;
                if (!href.Contains("compare-prices")) continue;

                // Make absolute URL
                if (!href.StartsWith("http"))
                    href = "https://www.allkeyshop.com" + href;

                if (!seenUrls.Add(href)) continue;

                // Get the game name from the link text or title
                var name = WebUtility.HtmlDecode(card.InnerText?.Trim() ?? "");
                if (string.IsNullOrEmpty(name) || name.Length > 100) continue;

                // Try to find release date, platform, and image near this card
                var parent = card.ParentNode;
                var releaseDate = ExtractNearbyText(parent, "release", "date", "20");
                var platform = ExtractPlatform(parent);
                var imageUrl = ExtractImageUrl(parent);
                var historicalLow = ExtractHistoricalLow(parent);

                results.Add((name, href, releaseDate, platform, imageUrl, historicalLow));

                if (results.Count >= MaxGames) break;
            }
        }
        catch (Exception)
        {
            // If scraping the list fails, return empty
        }

        return results;
    }

    private async Task<List<StorePrice>> GetGamePricesAsync(string gameUrl)
    {
        var prices = new List<StorePrice>();

        try
        {
            var html = await _httpClient.GetStringAsync(gameUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // AllKeyShop price tables use rows with store info, region, edition, and prices
            // Try to find table rows
            var rows = doc.DocumentNode.SelectNodes("//tr[td]")
                       ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'offers-table')]//div[contains(@class, 'row')]");

            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 4) continue;

                    var storeText = WebUtility.HtmlDecode(cells[0].InnerText?.Trim() ?? "");
                    var region = WebUtility.HtmlDecode(cells[1].InnerText?.Trim() ?? "");
                    var edition = WebUtility.HtmlDecode(cells[2].InnerText?.Trim() ?? "");

                    // Price is typically in the last cell(s)
                    var priceText = "";
                    for (int i = cells.Count - 1; i >= 3; i--)
                    {
                        priceText = WebUtility.HtmlDecode(cells[i].InnerText?.Trim() ?? "");
                        if (!string.IsNullOrEmpty(priceText) && priceText.Contains("€") || priceText.Contains("$") || priceText.Contains("£"))
                            break;
                    }

                    // Clean the store name (remove rating text)
                    var storeName = CleanStoreName(storeText);
                    var storeRating = ExtractRating(storeText);

                    if (string.IsNullOrEmpty(storeName) || string.IsNullOrEmpty(priceText)) continue;

                    var priceValue = ParsePrice(priceText);

                    prices.Add(new StorePrice
                    {
                        StoreName = storeName,
                        Region = region,
                        Edition = edition,
                        Price = priceText,
                        PriceValue = priceValue,
                        StoreRating = storeRating
                    });
                }
            }

            // Sort by price ascending
            prices = prices.Where(p => p.PriceValue > 0)
                           .OrderBy(p => p.PriceValue)
                           .ToList();

            // Remove duplicate stores, keep cheapest per store
            var uniqueStores = new List<StorePrice>();
            var seenStores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var price in prices)
            {
                if (seenStores.Add(price.StoreName))
                {
                    uniqueStores.Add(price);
                }
            }

            return uniqueStores;
        }
        catch
        {
            return prices;
        }
    }

    private string CleanStoreName(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Remove "Allkeyshop Recommends" prefix
        text = text.Replace("Allkeyshop Recommends", "").Trim();

        // Remove rating pattern like "4.14/5 (1662)"
        text = Regex.Replace(text, @"\s*\d+\.\d+/\d+\s*\(\d+\)", "").Trim();

        return text;
    }

    private string ExtractRating(string text)
    {
        var match = Regex.Match(text, @"(\d+\.\d+/\d+)\s*\((\d+)\)");
        return match.Success ? $"{match.Groups[1].Value} ({match.Groups[2].Value} reviews)" : "";
    }

    private decimal ParsePrice(string priceText)
    {
        if (string.IsNullOrEmpty(priceText)) return 0;

        // Extract numeric price value
        var match = Regex.Match(priceText, @"([\d]+[.,][\d]{2})");
        if (match.Success)
        {
            var priceStr = match.Groups[1].Value.Replace(",", ".");
            if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                return price;
        }

        return 0;
    }

    private string ExtractNearbyText(HtmlNode? node, params string[] keywords)
    {
        if (node == null) return "";
        var text = node.InnerText ?? "";

        // Try to find date patterns like "19 Mar, 2026"
        var dateMatch = Regex.Match(text, @"\d{2}\s+\w{3},?\s+\d{4}");
        if (dateMatch.Success) return dateMatch.Value;

        return "";
    }

    private string ExtractPlatform(HtmlNode? node)
    {
        if (node == null) return "PC";
        var html = node.InnerHtml ?? "";

        if (html.Contains("playstation", StringComparison.OrdinalIgnoreCase)) return "PlayStation";
        if (html.Contains("xbox", StringComparison.OrdinalIgnoreCase)) return "Xbox";
        if (html.Contains("switch", StringComparison.OrdinalIgnoreCase)) return "Nintendo Switch";
        return "PC";
    }

    private string ExtractImageUrl(HtmlNode? node)
    {
        if (node == null) return "";
        var img = node.SelectSingleNode(".//img");
        return img?.GetAttributeValue("src", img.GetAttributeValue("data-src", "")) ?? "";
    }

    private string ExtractHistoricalLow(HtmlNode? node)
    {
        if (node == null) return "";
        var text = node.InnerText ?? "";

        var match = Regex.Match(text, @"Historical Low:\s*([\d.,]+\s*[€$£])");
        if (match.Success) return match.Groups[1].Value;

        return "";
    }
}
