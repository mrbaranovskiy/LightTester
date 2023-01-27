using System.Reactive.Linq;
using System.Reactive.Subjects;
using LightTester;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;

namespace LightService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    const string token = "";

    private TelegramBotClient _botClient;
    private readonly CancellationTokenSource _cts = new();
    private NetworkChecker _networkChecker;
    private User _currentUser;
    private IDisposable _eventSource;

    private const string _timeFile = "lighter.time";
    private const string _logDb = "logdb.db";

    private static object _sync = new();

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start service");
        _botClient = new TelegramBotClient(token);
        _networkChecker = new NetworkChecker(_logger);

        _eventSource = Observable.FromEventPattern<EventHandler<LightState>, LightState>(
            hv => _networkChecker.OnStateUpdated += hv,
            hv => _networkChecker.OnStateUpdated -= hv).Subscribe(s => { HandleEventUpdate(this, s.EventArgs); });

        ReceiverOptions receiverOptions = new()
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
        };

        _logger.LogInformation("Start client initializing");
        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cts.Token
        );
        _logger.LogInformation("Client init successfull");
        _currentUser = await _botClient.GetMeAsync(cancellationToken);
        _logger.LogInformation("Getting user and starting service/");

        var networkTime = NetworkChecker.GetNetworkTime();
        await using var streamWriter = File.CreateText(_timeFile);
        streamWriter.Write(networkTime.ToFileTimeUtc());

        if (!File.Exists(_logDb))
        {
            var str = File.Create(_logDb);
            str.Close();
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Disposing service");
            _networkChecker?.Dispose();
            _eventSource.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ReceiverOptions receiverOptions = new()
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _logger.LogInformation("Start client initializing");
        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cts.Token);

        return Task.CompletedTask;
    }

    private static void HandleEventUpdate(Worker self, LightState state)
    {
        lock (_sync)
        {
            var textData = File.ReadAllText(_timeFile);

            if (!long.TryParse(textData, out var data) || state.NetworkState != NetworkState.Online) return;

            var lastTimeUtc = DateTime.FromFileTimeUtc(data);
            var internetTime = state.time;

            if (internetTime - lastTimeUtc > TimeSpan.FromMinutes(1))
            {
                self.AddStatistics(internetTime);
            }

            using var sw = System.IO.File.CreateText(_timeFile);
            sw.Write(state.time.ToFileTimeUtc());
        }
    }

    private void AddStatistics(DateTime time)
    {
        try
        {
            lock (_sync)
            {
                using var file = System.IO.File.AppendText(_logDb);
                file.WriteLine($"{time:f}");
            }

          
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message is not {Text: { }} message)
            return;

        var chatId = message.Chat.Id;

        var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton("Ping"),
                new KeyboardButton("Statistics"),
            },
        })
        {
            ResizeKeyboard = true,
        };

        switch (message.Text)
        {
            case ButtonConstants.Ping:
            {
                var response = GetStatistics(false);

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: response,
                    replyMarkup: replyKeyboardMarkup,
                    cancellationToken: cancellationToken);
                
                break;
            }
            case ButtonConstants.Statistics:
            {
                var stat = GetStatistics();

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: stat,
                    replyMarkup: replyKeyboardMarkup,
                    cancellationToken: cancellationToken);
                break;
            }
            default:
            {
                var sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Press the button.",
                    replyMarkup: new ReplyKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            new KeyboardButton("Ping"),
                        },
                    })
                    {
                        ResizeKeyboard = true
                    },
                    cancellationToken: cancellationToken);
                break;
            }
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }


    private string GetStatistics(bool takeAll = true)
    {
        lock (_sync)
        {
            var toTake = takeAll ? 10 : 1;
            
            var text = File.ReadAllLines(_logDb);

            if (!text.Any()) return "";
            
            var last10 = text.TakeLast(Math.Min(10, toTake));
            return last10.Aggregate((m, n) => m + Environment.NewLine + n);

        }
    }
}