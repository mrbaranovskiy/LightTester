using System.Reactive.Linq;
using System.Reactive.Subjects;
using LightTester;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace LightService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    const string token = "";
    
    private TelegramBotClient _botClient;
    private readonly CancellationTokenSource _cts = new();
    private NetworkChecker _networkChecker;
    private readonly BehaviorSubject<LightState> _lastState = new BehaviorSubject<LightState>(new LightState(States.On, DateTime.Now));
    private User _currentUser;
    private IDisposable _eventSource;

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
            hv => _networkChecker.OnStateChanged += hv,
            hv => _networkChecker.OnStateChanged -= hv).Subscribe(s =>
        {
            RecordToDb(s.EventArgs);
            _lastState.OnNext(s.EventArgs);
        });

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
    
    private string GetStatistics()
    {
        return ReadFromDb().Aggregate((n, m) => n + Environment.NewLine + m);
    }

    private List<string> ReadFromDb()
    {
        try
        {
            using var fs = System.IO.File.OpenText("");
            var buffer = new List<string>();

            while (fs.ReadLine() is { } str)
                buffer.Add(str);

            return buffer.Count < 10 ? buffer : buffer.TakeLast(10).ToList();
        }
        catch (Exception e)
        {
            return new List<string>();
        }
    }

    private static void RecordToDb(LightState state)
    {
        using var fs = System.IO.File.OpenWrite("");
        using var sw = new StreamWriter(fs);
        sw.WriteLine($"State {state.state} on {state.time:s}");
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
            ResizeKeyboard = true
        };

        switch (message.Text)
        {
            case ButtonConstants.Ping:
            {
                var result = await _lastState.FirstAsync();
                var response = ResponseCheckLight(result.state, result.time);

                var sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: response,
                    replyMarkup: replyKeyboardMarkup,
                    cancellationToken: cancellationToken);

                break;
            }
            case ButtonConstants.Statistics:
            {
                var sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Not implemented",
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

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
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


    private string ResponseCheckLight(States state, DateTime time) => state switch
    {
        States.On => $"😊 since {time:f}",
        States.Off => $"🕶 since {time:f}"
    };
}