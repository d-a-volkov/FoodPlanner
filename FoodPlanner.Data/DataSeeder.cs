using System.Text.Json;
using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;
using FoodPlanner.Data.Services;

namespace FoodPlanner.Data;

public static class DataSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task SeedFromExcelAsync(string excelPath, string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);

        var importer = new ExcelImportService();
        var products = await importer.ImportFromExcelAsync(excelPath);

        var productsJson = JsonSerializer.Serialize(products, JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "products.json"), productsJson);

        var recipesJson = JsonSerializer.Serialize(new List<Recipe>(), JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "recipes.json"), recipesJson);

        var shoppingListsJson = JsonSerializer.Serialize(new List<ShoppingList>(), JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "shoppinglists.json"), shoppingListsJson);

        Console.WriteLine($"Импортировано {products.Count} продуктов из Excel.");
        Console.WriteLine($"Данные сохранены в {dataDirectory}");

        PrintSummary(products);
    }

    private static void PrintSummary(List<Product> products)
    {
        Console.WriteLine("\n=== Сводка ===");

        var byCategory = products.GroupBy(p => p.Category).OrderBy(g => g.Key);
        foreach (var category in byCategory)
        {
            Console.WriteLine($"\n{category.Key.GetDisplayName()}:");
            foreach (var p in category)
            {
                var status = p.StockStatus switch
                {
                    StockStatus.InStock => p.HasReserve ? "[v+]" : "[v ]",
                    StockStatus.OutOfStock => "[x ]",
                    StockStatus.LowStock => "[0 ]",
                    _ => "[? ]"
                };
                Console.WriteLine($"  {status} {p.Name}");
            }
        }

        var inStock = products.Count(p => p.StockStatus == StockStatus.InStock);
        var outOfStock = products.Count(p => p.StockStatus == StockStatus.OutOfStock);
        var lowStock = products.Count(p => p.StockStatus == StockStatus.LowStock);

        Console.WriteLine($"\nИтого: {products.Count} продуктов");
        Console.WriteLine($"  В наличии: {inStock}");
        Console.WriteLine($"  Нет в наличии: {outOfStock}");
        Console.WriteLine($"  Мало/пусто: {lowStock}");

        var other = products.Count(p => p.Category == ProductCategory.Other);
        Console.WriteLine($"  Прочее (не распределено): {other}");
    }
}
