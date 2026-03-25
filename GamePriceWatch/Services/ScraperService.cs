using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamePriceWatch.Models;
using HtmlAgilityPack;

namespace GamePriceWatch.Services;

public class ScraperService
{
    private readonly HttpClient _httpClient;
    private const string MainPageUrl = "https://www.allkeyshop.com/blog/";
    private const int MaxGames = 50;
    private const int MaxStoresPerGame = 5;

    public ScraperService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Fetches the TOP 50 popular games from AllKeyShop main page using embedded preloadData JSON.
    /// Also downloads the sprite image and crops individual game thumbnails.
    /// </summary>
    public async Task<List<GameInfo>> GetTop50GamesAsync(IProgress<(int current, int total, string message)>? progress = null)
    {
        var games = new List<GameInfo>();
        progress?.Report((0, MaxGames, "Fetching AllKeyShop TOP 50..."));

        try
        {
            var html = await _httpClient.GetStringAsync(MainPageUrl);
            progress?.Report((1, MaxGames, "Parsing embedded data..."));

            // Extract the preloadData JSON from the page
            var preloadData = ExtractPreloadData(html);
            if (preloadData == null)
            {
                progress?.Report((0, MaxGames, "Could not find preloadData in page"));
                return games;
            }

            // Get sidebar -> all.popular items
            int listId = 0;
            int spriteVersion = 0;
            JsonElement sidebarItems = default;

            if (preloadData.Value.TryGetProperty("sidebar", out var sidebar))
            {
                if (sidebar.TryGetProperty("all.popular", out var allPopular))
                {
                    if (allPopular.TryGetProperty("items", out sidebarItems))
                    {
                        // Get sprite info
                        if (allPopular.TryGetProperty("data", out var data))
                        {
                            if (data.TryGetProperty("listId", out var lid))
                                listId = lid.GetInt32();
                            if (data.TryGetProperty("spriteVersion", out var sv))
                                spriteVersion = sv.GetInt32();
                        }
                    }
                }
            }

            if (sidebarItems.ValueKind != JsonValueKind.Array || sidebarItems.GetArrayLength() == 0)
            {
                progress?.Report((0, MaxGames, "No popular games found in preloadData"));
                return games;
            }

            // Parse game items
            int total = Math.Min(sidebarItems.GetArrayLength(), MaxGames);
            progress?.Report((2, total, $"Found {sidebarItems.GetArrayLength()} popular games, loading sprites..."));

            // Download and crop sprite image for thumbnails
            Dictionary<int, BitmapSource>? croppedImages = null;
            if (listId > 0 && spriteVersion > 0)
            {
                croppedImages = await DownloadAndCropSpriteAsync(listId, spriteVersion, total);
            }

            int rank = 0;
            foreach (var item in sidebarItems.EnumerateArray())
            {
                if (rank >= MaxGames) break;

                var game = ParsePreloadItem(item, rank + 1);
                if (game != null)
                {
                    // Assign cropped sprite image
                    if (croppedImages != null && croppedImages.TryGetValue(rank, out var img))
                    {
                        game.ThumbnailImage = img;
                    }

                    games.Add(game);
                    rank++;
                    progress?.Report((rank, total, $"Loaded {rank}/{total} games..."));
                }
            }

            progress?.Report((games.Count, total, $"Loaded {games.Count} games from TOP 50 Popular"));
        }
        catch (Exception ex)
        {
            progress?.Report((0, MaxGames, $"Error fetching TOP 50: {ex.Message}"));
        }

        return games;
    }

