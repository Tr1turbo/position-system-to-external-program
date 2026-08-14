#ifndef PSTOEP_SPS2_INCLUDED
#define PSTOEP_SPS2_INCLUDED

#include "PStoEP-Light.cginc"
#include "Packages/com.vrcfury.vrcfury/SPS/common/sps_cell_layout.cginc"
SPS_INIT_TEX(_VFGridFinal)

struct PStoEPSps2Target
{
    float3 worldPosition;
    float3 worldForward;
    float3 worldFrameUp;
    float worldScale;
    uint socketIdentity;
    uint socketFlags;
    bool frameValid;
    bool valid;
};

static const float PSTOEP_SPS2_RANGE_HOLE = 0.4106;
static const float PSTOEP_SPS2_RANGE_RING = 0.4206;
static const float PSTOEP_SPS2_RANGE_FRONT = 0.4506;
static const float PSTOEP_SPS2_FRONT_DISTANCE = 0.01;
static const uint PSTOEP_SPS2_SOCKET_FLAG_HOLE = 1u;
static const int PSTOEP_SPS2_GROUP_SOCKET_IDENTITY = 42;
static const int PSTOEP_GROUP_NORMAL_START = 43;
static const int PSTOEP_GROUP_TANGENT_START = 46;
static const int PSTOEP_SPS2_GROUP_SOCKET_FLAGS = 49;
static const int PSTOEP_SPS2_GROUP_WORLD_SCALE = 50;
static const int PSTOEP_SPS2_GROUP_END = 51;

bool PStoEP_Sps2IsFiniteVector(float3 value)
{
    return all(value == value) && all(abs(value) < 1.0e20);
}

bool PStoEP_Sps2IsReasonableScale(float value)
{
    return value == value && value >= 1.0e-6 && value <= 1.0e6;
}

uint PStoEP_Sps2SocketIdentity(uint playerId, uint uniqueId)
{
    uint identity = playerId ^ (uniqueId + 0x9e3779b9u + (playerId << 6u) + (playerId >> 2u));
    identity ^= identity >> 16u;
    identity *= 0x7feb352du;
    identity ^= identity >> 15u;
    identity *= 0x846ca68bu;
    identity ^= identity >> 16u;
    return identity != 0u ? identity : 1u;
}

PStoEPSps2Target PStoEP_EmptySps2Target()
{
    return (PStoEPSps2Target)0;
}

PStoEPSps2Target PStoEP_FindNearestSps2Socket()
{
    PStoEPSps2Target best = PStoEP_EmptySps2Target();
    SpsTexture tex = SPS_GET_TEX(_VFGridFinal);
    uint slotCount = sps_socket_slot_count();
    uint groupCount = min(
        (uint)SPS_CELL_DICTIONARY_GROUP_COUNT,
        (slotCount + (uint)SPS_CELL_DICTIONARY_GROUP_SIZE - 1u) / (uint)SPS_CELL_DICTIONARY_GROUP_SIZE
    );
    float3 observerWorld = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
    // Genuine legacy roots compete with SPS2 socket cells by world-space
    // distance. SPS2-marked compatibility lights are excluded by our light
    // parser so they cannot compete with their own authoritative atlas cell.
    // SPS2 wins an exact tie because it carries the richer native frame data.
    float legacyDistanceSq = PStoEP_NearestLegacySocketDistanceSq(observerWorld);
    float bestSps2DistanceSq = 3.402823466e+38;

    [loop]
    for (uint group = 0u; group < groupCount; group++)
    {
        bool groupUsed = all(SPS_READ_TEX(
            tex,
            uint2(
                group % (uint)SPS_CELL_DICTIONARY_GROUP_SIZE,
                group / (uint)SPS_CELL_DICTIONARY_GROUP_SIZE
            )
        ) == SPS_CELL_DICTIONARY_MAGIC);
        if (!groupUsed) continue;

        uint startIndex = group * (uint)SPS_CELL_DICTIONARY_GROUP_SIZE;
        [loop]
        for (uint groupMember = 0u; groupMember < (uint)SPS_CELL_DICTIONARY_GROUP_SIZE; groupMember++)
        {
            uint cellIndex = startIndex + groupMember;
            if (cellIndex >= slotCount) continue;

            SpsCell cell = sps_get_cell(tex, (int)cellIndex);
            if (!sps_cell_check_magic(cell)) continue;
            if (cell.read_uint(SPS_HEADER_VENDOR_INDEX) != SPS_VENDOR_SPS) continue;
            if (cell.read_uint(SPS_HEADER_PRODUCT_INDEX) != SPS_PRODUCT_SOCKET) continue;
            if (cell.read_uint(SPS_HEADER_VERSION_INDEX) != SPS_VERSION_SPS) continue;

            float3 worldPosition = sps_cell_header_world(cell);
            float3 worldForward = sps_cell_header_forward(cell);
            float3 worldFrameUp = sps_cell_header_up(cell);
            float worldScale = sps_cell_header_scale(cell);
            if (!PStoEP_Sps2IsFiniteVector(worldPosition)
                || !PStoEP_Sps2IsFiniteVector(worldForward)
                || !PStoEP_Sps2IsFiniteVector(worldFrameUp)) continue;

            float forwardLengthSq = dot(worldForward, worldForward);
            if (forwardLengthSq <= 0.000001) continue;
            worldForward *= rsqrt(forwardLengthSq);
            worldFrameUp -= worldForward * dot(worldFrameUp, worldForward);
            float upLengthSq = dot(worldFrameUp, worldFrameUp);
            if (upLengthSq <= 0.000001) continue;
            worldFrameUp *= rsqrt(upLengthSq);

            float3 offset = worldPosition - observerWorld;
            float distanceSq = dot(offset, offset);
            if (distanceSq > legacyDistanceSq) continue;
            if (distanceSq >= bestSps2DistanceSq) continue;

            best.worldPosition = worldPosition;
            best.worldForward = worldForward;
            best.worldFrameUp = worldFrameUp;
            best.worldScale = PStoEP_Sps2IsReasonableScale(worldScale) ? worldScale : 0;
            best.socketIdentity = PStoEP_Sps2SocketIdentity(
                sps_cell_header_player_id(cell),
                sps_cell_header_unique_id(cell)
            );
            best.socketFlags = cell.read_uint(
                sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_FLAGS)
            );
            best.frameValid = PStoEP_Sps2IsReasonableScale(worldScale);
            best.valid = true;
            bestSps2DistanceSq = distanceSq;
        }
    }
    return best;
}

