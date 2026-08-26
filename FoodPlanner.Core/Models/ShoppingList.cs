namespace FoodPlanner.Core.Models;

public class ShoppingList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<ShoppingItem> Items { get; set; } = [];
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
