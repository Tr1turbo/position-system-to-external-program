#ifndef PSTOEP_LIGHT_INCLUDED
#define PSTOEP_LIGHT_INCLUDED

#include "UnityCG.cginc"

static const float PSTOEP_LIGHT_RANGE_HOLE = 0.41;
static const float PSTOEP_LIGHT_RANGE_RING = 0.42;
static const float PSTOEP_LIGHT_RANGE_TOLERANCE = 0.005;

float3 PStoEP_LightWorldPosition(int index)
{
    return float3(unity_4LightPosX0[index], unity_4LightPosY0[index], unity_4LightPosZ0[index]);
}

float3 PStoEP_LightLocalPosition(int index)
{
    return mul(unity_WorldToObject, float4(PStoEP_LightWorldPosition(index), 1)).xyz;
}

float4 PStoEP_LightColor(int index)
{
    return unity_LightColor[index];
}

float PStoEP_LightAttenuation(int index)
{
    return unity_4LightAtten0[index];
}

float PStoEP_LightDecodedRange(int index)
{
    float attenuation = PStoEP_LightAttenuation(index);
    float range = 1.0;
    if (attenuation > 0.0 && attenuation <= 1000000.0)
    {
        range = 0.005 * sqrt(1000000.0 - attenuation) / sqrt(attenuation);
    }
    return range;
}

bool PStoEP_LightIsSps2CompatibilityMarker(int index)
{
    // VRCFury gives the real black compatibility lights emitted beside an
    // SPS2 atlas socket a range suffix of .0005 through .0007. They remain
    // valid raw light data, but must not become independent legacy candidates
    // and compete with the authoritative SPS2 socket that emitted them.
    float attenuation = PStoEP_LightAttenuation(index);
    if (attenuation <= 0.0) return false;

    // Use Unity's direct point-light range decode for this exact marker test,
    // matching VRCFury. The historical desktop range reconstruction above is
    // intentionally retained for ordinary legacy socket classification.
    float range = 5.0 * rsqrt(attenuation);
    int secondDecimal = (int)round(fmod(range, 0.1) * 100.0);
    if (secondDecimal != 1 && secondDecimal != 2 && secondDecimal != 5) return false;

    float fourthDecimal = fmod(range, 0.001) * 10000.0;
    return fourthDecimal >= 5.0 && fourthDecimal <= 7.0;
}

bool PStoEP_LightIsLegacySocketRoot(int index)
{
    float4 color = PStoEP_LightColor(index);
    if (color.a <= 0.0 || any(color.rgb != 0.0)) return false;

    float range = PStoEP_LightDecodedRange(index);
    if (PStoEP_LightIsSps2CompatibilityMarker(index)) return false;

    return abs(range - PSTOEP_LIGHT_RANGE_HOLE) < PSTOEP_LIGHT_RANGE_TOLERANCE
        || abs(range - PSTOEP_LIGHT_RANGE_RING) < PSTOEP_LIGHT_RANGE_TOLERANCE;
}

float PStoEP_NearestLegacySocketDistanceSq(float3 observerWorld)
{
    float bestDistanceSq = 3.402823466e+38;
    [unroll]
    for (int lightIndex = 0; lightIndex < 4; lightIndex++)
    {
        if (!PStoEP_LightIsLegacySocketRoot(lightIndex)) continue;

        float3 offset = PStoEP_LightWorldPosition(lightIndex) - observerWorld;
        bestDistanceSq = min(bestDistanceSq, dot(offset, offset));
    }
    return bestDistanceSq;
}

#define PSTOEP_PROVIDER_V2F_FIELDS
#define PSTOEP_PROVIDER_VERTEX_PREPARE(output)
#define PSTOEP_PROVIDER_CONTEXT_TYPE uint
#define PSTOEP_PROVIDER_CONTEXT_FROM_INPUT(input) 0u
#define PSTOEP_PROVIDER_LIGHT_POSITION(index, context) PStoEP_LightLocalPosition(index)
#define PSTOEP_PROVIDER_LIGHT_COLOR(index, context) PStoEP_LightColor(index)
#define PSTOEP_PROVIDER_LIGHT_ATTENUATION(index, context) PStoEP_LightAttenuation(index)
#define PSTOEP_PROVIDER_EXTENSION_DATA(wordIndex, context) 0u

#endif
