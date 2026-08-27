using System.Globalization;
using System.Text;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Services;

public static class CatalogMatcher
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "ai", "ban", "minh", "toi", "shop", "co", "khong", "la", "gi", "nao", "mot", "con", "cai",
        "cho", "voi", "va", "hay", "nhe", "duoc", "muon", "can", "mua", "tim", "hien", "gio", "nay", "do", "ve", "day",
        "buy", "want", "need", "find", "please", "sell", "have", "looking", "for", "the", "some"
    };

    private static readonly Dictionary<string, string> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tomato"] = "ca chua", ["tomatoes"] = "ca chua",
        ["strawberry"] = "dau tay", ["strawberries"] = "dau tay",
        ["vegetable"] = "rau cu", ["vegetables"] = "rau cu",
        ["fruit"] = "trai cay", ["fruits"] = "trai cay",
        ["egg"] = "trung", ["eggs"] = "trung",
        ["organic"] = "huu co", ["clean food"] = "thuc pham sach"
    };

    public static IReadOnlyList<Product> Find(string message, IReadOnlyList<Product> products, bool broadCatalog, int limit = 4)
    {
        if (broadCatalog)
        {
            return products.Where(product => product.stockQuantity > 0)
                .OrderByDescending(product => product.stockQuantity)
                .ThenBy(product => product.price)
                .Take(limit)
                .ToList();
        }

        var normalizedQuery = ExpandSynonyms(Normalize(message));
        var queryTokens = Tokens(normalizedQuery, removeStopWords: true);
        if (queryTokens.Length == 0) return [];

        return products.Where(product => product.stockQuantity > 0)
            .Select(product => new { Product = product, Score = Score(normalizedQuery, queryTokens, product) })
            .Where(match => match.Score >= 8)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Product.price)
            .Take(limit)
            .Select(match => match.Product)
            .ToList();
    }

    private static int Score(string query, string[] queryTokens, Product product)
    {
        var name = Normalize(product.Name);
        var category = Normalize(product.Category?.Name ?? string.Empty);
        var description = Normalize(product.Description);
        var nameTokens = Tokens(name, false);
        var categoryTokens = Tokens(category, false);
        var descriptionTokens = Tokens(description, false);
        var score = 0;

        var compactQuery = query.Replace(" ", string.Empty, StringComparison.Ordinal);
        var compactName = name.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (compactQuery.Length >= 4 && compactName.Contains(compactQuery, StringComparison.Ordinal)) score += 18;
        if (query.Contains(name, StringComparison.Ordinal) && name.Trim().Length >= 3) score += 20;

        foreach (var token in queryTokens)
        {
            if (token.Length >= 4 && compactName.Contains(token, StringComparison.Ordinal)) score += 18;
            else if (nameTokens.Contains(token)) score += 12;
            else if (categoryTokens.Contains(token)) score += 6;
            else if (nameTokens.Any(candidate => IsClose(token, candidate))) score += 8;
            else if (categoryTokens.Any(candidate => IsClose(token, candidate))) score += 4;
            else if (descriptionTokens.Contains(token)) score += 2;
        }
        return score;
    }

    private static bool IsClose(string left, string right)
    {
        if (left.Length < 3 || right.Length < 3 || Math.Abs(left.Length - right.Length) > 1) return false;
        var allowedDistance = Math.Max(left.Length, right.Length) >= 6 ? 2 : 1;
        return LevenshteinDistance(left, right) <= allowedDistance;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            previous = current;
        }
        return previous[right.Length];
    }

    private static string ExpandSynonyms(string normalized)
    {
        foreach (var synonym in Synonyms)
        {
            normalized = normalized.Replace($" {synonym.Key} ", $" {synonym.Value} ", StringComparison.OrdinalIgnoreCase);
        }
        return normalized;
    }

    public static string[] Tokens(string value, bool removeStopWords = true) =>
        Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2 && (!removeStopWords || !StopWords.Contains(token)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string Normalize(string value)
    {
        var decomposed = (value ?? string.Empty).ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }
        return " " + string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)) + " ";
    }
}
