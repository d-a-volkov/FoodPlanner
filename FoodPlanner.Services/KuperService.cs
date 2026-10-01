using System.Text;
using System.Text.Json;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using Microsoft.Extensions.Options;

namespace FoodPlanner.Services.Services;

/// <summary>Ошибка проксирования в kuper-bridge: несёт HTTP-статус и сообщение.</summary>
public class KuperBridgeException : Exception
{
    public int StatusCode { get; }

    public KuperBridgeException(int statusCode, string message) : base(message)
        => StatusCode = statusCode;
}

public class KuperService : IKuperService
{
    private const string HttpClientName = "KuperBridge";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KuperOptions _options;
    private readonly SemaphoreSlim _configLock = new(1, 1);

    public KuperService(IHttpClientFactory httpClientFactory, IOptions<KuperOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        Directory.CreateDirectory(_options.DataPath);
    }

    public async Task<string> GetStatusAsync() => await SendAsync(HttpMethod.Get, "/health");

    public async Task<string> GetSessionAsync() => await SendAsync(HttpMethod.Get, "/session");

    public async Task<string> ConfigureSessionAsync(KuperSessionRequest request)
    {
        var body = await SendAsync(HttpMethod.Post, "/session", request);
        var config = new KuperConfig
        {
            Lat = request.Lat,
            Lon = request.Lon,
            ConnectedAt = DateTime.UtcNow
        };
        await SaveConfigAsync(config);
        return body;
    }

    public async Task<string> SelectStoreAsync(int storeId)
    {
        var body = await SendAsync(HttpMethod.Post, "/session/store", new { store_id = storeId });
        var config = GetLocalConfig() ?? new KuperConfig { Lat = 55.7558, Lon = 37.6173 };
        config.StoreId = storeId;
        await SaveConfigAsync(config);
        return body;
    }

    public Task<string> RefreshHistoryAsync() => SendAsync(HttpMethod.Post, "/history/refresh", new { });

    public Task<string> ResolveAsync(KuperResolveRequest request)
        => SendAsync(HttpMethod.Post, "/resolve", new { items = request.Items });

    public Task<string> AddToCartAsync(KuperCartRequest request)
    {
        var items = request.Items.Select(i => new { product_id = i.ProductId, quantity = i.Quantity }).ToList();
        return SendAsync(HttpMethod.Post, "/cart", new { items });
    }

    public Task<string> GetCartAsync() => SendAsync(HttpMethod.Get, "/cart");

    public async Task ResetSessionAsync()
    {
        try { await SendAsync(HttpMethod.Delete, "/session"); }
        catch (KuperBridgeException) { /* сессия могла быть уже сброшена */ }
        if (File.Exists(_options.StatePath)) File.Delete(_options.StatePath);
    }

    public KuperConfig? GetLocalConfig()
    {
        try
        {
            if (!File.Exists(_options.StatePath)) return null;
            var json = File.ReadAllText(_options.StatePath);
            return JsonSerializer.Deserialize<KuperConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveConfigAsync(KuperConfig config)
    {
        await _configLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(_options.StatePath, json);
        }
        finally
        {
            _configLock.Release();
        }
    }

    private async Task<string> SendAsync(HttpMethod method, string path, object? body = null)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(method, path);
        if (body != null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions),
                Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new KuperBridgeException(502, "Сервис kuper-bridge недоступен. Проверьте, что он запущен.");
        }

        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var message = ExtractDetail(json);
            throw new KuperBridgeException((int)response.StatusCode, message);
        }
        return json;
    }

    private static string ExtractDetail(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString() ?? "Ошибка kuper-bridge";
        }
        catch (JsonException)
        {
        }
        return "Ошибка kuper-bridge: " + json.Truncate(200);
    }
}

internal static class StringExtensions
{
    public static string Truncate(this string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}