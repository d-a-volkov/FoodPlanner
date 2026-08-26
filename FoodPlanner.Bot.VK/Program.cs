using FoodPlanner.Bot.VK;
using FoodPlanner.Bot.VK.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VkNet;
using VkNet.Abstractions;
using VkNet.Model;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<BotConfiguration>(
            context.Configuration.GetSection("VK"));

        services.AddSingleton<FoodPlannerApiClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<BotConfiguration>>().Value;
            return new FoodPlannerApiClient(config.ApiBaseUrl);
        });

        services.AddSingleton<IVkApi>(sp =>
        {
            var api = new VkApi();
            var config = sp.GetRequiredService<IOptions<BotConfiguration>>().Value;
            api.Authorize(new ApiAuthParams
            {
                AccessToken = config.AccessToken
            });
            return api;
        });

        services.AddSingleton<BotUpdateHandler>();
    })
    .Build();

var config = host.Services.GetRequiredService<IOptions<BotConfiguration>>().Value;

if (string.IsNullOrWhiteSpace(config.AccessToken))
{
    Console.WriteLine("Ошибка: AccessToken не задан.");
    Console.WriteLine("Настройте токен в appsettings.json:");
    Console.WriteLine("  \"VK\": { \"AccessToken\": \"YOUR_TOKEN\", \"ConfirmationCode\": \"CODE\" }");
    return;
}

var handler = host.Services.GetRequiredService<BotUpdateHandler>();

Console.WriteLine("VK Bot запущен. Ожидание сообщений...");

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        await Task.Delay(100, cts.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("VK Bot остановлен.");
}
