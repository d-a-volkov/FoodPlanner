namespace FoodPlanner.Core.Enums;

public static class ProductCategoryExtensions
{
    public static string GetDisplayName(this ProductCategory category) => category switch
    {
        ProductCategory.Vegetables => "Овощи",
        ProductCategory.Fruits => "Фрукты и ягоды",
        ProductCategory.Greens => "Зелень и салаты",
        ProductCategory.Meat => "Мясо",
        ProductCategory.Poultry => "Птица",
        ProductCategory.Fish => "Рыба",
        ProductCategory.Seafood => "Морепродукты",
        ProductCategory.Eggs => "Яйца",
        ProductCategory.Dairy => "Молочные продукты",
        ProductCategory.Grains => "Крупы и макароны",
        ProductCategory.Bakery => "Хлеб и выпечка",
        ProductCategory.Legumes => "Бобовые",
        ProductCategory.NutsAndSeeds => "Орехи и семена",
        ProductCategory.OilsAndFats => "Масла и жиры",
        ProductCategory.Spices => "Специи и приправы",
        ProductCategory.Canned => "Консервы и заготовки",
        ProductCategory.Frozen => "Замороженные продукты",
        ProductCategory.Sweets => "Сладости",
        ProductCategory.Beverages => "Напитки",
        ProductCategory.Other => "Прочее",
        _ => category.ToString()
    };
}