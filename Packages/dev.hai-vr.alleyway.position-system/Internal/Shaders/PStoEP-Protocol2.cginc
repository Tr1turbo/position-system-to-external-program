#ifndef PSTOEP_PROTOCOL2_INCLUDED
#define PSTOEP_PROTOCOL2_INCLUDED

#include "UnityCG.cginc"

static const uint PSTOEP_CANONICAL_NAN = 0x7fc00000u;
static const uint PSTOEP_SOURCE_CLASSIC_LIGHT = 1u;
static const uint PSTOEP_SOURCE_CLASSIC_SPS1_LIGHT = 2u;
static const uint PSTOEP_SOURCE_SPS2_COMPATIBILITY_LIGHT = 3u;
static const uint PSTOEP_SOURCE_SPS2_ATLAS = 4u;
static const uint PSTOEP_ENTITY_UNKNOWN = 0u;
static const uint PSTOEP_ENTITY_HOLE = 1u;
static const uint PSTOEP_ENTITY_RING = 2u;
static const uint PSTOEP_ENTITY_ONE_WAY_RING = 3u;
static const uint PSTOEP_ENTITY_PLUG = 16u;

static const uint PSTOEP_FIELD_PRESENT = 1u << 0u;
static const uint PSTOEP_FIELD_OWNER_ID = 1u << 1u;
static const uint PSTOEP_FIELD_ENTITY_ID = 1u << 2u;
static const uint PSTOEP_FIELD_FORWARD = 1u << 3u;
static const uint PSTOEP_FIELD_UP = 1u << 4u;
static const uint PSTOEP_FIELD_SCALE = 1u << 5u;

struct PStoEPEntity
{
    uint descriptor;
    uint ownerIdentity;
    uint entityIdentity;
    uint fields;
    uint3 position;
    uint4 orientation;
    uint scale;
};

float PStoEP_NaN()
{
    return asfloat(PSTOEP_CANONICAL_NAN);
}

float3 PStoEP_NaN3()
{
    float value = PStoEP_NaN();
    return float3(value, value, value);
}

float4 PStoEP_NaN4()
{
    float value = PStoEP_NaN();
    return float4(value, value, value, value);
}

uint PStoEP_Descriptor(uint sourceKind, uint entityKind)
{
    return (sourceKind << 8u) | entityKind;
}

PStoEPEntity PStoEP_InvalidEntity()
{
    PStoEPEntity entity;
    entity.descriptor = 0u;
    entity.ownerIdentity = 0u;
    entity.entityIdentity = 0u;
    entity.fields = 0u;
    entity.position = uint3(PSTOEP_CANONICAL_NAN, PSTOEP_CANONICAL_NAN, PSTOEP_CANONICAL_NAN);
    entity.orientation = uint4(PSTOEP_CANONICAL_NAN, PSTOEP_CANONICAL_NAN, PSTOEP_CANONICAL_NAN, PSTOEP_CANONICAL_NAN);
    entity.scale = PSTOEP_CANONICAL_NAN;
    return entity;
}

bool PStoEP_IsFinite(float value)
{
    return value == value && abs(value) < 1.0e20;
}

bool PStoEP_IsFinite3(float3 value)
{
    return all(value == value) && all(abs(value) < 1.0e20);
}

bool PStoEP_NormalizeDirection(inout float3 value)
{
    if (!PStoEP_IsFinite3(value)) return false;
    float lengthSquared = dot(value, value);
    if (lengthSquared <= 1.0e-6) return false;
    value *= rsqrt(lengthSquared);
    return true;
}

bool PStoEP_NormalizeFrame(inout float3 forward, inout float3 up)
{
    if (!PStoEP_NormalizeDirection(forward)) return false;
    up -= forward * dot(up, forward);
    return PStoEP_NormalizeDirection(up);
}

