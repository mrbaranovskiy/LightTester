using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LightService;

namespace LightTester;

class NetworkChecker : IDisposable
{
    private readonly ILogger<Worker> _logger;
    public event EventHandler<LightState> OnStateUpdated;
    private readonly Timer _timer;
    private int _failed = 0;
    
    public NetworkChecker(ILogger<Worker> logger)
    {
        _logger = logger;
        _timer = new Timer(RunTimer, null, TimeSpan.FromMilliseconds(5000), TimeSpan.FromSeconds(10));
    }

    private async void RunTimer(object? state)
    {
        try
        {
            var available = await CheckNetworkAvailable();

            OnStateUpdated(this,
                available
                    ? new LightState(NetworkState.Online, GetNetworkTime())
                    : new LightState(NetworkState.Off, DateTime.MinValue));

            // if (available)
            // {
            //     
            //     if (_failed > 3) 
            //         OnStateChanged(this, new LightState(States.On, GetNetworkTime()));
            //
            //     _failed = 0;
            // }
            // else if(_failed == 3)
            // {
            //     OnStateChanged(this, new LightState(States.Off, GetNetworkTime()));
            //     _logger.LogWarning("Connection is lost!!!");
            // }
            // else
            // {
            //     _failed++;
            // }
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
            _failed++;
        }
    }
    
      
    public static DateTime GetNetworkTime()
    {

        try
        {
            const string ntpServer = "pool.ntp.org";
            var ntpData = new byte[48];
            ntpData[0] = 0x1B; //LeapIndicator = 0 (no warning), VersionNum = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            socket.Connect(ipEndPoint);
            socket.Send(ntpData);
            socket.Receive(ntpData);
            socket.Close();

            ulong intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | (ulong)ntpData[43];
            ulong fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | (ulong)ntpData[47];

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
            var networkDateTime = (new DateTime(1900, 1, 1)).AddMilliseconds((long)milliseconds);

            return networkDateTime + TimeSpan.FromHours(2);
        }
        catch (Exception e)
        {
            return DateTime.Now;
        }
    }

    public static async Task<bool> CheckNetworkAvailable()
    {
        var ping = new Ping();
        var hosts = await Dns.GetHostAddressesAsync("www.google.com");

        if (!hosts.Any()) return false;
        
        var pingReply = await ping.SendPingAsync(hosts.First());
        return pingReply.Status == IPStatus.Success;

    }

    public static async Task<bool> ForceCheck() => await CheckNetworkAvailable();

    public void Dispose()
    {
        _timer?.Dispose();
    }
}