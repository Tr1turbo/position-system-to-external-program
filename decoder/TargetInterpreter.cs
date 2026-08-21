using System.Numerics;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Decoder;

/// Interprets decoded data into exactly one robotics target.
public class TargetInterpreter
{
    private const float LightRangeForHole = 0.41f;
    private const float LightRangeForRing = 0.42f;
    private const float LightRangeForDirectionNormal = 0.45f;

    private const float SuspiciousNormalDistanceLimit = 0.3f;

    public InterpretedTargetData Interpret(DecodedData decoded)
    {
        if (ShaderProtocols.IsProtocol2(decoded.Version))
        {
            var selected = SelectTarget(decoded.Entities);
            if (selected == null)
            {
                return InterpretedTargetData.NoTarget();
            }

            var target = Interpret(selected.Value.Entity);
            target.hasSourceEntitySlot = true;
            target.sourceEntitySlot = selected.Value.Slot;
            return target;
        }
        if (ShaderProtocols.IsProtocol1(decoded.Version))
        {
            return Interpret(decoded.Lights);
        }
        return InterpretedTargetData.NoTarget();
    }

    /// Selects the nearest present, known, socket-like decoded entity.
    /// Plugs are retained for debugging but are not robotics targets.
    private static (DecodedEntity Entity, int Slot)? SelectTarget(DecodedEntity[] entities)
    {
        (DecodedEntity Entity, int Slot)? best = null;
        for (var slot = 0; slot < entities.Length; slot++)
        {
            var entity = entities[slot];
            if (!entity.Present || !entity.DescriptorKnown || !entity.IsSocketLike) continue;

            if (best == null || entity.Position.LengthSquared() < best.Value.Entity.Position.LengthSquared())
            {
                best = (entity, slot);
            }
        }
        return best;
    }

    private static InterpretedTargetData Interpret(DecodedEntity entity)
    {
        var interpreted = new InterpretedTargetData
        {
            hasTarget = true,
            position = entity.Position,
            isHole = entity.EntityKind == PositionSystemEntityKind.Hole,
            isRing = entity.EntityKind is PositionSystemEntityKind.Ring
                or PositionSystemEntityKind.OneWayRing,
            hasSocketWorldScale = float.IsFinite(entity.Scale),
            socketWorldScale = float.IsFinite(entity.Scale) ? entity.Scale : 1f,
        };

        if (entity.ForwardAvailable)
        {
            interpreted.hasNormal = true;
            interpreted.normal = -entity.Forward;
        }
        if (entity.UpAvailable)
        {
            interpreted.hasTangent = true;
            interpreted.tangent = entity.Up;
        }
        if (entity.OwnerIdentityAvailable || entity.EntityIdentityAvailable)
        {
            interpreted.hasSocketIdentity = true;
            interpreted.socketIdentity = HashIdentity(entity);
        }
        return interpreted;
    }

    private static uint HashIdentity(DecodedEntity entity)
    {
        uint value = entity.RawDescriptor ^ 0x9e3779b9u;
        value ^= entity.OwnerIdentity + 0x9e3779b9u + (value << 6) + (value >> 2);
        value ^= entity.EntityIdentity + 0x9e3779b9u + (value << 6) + (value >> 2);
        value ^= value >> 16;
        value *= 0x7feb352du;
        value ^= value >> 15;
        value *= 0x846ca68bu;
        value ^= value >> 16;
        return value != 0u ? value : 1u;
    }

    private static InterpretedTargetData Interpret(DecodedLight[] decodedLights)
    {
        var lights = decodedLights.Where(IsBlackLight).ToList();

        var holes = lights.Where(light => EncodesRange(light, LightRangeForHole)).OrderBy(LocalPosSqrMagnitude).ToList();
        var rings = lights.Where(light => EncodesRange(light, LightRangeForRing)).OrderBy(LocalPosSqrMagnitude).ToList();
        var directionIndicators = lights.Where(light => EncodesRange(light, LightRangeForDirectionNormal)).ToList();

        if (holes.Count > 0 || rings.Count > 0)
        {
            var holeOrRingElts = holes.Concat(rings)
                .OrderBy(LocalPosSqrMagnitude)
                .ToList();

            var our = holeOrRingElts.First();
            var position = our.position;
            if (directionIndicators.Count > 0)
            {
                var closestDirectionIndicators = directionIndicators
                    .OrderBy(directionIndicator => Vector3.Distance(position, directionIndicator.position))
                    .First();
                if (Vector3.Distance(position, closestDirectionIndicators.position) < SuspiciousNormalDistanceLimit)
                {
                    var normal = Vector3.Normalize(position - closestDirectionIndicators.position);

                    return new InterpretedTargetData
                    {
                        hasTarget = true,
                        position = position,
                        hasNormal = true,
                        normal = normal,
                        isHole = EncodesRange(our, LightRangeForHole),
                        isRing = EncodesRange(our, LightRangeForRing),
                    };
                }
            }

            return new InterpretedTargetData
            {
                hasTarget = true,
                position = position,
                isHole = EncodesRange(our, LightRangeForHole),
                isRing = EncodesRange(our, LightRangeForRing),
            };
        }

        return new InterpretedTargetData
        {
            hasTarget = false
        };
    }

    private static bool IsBlackLight(DecodedLight light) => light.color.X == 0f && light.color.Y == 0f && light.color.Z == 0f;
    private static bool EncodesRange(DecodedLight light, float encodedAmount) => light.enabled && MathF.Abs(light.range - encodedAmount) < 0.005f;
    private static float LocalPosSqrMagnitude(DecodedLight light) => Vector3.DistanceSquared(Vector3.Zero, light.position);
}