PStoEPSps2Target PStoEP_ReadSps2Target(
    float4 worldPositionValid,
    float4 worldForwardFlags,
    float4 worldFrameUp,
    uint socketIdentity)
{
    PStoEPSps2Target target = PStoEP_EmptySps2Target();
    target.worldPosition = worldPositionValid.xyz;
    target.worldForward = worldForwardFlags.xyz;
    target.worldFrameUp = worldFrameUp.xyz;
    target.worldScale = worldFrameUp.w;
    target.socketIdentity = socketIdentity;
    target.socketFlags = (uint)round(worldForwardFlags.w);
    target.valid = worldPositionValid.w > 0.5;
    target.frameValid = target.valid && PStoEP_Sps2IsReasonableScale(target.worldScale);
    return target;
}

float3 PStoEP_Sps2LocalNormal(PStoEPSps2Target target)
{
    // SPS2 sockets face along local +Z, while the Position System normal is
    // defined by normalize(root - front). VRCFury's compatibility front light
    // sits on socket +Z, so the equivalent Position System normal is -forward.
    return -normalize(mul((float3x3)unity_WorldToObject, target.worldForward));
}

float3 PStoEP_Sps2LocalTangent(PStoEPSps2Target target)
{
    float3 normal = PStoEP_Sps2LocalNormal(target);
    float3 localFrameUp = mul((float3x3)unity_WorldToObject, target.worldFrameUp);
    return normalize(localFrameUp - normal * dot(localFrameUp, normal));
}

float3 PStoEP_Sps2LightPosition(int index, PStoEPSps2Target target)
{
    // The SPS2 packet reserves slots 0 and 1 for the synthetic root/front pair.
    // Without a selected SPS2 socket, all four slots use the ordinary light
    // provider so legacy light targets remain fully compatible.
    float3 localPosition = float3(0, 0, 0);
    if (!target.valid)
    {
        localPosition = PStoEP_LightLocalPosition(index);
    }
    else if (index == 0)
    {
        localPosition = mul(unity_WorldToObject, float4(target.worldPosition, 1)).xyz;
    }
    else if (index == 1)
    {
        float3 frontWorldPosition = target.worldPosition
            + target.worldForward * PSTOEP_SPS2_FRONT_DISTANCE;
        localPosition = mul(unity_WorldToObject, float4(frontWorldPosition, 1)).xyz;
    }
    return localPosition;
}

float4 PStoEP_Sps2LightColor(int index, PStoEPSps2Target target)
{
    float4 color = float4(0, 0, 0, 0);
    if (!target.valid)
    {
        color = PStoEP_LightColor(index);
    }
    else if (index < 2)
    {
        color = float4(0, 0, 0, 1);
    }
    return color;
}

float PStoEP_Sps2AttenuationFromRange(float range)
{
    return 25.0 / (range * range);
}

