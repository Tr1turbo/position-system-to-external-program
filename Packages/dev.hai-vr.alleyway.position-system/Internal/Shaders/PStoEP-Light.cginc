#ifndef PSTOEP_LIGHT_INCLUDED
#define PSTOEP_LIGHT_INCLUDED

#include "PStoEP-Protocol2.cginc"

static const float PSTOEP_LIGHT_PAIR_DISTANCE_SQ = 0.09;

struct PStoEPProviderContext
{
    PStoEPEntity entity0;
    PStoEPEntity entity1;
};

float3 PStoEP_LightWorldPosition(int index)
{
    return float3(unity_4LightPosX0[index], unity_4LightPosY0[index], unity_4LightPosZ0[index]);
}

float PStoEP_LightRange(int index)
{
    float attenuation = unity_4LightAtten0[index];
    return attenuation > 0.0 ? 5.0 * rsqrt(attenuation) : 1.0;
}

bool PStoEP_LightIsBlack(int index)
{
    float4 color = unity_LightColor[index];
    return color.a > 0.0 && all(color.rgb == 0.0);
}

uint PStoEP_LightSuffix(int index)
{
    float range = PStoEP_LightRange(index);
    float millibase = floor(range * 1000.0 + 1.0e-4) * 0.001;
    return (uint)round((range - millibase) * 10000.0);
}

uint PStoEP_LightSourceKind(int index)
{
    uint suffix = PStoEP_LightSuffix(index);
    if (suffix == 2u) return PSTOEP_SOURCE_CLASSIC_SPS1_LIGHT;
    if (suffix >= 5u && suffix <= 7u) return PSTOEP_SOURCE_SPS2_COMPATIBILITY_LIGHT;
    return PSTOEP_SOURCE_CLASSIC_LIGHT;
}

uint PStoEP_LightChannel(int index)
{
    float range = PStoEP_LightRange(index);
    return (uint)round(fmod(range, 0.1) * 100.0);
}

bool PStoEP_LightIsFront(int index)
{
    if (!PStoEP_LightIsBlack(index)) return false;
    uint channel = PStoEP_LightChannel(index);
    return channel == 5u || channel == 6u;
}

uint PStoEP_LightEntityKind(int index)
{
    if (!PStoEP_LightIsBlack(index)) return PSTOEP_ENTITY_UNKNOWN;
    float range = PStoEP_LightRange(index);
    if (!PStoEP_IsFinite(range) || range >= 0.5) return PSTOEP_ENTITY_UNKNOWN;
    uint channel = PStoEP_LightChannel(index);
    if (channel == 1u || channel == 3u) return PSTOEP_ENTITY_HOLE;
    if (channel != 2u && channel != 4u) return PSTOEP_ENTITY_UNKNOWN;
    return PStoEP_LightSourceKind(index) == PSTOEP_SOURCE_CLASSIC_LIGHT
        ? PSTOEP_ENTITY_ONE_WAY_RING
        : PSTOEP_ENTITY_RING;
}

PStoEPEntity PStoEP_FindNearestClassicSocket(float3 observerWorld, bool excludeSps2Compatibility)
{
    PStoEPEntity best = PStoEP_InvalidEntity();
    float bestDistanceSq = 3.402823466e+38;
    [unroll]
    for (int rootIndex = 0; rootIndex < 4; rootIndex++)
    {
        uint entityKind = PStoEP_LightEntityKind(rootIndex);
        if (entityKind == PSTOEP_ENTITY_UNKNOWN) continue;
        uint sourceKind = PStoEP_LightSourceKind(rootIndex);
        if (excludeSps2Compatibility && sourceKind == PSTOEP_SOURCE_SPS2_COMPATIBILITY_LIGHT) continue;
        uint expectedFrontChannel = PStoEP_LightChannel(rootIndex) <= 2u ? 5u : 6u;

        float3 rootWorld = PStoEP_LightWorldPosition(rootIndex);
        float3 observerOffset = rootWorld - observerWorld;
        float distanceSq = dot(observerOffset, observerOffset);
        if (distanceSq >= bestDistanceSq) continue;

        float bestFrontDistanceSq = PSTOEP_LIGHT_PAIR_DISTANCE_SQ;
        float3 forwardLocal = PStoEP_NaN3();
        [unroll]
        for (int frontIndex = 0; frontIndex < 4; frontIndex++)
        {
            if (!PStoEP_LightIsFront(frontIndex)) continue;
            if (PStoEP_LightChannel(frontIndex) != expectedFrontChannel) continue;
            if (PStoEP_LightSourceKind(frontIndex) != sourceKind) continue;
            float3 rootToFrontWorld = PStoEP_LightWorldPosition(frontIndex) - rootWorld;
            float pairDistanceSq = dot(rootToFrontWorld, rootToFrontWorld);
            if (pairDistanceSq >= bestFrontDistanceSq) continue;
            bestFrontDistanceSq = pairDistanceSq;
            forwardLocal = mul((float3x3)unity_WorldToObject, rootToFrontWorld);
        }

        best = PStoEP_MakeForwardEntity(
            PStoEP_Descriptor(sourceKind, entityKind),
            mul(unity_WorldToObject, float4(rootWorld, 1.0)).xyz,
            forwardLocal);
        bestDistanceSq = distanceSq;
    }
    return best;
}

