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

struct appdata
{
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float4 vertex : SV_POSITION;
    PSTOEP_PROVIDER_V2F_FIELDS
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float _EncodedSquareSize;
float _IsTestScript;
uniform float _VRChatCameraMode;
uniform float _VRChatMirrorMode;

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
    // VRChat sets _VRChatMirrorMode on its mirror camera passes. The oblique-projection
    // check below does not detect VRChat's mirror, so without this clip VRChat's mirror
    // pass re-draws the encoded data over the HMD draw's encoded data.
    if (_VRChatMirrorMode != 0.0) return true;
    // https://github.com/cnlohr/shadertrixx/blob/main/README.md#are-you-in-a-mirror
    return unity_CameraProjection[2][0] != 0.0 || unity_CameraProjection[2][1] != 0.0;
}
// [[ END THIRD PARTY SECTION ]]
// -----------------------------------------------------------------------------------------------------------------

static const uint VENDOR = 1366692562u;
static const uint VERSION = PSTOEP_PROTOCOL_VERSION;
static const uint CANARY = 1431677610u;
static const int GROUP_Time = 1;
static const int GROUP_VendorCheck = 2;
static const int GROUP_VersionSemver = 3;
static const int GROUP_PresenceMask = 4;
static const int GROUP_CameraPositionStart = 5;
static const int GROUP_CameraEulerStart = 8;
static const int GROUP_Entity0Start = 11;
static const int GROUP_Entity1Start = 27;
static const int GROUP_ReservedStart = 43;
static const int GROUP_Canary = 51;
static const int GROUP_LENGTH = 52;

static const int SERIALIZE_NumberOfColumns = 16;
static const uint SERIALIZE_BitsPerWord = 32u;
static const uint SERIALIZE_WordShift = 5u; // 32 = 2^5
static const uint SERIALIZE_BitIndexMask = SERIALIZE_BitsPerWord - 1u;
static const int MARGIN = 1;
static const float GrayLevel = 0.5;
static const uint CRC32_POLYNOMIAL = 0xEDB88320u;

float3 PStoEP_GetUnityEulerAngles(float3x3 rotMatrix)
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
    euler.y -= 180.0;
    return euler;
}

uint PStoEP_PresenceMask(PSTOEP_PROVIDER_CONTEXT_TYPE context)
{
    uint mask = context.entity0.fields;
    mask |= context.entity1.fields << 6u;
    if (PStoEP_IsFinite3(_WorldSpaceCameraPos)) mask |= 1u << 12u;
    float3 cameraEuler = PStoEP_GetUnityEulerAngles((float3x3)UNITY_MATRIX_I_V);
    if (PStoEP_IsFinite3(cameraEuler)) mask |= 1u << 13u;
    return mask;
}

uint NthBit(uint value, uint bit)
{
    return value & (1u << bit);
}

uint getData(int wordIndex, PSTOEP_PROVIDER_CONTEXT_TYPE providerContext)
{
    if (wordIndex < GROUP_Time) return 0u;
    if (wordIndex < GROUP_VendorCheck) return asuint((float)_Time);
    if (wordIndex < GROUP_VersionSemver) return VENDOR;
    if (wordIndex < GROUP_PresenceMask) return VERSION;
    if (wordIndex < GROUP_CameraPositionStart) return PStoEP_PresenceMask(providerContext);
    if (wordIndex < GROUP_CameraEulerStart)
    {
        return PStoEP_IsFinite3(_WorldSpaceCameraPos)
            ? asuint(_WorldSpaceCameraPos[wordIndex - GROUP_CameraPositionStart])
            : PSTOEP_CANONICAL_NAN;
    }
    if (wordIndex < GROUP_Entity0Start)
    {
        float3 euler = PStoEP_GetUnityEulerAngles((float3x3)UNITY_MATRIX_I_V);
        return PStoEP_IsFinite3(euler)
            ? asuint(euler[wordIndex - GROUP_CameraEulerStart])
            : PSTOEP_CANONICAL_NAN;
    }
    if (wordIndex < GROUP_Entity1Start)
        return PStoEP_EntityWord(providerContext.entity0, (uint)(wordIndex - GROUP_Entity0Start));
    if (wordIndex < GROUP_ReservedStart)
        return PStoEP_EntityWord(providerContext.entity1, (uint)(wordIndex - GROUP_Entity1Start));
    if (wordIndex < GROUP_Canary) return 0u;
    if (wordIndex < GROUP_LENGTH) return CANARY;
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
    float lineCount = ceil(
        (GROUP_LENGTH * (float)SERIALIZE_BitsPerWord) / SERIALIZE_NumberOfColumns
    );
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
    PSTOEP_PROVIDER_VERTEX_PREPARE(output)
    return output;
}

fixed4 frag(v2f input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    PSTOEP_PROVIDER_CONTEXT_TYPE providerContext = PSTOEP_PROVIDER_CONTEXT_FROM_INPUT(input);

    #if defined(USING_STEREO_MATRICES)
        // Only the left eye carries the encoded data.
        if (isRightEye()) clip(-1);
    #else
        // Square desktop renders are usually avatar thumbnails or other cameras
        // where the encoded data should not be exposed. VR handheld cameras are allowed.
        if (_VRChatCameraMode != 1 && _ScreenParams.x == _ScreenParams.y) clip(-1);
    #endif
    if (isMirror()) clip(-1);

    float lineCount = ceil(
        (GROUP_LENGTH * (float)SERIALIZE_BitsPerWord) / SERIALIZE_NumberOfColumns
    );
    // Black margins prevent neighboring scene pixels from contaminating samples.
    // Encoded pixels deliberately use GrayLevel instead of full white to limit bloom.
    // The decoder also expects brightness to vary because transparency or other
    // shader effects can still draw over this screen region in some worlds.
    if (input.uv.x < 0 || input.uv.x >= SERIALIZE_NumberOfColumns
        || input.uv.y < 0 || input.uv.y >= lineCount)
    {
        // Negative output consumes bloom and remains black after post-processing.
        return half4(-10000, -10000, -10000, 1);
    }

    float2 serialized = floor(input.uv);
    uint bitOffset = (uint)floor(serialized.y * SERIALIZE_NumberOfColumns + serialized.x);
    // bitOffset is an integer in [0, GROUP_LENGTH * SERIALIZE_BitsPerWord - 1].
    uint bitIndex = bitOffset & SERIALIZE_BitIndexMask; // glsl_mod(bitOffset, 32)
    uint wordIndex = bitOffset >> SERIALIZE_WordShift; // floor(bitOffset / 32.0)
    uint data;
    if (wordIndex < (uint)GROUP_Time)
    {
        // Word 0 is the CRC of words 1 through 51. getData cannot recursively
        // produce its own checksum, so the checksum word is handled here.
        uint crc = 0xffffffffu;
        for (int word = GROUP_Time; word < GROUP_LENGTH; word++)
        {
            crc = CRC32UpdateUint(crc, getData(word, providerContext));
        }
        data = crc ^ 0xffffffffu;
    }
    else
    {
        data = getData((int)wordIndex, providerContext);
    }

    return NthBit(data, bitIndex) != 0u
        ? half4(GrayLevel, GrayLevel, GrayLevel, 1)
        : half4(-10000, -10000, -10000, 1);
}
