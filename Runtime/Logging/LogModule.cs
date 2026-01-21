namespace Strada.Core.Logging
{
    /// <summary>
    /// Enum representing all available log modules organized into three tiers:
    /// - Strada Core (0-99): Framework internals, visibility locked
    /// - Strada Module (100-999): Strada framework modules, visibility locked
    /// - Game (1000+): Project-specific modules, fully editable
    /// </summary>
    public enum LogModule
    {
        // ═══════════════════════════════════════════════════════════════════
        // TIER 1: STRADA CORE (0-99) - Framework internals, visibility locked
        // ═══════════════════════════════════════════════════════════════════

        [LogModuleInfo(LogModuleTier.StradaCore)]
        General = 0,

        [LogModuleInfo(LogModuleTier.StradaCore)]
        Core = 1,

        [LogModuleInfo(LogModuleTier.StradaCore)]
        Editor = 2,

        [LogModuleInfo(LogModuleTier.StradaCore)]
        DI = 3,

        [LogModuleInfo(LogModuleTier.StradaCore)]
        ECS = 4,

        [LogModuleInfo(LogModuleTier.StradaCore)]
        Sync = 5,

        [LogModuleInfo(LogModuleTier.StradaCore)]
        Bootstrap = 6,

        [LogModuleInfo(LogModuleTier.StradaCore)]
        Modules = 7,

        [LogModuleInfo(LogModuleTier.StradaCore)]
        Unknown = 99,

        // ═══════════════════════════════════════════════════════════════════
        // TIER 2: STRADA MODULES (100-999) - Framework modules, visibility locked
        // ═══════════════════════════════════════════════════════════════════

        [LogModuleInfo(LogModuleTier.StradaModule)]
        Screen = 100,

        // Game modules (1000+) are registered dynamically via LogModuleRegistry.RegisterGameModule()
    }
}