float4 PStoEP_QuaternionFromFrame(float3 forward, float3 up)
{
    float3 right = normalize(cross(up, forward));
    up = normalize(cross(forward, right));
    float m00 = right.x, m01 = up.x, m02 = forward.x;
    float m10 = right.y, m11 = up.y, m12 = forward.y;
    float m20 = right.z, m21 = up.z, m22 = forward.z;
    float4 q;
    float trace = m00 + m11 + m22;
    if (trace > 0.0)
    {
        float s = sqrt(trace + 1.0) * 2.0;
        q = float4((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25 * s);
    }
    else if (m00 > m11 && m00 > m22)
    {
        float s = sqrt(1.0 + m00 - m11 - m22) * 2.0;
        q = float4(0.25 * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
    }
    else if (m11 > m22)
    {
        float s = sqrt(1.0 + m11 - m00 - m22) * 2.0;
        q = float4((m01 + m10) / s, 0.25 * s, (m12 + m21) / s, (m02 - m20) / s);
    }
    else
    {
        float s = sqrt(1.0 + m22 - m00 - m11) * 2.0;
        q = float4((m02 + m20) / s, (m12 + m21) / s, 0.25 * s, (m10 - m01) / s);
    }
    q *= rsqrt(max(dot(q, q), 1.0e-20));
    return q.w < 0.0 ? -q : q;
}

PStoEPEntity PStoEP_MakeForwardEntity(uint descriptor, float3 position, float3 forward)
{
    PStoEPEntity entity = PStoEP_InvalidEntity();
    if (!PStoEP_IsFinite3(position)) return entity;
    entity.descriptor = descriptor;
    entity.position = asuint(position);
    entity.scale = asuint(1.0);
    entity.fields = PSTOEP_FIELD_PRESENT;
    if (PStoEP_NormalizeDirection(forward))
    {
        entity.orientation = uint4(asuint(forward), PSTOEP_CANONICAL_NAN);
        entity.fields |= PSTOEP_FIELD_FORWARD;
    }
    return entity;
}

PStoEPEntity PStoEP_MakeFrameEntity(
    uint descriptor,
    uint ownerIdentity,
    uint entityIdentity,
    float3 position,
    float3 forward,
    float3 up,
    float scale)
{
    PStoEPEntity entity = PStoEP_InvalidEntity();
    if (!PStoEP_IsFinite3(position)) return entity;
    entity.descriptor = descriptor;
    entity.ownerIdentity = ownerIdentity;
    entity.entityIdentity = entityIdentity;
    entity.position = asuint(position);
    entity.fields = PSTOEP_FIELD_PRESENT | PSTOEP_FIELD_OWNER_ID | PSTOEP_FIELD_ENTITY_ID;
    if (PStoEP_NormalizeFrame(forward, up))
    {
        entity.orientation = asuint(PStoEP_QuaternionFromFrame(forward, up));
        entity.fields |= PSTOEP_FIELD_FORWARD | PSTOEP_FIELD_UP;
    }
    if (PStoEP_IsFinite(scale) && scale >= 1.0e-6 && scale <= 1.0e6)
    {
        entity.scale = asuint(scale);
        entity.fields |= PSTOEP_FIELD_SCALE;
    }
    return entity;
}

uint PStoEP_EntityWord(PStoEPEntity entity, uint offset)
{
    if (offset == 0u) return entity.descriptor;
    if (offset == 1u) return entity.ownerIdentity;
    if (offset == 2u) return entity.entityIdentity;
    // Constant component access only: dynamic vector indexing (position[offset - 3u])
    // makes the D3D11 compiler emit invalid bytecode in some stereo configurations.
    if (offset < 6u)
    {
        uint component = offset - 3u;
        if (component == 0u) return entity.position.x;
        if (component == 1u) return entity.position.y;
        return entity.position.z;
    }
    if (offset < 10u)
    {
        uint component = offset - 6u;
        if (component == 0u) return entity.orientation.x;
        if (component == 1u) return entity.orientation.y;
        if (component == 2u) return entity.orientation.z;
        return entity.orientation.w;
    }
    if (offset == 10u) return entity.scale;
    return 0u;
}

#endif
