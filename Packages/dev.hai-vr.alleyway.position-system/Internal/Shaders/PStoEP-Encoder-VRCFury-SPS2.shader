// The encoder implementation is shared with the legacy-light-only variant.
// Attribution and implementation notes live in PStoEP-EncoderCore.cginc.
Shader "Hai/PositionSystemToExternalProgram-Encoder-VRCFury-SPS2"
{
    Properties
    {
        _EncodedSquareSize("Encoded Square Size", Float) = 4.0
        _IsTestScript("Force draw in test script", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest+105" }
        LOD 100

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ZTest Always // Try to draw even in the VR mask.
            Cull Off

            CGPROGRAM
            #pragma exclude_renderers metal
            #define PSTOEP_SPS2 1
            #define PSTOEP_PROTOCOL_VERSION 1002000u
            #include "PStoEP-EncoderCore.cginc"
            ENDCG
        }
    }
}
