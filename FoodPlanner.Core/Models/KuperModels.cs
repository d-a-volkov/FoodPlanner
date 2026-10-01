using System.Text.Json.Serialization;

namespace FoodPlanner.Core.Models;

/// <summary>Запрос подключения к аккаунту Купера (cookie из авторизованного браузера).</summary>
public class KuperSessionRequest
{
    public string Cookie { get; set; } = string.Empty;
    public double Lat { get; set; } = 55.7558;
    public double Lon { get; set; } = 37.6173;
}

/// <summary>Выбор магазина Купера, найденного на карте.</summary>
public class KuperStoreSelectionRequest
{
    public int StoreId { get; set; }
}

/// <summary>Пункт списка покупок для подбора в Купере.</summary>
public class KuperResolveItem
{
    public string Name { get; set; } = string.Empty;
    public double Amount { get; set; } = 1.0;
    public string Unit { get; set; } = "pieces";
}

/// <summary>Запрос подбора товаров: по списку покупок и/или появному набору пунктов.</summary>
public class KuperResolveRequest
{
    public Guid? ListId { get; set; }
    public List<KuperResolveItem>? Items { get; set; }
}

/// <summary>Товар для добавления в корзину Купера.</summary>
public class KuperCartItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

/// <summary>Запрос добавления набора товаров в корзину Купера.</summary>
public class KuperCartRequest
{
    public List<KuperCartItem> Items { get; set; } = [];
}

/// <summary>Локальные метаданные сессии kuper-bridge (без cookie).</summary>
public class KuperConfig
{
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int? StoreId { get; set; }
    public string? CartUrl { get; set; }
    public DateTime? ConnectedAt { get; set; }
}

/// <summary>Конфигурация интеграции Купера.</summary>
public class KuperOptions
{
    public const string SectionName = "Kuper";

    /// <summary>Базовый адрес kuper-bridge.</summary>
    public string BridgeUrl { get; set; } = "http://localhost:8082";

    /// <summary>Директория для локального файла состояния сессии (kuper.json).</summary>
    public string DataPath { get; set; } = "Data";

    /// <summary>Таймаут обращения к kuper-bridge.</summary>
    public int TimeoutSeconds { get; set; } = 240;

    public string StatePath => Path.Combine(DataPath, "kuper.json");
}