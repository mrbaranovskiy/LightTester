namespace LightTester;

static class ButtonConstants
{
    public const string Ping = "Ping";
    public const string Statistics = "Statistics";
}

record LightState(States state, DateTime time);
enum States { On, Off }