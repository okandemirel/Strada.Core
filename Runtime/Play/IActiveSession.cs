namespace Strada.Core.Play
{
    /// <summary>
    /// WHICH session is in progress, by the game's own index — the identity of
    /// the thing being played.
    ///
    /// Optional, and the framework needs it for exactly one reason: a game may
    /// start playing BY ITSELF after boot. The play-through then adopts that
    /// running session instead of starting one, and without this contract it
    /// has no way to know which session it adopted — it recorded the index it
    /// had ASKED for, so a game that auto-starts level 1 could certify level 7
    /// (Codex 2026-09-12 X). A game that registers this lets the framework
    /// check; a game that does not has its adopted session recorded as
    /// identity-unverified, and nothing is credited to it.
    /// </summary>
    public interface IActiveSession
    {
        /// <summary>
        /// The game's own index of the session in progress (the first is 1), or
        /// 0 when none is running.
        /// </summary>
        int ActiveSession { get; }
    }
}
