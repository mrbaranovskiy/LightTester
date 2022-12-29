using System.Net;
using System.Net.NetworkInformation;

namespace LightTester;

class NetworkChecker : IDisposable
{
    public event EventHandler<LightState> OnStateChanged;
    private readonly Timer _timer;
    private int _failed = 0;
    
    public NetworkChecker()
    {
        _timer = new Timer(RunTimer, null, TimeSpan.FromMilliseconds(1000), TimeSpan.FromSeconds(10));
    }

    private async void RunTimer(object? state)
    {
        try
        { 
            var available = await CheckNetworkAvailable();
            
            if (available)
            {
                if (_failed > 3) 
                    OnStateChanged(this, new LightState(States.On, DateTime.Now));

                _failed = 0;
            }
            else if(_failed == 3)
            {
                OnStateChanged(this, new LightState(States.Off, DateTime.Now));
            }
            else
            {
                _failed++;
            }
        }
        catch (Exception e)
        {
            _failed++;
        }
    }

    public static async Task<bool> CheckNetworkAvailable()
    {
        var ping = new Ping();
        var hosts = Dns.GetHostAddresses("www.google.com");

        if (hosts.Any())
        {
            var pingReply = await ping.SendPingAsync(hosts.First());
            return pingReply.Status == IPStatus.Success;
        }

        return false;
    }

    public static async Task<bool> ForceCheck() => await CheckNetworkAvailable();

    public void Dispose()
    {
        _timer?.Dispose();
    }
}