using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace FoodPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExternalRecipesController : ControllerBase
{
    private readonly IExternalRecipeService _service;

    public ExternalRecipesController(IExternalRecipeService service) => _service = service;

    [HttpGet("search")]
    public async Task<ActionResult<List<ExternalRecipeResult>>> Search(
        [FromQuery] string query,
        [FromQuery] ExternalRecipeSource? source = null,
        [FromQuery] int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Запрос не может быть пустым");

        try
        {
            return Ok(await _service.SearchAsync(query, source, maxResults));
        }
        catch
        {
            return StatusCode(502, "Не удалось получить результаты поиска. Попробуйте позже.");
        }
    }

    [HttpGet("search-by-available")]
    public async Task<ActionResult<List<ExternalRecipeResult>>> SearchByAvailable(
        [FromQuery] ExternalRecipeSource? source = null,
        [FromQuery] int maxResults = 10,
        [FromQuery] double minAvailability = 0)
    {
        try
        {
            return Ok(await _service.SearchByAvailableProductsAsync(source, maxResults, minAvailability));
        }
        catch
        {
            return StatusCode(502, "Не удалось получить результаты поиска. Попробуйте позже.");
        }
    }

    [HttpGet("detail")]
    public async Task<ActionResult<ExternalRecipeResult>> Details(
        [FromQuery] ExternalRecipeSource source,
        [FromQuery] string url)
    {
        var result = await _service.GetDetailsAsync(source, url);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("import")]
    public async Task<ActionResult<Recipe>> Import([FromBody] ExternalRecipeResult result)
    {
        try
        {
            var recipe = await _service.ImportAsync(result);
            return Ok(recipe);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}