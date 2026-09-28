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

    private static bool IdEquals(object? item, Guid id)
        => item?.GetType().GetProperty("Id")?.GetValue(item) is Guid gid && gid == id;

    private async Task<List<T>> ReadAllAsync()
    {
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
    }

    private async Task WriteAllAsync(List<T> entities)
        => await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(entities, JsonOptions));

    public async Task<List<T>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return await ReadAllAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadAllAsync();
            return items.FirstOrDefault(i => IdEquals(i, id));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> CreateAsync(T entity)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadAllAsync();
            items.Add(entity);
            await WriteAllAsync(items);
            return entity;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> UpdateAsync(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        var id = prop?.GetValue(entity) as Guid?;
        if (id == null) throw new InvalidOperationException("Entity must have an Id property");

        await _lock.WaitAsync();
        try
        {
            var items = await ReadAllAsync();
            var index = items.FindIndex(i => IdEquals(i, id.Value));
            if (index == -1) throw new KeyNotFoundException($"Entity with Id {id} not found");

            items[index] = entity;
            await WriteAllAsync(items);
            return entity;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var items = await ReadAllAsync();
            var removed = items.RemoveAll(i => IdEquals(i, id));
            if (removed == 0) return false;

            await WriteAllAsync(items);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAllAsync(List<T> entities)
    {
        await _lock.WaitAsync();
        try
        {
            await WriteAllAsync(entities);
        }
        finally
        {
            _lock.Release();
        }
    }
}
