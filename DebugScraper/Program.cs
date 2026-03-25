using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== AllKeyShop Price Investigation ===");

var handler = new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
};
var client = new HttpClient(handler);
client.DefaultRequestHeaders.Add("User-Agent",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

var gameUrl = "https://www.allkeyshop.com/blog/buy-gta-5-cd-key-compare-prices/";
Console.WriteLine($"Fetching {gameUrl}...");
var gameHtml = await client.GetStringAsync(gameUrl);
Console.WriteLine($"HTML length: {gameHtml.Length}");

// Find all external JS files
Console.WriteLine("\n--- External JS files ---");
var jsFileMatches = Regex.Matches(gameHtml, @"<script[^>]+src=""([^""]+\.js[^""]*)""\s*/?>", RegexOptions.IgnoreCase);
foreach (Match jsm in jsFileMatches)
    Console.WriteLine($"  {jsm.Groups[1].Value}");

// Search JS files for price math
Console.WriteLine("\n--- Searching JS for price deobfuscation ---");
foreach (Match jsm in jsFileMatches)
{
    var jsUrl = jsm.Groups[1].Value;
    if (!jsUrl.StartsWith("http"))
        jsUrl = "https://www.allkeyshop.com" + jsUrl;
    try
    {
        var jsContent = await client.GetStringAsync(jsUrl);
        bool hasPriceCalc = Regex.IsMatch(jsContent, @"price\s*[\*\/]\s*\d|basePriceFactor|priceFactor", RegexOptions.IgnoreCase);
        bool has002 = jsContent.Contains("0.02");
        if (hasPriceCalc || has002)
        {
            Console.WriteLine($"\n  ** FOUND in: {jsm.Groups[1].Value} (len={jsContent.Length})");
            var matches = Regex.Matches(jsContent, @"(?:price\s*[\*\/=]|\.price\s*[\*\/]|basePriceFactor|priceFactor|0\.02)[^\n]{0,200}", RegexOptions.IgnoreCase);
            var shown = new HashSet<string>();
            foreach (Match m in matches)
            {
                var val = m.Value.Trim();
                if (val.Length > 10 && shown.Count < 20 && shown.Add(val.Substring(0, Math.Min(50, val.Length))))
                    Console.WriteLine($"    {val.Substring(0, Math.Min(250, val.Length))}");
            }
        }
    }
    catch { }
}

// Data attributes with price info
Console.WriteLine("\n--- Data attributes with price info ---");
var dataAttrMatches = Regex.Matches(gameHtml, @"data-(?:price|factor|seed|key|multi|base)[^=]*=""([^""]+)""", RegexOptions.IgnoreCase);
foreach (Match dam in dataAttrMatches)
    Console.WriteLine($"  {dam.Value}");

// Global variables related to prices
Console.WriteLine("\n--- Global variables related to prices ---");
var windowVarMatches = Regex.Matches(gameHtml, @"(?:window\.|var\s+|let\s+|const\s+)(\w*(?:price|factor|seed|multi|base)\w*)\s*=\s*([^;\n]{1,100})", RegexOptions.IgnoreCase);
foreach (Match wvm in windowVarMatches)
    Console.WriteLine($"  {wvm.Value.Trim()}");

// gamePageTrans price entries
Console.WriteLine("\n--- gamePageTrans first 3 price entries ---");
var gptMatch = Regex.Match(gameHtml, @"gamePageTrans\s*=\s*\{", RegexOptions.Singleline);
if (gptMatch.Success)
{
    int gptStart = gptMatch.Index + gptMatch.Value.IndexOf('{');
    int braceCount = 0; bool inString = false; bool escape = false; int jsonEnd = -1;
    for (int i = gptStart; i < gameHtml.Length; i++)
    {
        char c = gameHtml[i];
        if (escape) { escape = false; continue; }
        if (c == '\\' && inString) { escape = true; continue; }
        if (c == '"') { inString = !inString; continue; }
        if (inString) continue;
        if (c == '{') braceCount++;
        else if (c == '}') { braceCount--; if (braceCount == 0) { jsonEnd = i; break; } }
    }
    var gptJson = JsonDocument.Parse(gameHtml.Substring(gptStart, jsonEnd + 1 - gptStart)).RootElement;
    if (gptJson.TryGetProperty("prices", out var prices))
    {
        Console.WriteLine($"Total: {prices.GetArrayLength()} entries");
        int idx = 0;
        foreach (var pe in prices.EnumerateArray())
        {
            if (idx >= 3) break;
            Console.WriteLine($"\n  [{idx}]:");
            foreach (var prop in pe.EnumerateObject())
                Console.WriteLine($"    {prop.Name}: {prop.Value}");
            idx++;
        }
    }
}

// topclickTrans
Console.WriteLine("\n--- topclickTrans keys ---");
var tcMatch = Regex.Match(gameHtml, @"topclickTrans\s*=\s*\{", RegexOptions.Singleline);
if (tcMatch.Success)
{
    int tcStart = tcMatch.Index + tcMatch.Value.IndexOf('{');
    int bc2 = 0; bool is2 = false; bool es2 = false; int je2 = -1;
    for (int i = tcStart; i < gameHtml.Length; i++)
    {
        char c = gameHtml[i];
        if (es2) { es2 = false; continue; }
        if (c == '\\' && is2) { es2 = true; continue; }
        if (c == '"') { is2 = !is2; continue; }
        if (is2) continue;
        if (c == '{') bc2++;
        else if (c == '}') { bc2--; if (bc2 == 0) { je2 = i; break; } }
    }
    var tcJson = JsonDocument.Parse(gameHtml.Substring(tcStart, je2 + 1 - tcStart)).RootElement;
    foreach (var prop in tcJson.EnumerateObject())
    {
        var kind = prop.Value.ValueKind;
        string summary;
        if (kind == JsonValueKind.Array) summary = $"Array[{prop.Value.GetArrayLength()}]";
        else if (kind == JsonValueKind.Object) summary = $"Object[{prop.Value.EnumerateObject().Count()} keys]";
        else if (kind == JsonValueKind.Number) summary = prop.Value.GetRawText();
        else if (kind == JsonValueKind.String) summary = $"\"{prop.Value.GetString().Substring(0, Math.Min(80, prop.Value.GetString().Length))}\"";
        else summary = kind.ToString();
        Console.WriteLine($"  {prop.Name}: {summary}");
    }
}

// Numeric global variables
Console.WriteLine("\n--- Numeric global variables ---");
var gnMatches = Regex.Matches(gameHtml, @"(?:window\.(\w+)|var\s+(\w+))\s*=\s*(\d+(?:\.\d+)?)\s*;");
foreach (Match gnm in gnMatches)
    Console.WriteLine($"  {gnm.Groups[1].Value}{gnm.Groups[2].Value} = {gnm.Groups[3].Value}");

Console.WriteLine("\n=== DONE ===");
