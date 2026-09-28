namespace FoodPlanner.Core.Exceptions;

/// <summary>
/// Ошибка бизнес-правил при работе с категориями. StatusCode позволяет
/// контроллеру отличить некорректный запрос (400) от конфликта (409).
/// </summary>
public class CategoryException : Exception
{
    public int StatusCode { get; }

    public CategoryException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}
