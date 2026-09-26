using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace FoodPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShoppingListsController : ControllerBase
{
    private readonly IShoppingListService _service;

    public ShoppingListsController(IShoppingListService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<ShoppingList>>> GetAll()
        => await _service.GetAllAsync();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShoppingList>> GetById(Guid id)
    {
        var list = await _service.GetByIdAsync(id);
        return list == null ? NotFound() : Ok(list);
    }

    [HttpPost("from-recipe/{recipeId:guid}")]
    public async Task<ActionResult<ShoppingList>> CreateFromRecipe(Guid recipeId,
        [FromServices] IRecipeService recipeService,
        [FromServices] IProductService productService)
    {
        var recipe = await recipeService.GetByIdAsync(recipeId);
        if (recipe == null) return NotFound("Рецепт не найден");

        var products = await productService.GetAllAsync();
        var list = await _service.CreateFromRecipeAsync(recipe, products);
        return CreatedAtAction(nameof(GetById), new { id = list.Id }, list);
    }

    [HttpPost("from-out-of-stock")]
    public async Task<ActionResult<ShoppingList>> CreateFromOutOfStock(
        [FromQuery] string? name = null,
        [FromQuery] bool includeLowStock = false,
        [FromQuery] string? zones = null)
    {
        try
        {
            List<int>? zoneIds = null;
            if (!string.IsNullOrWhiteSpace(zones))
            {
                zoneIds = zones
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var z) ? (int?)z : null)
                    .Where(z => z.HasValue)
                    .Select(z => z!.Value)
                    .ToList();
            }

            var list = await _service.CreateFromOutOfStockAsync(name, includeLowStock, zoneIds);
            return CreatedAtAction(nameof(GetById), new { id = list.Id }, list);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("merge")]
    public async Task<ActionResult<ShoppingList>> Merge(
        [FromQuery] Guid list1, [FromQuery] Guid list2)
    {
        try { return Ok(await _service.MergeListsAsync(list1, list2)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{listId:guid}/items/{itemId:guid}/toggle")]
    public async Task<ActionResult<ShoppingItem>> TogglePurchased(Guid listId, Guid itemId)
    {
        try { return Ok(await _service.TogglePurchasedAsync(listId, itemId)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{listId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid listId, Guid itemId)
        => await _service.DeleteItemAsync(listId, itemId) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteAsync(id) ? NoContent() : NotFound();
}
