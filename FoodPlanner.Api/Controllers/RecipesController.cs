using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace FoodPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _service;

    public RecipesController(IRecipeService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<Recipe>>> GetAll()
        => await _service.GetAllAsync();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Recipe>> GetById(Guid id)
    {
        var recipe = await _service.GetByIdAsync(id);
        return recipe == null ? NotFound() : Ok(recipe);
    }

    [HttpPost]
    public async Task<ActionResult<Recipe>> Create([FromBody] Recipe recipe)
    {
        var created = await _service.CreateAsync(recipe);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Recipe>> Update(Guid id, [FromBody] Recipe recipe)
    {
        if (id != recipe.Id) return BadRequest();
        try { return Ok(await _service.UpdateAsync(recipe)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteAsync(id) ? NoContent() : NotFound();

    [HttpPost("match")]
    public async Task<ActionResult<List<RecipeMatch>>> GetMatchingRecipes(
        [FromBody] List<Product> availableProducts)
        => await _service.GetRecipesByAvailableProductsAsync(availableProducts);
}
