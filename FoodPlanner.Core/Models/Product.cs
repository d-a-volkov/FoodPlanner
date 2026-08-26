using FoodPlanner.Core.Enums;

namespace FoodPlanner.Core.Models;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public StorageZone StorageZone { get; set; }
    public ProductCategory Category { get; set; }
    public StockStatus StockStatus { get; set; }
    public bool HasReserve { get; set; }
    public MeasurementUnit DefaultUnit { get; set; } = MeasurementUnit.Grams;

    public double CaloriesPer100g { get; set; }
    public double ProteinPer100g { get; set; }
    public double FatPer100g { get; set; }
    public double CarbsPer100g { get; set; }

    public double QuantityInStock { get; set; }
}
