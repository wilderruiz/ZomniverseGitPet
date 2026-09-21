namespace LynxLab;

/// <summary>
/// Lab-only motion frame for Guardian V3.
/// Phase 2 keeps movement restrained but lets each Git state carry a distinct
/// behavioural mood. All values stay deterministic so visual testing remains
/// reproducible.
/// </summary>
internal readonly record struct LynxAnimationFrame(
    float Breath,
    float Blink,
    float TailSwayDegrees,
    float ShieldPulse)
{
    public static LynxAnimationFrame Static { get; } =
        new(0f, 0f, 0f, 0.5f);

    public static LynxAnimationFrame FromSeconds(
        double seconds,
        LynxVisualState state,
        double secondsSinceStateChange = double.PositiveInfinity)
    {
        var profile = MotionProfile.For(state);

        var breath =
            (float)Math.Sin(seconds * Math.PI * 2d / profile.BreathSeconds) *
            profile.BreathAmplitude;

        var tailSway =
            (float)Math.Sin(seconds * Math.PI * 2d / profile.TailSeconds) *
            profile.TailDegrees;

        var shieldPulse =
            0.5f +
            0.5f * (float)Math.Sin(seconds * Math.PI * 2d / profile.ShieldSeconds);

        // A brief state-change acknowledgement. It deliberately modifies only
        // existing motion channels instead of moving the whole animal.
        if (secondsSinceStateChange >= 0d &&
            secondsSinceStateChange < profile.ReactionSeconds)
        {
            var t = secondsSinceStateChange / profile.ReactionSeconds;
            var reaction = (float)Math.Sin(t * Math.PI);

            tailSway += reaction * profile.ReactionTailDegrees;
            shieldPulse = Math.Max(
                shieldPulse,
                reaction * profile.ReactionShieldStrength);
        }

        var cycle = seconds % profile.BlinkCycleSeconds;
        var cycleIndex = (long)Math.Floor(seconds / profile.BlinkCycleSeconds);
        var blink = BlinkPulse(cycle, profile.BlinkAtSeconds, profile.BlinkDurationSeconds);

        if (profile.DoubleBlinkEvery > 0 &&
            cycleIndex % profile.DoubleBlinkEvery == profile.DoubleBlinkOffset)
        {
            blink = Math.Max(
                blink,
                BlinkPulse(
                    cycle,
                    profile.BlinkAtSeconds + profile.DoubleBlinkDelaySeconds,
                    profile.BlinkDurationSeconds));
        }

        return new LynxAnimationFrame(
            Breath: breath,
            Blink: blink,
            TailSwayDegrees: tailSway,
            ShieldPulse: Math.Clamp(shieldPulse, 0f, 1f));
    }

    private static float BlinkPulse(double cycle, double center, double duration)
    {
        var half = duration / 2d;
        var distance = Math.Abs(cycle - center);

        if (distance >= half)
            return 0f;

        return (float)(1d - distance / half);
    }

    private readonly record struct MotionProfile(
        double BreathSeconds,
        float BreathAmplitude,
        double TailSeconds,
        float TailDegrees,
        double ShieldSeconds,
        double BlinkCycleSeconds,
        double BlinkAtSeconds,
        double BlinkDurationSeconds,
        int DoubleBlinkEvery,
        int DoubleBlinkOffset,
        double DoubleBlinkDelaySeconds,
        double ReactionSeconds,
        float ReactionTailDegrees,
        float ReactionShieldStrength)
    {
        public static MotionProfile For(LynxVisualState state) =>
            state switch
            {
                // Rested and settled.
                LynxVisualState.Clean => new(
                    6.8, 0.72f,
                    9.2, 0.85f,
                    4.8,
                    7.4, 5.7, 0.18,
                    4, 1, 0.28,
                    0.70, 0.35f, 0.72f),

                // Something changed: more attentive, but not alarmed.
                LynxVisualState.Changes => new(
                    5.0, 0.92f,
                    5.2, 2.25f,
                    2.7,
                    5.5, 4.0, 0.17,
                    3, 1, 0.25,
                    0.78, 1.15f, 0.92f),

                // Alert posture: breathing controlled and tail intentionally quieter.
                LynxVisualState.Attention => new(
                    4.6, 0.62f,
                    4.0, 0.48f,
                    1.65,
                    8.6, 6.8, 0.15,
                    0, 0, 0.0,
                    0.82, 0.15f, 1.0f),

                // Acknowledgement states retain personality without celebration.
                LynxVisualState.Save => new(
                    5.8, 0.78f,
                    7.0, 1.35f,
                    3.1,
                    6.7, 5.0, 0.17,
                    4, 2, 0.26,
                    0.72, 0.65f, 0.86f),

                LynxVisualState.Get => new(
                    5.2, 0.88f,
                    5.8, 1.85f,
                    2.35,
                    5.8, 4.2, 0.17,
                    3, 1, 0.24,
                    0.78, 0.95f, 0.94f),

                LynxVisualState.Send => new(
                    5.4, 0.84f,
                    6.0, 1.70f,
                    2.8,
                    6.2, 4.7, 0.17,
                    4, 1, 0.25,
                    0.76, 0.85f, 0.90f),

                // Conflict is intentionally tense and still.
                LynxVisualState.Conflict => new(
                    4.3, 0.52f,
                    10.0, 0.22f,
                    1.35,
                    9.2, 7.3, 0.14,
                    0, 0, 0.0,
                    0.90, 0.0f, 1.0f),

                _ => new(
                    5.8, 1.0f,
                    7.4, 1.5f,
                    3.8,
                    6.4, 4.85, 0.18,
                    3, 1, 0.30,
                    0.68, 0.45f, 0.78f)
            };
    }
}

internal interface IAnimatedLynxRenderer
{
    void SetAnimationFrame(LynxAnimationFrame frame);
}
