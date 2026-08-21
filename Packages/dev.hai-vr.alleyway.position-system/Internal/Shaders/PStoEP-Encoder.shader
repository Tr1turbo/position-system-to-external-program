// The encoder implementation is shared with the VRCFury SPS2 variant.
// Attribution and implementation notes live in PStoEP-EncoderCore.cginc.
Shader "Hai/PositionSystemToExternalProgram-Encoder"
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
            #define PSTOEP_PROTOCOL_VERSION 2000000u
            #include "PStoEP-Light.cginc"
            #include "PStoEP-EncoderCore.cginc"
            ENDCG
        }
    }
}
