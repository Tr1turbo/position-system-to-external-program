// Parts of this code are loosely based on this talk by cnlohr titled:
//     "Game Made for VR on a $1 Processor?", cnlohr, published on 2023-02-23
//     https://youtu.be/VnZac6lA1_k?t=828
//
// Although no code from the following link was directly used, this file and its repository was referenced during study:
// https://github.com/cnlohr/swadge-vrchat-bridge/blob/db33f403d3dcfe81524320bbf736a78e9c1a169d/bridgeapp/bridgeapp.c
//
// This file contains snippets from cnlohr/shadertrixx: https://github.com/cnlohr/shadertrixx/blob/main/LICENSE

#pragma vertex vert
#pragma fragment frag
#pragma target 4.0
#pragma multi_compile_instancing

#include "UnityCG.cginc"

#if PSTOEP_SPS2
    // These files are referenced from the end user's official VRCFury installation.
    // Position System does not redistribute or modify them.
    #include "Packages/com.vrcfury.vrcfury/SPS/common/sps_cell_layout.cginc"
    SPS_INIT_TEX(_VFGridFinal)
#endif

struct appdata
{
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float4 vertex : SV_POSITION;
    nointerpolation float4 sps2WorldPositionValid : TEXCOORD1;
    nointerpolation float4 sps2WorldForwardFlags : TEXCOORD2;
    nointerpolation float4 sps2WorldFrameUp : TEXCOORD3;
    nointerpolation uint sps2SocketIdentity : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct PStoEPSps2Target
{
    float3 worldPosition;
    float3 worldForward;
    float3 worldFrameUp;
    float worldScale;
    uint socketIdentity;
    uint socketFlags;
    bool sps2FrameValid;
    bool valid;
};

float _EncodedSquareSize;
float _IsTestScript;
uniform float _VRChatCameraMode;

// -----------------------------------------------------------------------------------------------------------------
// [[ BEGIN THIRD PARTY SECTION -- LICENSE ONLY APPLIES TO THIS SECTION ]]
// The following camera-detection helpers and mod implementation are based on cnlohr/shadertrixx.
//
// MIT License
//
// Copyright (c) 2021 cnlohr, et. al.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// From https://github.com/cnlohr/shadertrixx?tab=readme-ov-file#the-most-important-trick
#define glsl_mod(x,y) (((x)-(y)*floor((x)/(y))))

// From https://github.com/cnlohr/shadertrixx?tab=readme-ov-file#detecting-if-you-are-on-desktop-vr-camera-etc
bool isRightEye()
{
    #if defined(USING_STEREO_MATRICES)
        return unity_StereoEyeIndex == 1;
    #else
        return false;
    #endif
}

bool isMirror()
{
    // https://github.com/cnlohr/shadertrixx/blob/main/README.md#are-you-in-a-mirror
    return unity_CameraProjection[2][0] != 0.0 || unity_CameraProjection[2][1] != 0.0;
}
// [[ END THIRD PARTY SECTION ]]
// -----------------------------------------------------------------------------------------------------------------

static const uint VENDOR = 1366692562u;
static const uint VERSION = PSTOEP_PROTOCOL_VERSION;
static const uint CANARY = 1431677610u;
static const int GROUP_32 = 32;
static const int GROUP_Time = 1;
static const int GROUP_VendorCheck = 2;
static const int GROUP_VersionSemver = 3;
static const int GROUP_LightPositionStart = 4;
static const int GROUP_LightColorStart = 16;
static const int GROUP_LightAttenuationStart = 32;
static const int GROUP_CameraPositionStart = 36;
static const int GROUP_CameraRotationStart = 39;
static const int GROUP_Sps2SocketIdentity = 42;
static const int GROUP_Sps2ForwardStart = 43;
static const int GROUP_Sps2FrameUpStart = 46;
static const int GROUP_Sps2SocketFlags = 49;
static const int GROUP_Sps2WorldScale = 50;
static const int GROUP_Canary = 51;
static const int GROUP_LENGTH = 52;

static const int SERIALIZE_NumberOfColumns = 16;
static const int MARGIN = 1;
static const float GrayLevel = 0.5;
static const uint CRC32_POLYNOMIAL = 0xEDB88320u;

static const float SPS2_LEGACY_RANGE_HOLE = 0.4106;
static const float SPS2_LEGACY_RANGE_RING = 0.4206;
static const float SPS2_LEGACY_RANGE_FRONT = 0.4506;
static const float SPS2_LEGACY_FRONT_DISTANCE = 0.01;
static const uint PSTOEP_SPS2_SOCKET_FLAG_HOLE = 1u;

bool PStoEP_IsFiniteVector(float3 value)
{
    return all(value == value) && all(abs(value) < 1.0e20);
}

bool PStoEP_IsReasonableScale(float value)
{
    return value == value && value >= 1.0e-6 && value <= 1.0e6;
}

uint PStoEP_SocketIdentity(uint playerId, uint uniqueId)
{
    // Produce one opaque protocol identifier from SPS2's two-part identity.
    // Zero remains reserved for "no selected SPS2 socket".
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
    PStoEPSps2Target target = (PStoEPSps2Target)0;
    return target;
}

#if PSTOEP_SPS2
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
    float bestDistanceSq = 3.402823466e+38;

