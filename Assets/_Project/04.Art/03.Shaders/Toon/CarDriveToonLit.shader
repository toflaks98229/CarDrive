// 메시용 툰 셰이더입니다. 차량·소품·귀신처럼 지면이 아닌 것에 씁니다.
//
// 조명 계산은 CarDriveToonLighting.hlsl 이 갖고 있고, 지면 셰이더와 <b>같은 것</b>을 씁니다.
// 그래야 땅과 그 위의 물체에 같은 모양의 경계가 생깁니다. 서로 다른 음영을 쓰면
// 물체가 배경에서 떠 보입니다.
//
// 외곽선은 법선을 따라 부풀린 뒷면을 그리는 고전적인 방법입니다.
// 기법은 ColinLeung-NiloCat 의 UnityURPToonLitShaderExample (MIT) 을 참고했습니다.
//   https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
//
// <b>외곽선 두께는 기본값이 0 입니다.</b> 화면이 픽셀화를 거치므로 얇은 선은
// 어차피 뭉개지고, 두꺼우면 저해상도에서 지저분해집니다. 필요할 때만 올리세요.

Shader "CarDrive/Toon Lit"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("바탕 텍스처", 2D) = "white" {}
        _BaseColor ("바탕색", Color) = (1, 1, 1, 1)

        [Header(Toon Shading)]
        _MidPoint ("명암 경계 (낮을수록 밝은 면이 넓음)", Range(0, 1)) = 0.35
        _Softness ("경계 부드러움", Range(0, 0.5)) = 0.05
        _Steps ("밝은 쪽 단계 수 (2 미만이면 끊지 않음)", Range(0, 8)) = 0
        _ShadowTint ("그림자 색", Color) = (0.42, 0.47, 0.62, 1)
        _ShadowStrength ("그림자 세기", Range(0, 1)) = 0.75
        _Ambient ("환경광", Range(0, 2)) = 0.85

        [Header(Highlights)]
        _SpecularStrength ("하이라이트 세기", Range(0, 2)) = 0
        _SpecularSize ("하이라이트 크기 (클수록 작아짐)", Range(1, 200)) = 40
        _RimStrength ("외곽 빛 세기", Range(0, 2)) = 0
        _RimWidth ("외곽 빛 폭 (클수록 좁아짐)", Range(1, 16)) = 4
        _RimColor ("외곽 빛 색", Color) = (1, 1, 1, 1)


        [Header(Ramp Shading)]
        [Toggle(_TOON_RAMP)] _UseRamp ("램프 텍스처 쓰기", Float) = 0
        [NoScaleOffset] _ToonRampMap ("램프 (가로축 = 밝기)", 2D) = "white" {}

        [Header(Height Gradient)]
        _HeightColor ("높이 색", Color) = (0.30, 0.34, 0.48, 1)
        _HeightBottom ("시작 높이", Float) = 0
        _HeightTop ("끝 높이", Float) = 20
        _HeightStrength ("높이 색 세기 (0이면 끔)", Range(0, 1)) = 0

        [Header(Outline)]
        _OutlineWidth ("외곽선 두께 (0이면 끔)", Range(0, 0.05)) = 0
        _OutlineColor ("외곽선 색", Color) = (0.08, 0.07, 0.10, 1)

        [Header(Cutout)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("알파 컷아웃 쓰기 (잎처럼 뚫린 것)", Float) = 0
        _Cutoff ("컷아웃 기준", Range(0, 1)) = 0.5

        [Header(Distance Fade)]
        [Toggle(_DITHER_FADE)] _UseDitherFade ("멀어지면 디더로 지우기", Float) = 0
        _FadeStart ("지워지기 시작하는 거리(m)", Float) = 240
        _FadeEnd ("완전히 지워지는 거리(m)", Float) = 330

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "CarDriveToonLighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _MidPoint;
            half   _Softness;
            half   _Steps;
            half4  _ShadowTint;
            half   _ShadowStrength;
            half   _Ambient;
            half   _SpecularStrength;
            half   _SpecularSize;
            half   _RimStrength;
            half   _RimWidth;
            half4  _RimColor;
            half4  _HeightColor;
            half   _HeightBottom;
            half   _HeightTop;
            half   _HeightStrength;
            half   _OutlineWidth;
            half4  _OutlineColor;
            half   _Cull;
            half   _Cutoff;
            float  _FadeStart;
            float  _FadeEnd;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        // LOD 가 바뀔 때 메시가 툭 갈리지 않도록 유니티가 주는 디더 크로스페이드입니다.
        // LODGroup 의 Fade Mode 를 Cross Fade 로 두었을 때만 켜집니다.
        #if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #define CARDRIVE_LOD_CROSSFADE(positionCS) LODFadeCrossFade(positionCS)
        #else
            #define CARDRIVE_LOD_CROSSFADE(positionCS)
        #endif

        // ── 디더로 지우기 ──
        //
        // 멀어지는 물체를 <b>알파로 흐리게</b> 하려면 반투명으로 그려야 하고,
        // 그러면 정렬 문제가 생기고 깊이도 못 씁니다. 나무가 수천 그루면 감당이 안 됩니다.
        //
        // 대신 <b>픽셀을 성기게 버립니다.</b> 화면 위치에 따라 정해진 문턱값을 두고
        // 남을 정도가 그보다 작으면 그 픽셀을 버립니다. 멀어질수록 더 많이 버려져
        // 서서히 성글어지다 사라집니다. 불투명 그대로라 값이 싸고 정렬도 필요 없습니다.
        //
        // 이 게임에는 특히 잘 맞습니다. 화면이 어차피 픽셀화를 거치므로
        // 디더 무늬가 <b>결점이 아니라 시대 표현</b>으로 읽힙니다.

        /// 4x4 Bayer 행렬입니다. 값이 고르게 흩어져 있어 무늬가 뭉치지 않습니다.
        static const half CarDriveBayer4x4[16] =
        {
             0.0h / 16.0h,  8.0h / 16.0h,  2.0h / 16.0h, 10.0h / 16.0h,
            12.0h / 16.0h,  4.0h / 16.0h, 14.0h / 16.0h,  6.0h / 16.0h,
             3.0h / 16.0h, 11.0h / 16.0h,  1.0h / 16.0h,  9.0h / 16.0h,
            15.0h / 16.0h,  7.0h / 16.0h, 13.0h / 16.0h,  5.0h / 16.0h,
        };

        /// 이 화면 픽셀의 문턱값을 구합니다.
        half CarDriveDitherThreshold(float2 pixelPos)
        {
            int2 cell = int2(fmod(abs(pixelPos), 4.0));
            return CarDriveBayer4x4[cell.y * 4 + cell.x];
        }

        /// 거리에 따라 얼마나 남을지 구합니다. 1이면 그대로, 0이면 다 지웁니다.
        half CarDriveFadeAmount(float3 positionWS)
        {
            #if defined(_DITHER_FADE)
                float d = length(GetCameraPositionWS() - positionWS);
                return saturate(1.0 - (d - _FadeStart) / max(0.001, _FadeEnd - _FadeStart));
            #else
                return 1.0h;
            #endif
        }

        /// 멀어진 만큼 픽셀을 버립니다.
        void CarDriveApplyDitherFade(float3 positionWS, float2 pixelPos)
        {
            #if defined(_DITHER_FADE)
                clip(CarDriveFadeAmount(positionWS) - CarDriveDitherThreshold(pixelPos) - 0.0001h);
            #endif
        }

        /// 잎처럼 뚫린 부분을 버립니다.
        void CarDriveApplyAlphaClip(half alpha)
        {
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
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
            p.rimStrength = _RimStrength;
            p.rimWidth = _RimWidth;
            p.rimColor = _RimColor.rgb;
            p.specularStrength = _SpecularStrength;
            p.specularSize = _SpecularSize;
            p.heightColor = _HeightColor.rgb;
            p.heightBottom = _HeightBottom;
            p.heightTop = _HeightTop;
            p.heightStrength = _HeightStrength;
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma shader_feature_local_fragment _TOON_RAMP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _DITHER_FADE
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

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
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                CarDriveApplyAlphaClip(baseSample.a * _BaseColor.a);
                CarDriveApplyDitherFade(input.positionWS, input.positionCS.xy);
                CARDRIVE_LOD_CROSSFADE(input.positionCS);

                ToonSurface s;
                s.albedo = baseSample.rgb * _BaseColor.rgb;
                s.normalWS = normalize(input.normalWS);
                s.positionWS = input.positionWS;
                s.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half3 color = ToonShade(s, BuildToonParams(), shadowCoord);

                color = MixFog(color, input.fogFactor);
                return half4(color, baseSample.a * _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // 뒷면만 그려서 실루엣만 남깁니다.
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex outlineVert
            #pragma fragment outlineFrag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _DITHER_FADE

            struct OutlineAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float  fogFactor  : TEXCOORD0;
            };

            OutlineVaryings outlineVert(OutlineAttributes input)
            {
                OutlineVaryings output = (OutlineVaryings)0;

                // 두께가 0이면 그릴 것이 없습니다. 세 꼭짓점을 한 점으로 눌러
                // 넓이 0인 삼각형으로 만들면 래스터라이저가 바로 버립니다.
                //
                // 기본값이 0이므로 사실상 <b>대부분의 물체가 이 패스를 건너뜁니다.</b>
                // 나무가 수천 그루라 이 한 줄이 실제로 큽니다.
                if (_OutlineWidth <= 0.0)
                {
                    output.positionCS = float4(0, 0, 0, 1);
                    return output;
                }

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // 카메라에서 멀어져도 두께가 비슷해 보이도록 거리에 비례해 부풀립니다.
                // 그러지 않으면 가까운 물체만 선이 두껍고 먼 물체는 선이 사라집니다.
                float distance = length(GetCameraPositionWS() - positionWS);
                positionWS += normalWS * (_OutlineWidth * distance);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 outlineFrag(OutlineVaryings input) : SV_Target
            {
                // 외곽선도 함께 성글어져야 합니다. 본체만 지우면 선만 남아 떠다닙니다.
                CarDriveApplyDitherFade(input.positionWS, input.positionCS.xy);

                half3 color = MixFog(_OutlineColor.rgb, input.fogFactor);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma target 3.0
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
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
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return output;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                // 잎이 뚫려 있는데 그림자가 통짜로 지면 나무가 아니라 상자 그림자가 됩니다.
                //
                // 거리 디더는 여기서 하지 않습니다. 그림자는 50m 안쪽에서만 그려지는데
                // 디더가 시작되는 것은 240m 부터라 계산해 봐야 늘 1입니다.
                CarDriveApplyAlphaClip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 3.0
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _DITHER_FADE
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 texcoord   : TEXCOORD0;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output;

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

                return output;
            }

            half4 depthFrag(DepthVaryings input) : SV_Target
            {
                // 색에서 지운 픽셀은 깊이에서도 지워야 합니다.
                // 한쪽만 지우면 보이지 않는 나무가 뒤의 것을 가립니다.
                CarDriveApplyAlphaClip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a);
                CarDriveApplyDitherFade(input.positionWS, input.positionCS.xy);
                CARDRIVE_LOD_CROSSFADE(input.positionCS);

                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
