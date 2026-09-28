using FoodPlanner.Core.Exceptions;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace FoodPlanner.Api.Controllers;

/// <summary>
/// Список категорий отдаёт ProductsController (/api/products/categories),
/// здесь только изменение: добавление, переименование и удаление.
/// </summary>
[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _service;

    public CategoriesController(ICategoryService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CategoryNameRequest request)
    {
        try
        {
            var created = await _service.CreateAsync(request.Name);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (CategoryException e)
        {
            return StatusCode(e.StatusCode, new { error = e.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, [FromBody] CategoryNameRequest request)
    {
        try
        {
            return Ok(await _service.RenameAsync(id, request.Name));
        }
        catch (CategoryException e)
        {
            return StatusCode(e.StatusCode, new { error = e.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (CategoryException e)
        {
            return StatusCode(e.StatusCode, new { error = e.Message });
        }
    }

    public class CategoryNameRequest
    {
        public string Name { get; set; } = string.Empty;
    }
}
