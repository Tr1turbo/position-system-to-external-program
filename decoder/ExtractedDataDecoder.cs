using System.Numerics;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Decoder;

/// <summary>Decodes the fixed 52-word shader data format.</summary>
public class ExtractedDataDecoder
{
    private const uint Crc32Polynomial = 0xedb88320u;
    private const float MinimumLengthSquared = 1.0e-6f;
    private const float MinimumScale = 1.0e-6f;
    private const float MaximumScale = 1.0e6f;

    private bool[] _data = Array.Empty<bool>();
    private DataValidity _lastChecksumPassingValidity;

    public const int GroupLength = 52;

    public void DecodeInto(DecodedData decoded, bool[] dataLines)
    {
        _data = dataLines;
        if (dataLines.Length < GroupLength * 32)
            throw new ArgumentException("Shader data must contain 52 32-bit words.", nameof(dataLines));

        if (Word(0) != CalculateChecksum())
        {
            decoded.validity = DataValidity.InvalidChecksum;
            return;
        }

        var time = Float(1);
        if (float.IsFinite(time) && Math.Abs(time - decoded.Time) < 0.0001f)
        {
            decoded.validity = _lastChecksumPassingValidity;
            return;
        }
        decoded.Time = time;

        if (Word(2) != ShaderV2_0_0.ProtocolIdentifier)
        {
            SetValidity(decoded, DataValidity.UnexpectedVendor);
            return;
        }

        decoded.Version = Word(3);
        if (!ShaderProtocols.IsKnown(decoded.Version))
        {
            SetValidity(decoded, DataValidity.UnexpectedVersion);
            return;
        }

        var valid = ShaderProtocols.IsProtocol2(decoded.Version)
            ? DecodeProtocol2(decoded)
            : DecodeProtocol1(decoded);
        SetValidity(decoded, valid ? DataValidity.Ok : DataValidity.InvalidPayload);
    }

    private bool DecodeProtocol1(DecodedData decoded)
    {
        decoded.PresenceMask = 0u;
        foreach (var entity in decoded.Entities) entity.Clear();
        DecodeLights(decoded);

        if (ShaderProtocols.SupportsCameraPosition(decoded.Version))
        {
            decoded.CameraPositionAvailable = ReadVector3AllowInfinity(36, out decoded.CameraPosition);
            if (!decoded.CameraPositionAvailable) decoded.CameraPosition = Vector3.Zero;
            decoded.CameraEulerAvailable = ReadVector3AllowInfinity(39, out decoded.CameraRotation);
            if (!decoded.CameraEulerAvailable) decoded.CameraRotation = Vector3.Zero;
        }
        else
        {
            decoded.CameraPositionAvailable = false;
            decoded.CameraEulerAvailable = false;
            decoded.CameraPosition = Vector3.Zero;
            decoded.CameraRotation = Vector3.Zero;
        }
        return true;
    }

    private bool DecodeProtocol2(DecodedData decoded)
    {
        ClearLights(decoded);
        if (Word(ShaderV2_0_0.CanaryWord) != ShaderV2_0_0.Canary) return false;

        decoded.PresenceMask = Word(ShaderV2_0_0.PresenceMask);
        if ((decoded.PresenceMask & ~ShaderV2_0_0.DefinedPresenceMask) != 0u) return false;
        for (var word = ShaderV2_0_0.ReservedStart; word <= ShaderV2_0_0.ReservedEnd; word++)
            if (Word(word) != 0u) return false;

        var cameraPositionPresent = (decoded.PresenceMask & ShaderV2_0_0.CameraPositionPresent) != 0u;
        decoded.CameraPositionAvailable = cameraPositionPresent
            && ReadFiniteVector3(ShaderV2_0_0.CameraPosition, out decoded.CameraPosition);
        if (cameraPositionPresent != decoded.CameraPositionAvailable
            || !cameraPositionPresent && !AreCanonicalNaNs(ShaderV2_0_0.CameraPosition, 3)) return false;

        var cameraEulerPresent = (decoded.PresenceMask & ShaderV2_0_0.CameraEulerPresent) != 0u;
        decoded.CameraEulerAvailable = cameraEulerPresent
            && ReadFiniteVector3(ShaderV2_0_0.CameraEuler, out decoded.CameraRotation);
        if (cameraEulerPresent != decoded.CameraEulerAvailable
            || !cameraEulerPresent && !AreCanonicalNaNs(ShaderV2_0_0.CameraEuler, 3)) return false;

        var slot0Valid = DecodeEntity(decoded.Entity0, ShaderV2_0_0.Entity0, decoded.PresenceMask, 0);
        var slot1Valid = DecodeEntity(decoded.Entity1, ShaderV2_0_0.Entity1, decoded.PresenceMask, 6);
        return slot0Valid && slot1Valid;
    }

