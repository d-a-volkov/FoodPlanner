using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using FoodPlanner.Services.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodPlanner.Api.Controllers;

/// <summary>
/// Список предпочтений из истории покупок Купера: просмотр, синхронизация, скрытие.
/// </summary>
[ApiController]
[Route("api/kuper/preferences")]
public class KuperPreferencesController : ControllerBase
{
    private readonly IKuperPreferencesService _service;

    public KuperPreferencesController(IKuperPreferencesService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<KuperPreferences>> Get([FromQuery] bool includeHidden = false)
    {
        var doc = await _service.GetAsync();
        if (!includeHidden && doc.Items.Any(i => i.Excluded))
            doc = FilterHidden(doc);
        return Ok(doc);
    }

    [HttpPost("sync")]
    public async Task<ActionResult<KuperPreferences>> Sync()
    {
        try { return Ok(await _service.SyncAsync()); }
        catch (KuperBridgeException e) { return StatusCode(e.StatusCode, new { error = e.Message }); }
    }

    [HttpDelete("{productId:long}")]
    public async Task<ActionResult<KuperPreferences>> Remove(long productId)
    {
        try { return Ok(await _service.RemoveAsync(productId)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{productId:long}/restore")]
    public async Task<ActionResult<KuperPreferences>> Restore(long productId)
    {
        try { return Ok(await _service.RestoreAsync(productId)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    private static KuperPreferences FilterHidden(KuperPreferences doc)
        => new()
        {
            Id = doc.Id,
            Items = doc.Items
                .Where(i => !i.Excluded)
                .OrderByDescending(i => i.TimesBought)
                .ToList(),
            SyncedAt = doc.SyncedAt
        };
}