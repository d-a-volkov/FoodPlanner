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
    public async Task<ActionResult<ShoppingList>> CreateFromOutOfStock([FromQuery] string? name = null)
    {
        try
        {
            var list = await _service.CreateFromOutOfStockAsync(name);
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteAsync(id) ? NoContent() : NotFound();
}
