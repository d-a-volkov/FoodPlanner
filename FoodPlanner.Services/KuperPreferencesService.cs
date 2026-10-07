using System.Text.Json;
using System.Text.Json.Serialization;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Services.Services;

/// <summary>Список предпочтений из истории покупок Купера.</summary>
public class KuperPreferencesService : IKuperPreferencesService
{
    private readonly IJsonStorageService<KuperPreferences> _storage;
    private readonly IKuperService _kuper;

    public KuperPreferencesService(
        IJsonStorageService<KuperPreferences> storage,
        IKuperService kuper)
    {
        _storage = storage;
        _kuper = kuper;
    }

    public async Task<KuperPreferences> GetAsync()
    {
        var all = await _storage.GetAllAsync();
        return all.FirstOrDefault() ?? new KuperPreferences();
    }

    public async Task<KuperPreferences> SyncAsync()
    {
        // Force-обновление: POST /history/refresh возвращает полный payload.
        var body = await _kuper.RefreshHistoryAsync();
        var bridge = JsonSerializer.Deserialize<BridgeHistory>(body, JsonOptions);

        var doc = (await _storage.GetAllAsync()).FirstOrDefault();
        var created = doc == null;
        doc ??= new KuperPreferences();

        var byId = doc.Items.ToDictionary(i => i.ProductId);
        foreach (var src in bridge?.Products ?? [])
        {
            if (string.IsNullOrWhiteSpace(src.Name)) continue;

            if (!byId.TryGetValue(src.ProductId, out var item))
            {
                item = new KuperPreferenceItem { ProductId = src.ProductId };
                doc.Items.Add(item);
                byId[src.ProductId] = item;
            }

            // Скрытые позиции синком не воскрешаются.
            if (item.Excluded) continue;

            item.Name = src.Name;
            if (!string.IsNullOrWhiteSpace(src.Sku)) item.Sku = src.Sku;
            if (!string.IsNullOrWhiteSpace(src.HumanVolume)) item.HumanVolume = src.HumanVolume;
            item.TimesBought = Math.Max(item.TimesBought, src.TimesBought);
            if (DateTime.TryParse(src.LastBoughtAt, out var boughtAt))
                item.LastBoughtAt = boughtAt;
            if (src.LastPrice is > 0)
                item.LastPrice = (decimal)src.LastPrice.Value;
            item.UpdatedAt = DateTime.UtcNow;
        }

        doc.SyncedAt = DateTime.UtcNow;
        return created
            ? await _storage.CreateAsync(doc)
            : await _storage.UpdateAsync(doc);
    }

    public async Task<KuperPreferences> RemoveAsync(long productId)
    {
        var doc = await GetStoredAsync();
        var item = doc.Items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new KeyNotFoundException($"Предпочтение {productId} не найдено");
        item.Excluded = true;
        item.UpdatedAt = DateTime.UtcNow;
        return await _storage.UpdateAsync(doc);
    }

    public async Task<KuperPreferences> RestoreAsync(long productId)
    {
        var doc = await GetStoredAsync();
        var item = doc.Items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new KeyNotFoundException($"Предпочтение {productId} не найдено");
        item.Excluded = false;
        item.UpdatedAt = DateTime.UtcNow;
        return await _storage.UpdateAsync(doc);
    }

    public async Task<List<KuperPreferRef>?> GetActiveRefsAsync()
    {
        var all = await _storage.GetAllAsync();
        var doc = all.FirstOrDefault();
        if (doc == null) return null;
        return doc.Items
            .Where(i => !i.Excluded && !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => new KuperPreferRef { ProductId = i.ProductId, Name = i.Name })
            .ToList();
    }

    private async Task<KuperPreferences> GetStoredAsync()
        => (await _storage.GetAllAsync()).FirstOrDefault()
           ?? throw new KeyNotFoundException("Список предпочтений пуст — сначала выполните синхронизацию");

    private static readonly JsonSerializerOptions JsonOptions = new();

    // Ответ kuper-bridge: snake_case поля истории покупок.
    private sealed class BridgeHistory
    {
        [JsonPropertyName("refreshed_at")]
        public double? RefreshedAt { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("products")]
        public List<BridgeHistoryProduct>? Products { get; set; }
    }

    private sealed class BridgeHistoryProduct
    {
        [JsonPropertyName("product_id")]
        public long ProductId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("sku")]
        public string? Sku { get; set; }

        [JsonPropertyName("human_volume")]
        public string? HumanVolume { get; set; }

        [JsonPropertyName("times_bought")]
        public int TimesBought { get; set; }

        [JsonPropertyName("last_bought_at")]
        public string? LastBoughtAt { get; set; }

        [JsonPropertyName("last_price")]
        public double? LastPrice { get; set; }
    }
}