    /// <summary>
    /// Extracts the preloadData JSON object from the page HTML.
    /// It's embedded in a JS variable: var topclickTrans = {...,"preloadData":{...},...};
    /// </summary>
    private JsonElement? ExtractPreloadData(string html)
    {
        var match = Regex.Match(html, @"""preloadData""\s*:\s*\{", RegexOptions.Singleline);
        if (!match.Success) return null;

        int jsonStart = match.Index + match.Value.IndexOf('{');
        int braceCount = 0;
        bool inString = false;
        bool escape = false;

        for (int i = jsonStart; i < html.Length; i++)
        {
            char c = html[i];
            if (escape) { escape = false; continue; }
            if (c == '\\' && inString) { escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (c == '{') braceCount++;
            else if (c == '}')
            {
                braceCount--;
                if (braceCount == 0)
                {
                    var jsonStr = html[jsonStart..(i + 1)];
                    try
                    {
                        return JsonDocument.Parse(jsonStr).RootElement;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Parses a single game item from the preloadData JSON.
    /// </summary>
    private GameInfo? ParsePreloadItem(JsonElement item, int rank)
    {
        var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(name)) return null;

        var merchant = item.TryGetProperty("merchant", out var m) ? m.GetString() ?? "" : "";
        var url = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
        var currency = item.TryGetProperty("currency", out var c) ? c.GetString() ?? "eur" : "eur";
        var platform = item.TryGetProperty("platform", out var pl) ? pl.GetString() ?? "pc" : "pc";

        decimal price = 0;
        string priceDisplay = "--€";
        if (item.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number)
        {
            price = p.GetDecimal();
            priceDisplay = FormatPrice(price, currency);
        }

        return new GameInfo
        {
            Rank = rank,
            Name = name,
            CheapestStore = merchant,
            CheapestPrice = priceDisplay,
            CheapestPriceValue = price,
            PageUrl = url,
            Platform = platform.ToUpperInvariant()
        };
    }

    /// <summary>
    /// Downloads the sprite image for a topclick list and crops individual game thumbnails.
    /// Each sprite element is 149x70 pixels, arranged horizontally.
    /// </summary>
    private async Task<Dictionary<int, BitmapSource>?> DownloadAndCropSpriteAsync(int listId, int spriteVersion, int count)
    {
        try
        {
            var spriteUrl = $"https://cdn.allkeyshop.com/topclick/lists/{listId}/sprite.webp?version={spriteVersion}";
            var spriteBytes = await _httpClient.GetByteArrayAsync(spriteUrl);

            var result = new Dictionary<int, BitmapSource>();

            // Decode on the UI thread since BitmapSource requires STA
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                using var ms = new MemoryStream(spriteBytes);
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var sprite = decoder.Frames[0];

                const int thumbWidth = 149;
                const int thumbHeight = 70;

                for (int i = 0; i < count && (i * thumbWidth) < sprite.PixelWidth; i++)
                {
                    try
                    {
                        var cropped = new CroppedBitmap(sprite, new System.Windows.Int32Rect(
                            i * thumbWidth, 0, thumbWidth, Math.Min(thumbHeight, sprite.PixelHeight)));
                        cropped.Freeze();
                        result[i] = cropped;
                    }
                    catch { /* Skip this thumbnail */ }
                }
            });

            return result.Count > 0 ? result : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fetches detailed store offers from a game's individual page.
    /// Extracts pre-rendered recommended offers and gamePageTrans data.
    /// </summary>
    public async Task<GameDetailResult> GetGameDetailAsync(string gameUrl)
    {
        var result = new GameDetailResult();

        try
        {
            var html = await _httpClient.GetStringAsync(gameUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 1. Extract cover image from LD+JSON or OG tag
            result.CoverImageUrl = ExtractCoverImage(html, doc);

            // 2. Extract pre-rendered recommended offers (these have real prices)
            result.Prices = ExtractRecommendedOffers(html, doc);

            // 3. Extract gamePageTrans for merchant info and offer structure
            var gamePageData = ExtractGamePageTrans(html);
            if (gamePageData != null)
            {
                // Get all merchants info
                var merchants = ExtractMerchants(gamePageData.Value);

                // Get editions & regions
                var editions = ExtractLookup(gamePageData.Value, "editions");
                var regions = ExtractLookup(gamePageData.Value, "regions");

                // Parse all offers from prices array
                // Note: price=0.02 is a sentinel meaning "not available" (used for official/first-party stores).
                // Third-party stores have real prices in the JSON.
                if (gamePageData.Value.TryGetProperty("prices", out var pricesArray))
                {
                    foreach (var offer in pricesArray.EnumerateArray())
                    {
                        var merchantId = offer.TryGetProperty("merchant", out var mid) ? mid.ToString() : "";
                        var merchantName = offer.TryGetProperty("merchantName", out var mn) ? mn.GetString() ?? "" : "";
                        var editionId = offer.TryGetProperty("edition", out var eid) ? eid.GetString() ?? "" : "";
                        var regionId = offer.TryGetProperty("region", out var rid) ? rid.GetString() ?? "" : "";
                        var voucherCode = offer.TryGetProperty("voucher_code", out var vc) && vc.ValueKind == JsonValueKind.String ? vc.GetString() ?? "" : "";
                        var voucherValue = offer.TryGetProperty("voucher_discount_value", out var vv) && vv.ValueKind == JsonValueKind.Number ? vv.GetInt32().ToString() : "";
                        var voucherType = offer.TryGetProperty("voucher_discount_type", out var vt) && vt.ValueKind == JsonValueKind.String ? vt.GetString() ?? "" : "";
                        var isOfficial = offer.TryGetProperty("isOfficial", out var io) && io.GetBoolean();
                        var isAccount = offer.TryGetProperty("account", out var acc) && acc.GetBoolean();
                        var activationPlatform = offer.TryGetProperty("activationPlatform", out var ap) ? ap.GetString() ?? "" : "";

                        // Only include Steam key offers (exclude accounts and non-Steam platforms)
                        if (isAccount) continue;
                        if (!string.IsNullOrEmpty(activationPlatform) &&
                            !activationPlatform.Equals("steam", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Extract real prices — 0.02 is sentinel for "not available"
                        // Use fee-inclusive prices (pricePaypal/priceCard) for accurate totals
                        decimal rawPrice = offer.TryGetProperty("price", out var rp) && rp.ValueKind == JsonValueKind.Number ? rp.GetDecimal() : 0;
                        decimal originalPrice = offer.TryGetProperty("originalPrice", out var op) && op.ValueKind == JsonValueKind.Number ? op.GetDecimal() : 0;
                        decimal pricePaypal = offer.TryGetProperty("pricePaypal", out var pp) && pp.ValueKind == JsonValueKind.Number ? pp.GetDecimal() : 0;
                        decimal priceCard = offer.TryGetProperty("priceCard", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetDecimal() : 0;
                        bool isPriceSentinel = rawPrice <= 0.02m;

                        // Pick the lowest fee-inclusive price between PayPal and Card
                        decimal bestPriceWithFees = rawPrice;
                        if (!isPriceSentinel)
                        {
                            var candidates = new List<decimal>();
                            if (pricePaypal > 0.02m) candidates.Add(pricePaypal);
                            if (priceCard > 0.02m) candidates.Add(priceCard);
                            if (candidates.Count > 0)
                                bestPriceWithFees = candidates.Min();
                            else
                                bestPriceWithFees = rawPrice;
                        }

                        var editionName = editions.GetValueOrDefault(editionId, "Standard");
                        var regionName = regions.GetValueOrDefault(regionId, "");
                        if (regionName.Length > 20)
                            regionName = regionName.Split('.')[0].Trim();

                        var merchantInfo = merchants.GetValueOrDefault(merchantId);
                        var rating = merchantInfo?.Rating ?? "";

                        // Build price display (using fee-inclusive price)
                        decimal finalPriceValue = isPriceSentinel ? 0 : bestPriceWithFees;
                        string finalPriceDisplay = isPriceSentinel ? "" : FormatPrice(bestPriceWithFees, "eur");
                        string originalPriceDisplay = "";
                        if (!isPriceSentinel && originalPrice > bestPriceWithFees + 0.01m)
                            originalPriceDisplay = FormatPrice(originalPrice, "eur");

                        var couponDisplay = "";
                        if (!string.IsNullOrEmpty(voucherCode))
                            couponDisplay = $"-{voucherValue}{voucherType}";

                        // Build a unique key: store + edition + region to avoid true duplicates
                        // but allow the same store with different editions/regions
                        var offerKey = $"{merchantName}|{editionId}|{regionId}".ToLowerInvariant();

                        // Skip if this exact offer (same store+edition+region) is already in pre-rendered list
                        if (!result.Prices.Any(p => p.StoreName.Equals(merchantName, StringComparison.OrdinalIgnoreCase)
                            && p.Edition == editionName && p.Region == regionName))
                        {
                            result.Prices.Add(new StorePrice
                            {
                                StoreName = merchantName,
                                StoreRating = rating,
                                Region = regionName,
                                Edition = editionName,
                                IsOfficial = isOfficial,
                                CouponCode = voucherCode,
                                CouponDiscount = couponDisplay,
                                OriginalPrice = originalPriceDisplay,
                                FinalPrice = finalPriceDisplay,
                                FinalPriceValue = finalPriceValue
                            });
                        }
                    }
                }
            }

            // 4. Extract price range from LD+JSON
            result.PriceRange = ExtractPriceRange(html);

            // Sort all offers by price (cheapest first), sentinel prices go to the end
            var withPrice = result.Prices
                .Where(p => p.FinalPriceValue > 0)
                .OrderBy(p => p.FinalPriceValue)
                .ToList();
            var withoutPrice = result.Prices
                .Where(p => p.FinalPriceValue <= 0)
                .ToList();

            // Keep the cheapest offer per unique store name
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var final = new List<StorePrice>();
            foreach (var p in withPrice.Concat(withoutPrice))
            {
                if (seen.Add(p.StoreName))
                    final.Add(p);
            }

            result.Prices = final.Take(MaxStoresPerGame).ToList();
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Extracts pre-rendered recommended offers from the game page HTML.
    /// These are the 1-2 offers shown before JavaScript loads (official + AKS recommends).
    /// </summary>
    private List<StorePrice> ExtractRecommendedOffers(string html, HtmlDocument doc)
    {
        var offers = new List<StorePrice>();

        var offerLinks = doc.DocumentNode.SelectNodes("//a[contains(@class, 'recomended_offers') or contains(@class, 'official_offer')]");
        if (offerLinks == null)
        {
            // Try alternative selectors
            offerLinks = doc.DocumentNode.SelectNodes("//a[.//span[contains(@class, 'offers-merchant-name')]]");
        }

        if (offerLinks != null)
        {
            foreach (var link in offerLinks)
            {
                var merchantNameNode = link.SelectSingleNode(".//span[contains(@class, 'offers-merchant-name')]");
                var priceNode = link.SelectSingleNode(".//div[contains(@class, 'offers-price')]");
                var titleNode = link.SelectSingleNode(".//div[contains(@class, 'recomended-title')]");

                var merchantName = WebUtility.HtmlDecode(merchantNameNode?.InnerText?.Trim() ?? "");
                var priceText = WebUtility.HtmlDecode(priceNode?.InnerText?.Trim() ?? "");
                var title = WebUtility.HtmlDecode(titleNode?.InnerText?.Trim() ?? "");

                if (string.IsNullOrEmpty(merchantName)) continue;

                // Clean price text (remove SVG content)
                priceText = Regex.Replace(priceText, @"\s+", " ").Trim();

                // Extract numeric price
                var priceMatch = Regex.Match(priceText, @"([\d]+[.,]\d{2})\s*([€$£])");
                decimal priceValue = 0;
                string priceDisplay = priceText;
                if (priceMatch.Success)
                {
                    priceValue = ParsePrice(priceMatch.Groups[1].Value);
                    priceDisplay = $"{priceMatch.Groups[1].Value}{priceMatch.Groups[2].Value}";
                }

                // Extract edition and region from the offer
                var editionRegionNodes = link.SelectNodes(".//div[contains(@class, 'offers-merchant-container')]//span");
                string edition = "", region = "";
                if (editionRegionNodes != null && editionRegionNodes.Count >= 3)
                {
                    // Typically: [merchant-icon, merchant-name, edition, region]
                    var texts = editionRegionNodes.Select(n => WebUtility.HtmlDecode(n.InnerText?.Trim() ?? ""))
                                                  .Where(t => !string.IsNullOrEmpty(t) && t != merchantName)
                                                  .ToList();
                    if (texts.Count >= 2) { edition = texts[0]; region = texts[1]; }
                    else if (texts.Count == 1) { edition = texts[0]; }
                }

                // Extract coupon info
                string couponCode = "", couponDiscount = "";
                var couponArea = link.SelectSingleNode(".//*[contains(@class, 'coupon-area')]");
                if (couponArea != null)
                {
                    var code = couponArea.GetAttributeValue("data-code", "");
                    var value = couponArea.GetAttributeValue("data-value", "");
                    var type = couponArea.GetAttributeValue("data-type", "");
                    if (!string.IsNullOrEmpty(code))
                    {
                        couponCode = code;
                        couponDiscount = $"-{value}{type}";
                    }
                }

                bool isOfficial = title.Contains("OFFICIAL", StringComparison.OrdinalIgnoreCase);

                offers.Add(new StorePrice
                {
                    StoreName = merchantName,
                    FinalPrice = priceDisplay,
                    FinalPriceValue = priceValue,
                    Edition = edition,
                    Region = region,
                    IsOfficial = isOfficial,
                    CouponCode = couponCode,
                    CouponDiscount = couponDiscount,
                    IsPreRendered = true
                });
            }
        }

        return offers;
    }

    /// <summary>
    /// Extracts gamePageTrans JSON from the page HTML.
    /// </summary>
    private JsonElement? ExtractGamePageTrans(string html)
    {
        var match = Regex.Match(html, @"gamePageTrans\s*=\s*\{", RegexOptions.Singleline);
        if (!match.Success) return null;

        int jsonStart = match.Index + match.Value.IndexOf('{');
        int braceCount = 0;
        bool inString = false;
        bool escape = false;

        for (int i = jsonStart; i < html.Length; i++)
        {
            char c = html[i];
            if (escape) { escape = false; continue; }
            if (c == '\\' && inString) { escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (c == '{') braceCount++;
            else if (c == '}')
            {
                braceCount--;
                if (braceCount == 0)
                {
                    var jsonStr = html[jsonStart..(i + 1)];
                    try
                    {
                        return JsonDocument.Parse(jsonStr).RootElement;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts merchant info from gamePageTrans.
    /// </summary>
    private Dictionary<string, MerchantInfo> ExtractMerchants(JsonElement gamePageTrans)
    {
        var merchants = new Dictionary<string, MerchantInfo>();
        if (!gamePageTrans.TryGetProperty("merchants", out var merchantsObj)) return merchants;

        foreach (var prop in merchantsObj.EnumerateObject())
        {
            var id = prop.Name;
            var val = prop.Value;
            var name = val.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var rating = "";
            if (val.TryGetProperty("rating", out var r))
            {
                var score = r.TryGetProperty("score", out var s) ? s.GetDouble() : 0;
                var count = r.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
                var max = r.TryGetProperty("maximum", out var mx) ? mx.GetInt32() : 5;
                if (score > 0)
                    rating = $"⭐ {score:F2}/{max} ({count})";
            }

            merchants[id] = new MerchantInfo { Name = name, Rating = rating };
        }
        return merchants;
    }

    /// <summary>
    /// Extracts a lookup dictionary (editions or regions) from gamePageTrans.
    /// </summary>
    private Dictionary<string, string> ExtractLookup(JsonElement gamePageTrans, string key)
    {
        var lookup = new Dictionary<string, string>();
        if (!gamePageTrans.TryGetProperty(key, out var obj)) return lookup;

        foreach (var prop in obj.EnumerateObject())
        {
            var id = prop.Name;
            if (key == "regions")
            {
                var filterName = prop.Value.TryGetProperty("filter_name", out var fn) ? fn.GetString() ?? "" : "";
                var regionName = prop.Value.TryGetProperty("region_name", out var rn) ? rn.GetString() ?? "" : "";
                lookup[id] = !string.IsNullOrEmpty(filterName) ? filterName : regionName;
            }
            else
            {
                var name = prop.Value.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                lookup[id] = name;
            }
        }
        return lookup;
    }

    /// <summary>
    /// Extracts the cover image URL from LD+JSON or OG meta tag.
    /// </summary>
    private string ExtractCoverImage(string html, HtmlDocument doc)
    {
        // Try LD+JSON first — look for Product schema with image
        var ldJsonMatches = Regex.Matches(html, @"<script\s+type=""application/ld\+json""[^>]*>(.*?)</script>",
            RegexOptions.Singleline);
        foreach (Match m in ldJsonMatches)
        {
            try
            {
                var json = JsonDocument.Parse(m.Groups[1].Value).RootElement;
                // Check for Product type with image
                if (json.TryGetProperty("@type", out var type) && type.GetString() == "Product")
                {
                    if (json.TryGetProperty("image", out var img))
                        return img.GetString() ?? "";
                }
            }
            catch { /* Not valid JSON or wrong structure */ }
        }

        // Fallback: OG image
        var ogImage = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
        if (ogImage != null)
            return ogImage.GetAttributeValue("content", "");

        return "";
    }

    /// <summary>
    /// Extracts price range from LD+JSON AggregateOffer.
    /// </summary>
    private string ExtractPriceRange(string html)
    {
        var ldJsonMatches = Regex.Matches(html, @"<script\s+type=""application/ld\+json""[^>]*>(.*?)</script>",
            RegexOptions.Singleline);
        foreach (Match m in ldJsonMatches)
        {
            try
            {
                var json = JsonDocument.Parse(m.Groups[1].Value).RootElement;
                if (json.TryGetProperty("@type", out var type) && type.GetString() == "Product")
                {
                    if (json.TryGetProperty("offers", out var offers))
                    {
                        var low = offers.TryGetProperty("lowPrice", out var lp) ? lp.GetDecimal() : 0;
                        var high = offers.TryGetProperty("highPrice", out var hp) ? hp.GetDecimal() : 0;
                        var currency = offers.TryGetProperty("priceCurrency", out var pc) ? pc.GetString() ?? "EUR" : "EUR";
                        var count = offers.TryGetProperty("offerCount", out var oc) ? oc.GetInt32() : 0;
                        if (low > 0)
                            return $"{FormatPrice(low, currency)} - {FormatPrice(high, currency)} ({count} offers)";
                    }
                }
            }
            catch { }
        }
        return "";
    }

    private string FormatPrice(decimal price, string currency)
    {
        var symbol = currency.ToLower() switch
        {
            "eur" => "€",
            "usd" => "$",
            "gbp" => "£",
            _ => "€"
        };
        return $"{price:F2}{symbol}";
    }

    private decimal ParsePrice(string priceText)
    {
        if (string.IsNullOrEmpty(priceText)) return 0;
        var normalized = priceText.Replace(",", ".");
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            return price;
        return 0;
    }

    private class MerchantInfo
    {
        public string Name { get; set; } = "";
        public string Rating { get; set; } = "";
    }
}

public class GameDetailResult
{
    public List<StorePrice> Prices { get; set; } = new();
    public string CoverImageUrl { get; set; } = "";
    public string PriceRange { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}
