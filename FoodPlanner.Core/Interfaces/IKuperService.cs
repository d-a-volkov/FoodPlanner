using System.Text.Json;
using FoodPlanner.Core.Models;

namespace FoodPlanner.Core.Interfaces;

/// <summary>
/// Прокси к sidecar-сервису kuper-bridge (сборка корзины Купера).
/// Cookie Купера хранится только в sidecar; здесь — только метаданные сессии.
/// </summary>
public interface IKuperService
{
    Task<string> GetStatusAsync();
    Task<string> GetSessionAsync();
    Task<string> ConfigureSessionAsync(KuperSessionRequest request);
    Task<string> SelectStoreAsync(int storeId);
    Task<string> RefreshStoresAsync(KuperStoresRequest request);
    Task<string> RefreshHistoryAsync();
    Task<string> GetHistoryAsync();
    Task<string> ResolveAsync(KuperResolveRequest request);
    Task<string> SearchAsync(KuperSearchRequest request);
    Task<string> AddToCartAsync(KuperCartRequest request);
    Task<string> GetCartAsync();
    Task ResetSessionAsync();
    KuperConfig? GetLocalConfig();
}