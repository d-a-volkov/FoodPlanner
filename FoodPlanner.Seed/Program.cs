using FoodPlanner.Data;

var excelPath = args.Length > 0
    ? args[0]
    : @"D:\MyProject\OpenCode\FoodPlanner\Список основных продуктов.xlsx";

var dataPath = args.Length > 1
    ? args[1]
    : @"D:\MyProject\OpenCode\FoodPlanner\FoodPlanner.Api\Data";

if (!File.Exists(excelPath))
{
    Console.WriteLine($"Файл не найден: {excelPath}");
    Console.WriteLine("Использование: dotnet run -- <путь_к_excel> <путь_к_данным>");
    return;
}

Console.WriteLine($"Импорт из: {excelPath}");
Console.WriteLine($"Данные в: {dataPath}");

await DataSeeder.SeedFromExcelAsync(excelPath, dataPath);
