namespace Strada.Core.Logging
{
    /// <summary>
    /// Enum representing all available log modules.
    /// System modules (0-99) are reserved for Strada core functionality.
    /// Project modules (100+) can be added by extending this enum or using the string-based API.
    /// </summary>
    public enum LogModule
    {
        General = 0,
        Core = 1,
        Editor = 2,
        DI = 3,
        ECS = 4,
        Sync = 5,
        Bootstrap = 6,
        Modules = 7,
        Unknown = 99,
    }
}
