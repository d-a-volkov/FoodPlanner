using ClosedXML.Excel;
using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Data.Services;

public class ExcelImportService
{
    private static readonly Dictionary<string, StorageZone> ZoneMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Холодильник"] = StorageZone.Fridge,
        ["Полка для овощей и фруктов"] = StorageZone.VegetableAndFruitShelf,
        ["Полка для молочных продуктов"] = StorageZone.DairyShelf,
        ["Полка для консерв и закаток"] = StorageZone.CannedGoodsShelf,
        ["Дверца"] = StorageZone.FridgeDoor,
        ["Морозилка"] = StorageZone.Freezer,
        ["Все для выпечки"] = StorageZone.BakingShelf,
        ["крупами"] = StorageZone.GrainsAndPastaShelf,
        ["Специи"] = StorageZone.SpicesAndSeasoningsShelf,
        ["Кофе"] = StorageZone.CoffeeAndTea,
        ["Нужные мелочи"] = StorageZone.HouseholdSupplies
    };

    private static readonly Dictionary<string, ProductCategory> CategoryMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["овощ"] = ProductCategory.Vegetables,
        ["фрукт"] = ProductCategory.Fruits,
        ["крупа"] = ProductCategory.WholeGrains,
        ["макарон"] = ProductCategory.WholeGrains,
        ["хлопья"] = ProductCategory.WholeGrains,
        ["мяс"] = ProductCategory.Proteins,
        ["колбас"] = ProductCategory.Proteins,
        ["сосиск"] = ProductCategory.Proteins,
        ["куриц"] = ProductCategory.Proteins,
        ["индейк"] = ProductCategory.Proteins,
        ["свин"] = ProductCategory.Proteins,
        ["рыб"] = ProductCategory.Proteins,
        ["фарш"] = ProductCategory.Proteins,
        ["яйц"] = ProductCategory.Proteins,
        ["творог"] = ProductCategory.Dairy,
        ["молок"] = ProductCategory.Dairy,
        ["кефир"] = ProductCategory.Dairy,
        ["сметан"] = ProductCategory.Dairy,
        ["сыр"] = ProductCategory.Dairy,
        ["масло"] = ProductCategory.HealthyFats,
        ["масл"] = ProductCategory.HealthyFats,
        ["бобов"] = ProductCategory.Legumes,
        ["фасол"] = ProductCategory.Legumes,
        ["горох"] = ProductCategory.Legumes,
        ["чечевиц"] = ProductCategory.Legumes,
        ["специ"] = ProductCategory.Spices,
        ["приправ"] = ProductCategory.Spices,
        ["перец"] = ProductCategory.Spices,
        ["соль"] = ProductCategory.Spices,
        ["трав"] = ProductCategory.Spices,
        ["шоколад"] = ProductCategory.Other,
    };

    public async Task<List<Product>> ImportFromExcelAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var products = new List<Product>();

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RowsUsed().ToList();

            StorageZone currentZone = StorageZone.Fridge;

            foreach (var row in rows)
            {
                var nameCell = row.Cell(1).GetString().Trim();
                var statusB = row.Cell(2).GetString().Trim().ToLowerInvariant();
                var statusC = row.Cell(3).GetString().Trim().ToLowerInvariant();

                if (string.IsNullOrEmpty(nameCell))
                    continue;

                var detectedZone = DetectZone(nameCell);
                if (detectedZone != null)
                {
                    currentZone = detectedZone.Value;
                    continue;
                }

                if (nameCell.EndsWith(':'))
                    continue;

                var stockStatus = ParseStockStatus(statusB);
                bool hasReserve = statusC == "v";

                if (currentZone == StorageZone.HouseholdSupplies)
                    continue;

                var product = new Product
                {
                    Name = nameCell,
                    StorageZone = currentZone,
                    Category = DetectCategory(nameCell, currentZone),
                    StockStatus = stockStatus,
                    HasReserve = hasReserve
                };

                products.Add(product);
            }

            return products;
        });
    }

    private static StorageZone? DetectZone(string cellValue)
    {
        foreach (var (keyword, zone) in ZoneMapping)
        {
            if (cellValue.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return zone;
        }

        if (cellValue.Contains("Кофе", StringComparison.OrdinalIgnoreCase) ||
            cellValue.Contains("Чай", StringComparison.OrdinalIgnoreCase))
            return StorageZone.CoffeeAndTea;

        return null;
    }

    private static ProductCategory DetectCategory(string name, StorageZone zone)
    {
        foreach (var (keyword, category) in CategoryMapping)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return category;
        }

        return zone switch
        {
            StorageZone.VegetableAndFruitShelf => ProductCategory.Vegetables,
            StorageZone.DairyShelf => ProductCategory.Dairy,
            StorageZone.GrainsAndPastaShelf => ProductCategory.WholeGrains,
            StorageZone.SpicesAndSeasoningsShelf => ProductCategory.Spices,
            _ => ProductCategory.Other
        };
    }

    private static StockStatus ParseStockStatus(string status)
    {
        return status switch
        {
            "v" => StockStatus.InStock,
            "x" => StockStatus.OutOfStock,
            "0.0" => StockStatus.LowStock,
            _ => StockStatus.OutOfStock
        };
    }
}
