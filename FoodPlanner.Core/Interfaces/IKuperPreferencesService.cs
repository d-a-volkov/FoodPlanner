using FoodPlanner.Core.Models;

namespace FoodPlanner.Core.Interfaces;

/// <summary>
/// Список предпочтений из истории покупок Купера: синк, показ, скрытие.
/// Отредактированный пользователем список — источник истины для подбора.
/// </summary>
public interface IKuperPreferencesService
{
    /// <summary>Текущий документ; при отсутствии — пустой (без сохранения).</summary>
    Task<KuperPreferences> GetAsync();

    /// <summary>Обновить из истории покупок Купера (POST /history/refresh бриджа).</summary>
    Task<KuperPreferences> SyncAsync();

    /// <summary>Скрыть позицию (не воскрешается следующим синком).</summary>
    Task<KuperPreferences> RemoveAsync(long productId);

    /// <summary>Вернуть скрытую позицию в активный список.</summary>
    Task<KuperPreferences> RestoreAsync(long productId);

    /// <summary>
    /// Активные предпочтения для подбора в Купере.
    /// <c>null</c> — документа ещё нет (бридж сам берёт сохранённую историю);
    /// пустой список — документ есть, но всё скрыто.
    /// </summary>
    Task<List<KuperPreferRef>?> GetActiveRefsAsync();
}