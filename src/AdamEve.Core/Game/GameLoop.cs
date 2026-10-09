namespace AdamEve.Core.Game;

/// <summary>
/// The fixed 60 Hz step of the simulation with an accumulator (design, section 7.2): frames come when the browser
/// draws, the world advances in equal steps, and rendering interpolates between the last two.
/// </summary>
public sealed class GameLoop
{
    /// <summary>The length of one step of the simulation.</summary>
    public const double StepSeconds = 1.0 / 60;

    /// <summary>The longest time one frame may add: a tab that was hidden does not make the world jump.</summary>
    public const double LongestFrameSeconds = 0.1;

    private double last = double.NaN;
    private double accumulator;

    /// <summary>The time the last frame added, in seconds.</summary>
    public double FrameSeconds { get; private set; }

    /// <summary>How far the time of the frame lies between the last two steps: 0 to 1.</summary>
    public double Alpha { get; private set; }

    /// <summary>Takes the time of a frame.</summary>
    /// <param name="nowMilliseconds">The time of the frame, as the browser gives it.</param>
    /// <returns>How many steps of the simulation to run before drawing.</returns>
    public int Advance(double nowMilliseconds)
    {
        if (double.IsNaN(last))
        {
            last = nowMilliseconds;
            return 0;
        }

        FrameSeconds = Math.Clamp((nowMilliseconds - last) / 1000, 0, LongestFrameSeconds);
        last = nowMilliseconds;
        accumulator += FrameSeconds;
        var steps = (int)((accumulator / StepSeconds) + 1e-9);
        accumulator = Math.Max(0, accumulator - (steps * StepSeconds));
        Alpha = Math.Clamp(accumulator / StepSeconds, 0, 1);
        return steps;
    }
}
