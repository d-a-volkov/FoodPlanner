using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using FoodPlanner.Services.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodPlanner.Api.Controllers;

/// <summary>
/// РРЅС‚РµРіСЂР°С†РёСЏ СЃ РєРѕСЂР·РёРЅРѕР№ РљСѓРїРµСЂР° С‡РµСЂРµР· kuper-bridge.
/// РџРѕРґР±РѕСЂ С‚РѕРІР°СЂРѕРІ РїРѕ СЃРїРёСЃРєСѓ РїРѕРєСѓРїРѕРє, РїРѕРґС‚РІРµСЂР¶РґРµРЅРёРµ Р·Р°РјРµРЅ Рё РґРѕР±Р°РІР»РµРЅРёРµ РІ РєРѕСЂР·РёРЅСѓ.
/// </summary>
[ApiController]
[Route("api/kuper")]
public class KuperController : ControllerBase
{
    private readonly IKuperService _kuper;
    private readonly IShoppingListService _lists;

    public KuperController(IKuperService kuper, IShoppingListService lists)
    {
        _kuper = kuper;
        _lists = lists;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
        => await Proxy(() => _kuper.GetStatusAsync());

    [HttpGet("session")]
    public async Task<IActionResult> Session()
        => await Proxy(() => _kuper.GetSessionAsync());

    [HttpPost("session")]
    public async Task<IActionResult> ConfigureSession([FromBody] KuperSessionRequest request)
        => await Proxy(() => _kuper.ConfigureSessionAsync(request));

    [HttpPost("session/store")]
    public async Task<IActionResult> SelectStore([FromBody] KuperStoreSelectionRequest request)
        => await Proxy(() => _kuper.SelectStoreAsync(request.StoreId));

    [HttpPost("history/refresh")]
    public async Task<IActionResult> RefreshHistory()
        => await Proxy(() => _kuper.RefreshHistoryAsync());

    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve([FromBody] KuperResolveRequest request)
    {
        var items = request.Items;
        if ((items == null || items.Count == 0) && request.ListId.HasValue)
        {
            var list = await _lists.GetByIdAsync(request.ListId.Value);
            if (list == null) return NotFound(new { error = "РЎРїРёСЃРѕРє РїРѕРєСѓРїРѕРє РЅРµ РЅР°Р№РґРµРЅ" });

            items = list.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.ProductName))
                .Select(i => new KuperResolveItem
                {
                    Name = i.ProductName,
                    Amount = i.Amount,
                    Unit = (i.Unit.ToString() ?? "pieces").ToLowerInvariant()
                })
                .ToList();
        }

        if (items == null || items.Count == 0)
            return BadRequest(new { error = "РќРµ СѓРєР°Р·Р°РЅС‹ РїСѓРЅРєС‚С‹ РґР»СЏ РїРѕРґР±РѕСЂР°" });

        return await Proxy(() => _kuper.ResolveAsync(new KuperResolveRequest { Items = items }));
    }

    [HttpPost("cart")]
    public async Task<IActionResult> AddToCart([FromBody] KuperCartRequest request)
        => await Proxy(() => _kuper.AddToCartAsync(request));

    [HttpGet("cart")]
    public async Task<IActionResult> GetCart()
        => await Proxy(() => _kuper.GetCartAsync());

    [HttpDelete("session")]
    public async Task<IActionResult> ResetSession()
        => await Proxy(() => _kuper.ResetSessionAsync());

    private async Task<IActionResult> Proxy(Func<Task<string>> call)
    {
        try
        {
            return Content(await call(), "application/json");
        }
        catch (KuperBridgeException e)
        {
            return StatusCode(e.StatusCode, new { error = e.Message });
        }
    }

    private async Task<IActionResult> Proxy(Func<Task> call)
    {
        try
        {
            await call();
            return Content("{}", "application/json");
        }
        catch (KuperBridgeException e)
        {
            return StatusCode(e.StatusCode, new { error = e.Message });
        }
    }
}