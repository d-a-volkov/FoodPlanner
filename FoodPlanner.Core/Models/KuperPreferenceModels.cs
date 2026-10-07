namespace FoodPlanner.Core.Models;

/// <summary>
/// Позиция списка предпочтений, собранного из истории покупок Купера.
/// </summary>
public class KuperPreferenceItem
{
    /// <summary>Идентификатор товара Купера (длинный, например 30703909781).</summary>
    public long ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? HumanVolume { get; set; }

    public int TimesBought { get; set; }

    public decimal? LastPrice { get; set; }

    /// <summary>Дата последней покупки (из окна доставки заказа).</summary>
    public DateTime? LastBoughtAt { get; set; }

    /// <summary>Скрыто пользователем: не участвует в подборе и не воскрешается синком.</summary>
    public bool Excluded { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Документ списка предпочтений (один на приложение).</summary>
public class KuperPreferences
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public List<KuperPreferenceItem> Items { get; set; } = [];

    public DateTime? SyncedAt { get; set; }
}

/// <summary>Ссылка на предпочтение для подбора: идентификатор и имя.</summary>
public class KuperPreferRef
{
    public long ProductId { get; set; }

    public string Name { get; set; } = string.Empty;
}