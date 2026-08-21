using System.Numerics;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Decoder;

/// Using light information coming from ExtractedDataDecoder, interpret DPS-like light data.
public class DpsLightInterpreter
{
    private const float LightRangeForHole = 0.41f;
    private const float LightRangeForRing = 0.42f;
    private const float LightRangeForDirectionNormal = 0.45f;
        
    private const float SuspiciousNormalDistanceLimit = 0.3f;

    public InterpretedLightData Interpret(DecodedData decoded)
    {
        if (decoded.Version == ShaderV2_0_0.Version)
        {
            return InterpretSemanticEntities(decoded);
        }

        return InterpretLegacyLights(decoded);
    }

    private static InterpretedLightData InterpretSemanticEntities(DecodedData decoded)
    {
        var target = decoded.Entities
            .Select((entity, slot) => new { entity, slot })
            .Where(candidate => candidate.entity.Present
                && candidate.entity.DescriptorKnown
                && candidate.entity.IsSocketLike)
            .OrderBy(candidate => candidate.entity.Position.LengthSquared())
            .ThenBy(candidate => candidate.slot)
            .Select(candidate => candidate.entity)
            .FirstOrDefault();

        if (target == null)
        {
            return new InterpretedLightData { hasTarget = false };
        }

        var interpreted = new InterpretedLightData
        {
            hasTarget = true,
            position = target.Position,
            isHole = target.EntityKind == PositionSystemEntityKind.Hole,
            isRing = target.EntityKind is PositionSystemEntityKind.Ring
                or PositionSystemEntityKind.OneWayRing,
            hasSocketWorldScale = float.IsFinite(target.Scale),
            socketWorldScale = float.IsFinite(target.Scale) ? target.Scale : 1f,
        };

        if (target.ForwardAvailable)
        {
            interpreted.hasNormal = true;
            interpreted.normal = -target.Forward;
        }
        if (target.UpAvailable)
        {
            interpreted.hasTangent = true;
            interpreted.tangent = target.Up;
        }
        if (target.OwnerIdentityAvailable || target.EntityIdentityAvailable)
        {
            interpreted.hasSocketIdentity = true;
            interpreted.socketIdentity = HashIdentity(target);
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

    private static InterpretedLightData InterpretLegacyLights(DecodedData decoded)
    {
        var lights = decoded.Lights.Where(IsBlackLight).ToList();

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
                    
                    return new InterpretedLightData
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

            return new InterpretedLightData
            {
                hasTarget = true,
                position = position,
                isHole = EncodesRange(our, LightRangeForHole),
                isRing = EncodesRange(our, LightRangeForRing),
            };
        }

        return new InterpretedLightData
        {
            hasTarget = false
        };
    }

    private static bool IsBlackLight(DecodedLight light) => light.color.X == 0f && light.color.Y == 0f && light.color.Z == 0f;
    private static bool EncodesRange(DecodedLight light, float encodedAmount) => light.enabled && MathF.Abs(light.range - encodedAmount) < 0.005f;
    private static float LocalPosSqrMagnitude(DecodedLight light) => Vector3.DistanceSquared(Vector3.Zero, light.position);
}
