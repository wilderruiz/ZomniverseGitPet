namespace LynxLab;

/// <summary>
/// Lab-only motion frame for Guardian V3.
///
/// Phase 2 established restrained state-aware ambient loops.
/// Phase 3 adds short transition reactions when the Git state changes without
/// changing the approved character geometry or introducing cartoon movement.
/// </summary>
internal readonly record struct LynxAnimationFrame(
    float Breath,
    float Blink,
    float TailSwayDegrees,
    float ShieldPulse,
    float BodyOffsetY,
    float TransitionAmount)
{
    public static LynxAnimationFrame Static { get; } =
        new(0f, 0f, 0f, 0.5f, 0f, 0f);

    public static LynxAnimationFrame FromSeconds(
        double seconds,
        LynxVisualState state,
        double secondsSinceStateChange = double.PositiveInfinity)
    {
        var profile = MotionProfile.For(state);
        var transition = TransitionProfile.For(state);

        var breath =
            (float)Math.Sin(seconds * Math.PI * 2d / profile.BreathSeconds) *
            profile.BreathAmplitude;

        var tailSway =
            (float)Math.Sin(seconds * Math.PI * 2d / profile.TailSeconds) *
            profile.TailDegrees;

        var shieldPulse =
            0.5f +
            0.5f * (float)Math.Sin(seconds * Math.PI * 2d / profile.ShieldSeconds);

        var transitionAmount = 0f;
        var bodyOffsetY = 0f;

        if (secondsSinceStateChange >= 0d &&
            secondsSinceStateChange < transition.DurationSeconds)
        {
            var t = Math.Clamp(
                secondsSinceStateChange / transition.DurationSeconds,
                0d,
                1d);

            // One smooth pulse: clear reaction in, clean return to the state loop.
            // No bounce, overshoot, jump or whole-character shake.
            transitionAmount = (float)Math.Sin(t * Math.PI);
            bodyOffsetY = transition.BodyOffsetY * transitionAmount;

            tailSway += transitionAmount * transition.TailKickDegrees;
            shieldPulse = Math.Max(
                shieldPulse,
                transitionAmount * transition.ShieldStrength);
        }

        var cycle = seconds % profile.BlinkCycleSeconds;
        var cycleIndex = (long)Math.Floor(seconds / profile.BlinkCycleSeconds);
        var blink = BlinkPulse(
            cycle,
            profile.BlinkAtSeconds,
            profile.BlinkDurationSeconds);

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

        // Some transitions acknowledge the event with one intentional blink.
        // Conflict/Attention deliberately keep the eyes open for a focused stare.
        if (secondsSinceStateChange >= 0d &&
            transition.BlinkStrength > 0f)
        {
            var reactionBlink = BlinkPulse(
                secondsSinceStateChange,
                transition.BlinkCenterSeconds,
                transition.BlinkDurationSeconds) *
                transition.BlinkStrength;

            blink = Math.Max(blink, reactionBlink);
        }

        return new LynxAnimationFrame(
            Breath: breath,
            Blink: Math.Clamp(blink, 0f, 1f),
            TailSwayDegrees: tailSway,
            ShieldPulse: Math.Clamp(shieldPulse, 0f, 1f),
            BodyOffsetY: bodyOffsetY,
            TransitionAmount: transitionAmount);
    }

    private static float BlinkPulse(double value, double center, double duration)
    {
        var half = duration / 2d;
        var distance = Math.Abs(value - center);

        if (distance >= half)
            return 0f;

        return (float)(1d - distance / half);
    }

    private readonly record struct TransitionProfile(
        double DurationSeconds,
        float BodyOffsetY,
        float TailKickDegrees,
        float ShieldStrength,
        double BlinkCenterSeconds,
        double BlinkDurationSeconds,
        float BlinkStrength)
    {
        public static TransitionProfile For(LynxVisualState state) =>
            state switch
            {
                // Calm settle: almost no visible movement.
                LynxVisualState.Clean => new(
                    0.82, 0.20f, 0.28f, 0.74f,
                    0.34, 0.18, 0.72f),

                // Local change noticed: rise slightly and become alert.
                LynxVisualState.Changes => new(
                    0.72, -0.48f, 1.20f, 0.94f,
                    0.30, 0.16, 0.82f),

                // Attention means hold the stare rather than blink.
                LynxVisualState.Attention => new(
                    0.78, -0.62f, 0.12f, 1.00f,
                    0.0, 0.0, 0.0f),

                // Save acknowledgment: tiny settle and a single blink.
                LynxVisualState.Save => new(
                    0.76, 0.42f, 0.60f, 0.88f,
                    0.30, 0.17, 0.92f),

                // Incoming work: lift slightly and flick the tail.
                LynxVisualState.Get => new(
                    0.72, -0.58f, 1.05f, 0.96f,
                    0.28, 0.16, 0.78f),

                // Send completion: light confident lift, no celebration.
                LynxVisualState.Send => new(
                    0.78, -0.34f, 0.82f, 0.92f,
                    0.32, 0.17, 0.88f),

                // Conflict braces downward and holds eye contact.
                LynxVisualState.Conflict => new(
                    0.94, 0.72f, 0.0f, 1.00f,
                    0.0, 0.0, 0.0f),

                // Idle transitions are intentionally almost invisible.
                _ => new(
                    0.64, 0.0f, 0.25f, 0.76f,
                    0.28, 0.17, 0.55f)
            };
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
        double DoubleBlinkDelaySeconds)
    {
        public static MotionProfile For(LynxVisualState state) =>
            state switch
            {
                LynxVisualState.Clean => new(
                    6.8, 0.72f,
                    9.2, 0.85f,
                    4.8,
                    7.4, 5.7, 0.18,
                    4, 1, 0.28),

                LynxVisualState.Changes => new(
                    5.0, 0.92f,
                    5.2, 2.25f,
                    2.7,
                    5.5, 4.0, 0.17,
                    3, 1, 0.25),

                LynxVisualState.Attention => new(
                    4.6, 0.62f,
                    4.0, 0.48f,
                    1.65,
                    8.6, 6.8, 0.15,
                    0, 0, 0.0),

                LynxVisualState.Save => new(
                    5.8, 0.78f,
                    7.0, 1.35f,
                    3.1,
                    6.7, 5.0, 0.17,
                    4, 2, 0.26),

                LynxVisualState.Get => new(
                    5.2, 0.88f,
                    5.8, 1.85f,
                    2.35,
                    5.8, 4.2, 0.17,
                    3, 1, 0.24),

                LynxVisualState.Send => new(
                    5.4, 0.84f,
                    6.0, 1.70f,
                    2.8,
                    6.2, 4.7, 0.17,
                    4, 1, 0.25),

                LynxVisualState.Conflict => new(
                    4.3, 0.52f,
                    10.0, 0.22f,
                    1.35,
                    9.2, 7.3, 0.14,
                    0, 0, 0.0),

                _ => new(
                    5.8, 1.0f,
                    7.4, 1.5f,
                    3.8,
                    6.4, 4.85, 0.18,
                    3, 1, 0.30)
            };
    }
}

internal interface IAnimatedLynxRenderer
{
    void SetAnimationFrame(LynxAnimationFrame frame);
}
