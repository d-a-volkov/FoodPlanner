using FoodPlanner.Core.Enums;
using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace FoodPlanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll()
        => await _service.GetAllAsync();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Product>> GetById(Guid id)
    {
        var product = await _service.GetByIdAsync(id);
        return product == null ? NotFound() : Ok(product);
    }

    [HttpGet("zone/{zone}")]
    public async Task<ActionResult<List<Product>>> GetByZone(StorageZone zone)
        => await _service.GetByZoneAsync(zone);

    [HttpGet("category/{category}")]
    public async Task<ActionResult<List<Product>>> GetByCategory(ProductCategory category)
        => await _service.GetByCategoryAsync(category);

    [HttpGet("status/{status}")]
    public async Task<ActionResult<List<Product>>> GetByStatus(StockStatus status)
        => await _service.GetByStockStatusAsync(status);

    [HttpGet("search")]
    public async Task<ActionResult<List<Product>>> Search([FromQuery] string q)
        => await _service.SearchAsync(q);

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
