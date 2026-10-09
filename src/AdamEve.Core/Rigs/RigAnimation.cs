namespace AdamEve.Core.Rigs;

/// <summary>A keyframe of one bone: where it is moved and turned at a time.</summary>
/// <param name="Time">The time in seconds from the start of the animation.</param>
/// <param name="X">The move along x, added to the bone's place.</param>
/// <param name="Y">The move along y, added to the bone's place.</param>
/// <param name="Rotation">The turn about the bone's origin, in degrees.</param>
public sealed record Keyframe(double Time, double X, double Y, double Rotation);

/// <summary>The keyframes of one bone.</summary>
/// <param name="Bone">The bone.</param>
/// <param name="FrontRotation">How much of the turn is seen from the front or from behind: a swing toward the viewer is foreshortened.</param>
/// <param name="Keys">The keyframes, in order of time.</param>
public sealed record AnimationTrack(string Bone, double FrontRotation, IReadOnlyList<Keyframe> Keys);

/// <summary>An animation: keyframes for some bones, the same for every rig with that skeleton.</summary>
/// <param name="Id">The id: "idle" or "walk".</param>
/// <param name="Duration">The length in seconds; the animation repeats.</param>
/// <param name="Tracks">The keyframes, by bone.</param>
public sealed record RigAnimation(string Id, double Duration, IReadOnlyList<AnimationTrack> Tracks)
{
    /// <summary>
    /// The move and turn of a bone at a time, between its keyframes. A bone without a track does not move.
    /// </summary>
    /// <param name="bone">The bone.</param>
    /// <param name="time">The time in seconds; it wraps at the animation's length.</param>
    /// <param name="view">The view the rig is seen in.</param>
    /// <returns>The move along x and y, and the turn in degrees.</returns>
    public (double X, double Y, double Rotation) Sample(string bone, double time, RigView view)
    {
        foreach (var track in Tracks)
        {
            if (!string.Equals(track.Bone, bone, StringComparison.Ordinal) || track.Keys.Count == 0)
            {
                continue;
            }

            var at = Duration > 0 ? time % Duration : 0;
            if (at < 0)
            {
                at += Duration;
            }

            var keys = track.Keys;
            var next = 0;
            while (next < keys.Count && keys[next].Time <= at)
            {
                next++;
            }

            var from = keys[Math.Max(0, next - 1)];
            var to = keys[Math.Min(keys.Count - 1, next)];
            var span = to.Time - from.Time;
            var mix = span > 0 ? Math.Clamp((at - from.Time) / span, 0, 1) : 0;
            var turn = view == RigView.Side ? 1 : track.FrontRotation;
            return (
                from.X + ((to.X - from.X) * mix),
                from.Y + ((to.Y - from.Y) * mix),
                (from.Rotation + ((to.Rotation - from.Rotation) * mix)) * turn);
        }

        return (0, 0, 0);
    }
}
