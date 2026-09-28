using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using FoodPlanner.Data.Services;
using FoodPlanner.Services.ExternalRecipe;
using FoodPlanner.Services.Services;

var builder = WebApplication.CreateBuilder(args);

var dataPath = builder.Configuration.GetValue<string>("DataPath")
    ?? Path.Combine(AppContext.BaseDirectory, "Data");
Directory.CreateDirectory(dataPath);

builder.Services.AddSingleton<IJsonStorageService<Product>>(new JsonStorageService<Product>(
    Path.Combine(dataPath, "products.json")));
builder.Services.AddSingleton<IJsonStorageService<Recipe>>(new JsonStorageService<Recipe>(
    Path.Combine(dataPath, "recipes.json")));
builder.Services.AddSingleton<IJsonStorageService<ShoppingList>>(new JsonStorageService<ShoppingList>(
    Path.Combine(dataPath, "shoppinglists.json")));
builder.Services.AddSingleton<IJsonStorageService<CustomCategory>>(new JsonStorageService<CustomCategory>(
    Path.Combine(dataPath, "categories.json")));

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<IHarvardPlateService, HarvardPlateService>();
builder.Services.AddScoped<IShoppingListService, ShoppingListService>();
builder.Services.AddScoped<IExternalRecipeService, ExternalRecipeService>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("ExternalRecipes", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd(
        "text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en;q=0.8");
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
    var productsPath = Path.Combine(dataPath, "products.json");
    if (File.Exists(productsPath))
    {
        var raw = await File.ReadAllTextAsync(productsPath);
        if (raw.Contains("storageZone", StringComparison.OrdinalIgnoreCase))
        {
            await productService.RecategorizeAllAsync();
            Console.WriteLine("Миграция: продукты перераспределены по новым категориям.");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

var frontendPath = builder.Configuration.GetValue<string>("FrontendPath")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "FoodPlanner.Web", "dist");

if (Directory.Exists(frontendPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        DefaultFileNames = new List<string> { "index.html" },
        RequestPath = ""
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        RequestPath = "",
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath)
    });

    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath)
    });
}

app.MapControllers();

app.Run();