    // Follow VRCFury's raw-candidate enumeration pattern: atlas cell -1 is a
    // 16x16 dictionary whose pixels mark occupied groups of 16 socket slots.
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
            if (!PStoEP_IsFiniteVector(worldPosition)
                || !PStoEP_IsFiniteVector(worldForward)
                || !PStoEP_IsFiniteVector(worldFrameUp)) continue;

            float forwardLengthSq = dot(worldForward, worldForward);
            if (forwardLengthSq <= 0.000001) continue;
            worldForward *= rsqrt(forwardLengthSq);
            worldFrameUp -= worldForward * dot(worldFrameUp, worldForward);
            float upLengthSq = dot(worldFrameUp, worldFrameUp);
            if (upLengthSq <= 0.000001) continue;
            worldFrameUp *= rsqrt(upLengthSq);

            float3 offset = worldPosition - observerWorld;
            float distanceSq = dot(offset, offset);
            // This function is rerun for every encoder draw, so target selection has
            // no persistent identity: whichever valid socket is closest this frame wins.
            if (distanceSq >= bestDistanceSq) continue;

            best.worldPosition = worldPosition;
            best.worldForward = worldForward;
            best.worldFrameUp = worldFrameUp;
            best.worldScale = PStoEP_IsReasonableScale(worldScale) ? worldScale : 0;
            best.socketIdentity = PStoEP_SocketIdentity(
                sps_cell_header_player_id(cell),
                sps_cell_header_unique_id(cell)
            );
            best.socketFlags = cell.read_uint(
                sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_FLAGS)
            );
            // An invalid scale suppresses only the optional SPS2 frame fields.
            // The synthetic root/front representation remains valid and decodable.
            best.sps2FrameValid = PStoEP_IsReasonableScale(worldScale);
            best.valid = true;
            bestDistanceSq = distanceSq;
        }
    }
    return best;
}
#else
PStoEPSps2Target PStoEP_FindNearestSps2Socket()
{
    return PStoEP_EmptySps2Target();
}
#endif

void PStoEP_WriteSps2Varyings(PStoEPSps2Target target, inout v2f output)
{
    output.sps2WorldPositionValid = float4(target.worldPosition, target.valid ? 1.0 : 0.0);
    output.sps2WorldForwardFlags = float4(target.worldForward, (float)target.socketFlags);
    output.sps2WorldFrameUp = float4(target.worldFrameUp, target.sps2FrameValid ? target.worldScale : 0);
    output.sps2SocketIdentity = target.socketIdentity;
}

