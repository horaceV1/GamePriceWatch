using System.Net;
using System.Net.Http;

var handler = new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
};
var http = new HttpClient(handler);
http.DefaultRequestHeaders.Add("User-Agent",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
http.DefaultRequestHeaders.Add("Accept",
    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");

// Test 1: Main page
Console.WriteLine("=== Fetching main page ===");
var html = await http.GetStringAsync("https://www.allkeyshop.com/blog/");
File.WriteAllText("debug_main.html", html);
Console.WriteLine($"Main page HTML length: {html.Length}");
Console.WriteLine($"Contains 'topclick': {html.Contains("topclick")}");
Console.WriteLine($"Contains 'gpu-widget': {html.Contains("gpu-widget")}");
Console.WriteLine($"Contains 'top-50': {html.Contains("top-50")}");
Console.WriteLine($"Contains 'popular': {html.Contains("popular", StringComparison.OrdinalIgnoreCase)}");
Console.WriteLine($"Contains 'data-slug': {html.Contains("data-slug")}");
Console.WriteLine($"Contains 'buy-crimson': {html.Contains("buy-crimson")}");
Console.WriteLine($"Contains 'compare-prices': {html.Contains("compare-prices")}");

// Snippets around 'popular'
var idx = html.IndexOf("POPULAR", StringComparison.OrdinalIgnoreCase);
if (idx >= 0)
{
    var start = Math.Max(0, idx - 200);
    var end = Math.Min(html.Length, idx + 500);
    Console.WriteLine($"\n=== Around 'POPULAR' (idx={idx}) ===");
    Console.WriteLine(html[start..end]);
}

// Check for gpu-widget
idx = html.IndexOf("gpu-widget", StringComparison.OrdinalIgnoreCase);
if (idx >= 0)
{
    var start = Math.Max(0, idx - 100);
    var end = Math.Min(html.Length, idx + 500);
    Console.WriteLine($"\n=== Around 'gpu-widget' (idx={idx}) ===");
    Console.WriteLine(html[start..end]);
}

// Test 2: Game page
Console.WriteLine("\n=== Fetching Crimson Desert page ===");
var html2 = await http.GetStringAsync("https://www.allkeyshop.com/blog/buy-crimson-desert-cd-key-compare-prices/");
File.WriteAllText("debug_game.html", html2);
Console.WriteLine($"Game page HTML length: {html2.Length}");
Console.WriteLine($"Contains 'offers': {html2.Contains("offers")}");
Console.WriteLine($"Contains '<tr': {html2.Contains("<tr")}");
Console.WriteLine($"Contains '<td': {html2.Contains("<td")}");
Console.WriteLine($"Contains 'GAMIVO': {html2.Contains("GAMIVO")}");
Console.WriteLine($"Contains 'Driffle': {html2.Contains("Driffle")}");
Console.WriteLine($"Contains 'Eneba': {html2.Contains("Eneba")}");

// Check for offers div
idx = html2.IndexOf("offers", StringComparison.OrdinalIgnoreCase);
if (idx >= 0)
{
    var start = Math.Max(0, idx - 100);
    var end = Math.Min(html2.Length, idx + 1000);
    Console.WriteLine($"\n=== Around 'offers' (idx={idx}) ===");
    Console.WriteLine(html2[start..end]);
}
