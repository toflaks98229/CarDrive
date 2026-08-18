// PSX 시절 3D 게임의 지면 느낌을 내는 터레인 셰이더입니다.
//
// 그 시절 하드웨어가 "못 해서" 생긴 특징들을 일부러 되살립니다.
//
//  1. 정점 떨림 — 화면 좌표를 낮은 해상도 격자에 맞춰 끊어 버립니다.
//     PSX에는 서브픽셀 정밀도가 없어서 정점이 격자에 붙었고, 그래서 카메라가
//     움직일 때 지면이 자글자글 흔들렸습니다.
//
//  2. 텍스처 뒤틀림 — 원근 보정 없이 화면 공간에서 선형으로 보간합니다.
//     PSX에는 원근 보정 나눗셈이 없었습니다. 비스듬히 보이는 넓은 삼각형에서
//     텍스처가 휘어 보이는 것이 이 시절 지면의 결정적인 인상입니다.
//
//  3. 정점 조명 — 조명을 픽셀이 아니라 정점에서 계산합니다. 면이 평평하게 칠해집니다.
//
//  4. 색 계단 — 15비트 색을 흉내 내 색 단계를 줄입니다.
//
// 안개는 URP 설정을 그대로 씁니다. 짧은 시야를 안개로 가리는 것도 이 시절 방식입니다.
Shader "CarDrive/PSX Terrain"
{
    Properties
    {
        // --- 아래 넷은 터레인 시스템이 채웁니다. 직접 건드리지 마세요. ---
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Layer 0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("Layer 1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("Layer 2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("Layer 3", 2D) = "grey" {}

        [Header(PSX)]
        _JitterResolution ("정점 떨림 격자 (가로 세로)", Vector) = (320, 240, 0, 0)
        _JitterAmount     ("정점 떨림 세기", Range(0, 1)) = 1
        _AffineAmount     ("텍스처 뒤틀림", Range(0, 1)) = 1
        _ColorLevels      ("색 계단 수", Range(4, 64)) = 32

        [Header(Lighting)]
        _AmbientBoost     ("주변광 보정", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry-100"
            "TerrainCompatible" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Control_ST;
            float4 _Splat0_ST;
            float4 _Splat1_ST;
            float4 _Splat2_ST;
            float4 _Splat3_ST;
            float4 _JitterResolution;
            float  _JitterAmount;
            float  _AffineAmount;
            float  _ColorLevels;
            float  _AmbientBoost;
        CBUFFER_END

        TEXTURE2D(_Control); SAMPLER(sampler_Control);
        TEXTURE2D(_Splat0);  SAMPLER(sampler_Splat0);
        TEXTURE2D(_Splat1);
        TEXTURE2D(_Splat2);
        TEXTURE2D(_Splat3);

        // 화면 좌표를 낮은 해상도 격자에 붙입니다. 이것이 PSX 특유의 흔들림입니다.
        float4 SnapVertex(float4 positionCS)
        {
            if (_JitterAmount <= 0.001) return positionCS;

            float2 grid = max(_JitterResolution.xy, 2.0);
            float2 ndc = positionCS.xy / positionCS.w;
            float2 snapped = floor(ndc * grid + 0.5) / grid;

            positionCS.xy = lerp(ndc, snapped, _JitterAmount) * positionCS.w;
            return positionCS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                // 같은 UV를 두 벌 넘깁니다.
                // 하나는 원근 보정을 하고(요즘 방식), 하나는 하지 않습니다(PSX 방식).
                // 프래그먼트에서 둘을 섞어 뒤틀림 정도를 조절합니다.
                noperspective float2 uvAffine : TEXCOORD0;
                float2 uvCorrect              : TEXCOORD1;

                half3 lighting  : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = SnapVertex(pos.positionCS);

                output.uvAffine = input.texcoord;
                output.uvCorrect = input.texcoord;

                // 조명은 정점에서 한 번만 계산합니다. 면이 평평하게 칠해져야 그 시절 느낌이 납니다.
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                Light mainLight = GetMainLight();
                half3 lit = mainLight.color * saturate(dot(normalWS, mainLight.direction));
                lit += SampleSH(normalWS) * _AmbientBoost;

                output.lighting = lit;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 원근 보정을 뺄수록 비스듬한 면에서 텍스처가 휩니다.
                float2 uv = lerp(input.uvCorrect, input.uvAffine, _AffineAmount);

                half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, uv * _Control_ST.xy + _Control_ST.zw);

                // 가중치 합이 1이 아니면 지면이 어두워지거나 날아갑니다.
                half total = dot(control, half4(1, 1, 1, 1));
                control /= max(total, 1e-4h);

                half3 albedo =
                      control.r * SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv * _Splat0_ST.xy + _Splat0_ST.zw).rgb
                    + control.g * SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uv * _Splat1_ST.xy + _Splat1_ST.zw).rgb
                    + control.b * SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uv * _Splat2_ST.xy + _Splat2_ST.zw).rgb
                    + control.a * SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uv * _Splat3_ST.xy + _Splat3_ST.zw).rgb;

                half3 color = albedo * input.lighting;

                // 15비트 색을 흉내 내 단계를 줄입니다.
                float levels = max(_ColorLevels, 2.0);
                color = floor(color * levels + 0.5) / levels;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                // 그림자도 같은 격자에 붙여야 본체와 어긋나지 않습니다.
                output.positionCS = SnapVertex(positionCS);
                return output;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 4.5

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = SnapVertex(TransformObjectToHClip(input.positionOS.xyz));
                return output;
            }

            half4 depthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