PStoEPSps2Target PStoEP_ReadSps2Varyings(v2f input)
{
    PStoEPSps2Target target = PStoEP_EmptySps2Target();
    target.worldPosition = input.sps2WorldPositionValid.xyz;
    target.worldForward = input.sps2WorldForwardFlags.xyz;
    target.worldFrameUp = input.sps2WorldFrameUp.xyz;
    target.worldScale = input.sps2WorldFrameUp.w;
    target.socketIdentity = input.sps2SocketIdentity;
    target.socketFlags = (uint)round(input.sps2WorldForwardFlags.w);
    target.valid = input.sps2WorldPositionValid.w > 0.5;
    target.sps2FrameValid = target.valid && PStoEP_IsReasonableScale(target.worldScale);
    return target;
}

float3 PStoEP_LocalForward(PStoEPSps2Target target)
{
    return normalize(mul((float3x3)unity_WorldToObject, target.worldForward));
}

float3 PStoEP_LocalFrameUp(PStoEPSps2Target target)
{
    float3 forward = PStoEP_LocalForward(target);
    float3 localFrameUp = mul((float3x3)unity_WorldToObject, target.worldFrameUp);
    return normalize(localFrameUp - forward * dot(localFrameUp, forward));
}

float3 PStoEP_UnityLightWorldPosition(uint index)
{
    return float3(unity_4LightPosX0[index], unity_4LightPosY0[index], unity_4LightPosZ0[index]);
}

float3 GetEncodedLightPosition(uint index, PStoEPSps2Target target)
{
#if PSTOEP_SPS2
    // The SPS2 encoder reserves only slots 0 and 1 for its compatibility
    // root/front pair. Slots 2 and 3 are always deterministic disabled zeros.
    if (index >= 2u) return 0;
#endif

    float3 worldPosition = PStoEP_UnityLightWorldPosition(index);
    if (target.valid)
    {
        if (index == 0u) worldPosition = target.worldPosition;
        else if (index == 1u)
        {
            // The desktop decoder uses normalize(root - front), so the synthetic
            // front marker is placed opposite the SPS socket's forward vector.
            worldPosition = target.worldPosition - target.worldForward * SPS2_LEGACY_FRONT_DISTANCE;
        }
    }
    return mul(unity_WorldToObject, float4(worldPosition, 1)).xyz;
}

float4 GetEncodedLightColor(uint index, PStoEPSps2Target target)
{
#if PSTOEP_SPS2
    if (index >= 2u) return 0;
#endif

    float4 color = unity_LightColor[index];
    if (!target.valid) return color;
    if (index < 2u) return float4(0, 0, 0, 1);
    return 0;
}

float UnityAttenuationFromRange(float range)
{
    return 25.0 / (range * range);
}

float GetEncodedLightAttenuation(uint index, PStoEPSps2Target target)
{
#if PSTOEP_SPS2
    if (index >= 2u) return 0;
#endif

    float attenuation = unity_4LightAtten0[index];
    if (!target.valid) return attenuation;
    if (index == 0u)
    {
        bool isHole = (target.socketFlags & PSTOEP_SPS2_SOCKET_FLAG_HOLE) != 0u;
        return UnityAttenuationFromRange(isHole ? SPS2_LEGACY_RANGE_HOLE : SPS2_LEGACY_RANGE_RING);
    }
    if (index == 1u) return UnityAttenuationFromRange(SPS2_LEGACY_RANGE_FRONT);
    return attenuation;
}

float3 GetUnityEulerAngles(float3x3 rotMatrix)
{
    float3 euler;
    float sinX = clamp(rotMatrix[1][2], -1.0, 1.0);
    euler.x = asin(sinX);
    if (abs(rotMatrix[1][2]) < 0.99999)
    {
        euler.y = atan2(rotMatrix[0][2], rotMatrix[2][2]);
        euler.z = atan2(rotMatrix[1][0], rotMatrix[1][1]);
    }
    else
    {
        euler.y = atan2(-rotMatrix[2][0], rotMatrix[0][0]);
        euler.z = 0.0;
    }
    euler = degrees(euler);
    // The matrix-to-Euler conversion above still needs this correction to match
    // Unity's reported camera orientation. Keep this explicit until it is replaced
    // with a conversion that produces Unity's convention directly.
    euler.y -= 180.0;
    return euler;
}

