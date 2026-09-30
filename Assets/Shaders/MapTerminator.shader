Shader "MinistryOfPower/MapTerminator"
{
    Properties
    {
        _NightColor ("Night Color", Color) = (0.02, 0.04, 0.10, 1)
        _NightAlpha ("Night Alpha", Range(0, 1)) = 0.72
        _Softness ("Edge Softness", Range(0.02, 0.45)) = 0.14
        _DayFraction ("Day Fraction", Range(0, 1)) = 0.35
        _SeasonTilt ("Season Tilt (rad)", Float) = 0
        _LonSpan ("Longitude Span", Range(0.3, 2.5)) = 1.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "MapTerminator"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _NightColor;
                float _NightAlpha;
                float _Softness;
                float _DayFraction;
                float _SeasonTilt;
                float _LonSpan;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv - 0.5;
                float c = cos(_SeasonTilt);
                float s = sin(_SeasonTilt);
                // Tilted "east" axis — modest seasonal N–S lean on the terminator.
                float along = p.x * c - p.y * s;

                // Sun longitude: overhead (0) at noon, ±π at midnight.
                float sunLon = (0.5 - _DayFraction) * 6.2831853;
                float mapLon = along * _LonSpan;
                float dayness = cos(mapLon - sunLon);

                float soft = max(_Softness, 0.02);
                float night = 1.0 - smoothstep(-soft, soft, dayness);
                float alpha = saturate(night * _NightAlpha);

                return half4(_NightColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
