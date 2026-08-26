using FoodPlanner.Bot.Telegram;
using FoodPlanner.Bot.Telegram.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<BotConfiguration>(
            context.Configuration.GetSection("Telegram"));

        services.AddSingleton<FoodPlannerApiClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<BotConfiguration>>().Value;
            return new FoodPlannerApiClient(config.ApiBaseUrl);
        });

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<BotConfiguration>>().Value;
            return new TelegramBotClient(config.BotToken);
        });

        services.AddSingleton<BotUpdateHandler>();
    })
    .Build();

var config = host.Services.GetRequiredService<IOptions<BotConfiguration>>().Value;

if (string.IsNullOrWhiteSpace(config.BotToken))
{
    Console.WriteLine("Ошибка: BotToken не задан.");
    Console.WriteLine("Настройте токен в appsettings.json:");
    Console.WriteLine("  \"Telegram\": { \"BotToken\": \"YOUR_TOKEN\", \"ApiBaseUrl\": \"https://localhost:7215\" }");
    return;
}

var botClient = host.Services.GetRequiredService<ITelegramBotClient>();
var handler = host.Services.GetRequiredService<BotUpdateHandler>();

var me = await botClient.GetMe();
Console.WriteLine($"Бот запущен: @{me.Username} (ID: {me.Id})");

using var cts = new CancellationTokenSource();

botClient.StartReceiving(
    updateHandler: handler.HandleUpdateAsync,
    errorHandler: async (bot, error, ct) =>
    {
        Console.WriteLine($"Ошибка: {error.Message}");
    },
    receiverOptions: new ReceiverOptions
    {
        AllowedUpdates = [UpdateType.Message]
    },
    cancellationToken: cts.Token
);

Console.WriteLine("Бот работает. Нажмите Ctrl+C для остановки.");

var hostLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await host.RunAsync();
}
catch (OperationCanceledException)
{
    Console.WriteLine("Бот остановлен.");
}
