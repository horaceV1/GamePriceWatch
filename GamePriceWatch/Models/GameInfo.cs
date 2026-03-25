using System.Windows.Media.Imaging;

namespace GamePriceWatch.Models;

public class GameInfo
{
    public int Rank { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CheapestStore { get; set; } = string.Empty;
    public string CheapestPrice { get; set; } = string.Empty;
    public decimal CheapestPriceValue { get; set; }
    public string PageUrl { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Platform { get; set; } = "PC";
    public BitmapSource? ThumbnailImage { get; set; }
    public List<StorePrice> StorePrices { get; set; } = new();
    public bool IsLoadingPrices { get; set; }
    public bool HasLoadedPrices { get; set; }
    public string CoverImageUrl { get; set; } = string.Empty;
    public string PriceRange { get; set; } = string.Empty;
}

public class StorePrice
{
    public string StoreName { get; set; } = string.Empty;
    public string StoreRating { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string OriginalPrice { get; set; } = string.Empty;
    public string FinalPrice { get; set; } = string.Empty;
    public decimal FinalPriceValue { get; set; }
    public string CouponCode { get; set; } = string.Empty;
    public string CouponDiscount { get; set; } = string.Empty;
    public bool IsOfficial { get; set; }
    public bool IsPreRendered { get; set; }
}
