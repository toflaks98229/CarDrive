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

        // 끄면 스플랫 텍스처 샘플이 <b>컴파일되지 않습니다.</b> 색은 위의 팔레트가 전부 정합니다.
        [Toggle(_SPLAT_TEXTURES)] _UseSplatTextures ("레이어 텍스처 섞기", Float) = 0
        _TextureBlend ("텍스처 섞는 정도", Range(0, 1)) = 0

        [Header(Hand Drawn)]
        [Toggle(_HATCHING)] _UseHatching ("빗금으로 음영 그리기", Float) = 0

        [Header(Toon Shading)]
        _MidPoint ("명암 경계 (낮을수록 밝은 면이 넓음)", Range(0, 1)) = 0.42
        _Softness ("경계 부드러움", Range(0, 0.5)) = 0.06
        _ShadowSoftness ("그림자 경계 부드러움", Range(0, 1)) = 0.25
        // 벽에 붙는 자국(UrineSplat.mat)과 <b>같은 색</b>이어야 합니다. 다르면 같은 오줌인데
        // 땅과 벽이 다른 물건으로 보입니다. a 는 땅색을 얼마나 덮을지입니다.
        _SplatColor ("얼룩 색", Color) = (0.78, 0.68, 0.32, 0.85)
        _SplatDarken ("젖으면 어두워지는 정도", Range(0, 1)) = 0.45
        _SplatGloss ("젖으면 생기는 반짝임", Range(0, 1)) = 0.55

        // 벽 자국과 같은 문법으로 그리기 위한 값들입니다.
        _SplatNoiseScale ("테두리 잡음 잘기 (1m 당 주기)", Range(0.5, 20)) = 3.5
        _SplatEdgeBite ("테두리를 갉는 정도", Range(0, 2)) = 1.7
        _SplatEdgeSharp ("테두리 경사 세우기", Range(1, 12)) = 1.5
        _SplatHatchScale ("획 한 판이 덮는 거리(m)", Range(0.05, 2)) = 0.6
        _SplatHatchBite ("획이 얼룩을 갉는 정도", Range(0, 1)) = 1

        _StepSoftness ("단계 사이 부드러움", Range(0, 1)) = 0.35
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
            half   _ShadowSoftness;
            half   _StepSoftness;
            half   _Steps;
            half4  _ShadowTint;
            half   _ShadowStrength;
            half   _Ambient;
            half4  _HeightColor;
            half   _HeightBottom;
            half   _HeightTop;
            half   _HeightStrength;
            half4  _SplatColor;
            half   _SplatDarken;
            half   _SplatGloss;
            float  _SplatNoiseScale;
            half   _SplatEdgeBite;
            half   _SplatEdgeSharp;
            float  _SplatHatchScale;
            half   _SplatHatchBite;
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
        // 세계 전체의 젖음 지도입니다. SplatManager 가 전역으로 올립니다.
        // 지도가 없으면 CarDriveSplatWetness 가 0 을 돌려주므로 아래 계산이 전부 사라집니다.
        #include "CarDriveSplatMap.hlsl"

        ToonParams BuildToonParams()
        {
            ToonParams p = DefaultToonParams();
            p.midPoint = _MidPoint;
            p.softness = _Softness;
            p.shadowSoftness = _ShadowSoftness;
            p.stepSoftness = _StepSoftness;
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

        /// <summary>
        /// 젖은 만큼 하이라이트를 <b>되살립니다.</b>
        ///
        /// 마른 지면은 위에서 보듯 하이라이트를 끕니다. 그런데 젖은 흙은 물막이 생겨
        /// 실제로 번들거립니다 — 이 파이프라인에는 Smoothness 가 없으므로,
        /// 그 뜻을 <c>specularStrength</c> 와 <c>specularSize</c> 로 옮깁니다.
        ///
        /// <b>wet 이 0 이면 위와 완전히 같은 값이 나옵니다.</b> 곱하기뿐이라 마른 지면의
        /// 그림은 한 픽셀도 안 바뀝니다.
        /// </summary>
        ToonParams BuildToonParamsWet(half wet)
        {
            ToonParams p = BuildToonParams();

            p.specularStrength = _SplatGloss * wet;

            // 젖은 반짝임은 좁고 또렷합니다. 넓게 퍼지면 안개처럼 보여 물로 안 읽힙니다.
            p.specularSize = max(p.specularSize, 48.0h);
            return p;
        }
        
        /// </summary>

ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma shader_feature_local_fragment _TOON_RAMP
            #pragma shader_feature_local_fragment _HATCHING

            // <b>스플랫 텍스처를 쓰지 않으면 아예 컴파일되지 않게 합니다.</b>
            //
            // 예전에는 <c>if (_TextureBlend > 0.001)</c> 라는 <b>실행 중 분기</b>였습니다.
            // 값이 0이라 픽셀마다 건너뛰기는 했지만, 분기 안에 텍스처 샘플 넷이 남아 있는 한
            // 컴파일러는 <b>그 경로가 돌 것을 가정하고</b> 레지스터와 샘플러를 잡아 둡니다.
            // 실제로 안 도는 코드가 도는 코드의 점유율을 깎는 셈입니다.
            //
            // 키워드로 바꾸면 쓰지 않는 배리언트에는 샘플러도 좌표 계산도 남지 않습니다.
            // 텍스처를 다시 쓰고 싶으면 머터리얼에서 이 토글만 켜면 됩니다.
            #pragma shader_feature_local_fragment _SPLAT_TEXTURES

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
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.texcoord;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control,
                                                 uv * _Control_ST.xy + _Control_ST.zw);

                half3 albedo = SampleGroundAlbedo(BuildPalette(), input.positionWS.xz, control);

                // 텍스처를 다시 쓰고 싶을 때를 위해 남겨 둔 길입니다.
                // 머터리얼에서 켜지 않으면 아래는 <b>컴파일되지 않습니다.</b>
                #ifdef _SPLAT_TEXTURES
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
                #endif

                // ── 오줌 얼룩 ──
                //
                // <b>벽에 붙는 자국과 같은 문법으로 그립니다.</b> 지도는 "얼마나 젖었나" 만
                // 담고(텍셀이 6cm 라 잔결을 못 담습니다), 너덜너덜한 테두리와 손그림 획은
                // 여기서 픽셀 해상도로 만듭니다.
                //
                // 지도가 없거나 이 자리가 마르면 wet 이 0 이라 아래가 전부 사라집니다 —
                // <b>마른 지면의 그림은 예전과 픽셀 단위로 같습니다.</b>
                half wet = CarDriveSplatStain(input.positionWS, normalize(input.normalWS),
                                              _SplatNoiseScale, _SplatEdgeBite, _SplatEdgeSharp,
                                              _SplatHatchScale, _SplatHatchBite);

                // 젖은 흙은 어둡습니다. 빛을 덜 튕겨 내고 속으로 먹기 때문입니다.
                // 그 위에 오줌 색을 얹습니다 — 벽 자국이 하는 것과 같습니다.
                half3 stained = lerp(albedo * (1.0h - _SplatDarken), _SplatColor.rgb, _SplatColor.a);
                albedo = lerp(albedo, stained, wet);

                ToonSurface s;
                s.albedo = albedo;
                s.normalWS = normalize(input.normalWS);
                s.positionWS = input.positionWS;
                s.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half3 color = ToonShade(s, BuildToonParamsWet(wet), shadowCoord);

                color = color;
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
