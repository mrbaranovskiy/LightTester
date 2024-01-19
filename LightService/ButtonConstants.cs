namespace LightTester;

static class ButtonConstants
{
    public const string Ping = "Ping";
    public const string Statistics = "Statistics";

    public const string Cam0 = "CAM_0";
    public const string Cam1 = "CAM_1";
}

record LightState(NetworkState NetworkState, DateTime time);
enum NetworkState { Online, Off }