namespace LightTester;

static class ButtonConstants
{
    public const string Ping = "Ping";
    public const string Statistics = "Statistics";
}

record LightState(NetworkState NetworkState, DateTime time);
enum NetworkState { Online, Off }