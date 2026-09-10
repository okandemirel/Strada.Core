namespace Strada.Core.Play
{
    /// <summary>
    /// How many sessions the game offers — the levels, rounds, puzzles or runs
    /// that <see cref="IPlaythroughDriver.StartSession"/> accepts, indexed
    /// 1..<see cref="SessionCount"/>. Optional: a game that registers one lets
    /// the framework's tooling measure "12 levels" in a design document
    /// against what is actually shipped, and play every session in turn to
    /// prove each one can be finished. A game that registers none is reported
    /// as "level count not measurable", never as having the number it claims.
    /// </summary>
    public interface ISessionCatalog
    {
        /// <summary>Number of sessions StartSession accepts; 0 when the game has none yet.</summary>
        int SessionCount { get; }
    }
}
