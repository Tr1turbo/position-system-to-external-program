namespace Hai.PositionSystemToExternalProgram.Core;

/// <summary>Protocol version constants and predicates shared by decoder, interpreter, and UI.</summary>
public static class ShaderProtocols
{
    /// Canonical Protocol 1 version. The Protocol 1 family also accepts other 1.x.x
    /// versions emitted by older shaders (upstream writes 1.1.1 and the earlier
    /// SPS2 encoder wrote 1.2.0).
    public const uint Protocol1 = 1_001_000u;
    public const uint Protocol2 = ShaderV2_0_0.Version;

    public static bool IsKnown(uint version) =>
        IsProtocol1(version) || IsProtocol2(version);

    public static bool IsProtocol1(uint version) =>
        version / 1_000_000 == 1;

    public static bool IsProtocol2(uint version) =>
        version == Protocol2;

    public static bool SupportsCameraPosition(uint version) =>
        IsProtocol1(version) || IsProtocol2(version);
}
