using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LightTester;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;

namespace LightService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IFileService _service;
    const string token = "";

    private TelegramBotClient _botClient;
    private readonly CancellationTokenSource _cts = new();
    private User _currentUser;
    private IDisposable _eventSource;

    private const string _timeFile = "lighter.time";
    private const string _logDb = "logdb.db";

    private static object _sync = new();

    public Worker(ILogger<Worker> logger,
        IFileService service)
    {
        _logger = logger;
        _service = service;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start service");
        _botClient = new TelegramBotClient(token);
        
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
        _logger.LogInformation("Client init successful");
        _currentUser = await _botClient.GetMeAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Disposing service");
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

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message is not {Text: { }} message)
            return;

        _logger.LogInformation($"Message {message.Text}");
        var chatId = message.Chat.Id;

        switch (message.Text)
        {
            case ButtonConstants.Cam0:
            case ButtonConstants.Cam1:
            {

                try
                {
                     // await _service.WriteServerCommand(message.Text);
                     // var file = await _service.LoadFileFromServer();
                     var client = new TcpClient("localhost", 31983);
                     var networkStream = client.GetStream();
                     await networkStream.WriteAsync(Encoding.UTF8.GetBytes(message.Text));

                     var memorystream = new MemoryStream();
                     await networkStream.CopyToAsync(memorystream, cancellationToken);
                     memorystream.Seek(0, SeekOrigin.Begin);
                     
                     InputOnlineFile photo = new InputMedia(memorystream, "photo.png");
                
                    await botClient.SendPhotoAsync(
                        chatId: chatId,
                        photo : photo, 
                        cancellationToken : cancellationToken);
               
                }
                catch (System.Exception ex)
                {
                   _logger.LogInformation(ex.ToString());
                }
            break;    
            }
            default:
            {
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

            if (!text.Any()) return "No data";
            
            var last10 = text.TakeLast(Math.Min(10, toTake));
            return last10.Aggregate((m, n) => m + Environment.NewLine + n);
        }
    }
}