using FoodPlanner.Core.Interfaces;
using FoodPlanner.Core.Models;
using FoodPlanner.Data.Services;
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

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<IHarvardPlateService, HarvardPlateService>();
builder.Services.AddScoped<IShoppingListService, ShoppingListService>();

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
