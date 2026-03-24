namespace GamePriceWatch.Models;

public class GameInfo
{
    public string Name { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string PageUrl { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string HistoricalLow { get; set; } = string.Empty;
    public List<StorePrice> StorePrices { get; set; } = new();
}

public class StorePrice
{
    public string StoreName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public decimal PriceValue { get; set; }
    public string Discount { get; set; } = string.Empty;
    public string StoreRating { get; set; } = string.Empty;
}
