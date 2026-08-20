// 지면용 툰 셰이더입니다.
//
// 바탕색은 기존 LowPoly 지면과 <b>같은 방식</b>으로 만듭니다.
// (LowPolyGround.hlsl 의 팔레트 · 스플랫 가중치를 그대로 씁니다)
// 그래서 이미 구워 둔 터레인의 스플랫맵과 인스펙터 색을 그대로 물려받습니다.
// 바뀌는 것은 <b>조명뿐</b>입니다.
//
// 조명은 CarDriveToonLighting.hlsl 로, 메시용 CarDrive/Toon Lit 과 같은 것을 씁니다.
// 땅과 그 위의 물체가 같은 모양의 경계를 가져야 물체가 배경에서 떠 보이지 않습니다.
//
// 터레인은 회전을 무시하고 LOD 로 삼각형을 바꿔 버리므로, 면 법선을 써서 각을 내는 방식은
// 쓰지 않았습니다. (기존 LowPolyTerrain 의 _FlatShading 주석에 그 이유가 적혀 있습니다)
// 툰 룩의 각진 인상은 <b>법선이 아니라 명암 경계</b>에서 냅니다.

Shader "CarDrive/Toon Terrain"
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
        _RoadColorB  ("도로 (밝은 쪽)",   Color) = (0.353, 0.349, 0.365, 1)
        _ColorNoiseScale ("색 얼룩 크기", Float) = 0.08
        _TextureBlend ("텍스처 섞기", Range(0, 1)) = 0

        [Header(Toon Shading)]
        _MidPoint ("명암 경계 (낮을수록 밝은 면이 넓음)", Range(0, 1)) = 0.42
        _Softness ("경계 부드러움", Range(0, 0.5)) = 0.06
        _Steps ("밝은 쪽 단계 수 (2 미만이면 끊지 않음)", Range(0, 8)) = 3
        _ShadowTint ("그림자 색", Color) = (0.40, 0.46, 0.62, 1)
        _ShadowStrength ("그림자 세기", Range(0, 1)) = 0.7
        _Ambient ("환경광", Range(0, 2)) = 0.9

        [Header(Ramp Shading)]
        [Toggle(_TOON_RAMP)] _UseRamp ("램프 텍스처 쓰기", Float) = 0
        [NoScaleOffset] _ToonRampMap ("램프 (가로축 = 밝기)", 2D) = "white" {}

        [Header(Height Gradient)]
        _HeightColor ("높이 색", Color) = (0.30, 0.34, 0.48, 1)
        _HeightBottom ("시작 높이", Float) = 0
        _HeightTop ("끝 높이", Float) = 20
        _HeightStrength ("높이 색 세기 (0이면 끔)", Range(0, 1)) = 0

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
        #include "CarDriveToonLighting.hlsl"
        #include "../LowPoly/LowPolyGround.hlsl"

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
            float  _ColorNoiseScale;
            float  _TextureBlend;
            half   _MidPoint;
            half   _Softness;
            half   _Steps;
            half4  _ShadowTint;
            half   _ShadowStrength;
            half   _Ambient;
            half4  _HeightColor;
            half   _HeightBottom;
            half   _HeightTop;
            half   _HeightStrength;
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

        /// <summary>인스펙터 값을 툰 설정 묶음으로 모읍니다.</summary>
        ToonParams BuildToonParams()
        {
            ToonParams p = DefaultToonParams();
            p.midPoint = _MidPoint;
            p.softness = _Softness;
            p.steps = _Steps;
            p.shadowTint = _ShadowTint.rgb;
            p.shadowStrength = _ShadowStrength;
            p.ambient = _Ambient;
            p.heightColor = _HeightColor.rgb;
            p.heightBottom = _HeightBottom;
            p.heightTop = _HeightTop;
            p.heightStrength = _HeightStrength;

            // 지면에는 하이라이트와 외곽 빛을 쓰지 않습니다.
            // 넓은 면에 얹으면 얼룩으로 보이고, 픽셀화를 거치면 더 지저분해집니다.
            p.specularStrength = 0.0h;
            p.rimStrength = 0.0h;
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma shader_feature_local_fragment _TOON_RAMP

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

                ToonSurface s;
                s.albedo = albedo;
                s.normalWS = normalize(input.normalWS);
                s.positionWS = input.positionWS;
                s.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half3 color = ToonShade(s, BuildToonParams(), shadowCoord);

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

    FallBack "CarDrive/LowPoly Terrain"
}
