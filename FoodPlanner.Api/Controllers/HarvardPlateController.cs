using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace FoodPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HarvardPlateController : ControllerBase
{
    private readonly IHarvardPlateService _service;

    public HarvardPlateController(IHarvardPlateService service) => _service = service;

    [HttpGet("analysis")]
    public async Task<ActionResult<HarvardPlateAnalysis>> Analyze()
        => await _service.AnalyzeCurrentStockAsync();

    [HttpGet("recipes")]
    public async Task<ActionResult<List<RecipeMatch>>> GetMatchingRecipes()
        => await _service.FindRecipesByAvailableProductsAsync();
}
