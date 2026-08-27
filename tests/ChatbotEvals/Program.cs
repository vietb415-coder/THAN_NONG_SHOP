using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Services;

var vegetables = new Category { Id = 1, Name = "Rau củ quả sạch", Description = "" };
var fruits = new Category { Id = 2, Name = "Trái cây hữu cơ", Description = "" };
var foods = new Category { Id = 3, Name = "Thực phẩm", Description = "" };
var products = new List<Product>
{
    new() { Id = 1, Name = "Cà chua hữu cơ Đà Lạt", Description = "Cà chua chín tự nhiên", Category = vegetables, categoryId = 1, price = 35000, stockQuantity = 20 },
    new() { Id = 2, Name = "Dâu tây hữu cơ", Description = "Dâu tây tươi", Category = fruits, categoryId = 2, price = 150000, stockQuantity = 10 },
    new() { Id = 3, Name = "Đậu đỏ", Description = "Đậu đỏ tuyển chọn", Category = foods, categoryId = 3, price = 65000, stockQuantity = 30 },
    new() { Id = 4, Name = "Rau muống hữu cơ", Description = "Rau muống tươi", Category = vegetables, categoryId = 1, price = 15000, stockQuantity = 0 }
};

var failures = new List<string>();

ExpectNone("TV must not map to food", "Tôi muốn mua tivi");
ExpectNone("TV phrased naturally must not match passion fruit", "tôi có thể mua tv ở đây không");
ExpectNone("Laptop must not map to food", "shop có bán laptop không?");
ExpectNone("Phone must not map to food", "I want to buy a smartphone");
ExpectFirst("Vietnamese exact product", "mua cà chua", 1);
ExpectFirst("Missing accents", "mua ca chua", 1);
ExpectFirst("Joined product name", "shop có cachua không", 1);
ExpectFirst("Minor typo", "tôi cần dâu tâi", 2);
ExpectFirst("English synonym", "Do you have organic tomatoes?", 1);
ExpectFirst("Category query", "tôi muốn mua trái cây", 2);
ExpectNone("Out-of-stock product is hidden", "rau muống", expectedProductId: 4);

var broad = CatalogMatcher.Find("Sản phẩm nào đang có sẵn?", products, broadCatalog: true);
Check(broad.Count == 3 && broad.All(product => product.stockQuantity > 0), "Broad catalog returns only in-stock products");

if (failures.Count > 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nChatbot eval failed: {failures.Count} case(s).");
    foreach (var failure in failures) Console.WriteLine($"- {failure}");
    Environment.Exit(1);
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("\nAll chatbot catalog evals passed.");

void ExpectNone(string name, string query, int? expectedProductId = null)
{
    var matches = CatalogMatcher.Find(query, products, broadCatalog: false);
    var passed = expectedProductId.HasValue
        ? matches.All(product => product.Id != expectedProductId.Value)
        : matches.Count == 0;
    Check(passed, name);
}

void ExpectFirst(string name, string query, int expectedId)
{
    var matches = CatalogMatcher.Find(query, products, broadCatalog: false);
    Check(matches.FirstOrDefault()?.Id == expectedId, name);
}

void Check(bool condition, string name)
{
    if (condition)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"PASS  {name}");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"FAIL  {name}");
        failures.Add(name);
    }
    Console.ResetColor();
}