    private bool DecodeEntity(DecodedEntity entity, int start, uint mask, int bitOffset)
    {
        entity.Clear();
        for (var offset = ShaderV2_0_0.EntityReservedOffset; offset < ShaderV2_0_0.EntityWordCount; offset++)
            entity.ReservedWordsZero &= Word(start + offset) == 0u;
        if (!entity.ReservedWordsZero) return false;

        var present = Has(mask, bitOffset);
        if (!present)
        {
            return Word(start) == 0u && Word(start + 1) == 0u && Word(start + 2) == 0u
                && AreCanonicalNaNs(start + 3, 8);
        }

        entity.RawDescriptor = Word(start);
        entity.SourceKind = (PositionSystemSourceKind)((entity.RawDescriptor >> 8) & 0xffu);
        entity.EntityKind = (PositionSystemEntityKind)(entity.RawDescriptor & 0xffu);
        entity.DescriptorKnown = (entity.RawDescriptor & 0xffff0000u) == 0u
            && IsKnownSource(entity.SourceKind) && IsKnownEntity(entity.EntityKind);
        entity.OwnerIdentityAvailable = Has(mask, bitOffset + 1);
        entity.OwnerIdentity = entity.OwnerIdentityAvailable ? Word(start + 1) : 0u;
        entity.EntityIdentityAvailable = Has(mask, bitOffset + 2);
        entity.EntityIdentity = entity.EntityIdentityAvailable ? Word(start + 2) : 0u;
        if (!entity.OwnerIdentityAvailable && Word(start + 1) != 0u
            || !entity.EntityIdentityAvailable && Word(start + 2) != 0u) return false;

        if (!ReadFiniteVector3(start + 3, out entity.Position))
        {
            entity.Clear();
            return true;
        }
        entity.Present = true;

        var hasForward = Has(mask, bitOffset + 3);
        var hasUp = Has(mask, bitOffset + 4);
        if (hasForward && hasUp)
        {
            if (ReadNormalizedQuaternion(start + 6, out var rotation))
            {
                entity.Rotation = rotation;
                entity.Forward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, rotation));
                entity.Up = Vector3.Transform(Vector3.UnitY, rotation);
                entity.Up -= entity.Forward * Vector3.Dot(entity.Up, entity.Forward);
                if (IsUsableDirection(entity.Up))
                {
                    entity.Up = Vector3.Normalize(entity.Up);
                    entity.ForwardAvailable = true;
                    entity.UpAvailable = true;
                }
            }
        }
        else if (hasForward && !hasUp)
        {
            if (Word(start + 9) != ShaderV2_0_0.CanonicalNaN) return false;
            if (ReadFiniteVector3(start + 6, out var forward)
                && IsUsableDirection(forward)
                && IsNormalized(forward.LengthSquared()))
            {
                entity.Forward = Vector3.Normalize(forward);
                entity.ForwardAvailable = true;
            }
        }
        else if (!hasForward && !hasUp && !AreCanonicalNaNs(start + 6, 4))
        {
            return false;
        }
        else if (!hasForward && hasUp)
        {
            return false;
        }

        var scaleWord = Word(start + 10);
        var scale = Float(start + 10);
        if (Has(mask, bitOffset + 5) && IsReasonableScale(scale))
        {
            entity.ScaleAvailable = true;
            entity.Scale = scale;
        }
        else if (!Has(mask, bitOffset + 5) && scale == 1f)
        {
            entity.Scale = 1f;
        }
        else if (!Has(mask, bitOffset + 5) && scaleWord == ShaderV2_0_0.CanonicalNaN)
        {
            entity.Scale = float.NaN;
        }
        else
        {
            return false;
        }
        return true;
    }

    private void DecodeLights(DecodedData decoded)
    {
        for (var index = 0; index < decoded.Lights.Length; index++)
        {
            DecodeLight(index, decoded.Lights[index]);
        }
    }

    private void DecodeLight(int index, DecodedLight light)
    {
        light.positionAvailable = ReadVector3AllowInfinity(4 + index * 3, out light.position);
        light.colorAvailable = ReadVector4AllowInfinity(16 + index * 4, out var color);
        if (light.colorAvailable)
        {
            light.color = new Vector3(color.X, color.Y, color.Z);
            light.intensity = color.W;
            light.enabled = color.W > 0f;
        }
        light.rangeAvailable = ReadFloatAllowInfinity(32 + index, out var attenuation);
        if (light.rangeAvailable) light.range = ConvertAttenuationToRangeOrOne(attenuation);
    }

    private static void ClearLights(DecodedData decoded)
    {
        foreach (var light in decoded.Lights)
        {
            light.positionAvailable = light.colorAvailable = light.rangeAvailable = light.enabled = false;
            light.position = light.color = Vector3.Zero;
            light.intensity = light.range = 0f;
        }
    }

    private static bool Has(uint mask, int bit) => (mask & (1u << bit)) != 0u;
    private bool AreCanonicalNaNs(int start, int count)
    {
        for (var offset = 0; offset < count; offset++)
            if (Word(start + offset) != ShaderV2_0_0.CanonicalNaN) return false;
        return true;
    }

    private static bool IsKnownSource(PositionSystemSourceKind source) => source is
        PositionSystemSourceKind.ClassicLight or PositionSystemSourceKind.ClassicSps1Light
        or PositionSystemSourceKind.Sps2CompatibilityLight or PositionSystemSourceKind.Sps2Atlas;
    private static bool IsKnownEntity(PositionSystemEntityKind kind) => kind is
        PositionSystemEntityKind.Hole or PositionSystemEntityKind.Ring
        or PositionSystemEntityKind.OneWayRing or PositionSystemEntityKind.Plug;
    private static bool IsReasonableScale(float value) => float.IsFinite(value)
        && value >= MinimumScale && value <= MaximumScale;
    private static bool IsUsableDirection(Vector3 value) => IsFinite(value)
        && value.LengthSquared() > MinimumLengthSquared;
    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X)
        && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private bool ReadNormalizedQuaternion(int start, out Quaternion result)
    {
        result = new Quaternion(Float(start), Float(start + 1), Float(start + 2), Float(start + 3));
        if (!float.IsFinite(result.X) || !float.IsFinite(result.Y)
            || !float.IsFinite(result.Z) || !float.IsFinite(result.W)
            || !IsNormalized(result.LengthSquared()))
        {
            result = Quaternion.Identity;
            return false;
        }
        result = Quaternion.Normalize(result);
        return true;
    }

    private static bool IsNormalized(float lengthSquared) => float.IsFinite(lengthSquared)
        && Math.Abs(lengthSquared - 1f) <= 0.01f;

    private bool ReadFiniteVector3(int start, out Vector3 result)
    {
        result = new Vector3(Float(start), Float(start + 1), Float(start + 2));
        if (IsFinite(result)) return true;
        result = Vector3.Zero;
        return false;
    }

    private bool ReadVector3AllowInfinity(int start, out Vector3 result)
    {
        result = new Vector3(Float(start), Float(start + 1), Float(start + 2));
        if (!float.IsNaN(result.X) && !float.IsNaN(result.Y) && !float.IsNaN(result.Z)) return true;
        result = Vector3.Zero;
        return false;
    }

    private bool ReadVector4AllowInfinity(int start, out Vector4 result)
    {
        result = new Vector4(Float(start), Float(start + 1), Float(start + 2), Float(start + 3));
        if (!float.IsNaN(result.X) && !float.IsNaN(result.Y)
            && !float.IsNaN(result.Z) && !float.IsNaN(result.W)) return true;
        result = Vector4.Zero;
        return false;
    }

    private bool ReadFloatAllowInfinity(int word, out float result)
    {
        result = Float(word);
        if (!float.IsNaN(result)) return true;
        result = 0f;
        return false;
    }

    private static float ConvertAttenuationToRangeOrOne(float attenuation)
    {
        var result = (float)((0.005f * Math.Sqrt(1_000_000f - attenuation)) / Math.Sqrt(attenuation));
        return float.IsFinite(result) ? result : 1f;
    }

    private float Float(int word) => BitConverter.Int32BitsToSingle((int)Word(word));
    private uint Word(int word)
    {
        var start = checked(word * 32);
        uint value = 0u;
        for (var bit = 0; bit < 32; bit++) if (_data[start + bit]) value |= 1u << bit;
        return value;
    }

    private uint CalculateChecksum()
    {
        uint crc = 0xffffffffu;
        for (var word = 1; word < GroupLength; word++) crc = Crc32UpdateUint(crc, Word(word));
        return crc ^ 0xffffffffu;
    }

    private static uint Crc32UpdateUint(uint crc, uint value)
    {
        crc = Crc32UpdateByte(crc, value & 0xffu);
        crc = Crc32UpdateByte(crc, (value >> 8) & 0xffu);
        crc = Crc32UpdateByte(crc, (value >> 16) & 0xffu);
        return Crc32UpdateByte(crc, value >> 24);
    }

    private static uint Crc32UpdateByte(uint crc, uint value)
    {
        var temporary = crc ^ value;
        for (var bit = 0; bit < 8; bit++)
            temporary = (temporary & 1u) != 0u ? (temporary >> 1) ^ Crc32Polynomial : temporary >> 1;
        return temporary;
    }

    private void SetValidity(DecodedData decoded, DataValidity validity)
    {
        decoded.validity = validity;
        _lastChecksumPassingValidity = validity;
    }
}
