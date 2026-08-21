using System.Numerics;

namespace Hai.PositionSystemToExternalProgram.Core;

public class DecodedData
{
    /// If the last checksum failed, the data will still contain the last known valid decoded data.
    public DataValidity validity;

    public uint Version = 0;
    public float Time = -1f;
    public DecodedLight Light0 { get; } = new();
    public DecodedLight Light1 { get; } = new();
    public DecodedLight Light2 { get; } = new();
    public DecodedLight Light3 { get; } = new();
    public DecodedLight[] Lights { get; }
    public Vector3 CameraPosition;
    public Vector3 CameraRotation;
    public bool CameraPositionAvailable;
    public bool CameraEulerAvailable;

    public uint PresenceMask;
    public DecodedEntity Entity0 { get; } = new();
    public DecodedEntity Entity1 { get; } = new();
    public DecodedEntity[] Entities { get; }

    public string AsSemverString()
    {
        var major = Version / 1_000_000;
        var minor = (Version / 1_000) % 1000;
        var patch = Version % 1000;
        return $"{major}.{minor}.{patch}";
    }

    public DecodedData()
    {
        Lights = new [] { Light0, Light1, Light2, Light3 };
        Entities = new[] { Entity0, Entity1 };
    }
}

public class DecodedEntity
{
    public bool Present;
    public uint RawDescriptor;
    public PositionSystemSourceKind SourceKind;
    public PositionSystemEntityKind EntityKind;
    public bool DescriptorKnown;

    public bool OwnerIdentityAvailable;
    public uint OwnerIdentity;
    public bool EntityIdentityAvailable;
    public uint EntityIdentity;

    public Vector3 Position;
    public bool ForwardAvailable;
    public Vector3 Forward;
    public bool UpAvailable;
    public Vector3 Up;
    public Quaternion Rotation = Quaternion.Identity;

    public bool ScaleAvailable;
    public float Scale = float.NaN;
    public bool ReservedWordsZero = true;

    public bool IsSocketLike => EntityKind is PositionSystemEntityKind.Hole
        or PositionSystemEntityKind.Ring
        or PositionSystemEntityKind.OneWayRing;

    public void Clear()
    {
        Present = false;
        RawDescriptor = 0;
        SourceKind = PositionSystemSourceKind.Unknown;
        EntityKind = PositionSystemEntityKind.Unknown;
        DescriptorKnown = false;
        OwnerIdentityAvailable = false;
        OwnerIdentity = 0;
        EntityIdentityAvailable = false;
        EntityIdentity = 0;
        Position = Vector3.Zero;
        ForwardAvailable = false;
        Forward = Vector3.Zero;
        UpAvailable = false;
        Up = Vector3.Zero;
        Rotation = Quaternion.Identity;
        ScaleAvailable = false;
        Scale = float.NaN;
        ReservedWordsZero = true;
    }
}

public enum DataValidity
{
    NotInitialized,
    Ok,
    InvalidChecksum,
    UnexpectedVendor,
    UnexpectedVersion,
    InvalidPayload,
}
    
public class DecodedLight
{
    public bool colorAvailable;
    public bool positionAvailable;
    public bool rangeAvailable;
    
    public Vector3 color;
    public bool enabled;
    public float intensity;
        
    public Vector3 position;
    public float range;

    public DecodedLight()
    {
        color = Vector3.Zero;
        position = Vector3.Zero;
    }
}
