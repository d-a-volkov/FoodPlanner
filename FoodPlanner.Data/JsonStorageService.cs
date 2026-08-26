using System.Text.Json;
using FoodPlanner.Core.Interfaces;

namespace FoodPlanner.Data.Services;

public class JsonStorageService<T> : IJsonStorageService<T> where T : class
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonStorageService(string filePath)
    {
        _filePath = filePath;
        EnsureFileExists();
    }

    private void EnsureFileExists()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(_filePath))
            File.WriteAllText(_filePath, "[]");
    }

    public async Task<List<T>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        var items = await GetAllAsync();
        var prop = typeof(T).GetProperty("Id");
        return items.FirstOrDefault(i =>
            prop?.GetValue(i) is Guid gid && gid == id);
    }

    public async Task<T> CreateAsync(T entity)
    {
        var items = await GetAllAsync();
        items.Add(entity);
        await SaveAllAsync(items);
        return entity;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        var items = await GetAllAsync();
        var prop = typeof(T).GetProperty("Id");
        var id = prop?.GetValue(entity) as Guid?;
        if (id == null) throw new InvalidOperationException("Entity must have an Id property");

        var index = items.FindIndex(i =>
            prop?.GetValue(i) is Guid gid && gid == id.Value);
        if (index == -1) throw new KeyNotFoundException($"Entity with Id {id} not found");

        items[index] = entity;
        await SaveAllAsync(items);
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var items = await GetAllAsync();
        var prop = typeof(T).GetProperty("Id");
        var removed = items.RemoveAll(i =>
            prop?.GetValue(i) is Guid gid && gid == id);
        if (removed > 0)
        {
            await SaveAllAsync(items);
            return true;
        }
        return false;
    }

    public async Task SaveAllAsync(List<T> entities)
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(entities, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }
}
