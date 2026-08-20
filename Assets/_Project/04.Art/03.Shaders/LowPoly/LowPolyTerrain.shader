// 로우폴리 · 코지 룩의 지면 셰이더입니다.
//
// PSX 셰이더에서 일부러 넣었던 것들을 전부 걷어냈습니다.
//  - 원근 보정 없는 텍스처 뒤틀림 (가까이 가면 휘던 것)
//  - 화면 격자에 붙던 정점 떨림
//  - 15비트 색 계단
//
// 로우폴리의 인상은 <b>텍스처 없는 평평한 색</b>에서 나옵니다.
// 색을 좌표에서 바로 계산하므로 반복 이음매가 아예 생기지 않습니다.
//
// 각진 음영을 내는 길도 둘 마련해 두었지만 <b>기본값은 둘 다 꺼져 있습니다.</b>
//
//  - _FlatShading: 옆 픽셀과의 좌표 차이로 면 법선을 구합니다.
//    삼각형이 각지게 드러나지만, 그 삼각형은 <b>터레인 LOD가 거리에 따라 바꿔 버립니다.</b>
//    그래서 각진 얼룩이 카메라를 따라 움직여 조잡해 보입니다.
//
//  - _ShadeSteps: 밝기를 단계로 끊습니다.
//    완만한 지형에 쓰면 등고선 같은 띠가 생기는데, 그 띠가 또 하나의 경계가 됩니다.
//
// 둘 다 켜고 싶으면 머티리얼에서 올리면 됩니다. 각진 지형을 원한다면 셰이더가 아니라
// 높이맵 해상도를 낮춰 <b>진짜로 삼각형을 크게 굽는 편</b>이 맞습니다.
//
// 텍스처도 지우지 않고 남겨 두었습니다. _TextureBlend 를 올리면 다시 섞입니다.
Shader "CarDrive/LowPoly Terrain"
{
    Properties
    {
        // --- 아래 다섯은 터레인 시스템이 채웁니다. 직접 건드리지 마세요. ---
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Layer 0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("Layer 1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("Layer 2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("Layer 3", 2D) = "grey" {}

        [Header(Ground Colors)]
        _GrassColorA ("잔디 (어두운 쪽)", Color) = (0.243, 0.451, 0.259, 1)
        _GrassColorB ("잔디 (밝은 쪽)",   Color) = (0.404, 0.616, 0.310, 1)
        _DirtColorA  ("흙 (어두운 쪽)",   Color) = (0.427, 0.333, 0.235, 1)
        _DirtColorB  ("흙 (밝은 쪽)",     Color) = (0.573, 0.463, 0.333, 1)
        _RoadColorA  ("도로 (어두운 쪽)", Color) = (0.290, 0.286, 0.302, 1)
        _RoadColorB  ("도로 (밝은 쪽)",   Color) = (0.396, 0.392, 0.404, 1)
        _ColorNoiseScale ("색 얼룩 크기", Range(0.002, 0.2)) = 0.022

        [Header(Low Poly)]
        _FlatShading ("각진 면 정도", Range(0, 1)) = 0
        _ShadeSteps  ("밝기 단계 수", Range(1, 8)) = 1

        [Header(Cozy)]
        _ShadowColor  ("그늘 색", Color) = (0.451, 0.522, 0.647, 1)
        _AmbientBoost ("주변광 보정", Range(0, 2)) = 1

        [Header(Optional)]
        _TextureBlend ("텍스처 섞기", Range(0, 1)) = 0
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
        #include "LowPolyGround.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Control_ST;
            float4 _Splat0_ST;
            float4 _Splat1_ST;
            float4 _Splat2_ST;
            float4 _Splat3_ST;
            half4  _GrassColorA;
            half4  _GrassColorB;
            half4  _DirtColorA;
            half4  _DirtColorB;
            half4  _RoadColorA;
            half4  _RoadColorB;
            half4  _ShadowColor;
            float  _ColorNoiseScale;
            float  _FlatShading;
            float  _ShadeSteps;
            float  _AmbientBoost;
            float  _TextureBlend;
        CBUFFER_END

        TEXTURE2D(_Control); SAMPLER(sampler_Control);
        TEXTURE2D(_Splat0);  SAMPLER(sampler_Splat0);
        TEXTURE2D(_Splat1);
        TEXTURE2D(_Splat2);
        TEXTURE2D(_Splat3);

        /// <summary>인스펙터에 노출된 색들을 한 묶음으로 모읍니다.</summary>
        GroundPalette BuildPalette()
        {
            GroundPalette p;
            p.grassA = _GrassColorA.rgb;
            p.grassB = _GrassColorB.rgb;
            p.dirtA  = _DirtColorA.rgb;
            p.dirtB  = _DirtColorB.rgb;
            p.roadA  = _RoadColorA.rgb;
            p.roadB  = _RoadColorB.rgb;
            p.noiseScale = _ColorNoiseScale;
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.texcoord;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control,
                                                 uv * _Control_ST.xy + _Control_ST.zw);

                half3 albedo = SampleGroundAlbedo(BuildPalette(), input.positionWS.xz, control);

                // 텍스처를 다시 쓰고 싶을 때를 위해 남겨 둔 길입니다. 기본값은 0이라 건너뜁니다.
                if (_TextureBlend > 0.001)
                {
                    half total = dot(control, half4(1, 1, 1, 1));
                    half4 w = control / max(total, 1e-4h);

                    half3 tex =
                          w.r * SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv * _Splat0_ST.xy + _Splat0_ST.zw).rgb
                        + w.g * SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uv * _Splat1_ST.xy + _Splat1_ST.zw).rgb
                        + w.b * SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uv * _Splat2_ST.xy + _Splat2_ST.zw).rgb
                        + w.a * SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uv * _Splat3_ST.xy + _Splat3_ST.zw).rgb;

                    albedo = lerp(albedo, albedo * tex * 2.0h, _TextureBlend);
                }

                float3 smoothNormal = normalize(input.normalWS);
                float3 normalWS = normalize(lerp(smoothNormal,
                                                 FaceNormal(input.positionWS, smoothNormal),
                                                 _FlatShading));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half3 color = CozyShade(albedo, normalWS, shadowCoord,
                                        _ShadeSteps, _ShadowColor.rgb, _AmbientBoost);

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
            #pragma target 3.0

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

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
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
            #pragma target 3.0

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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
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