uint NthBit(uint value, int bit)
{
    return value & (uint)(1 << bit);
}

uint getData(float groupY, PStoEPSps2Target target)
{
    if (groupY < GROUP_Time) return 0u;
    if (groupY < GROUP_VendorCheck) return asuint((float)_Time);
    // Vendor and version are integers. Casting either to float would lose bits.
    if (groupY < GROUP_VersionSemver) return VENDOR;
    if (groupY < GROUP_LightPositionStart) return VERSION;
    if (groupY < GROUP_LightColorStart)
    {
        uint lightIndex = (uint)floor((groupY - GROUP_LightPositionStart) / 3);
        float3 position = GetEncodedLightPosition(lightIndex, target);
        return asuint(position[(uint)glsl_mod(groupY - GROUP_LightPositionStart, 3)]);
    }
    if (groupY < GROUP_LightAttenuationStart)
    {
        uint lightIndex = (uint)floor((groupY - GROUP_LightColorStart) / 4);
        float4 color = GetEncodedLightColor(lightIndex, target);
        return asuint(color[(uint)glsl_mod(groupY - GROUP_LightColorStart, 4)]);
    }
    if (groupY < GROUP_CameraPositionStart)
    {
        return asuint(GetEncodedLightAttenuation((uint)(groupY - GROUP_LightAttenuationStart), target));
    }
    if (groupY < GROUP_CameraRotationStart)
    {
        return asuint(_WorldSpaceCameraPos[(uint)(groupY - GROUP_CameraPositionStart)]);
    }
    if (groupY < GROUP_Sps2SocketIdentity)
    {
        float3 euler = GetUnityEulerAngles((float3x3)UNITY_MATRIX_I_V);
        return asuint(euler[(uint)(groupY - GROUP_CameraRotationStart)]);
    }
    if (groupY < GROUP_Sps2ForwardStart)
    {
        return target.sps2FrameValid ? target.socketIdentity : 0u;
    }
    if (groupY < GROUP_Sps2FrameUpStart)
    {
        float3 forward = target.sps2FrameValid ? PStoEP_LocalForward(target) : 0;
        return asuint(forward[(uint)(groupY - GROUP_Sps2ForwardStart)]);
    }
    if (groupY < GROUP_Sps2SocketFlags)
    {
        float3 frameUp = target.sps2FrameValid ? PStoEP_LocalFrameUp(target) : 0;
        return asuint(frameUp[(uint)(groupY - GROUP_Sps2FrameUpStart)]);
    }
    if (groupY < GROUP_Sps2WorldScale) return target.sps2FrameValid ? target.socketFlags : 0u;
    if (groupY < GROUP_Canary) return target.sps2FrameValid ? asuint(target.worldScale) : 0u;
    if (groupY < GROUP_LENGTH) return CANARY;
    return 0u;
}

uint CRC32UpdateByte(uint crc, uint byteValue)
{
    uint temporary = crc ^ byteValue;
    for (int i = 0; i < 8; i++)
    {
        temporary = (temporary & 1u) != 0u
            ? (temporary >> 1) ^ CRC32_POLYNOMIAL
            : temporary >> 1;
    }
    return temporary;
}

uint CRC32UpdateUint(uint crc, uint value)
{
    crc = CRC32UpdateByte(crc, value & 0xffu);
    crc = CRC32UpdateByte(crc, (value >> 8) & 0xffu);
    crc = CRC32UpdateByte(crc, (value >> 16) & 0xffu);
    return CRC32UpdateByte(crc, (value >> 24) & 0xffu);
}

