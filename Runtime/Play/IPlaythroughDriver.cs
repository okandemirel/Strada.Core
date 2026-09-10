namespace Strada.Core.Play
{
    /// <summary>
    /// The one contract through which the framework plays a game it knows nothing about.
    ///
    /// A game registers exactly one implementation in its service container
    /// (an adapter over its own flow, level and input services). The framework's
    /// tooling — the headless play-through that proves a build is playable —
    /// resolves it with <c>GameBootstrapper.Services.TryGet&lt;IPlaythroughDriver&gt;</c>
    /// and drives it: start a session, act until the session ends, read the
    /// outcome. No tool needs the game's type names, scene names or rules, so the
    /// same tooling serves every game built on Strada.Core.
    ///
    /// A game that does not register one cannot be proven playable, and is
    /// reported as such.
    /// </summary>
    public interface IPlaythroughDriver
    {
        /// <summary>
        /// The game's own name for what it is doing now ("Booting", "Home",
        /// "Playing", "Paused", "LevelWon"…). Free text for people and logs;
        /// nothing is judged on it.
        /// </summary>
        string Phase { get; }

        /// <summary>
        /// True while a session (level, round, run, match) is in progress. Read
        /// shortly after boot, before <see cref="StartSession"/> is called, it
        /// answers whether the game starts play by itself — the Home → play
        /// wiring a person expects when they open the entry scene.
        /// </summary>
        bool IsSessionActive { get; }

        /// <summary>
        /// Outcome of the most recent session; <see cref="PlaythroughOutcome.None"/>
        /// while one runs or before any started.
        /// </summary>
        PlaythroughOutcome Outcome { get; }

        /// <summary>
        /// Start a session. <paramref name="index"/> is the game's own notion of
        /// which one (level number, round, seed); the first session is 1.
        /// Returns false when the game refuses (not booted, already playing,
        /// no such session).
        /// </summary>
        bool StartSession(int index);

        /// <summary>
        /// Perform one unit of the game's primary interaction — the thing a
        /// player does most (a tap, a shot, a move, a swipe). The driver decides
        /// what that means and where it lands, and should pick something that
        /// advances the session (the front pig, the nearest target, a legal
        /// move). Returns false when nothing can be done this frame.
        /// </summary>
        bool Act();
    }
}
