namespace Relay.Features.SystemActions;

public static class VolumeSteps
{
    public static IReadOnlyList<int> Percents { get; } = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];

    public static int Clamp(int percent) => Math.Clamp(percent, 0, 100);

    public static int UpsFromPercent(int percent) =>
        (int)Math.Round(Clamp(percent) / 2.0);
}
