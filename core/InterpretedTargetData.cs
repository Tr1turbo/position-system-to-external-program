using System.Numerics;

namespace Hai.PositionSystemToExternalProgram.Core;

/// The single robotics target produced from decoded data. Robotics consumes only this.
public struct InterpretedTargetData
{
    public bool hasTarget;
    public Vector3 position;

    public bool hasNormal;
    public Vector3 normal;
    public bool isHole;
    public bool isRing;

    public bool hasTangent;
    public Vector3 tangent;
    public bool hasSocketIdentity;
    public uint socketIdentity;
    public bool hasSocketWorldScale;
    public float socketWorldScale;

    /// True when the target came from a decoded protocol 2.0 entity slot.
    /// Diagnostics only; robotics must ignore this.
    public bool hasSourceEntitySlot;
    public int sourceEntitySlot;

    public static InterpretedTargetData NoTarget() => new() { hasTarget = false };
}
