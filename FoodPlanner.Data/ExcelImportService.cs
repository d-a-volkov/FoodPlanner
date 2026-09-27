using ClosedXML.Excel;
using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Models;
using FoodPlanner.Core.Services;

namespace FoodPlanner.Data.Services;

public class ExcelImportService
{
    private static readonly HashSet<string> SectionHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Холодильник", "Полка для овощей и фруктов", "Полка для молочных продуктов",
        "Полка для консерв и закаток", "Дверца", "Морозилка", "Все для выпечки",
        "Крупы", "Полка с крупами", "Специи", "Кофе", "Чай", "Нужные мелочи"
    };

    public async Task<List<Product>> ImportFromExcelAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var products = new List<Product>();

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RowsUsed().ToList();

            foreach (var row in rows)
            {
                var nameCell = row.Cell(1).GetString().Trim();
                var statusB = row.Cell(2).GetString().Trim().ToLowerInvariant();
                var statusC = row.Cell(3).GetString().Trim().ToLowerInvariant();

                if (string.IsNullOrEmpty(nameCell))
                    continue;

                if (nameCell.EndsWith(':') || SectionHeaders.Contains(nameCell))
                    continue;

                var stockStatus = ParseStockStatus(statusB);
                bool hasReserve = statusC == "v";

                var product = new Product
                {
                    Name = nameCell,
                    Category = ProductCategoryDetector.Detect(nameCell),
                    StockStatus = stockStatus,
                    HasReserve = hasReserve
                };

                products.Add(product);
            }

            return products;
        });
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
