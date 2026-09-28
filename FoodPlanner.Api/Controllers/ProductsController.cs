using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using FoodPlanner.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    private readonly ICategoryService _categories;

    public ProductsController(IProductService service, ICategoryService categories)
    {
        _service = service;
        _categories = categories;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll()
        => await _service.GetAllAsync();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Product>> GetById(Guid id)
    {
        var product = await _service.GetByIdAsync(id);
        return product == null ? NotFound() : Ok(product);
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult<List<Product>>> GetByCategory(ProductCategory category)
        => await _service.GetByCategoryAsync(category);

    [HttpGet("categories")]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories()
        => await _categories.GetAllAsync();

    [HttpGet("status/{status}")]
    public async Task<ActionResult<List<Product>>> GetByStatus(StockStatus status)
        => await _service.GetByStockStatusAsync(status);

    [HttpGet("search")]
    public async Task<ActionResult<List<Product>>> Search([FromQuery] string q)
        => await _service.SearchAsync(q);

    // Категория, которую детектор предложит в форме добавления. Клиент показывает
    // её как предложение по умолчанию, но пользователь может её переопределить.
    [HttpGet("detect-category")]
    public ActionResult<object> DetectCategory([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Ok(new { category = -1, categoryName = string.Empty });
        var category = ProductCategoryDetector.Detect(name);
        return Ok(new { category = (int)category, categoryName = category.GetDisplayName() });
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create([FromBody] Product product)
    {
        var created = await _service.CreateAsync(product);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Product>> Update(Guid id, [FromBody] Product product)
    {
        if (id != product.Id) return BadRequest();
        try { return Ok(await _service.UpdateAsync(product)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
        => await _service.DeleteAsync(id) ? NoContent() : NotFound();
}