v2f vert(appdata input)
{
    v2f output = (v2f)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // The mesh has four vertices, all with a different vertex color.
    if (input.color.r > 0) output.uv = float2(0, 0);
    else if (input.color.g > 0) output.uv = float2(1, 0);
    else if (input.color.b > 0) output.uv = float2(1, 1);
    else output.uv = float2(0, 1);

    #if defined(USING_STEREO_MATRICES)
        float yShift = 0.5;
        float relativeY2 = _ScreenParams.y / 1000;
    #else
        float yShift = 0.0;
        float relativeY2 = 2;
    #endif

    float makeBigger = _IsTestScript > 0.5 ? 10 : 1;
    float lineCount = ceil((GROUP_LENGTH * 32.0) / SERIALIZE_NumberOfColumns);
    float relativeX = makeBigger * (SERIALIZE_NumberOfColumns + MARGIN * 2)
        * _EncodedSquareSize / _ScreenParams.x * relativeY2;
    float relativeY = makeBigger * (lineCount + MARGIN * 2)
        * _EncodedSquareSize / _ScreenParams.y * relativeY2;

    // Make the geometry screen-relative and place it as close to the camera as possible.
    output.vertex = float4(output.uv.x * relativeX, output.uv.y * relativeY, UNITY_NEAR_CLIP_VALUE, 1);
    output.vertex += float4(-1, (yShift - 0.5) * 2, 0, 0);
    output.vertex.y -= relativeY * yShift;
    output.uv = output.uv * float2(SERIALIZE_NumberOfColumns + MARGIN * 2, lineCount + MARGIN * 2)
        - float2(MARGIN, MARGIN);
    PStoEP_WriteSps2Varyings(PStoEP_FindNearestSps2Socket(), output);
    return output;
}

fixed4 frag(v2f input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    PStoEPSps2Target target = PStoEP_ReadSps2Varyings(input);

    #if defined(USING_STEREO_MATRICES)
        // Only the left eye carries the packet.
        if (isRightEye()) clip(-1);
    #else
        // Square desktop renders are usually avatar thumbnails or other cameras
        // where the packet should not be exposed. VR handheld cameras are allowed.
        if (_VRChatCameraMode != 1 && _ScreenParams.x == _ScreenParams.y) clip(-1);
    #endif
    if (isMirror()) clip(-1);

    float lineCount = ceil((GROUP_LENGTH * 32.0) / SERIALIZE_NumberOfColumns);
    // Black margins prevent neighboring scene pixels from contaminating samples.
    // Packet pixels deliberately use GrayLevel instead of full white to limit bloom.
    // The decoder also expects brightness to vary because transparency or other
    // shader effects can still draw over this screen region in some worlds.
    if (input.uv.x < 0 || input.uv.x >= SERIALIZE_NumberOfColumns
        || input.uv.y < 0 || input.uv.y >= lineCount)
    {
        // Negative output consumes bloom and remains black after post-processing.
        return half4(-10000, -10000, -10000, 1);
    }

    float2 serialized = floor(input.uv);
    int bitOffset = (int)floor(serialized.y * SERIALIZE_NumberOfColumns + serialized.x);
    float2 group = floor(float2(glsl_mod(bitOffset, GROUP_32), bitOffset / GROUP_32));
    uint data;
    if (group.y < GROUP_Time)
    {
        // Word 0 is the CRC of words 1 through 51. getData cannot recursively
        // produce its own checksum, so the checksum word is handled here.
        uint crc = 0xffffffffu;
        for (int word = GROUP_Time; word < GROUP_LENGTH; word++)
        {
            crc = CRC32UpdateUint(crc, getData(word, target));
        }
        data = crc ^ 0xffffffffu;
    }
    else
    {
        data = getData(group.y, target);
    }

    return NthBit(data, group.x) != 0u
        ? half4(GrayLevel, GrayLevel, GrayLevel, 1)
        : half4(-10000, -10000, -10000, 1);
}