PStoEPProviderContext PStoEP_LightProviderContext()
{
    PStoEPProviderContext context;
    float3 observerWorld = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
    context.entity0 = PStoEP_FindNearestClassicSocket(observerWorld, false);
    context.entity1 = PStoEP_InvalidEntity();
    return context;
}

#define PSTOEP_PROVIDER_V2F_FIELDS \
    nointerpolation uint4 pstoepEntity0Info : TEXCOORD1; \
    nointerpolation uint3 pstoepEntity0Position : TEXCOORD2; \
    nointerpolation uint4 pstoepEntity0Orientation : TEXCOORD3; \
    nointerpolation uint pstoepEntity0Scale : TEXCOORD4; \
    nointerpolation uint4 pstoepEntity1Info : TEXCOORD5; \
    nointerpolation uint3 pstoepEntity1Position : TEXCOORD6; \
    nointerpolation uint4 pstoepEntity1Orientation : TEXCOORD7; \
    nointerpolation uint pstoepEntity1Scale : TEXCOORD8;

#define PSTOEP_WRITE_ENTITY(output, prefix, entity) \
    output.prefix##Info = uint4(entity.descriptor, entity.ownerIdentity, entity.entityIdentity, entity.fields); \
    output.prefix##Position = entity.position; \
    output.prefix##Orientation = entity.orientation; \
    output.prefix##Scale = entity.scale;

#define PSTOEP_PROVIDER_VERTEX_PREPARE(output) \
    PStoEPProviderContext pstoepVertexContext = PStoEP_LightProviderContext(); \
    PSTOEP_WRITE_ENTITY(output, pstoepEntity0, pstoepVertexContext.entity0) \
    PSTOEP_WRITE_ENTITY(output, pstoepEntity1, pstoepVertexContext.entity1)

PStoEPEntity PStoEP_ReadEntity(uint4 info, uint3 position, uint4 orientation, uint scale)
{
    PStoEPEntity entity;
    entity.descriptor = info.x;
    entity.ownerIdentity = info.y;
    entity.entityIdentity = info.z;
    entity.fields = info.w;
    entity.position = position;
    entity.orientation = orientation;
    entity.scale = scale;
    return entity;
}

PStoEPProviderContext PStoEP_ProviderContextFromFields(
    uint4 entity0Info, uint3 entity0Position, uint4 entity0Orientation, uint entity0Scale,
    uint4 entity1Info, uint3 entity1Position, uint4 entity1Orientation, uint entity1Scale)
{
    PStoEPProviderContext context;
    context.entity0 = PStoEP_ReadEntity(entity0Info, entity0Position, entity0Orientation, entity0Scale);
    context.entity1 = PStoEP_ReadEntity(entity1Info, entity1Position, entity1Orientation, entity1Scale);
    return context;
}

#define PSTOEP_PROVIDER_CONTEXT_TYPE PStoEPProviderContext
#define PSTOEP_PROVIDER_CONTEXT_FROM_INPUT(input) PStoEP_ProviderContextFromFields( \
    input.pstoepEntity0Info, input.pstoepEntity0Position, input.pstoepEntity0Orientation, input.pstoepEntity0Scale, \
    input.pstoepEntity1Info, input.pstoepEntity1Position, input.pstoepEntity1Orientation, input.pstoepEntity1Scale)

#endif
