namespace LynxLab;

/// <summary>
/// Lab-only ambient motion frame. Values are deliberately small so Guardian V3
/// feels alive without turning into a cartoon animation.
/// </summary>
internal readonly record struct LynxAnimationFrame(
    float Breath,
    float Blink,
    float TailSwayDegrees,
    float ShieldPulse)
{
    public static LynxAnimationFrame Static { get; } =
        new(0f, 0f, 0f, 0.5f);

    public static LynxAnimationFrame FromSeconds(double seconds)
    {
        var breath = (float)Math.Sin(seconds * Math.PI * 2d / 5.8d);
        var tailSway = (float)Math.Sin(seconds * Math.PI * 2d / 7.4d) * 1.5f;
        var shieldPulse =
            0.5f + 0.5f * (float)Math.Sin(seconds * Math.PI * 2d / 3.8d);

        // A deterministic cadence keeps tests reproducible while avoiding a
        // constant metronome feel. Every third cycle includes a small double blink.
        const double blinkCycleSeconds = 6.4d;
        var cycle = seconds % blinkCycleSeconds;
        var cycleIndex = (long)Math.Floor(seconds / blinkCycleSeconds);

        var blink = BlinkPulse(cycle, 4.85d, 0.18d);

        if (cycleIndex % 3L == 1L)
            blink = Math.Max(blink, BlinkPulse(cycle, 5.15d, 0.12d));

        return new LynxAnimationFrame(
            Breath: breath,
            Blink: blink,
            TailSwayDegrees: tailSway,
            ShieldPulse: shieldPulse);
    }

    private static float BlinkPulse(double cycle, double center, double duration)
    {
        var half = duration / 2d;
        var distance = Math.Abs(cycle - center);

        if (distance >= half)
            return 0f;

        return (float)(1d - distance / half);
    }
}

internal interface IAnimatedLynxRenderer
{
    void SetAnimationFrame(LynxAnimationFrame frame);
}
