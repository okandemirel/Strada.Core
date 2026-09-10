namespace Strada.Core.Play
{
    /// <summary>
    /// How a play session ended, as the game itself reports it. The framework's
    /// tooling judges only this value; every other word the game uses for its
    /// phases is free text (<see cref="IPlaythroughDriver.Phase"/>).
    /// </summary>
    public enum PlaythroughOutcome
    {
        /// <summary>No session has ended yet — none was started, or one is still running.</summary>
        None = 0,
        /// <summary>The player won the session (level cleared, match won, run completed).</summary>
        Won = 1,
        /// <summary>The player lost the session (level failed, match lost, run over).</summary>
        Lost = 2,
        /// <summary>The session ended without a win/loss meaning (a sandbox loop, a story beat, a timed demo).</summary>
        Ended = 3,
    }
}