float PStoEP_Sps2LightAttenuation(int index, PStoEPSps2Target target)
{
    float attenuation = 0;
    if (!target.valid)
    {
        attenuation = PStoEP_LightAttenuation(index);
    }
    else if (index == 0)
    {
        bool isHole = (target.socketFlags & PSTOEP_SPS2_SOCKET_FLAG_HOLE) != 0u;
        float range = isHole ? PSTOEP_SPS2_RANGE_HOLE : PSTOEP_SPS2_RANGE_RING;
        attenuation = PStoEP_Sps2AttenuationFromRange(range);
    }
    else if (index == 1)
    {
        attenuation = PStoEP_Sps2AttenuationFromRange(PSTOEP_SPS2_RANGE_FRONT);
    }
    return attenuation;
}

uint PStoEP_Sps2ExtensionData(int wordIndex, PStoEPSps2Target target)
{
    uint extensionData = 0u;
    if (target.frameValid && wordIndex < PSTOEP_GROUP_NORMAL_START)
    {
        if (wordIndex >= PSTOEP_SPS2_GROUP_SOCKET_IDENTITY)
        {
            extensionData = target.socketIdentity;
        }
    }
    else if (target.frameValid && wordIndex < PSTOEP_GROUP_TANGENT_START)
    {
        float3 normal = PStoEP_Sps2LocalNormal(target);
        extensionData = asuint(normal[wordIndex - PSTOEP_GROUP_NORMAL_START]);
    }
    else if (target.frameValid && wordIndex < PSTOEP_SPS2_GROUP_SOCKET_FLAGS)
    {
        float3 tangent = PStoEP_Sps2LocalTangent(target);
        extensionData = asuint(tangent[wordIndex - PSTOEP_GROUP_TANGENT_START]);
    }
    else if (target.frameValid && wordIndex < PSTOEP_SPS2_GROUP_WORLD_SCALE)
    {
        extensionData = target.socketFlags;
    }
    else if (target.frameValid && wordIndex < PSTOEP_SPS2_GROUP_END)
    {
        extensionData = asuint(target.worldScale);
    }
    return extensionData;
}

#undef PSTOEP_PROVIDER_V2F_FIELDS
#undef PSTOEP_PROVIDER_VERTEX_PREPARE
#undef PSTOEP_PROVIDER_CONTEXT_TYPE
#undef PSTOEP_PROVIDER_CONTEXT_FROM_INPUT
#undef PSTOEP_PROVIDER_LIGHT_POSITION
#undef PSTOEP_PROVIDER_LIGHT_COLOR
#undef PSTOEP_PROVIDER_LIGHT_ATTENUATION
#undef PSTOEP_PROVIDER_EXTENSION_DATA

#define PSTOEP_PROVIDER_V2F_FIELDS \
    nointerpolation float4 sps2WorldPositionValid : TEXCOORD1; \
    nointerpolation float4 sps2WorldForwardFlags : TEXCOORD2; \
    nointerpolation float4 sps2WorldFrameUp : TEXCOORD3; \
    nointerpolation uint sps2SocketIdentity : TEXCOORD4;

#define PSTOEP_PROVIDER_VERTEX_PREPARE(output) \
    PStoEPSps2Target pstoepSps2VertexTarget = PStoEP_FindNearestSps2Socket(); \
    output.sps2WorldPositionValid = float4(pstoepSps2VertexTarget.worldPosition, pstoepSps2VertexTarget.valid ? 1.0 : 0.0); \
    output.sps2WorldForwardFlags = float4(pstoepSps2VertexTarget.worldForward, (float)pstoepSps2VertexTarget.socketFlags); \
    output.sps2WorldFrameUp = float4(pstoepSps2VertexTarget.worldFrameUp, pstoepSps2VertexTarget.frameValid ? pstoepSps2VertexTarget.worldScale : 0); \
    output.sps2SocketIdentity = pstoepSps2VertexTarget.socketIdentity;

#define PSTOEP_PROVIDER_CONTEXT_TYPE PStoEPSps2Target
#define PSTOEP_PROVIDER_CONTEXT_FROM_INPUT(input) PStoEP_ReadSps2Target( \
        input.sps2WorldPositionValid, \
        input.sps2WorldForwardFlags, \
        input.sps2WorldFrameUp, \
        input.sps2SocketIdentity \
    )

#define PSTOEP_PROVIDER_LIGHT_POSITION(index, context) PStoEP_Sps2LightPosition(index, context)
#define PSTOEP_PROVIDER_LIGHT_COLOR(index, context) PStoEP_Sps2LightColor(index, context)
#define PSTOEP_PROVIDER_LIGHT_ATTENUATION(index, context) PStoEP_Sps2LightAttenuation(index, context)
#define PSTOEP_PROVIDER_EXTENSION_DATA(wordIndex, context) PStoEP_Sps2ExtensionData(wordIndex, context)

#endif
