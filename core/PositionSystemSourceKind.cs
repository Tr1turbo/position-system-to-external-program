namespace Hai.PositionSystemToExternalProgram.Core;

/// Identifies how an interpreted target entered the application.
/// Values 0 through 4 mirror the source-kind values serialized by Protocol 2.
public enum PositionSystemSourceKind : byte
{
    Unknown = 0,
    ClassicLight = 1,
    ClassicSps1Light = 2,
    Sps2CompatibilityLight = 3,
    Sps2Atlas = 4,

    /// Interpreted-only source kind; never valid in a Protocol 2 descriptor.
    WebSocket = byte.MaxValue,
}
