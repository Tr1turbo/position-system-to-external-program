namespace Hai.PositionSystemToExternalProgram.Core;

public static class ShaderV2_0_0
{
    public const uint ProtocolIdentifier = 1366692562u;
    public const uint Version = 2_000_000u;
    public const uint Canary = 1431677610u;
    public const uint CanonicalNaN = 0x7FC00000u;

    public const int Checksum = 0;
    public const int Time = 1;
    public const int Identifier = 2;
    public const int VersionWord = 3;
    public const int PresenceMask = 4;
    public const int CameraPosition = 5;
    public const int CameraEuler = 8;
    public const int Entity0 = 11;
    public const int Entity1 = 27;
    public const int ReservedStart = 43;
    public const int ReservedEnd = 50;
    public const int CanaryWord = 51;
    public const int WordCount = 52;
    public const int EntityWordCount = 16;
    public const int EntityReservedOffset = 11;
    public const int EntityReservedWordCount = 5;

    public const uint Slot0Present = 1u << 0;
    public const uint Slot0OwnerIdentity = 1u << 1;
    public const uint Slot0EntityIdentity = 1u << 2;
    public const uint Slot0Forward = 1u << 3;
    public const uint Slot0Up = 1u << 4;
    public const uint Slot0Scale = 1u << 5;
    public const uint Slot1Present = 1u << 6;
    public const uint Slot1OwnerIdentity = 1u << 7;
    public const uint Slot1EntityIdentity = 1u << 8;
    public const uint Slot1Forward = 1u << 9;
    public const uint Slot1Up = 1u << 10;
    public const uint Slot1Scale = 1u << 11;
    public const uint CameraPositionPresent = 1u << 12;
    public const uint CameraEulerPresent = 1u << 13;
    public const uint DefinedPresenceMask = 0x00003fffu;
}

public enum PositionSystemEntityKind : byte
{
    Unknown = 0,
    Hole = 1,
    Ring = 2,
    OneWayRing = 3,
    Plug = 16,
}
