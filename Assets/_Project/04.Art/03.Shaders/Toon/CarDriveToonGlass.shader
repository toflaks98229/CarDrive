// 유리용 툰 셰이더입니다. 차창처럼 <b>비치는 면</b>에 씁니다.
//
// 조명은 CarDriveToonLighting.hlsl 의 ToonShade 를 그대로 씁니다 — 차체·건물·땅과
// <b>같은 함수</b>입니다. 그래야 한 화면에 두 종류의 명암 경계가 생기지 않습니다.
// 여기가 CarDriveToonLit 과 다른 점은 <b>섞어 그린다</b>는 것뿐입니다.
//
// ── 왜 CarDriveToonLit 에 투명을 넣지 않았는가 ──
//
// 그 셰이더는 35개 머티리얼이 함께 쓰고, 전부 불투명이라는 전제 위에 서 있습니다
// (ZWrite On · Queue Geometry · 깊이 패스 · 외곽선 패스). 블렌드 상태를 머티리얼
// 프로퍼티로 열면 그 전제가 <b>서른다섯 곳에서 동시에</b> 흔들립니다.
// 유리는 파일 하나 값이 더 쌉니다.
//
// ── 그림자를 드리우지 않습니다 ──
//
// ShadowCaster 패스가 <b>일부러</b> 없습니다. 벤더의 URP/Lit 유리는 반투명인데도
// 그림자 맵에는 통짜로 찍혀, 차창이 운전석 안쪽에 <b>판때기 그림자</b>를 드리우고
// 있었습니다. 유리는 빛을 통과시키는 물건입니다.
//
// ── 스카이박스 반사를 쓰지 않습니다 ──
//
// 이 게임에는 반사 프로브가 사실상 없어서, PBR 유리는 반사 프로브 자리에
// <b>스카이박스를 그대로</b> 얹습니다. 그러면 유리만 주변과 다른 하늘을 비춰
// 차가 배경에서 떠 보입니다. 대신 여기서는 <b>보는 각도</b>로 밝기를 만듭니다 —
// 비스듬히 보는 면일수록 하얗게 서고 진해집니다(프레넬). 값이 싸고, 차가 움직여도
// 하늘이 미끄러지지 않습니다.

Shader "CarDrive/Toon Glass"
{
    Properties
    {
        [Header(Base)]
        // 알파가 <b>정면에서 볼 때의</b> 진하기입니다. 비스듬한 면은 아래 프레넬이 더 채웁니다.
        _BaseColor ("유리색 (알파 = 진하기)", Color) = (0.27, 0.27, 0.27, 0.4)

        [Header(Toon Shading)]
        // <b>차체 머티리얼과 같은 값으로 두십시오.</b> 다르면 같은 차인데 창과 문짝의
        // 명암 경계가 서로 다른 자리에 생깁니다.
        _MidPoint ("명암 경계 (낮을수록 밝은 면이 넓음)", Range(0, 1)) = 0.35
        _Softness ("경계 부드러움", Range(0, 0.5)) = 0.05
        _ShadowSoftness ("그림자 경계 부드러움", Range(0, 1)) = 0.25
        _StepSoftness ("단계 사이 부드러움", Range(0, 1)) = 0.35
        _Steps ("밝은 쪽 단계 수 (2 미만이면 끊지 않음)", Range(0, 8)) = 0
        _ShadowTint ("그림자 색", Color) = (0.42, 0.47, 0.62, 1)
        _ShadowStrength ("그림자 세기", Range(0, 1)) = 0.75
        _Ambient ("환경광", Range(0, 2)) = 0.85

        [Header(Highlights)]
        // 유리에 얹히는 <b>납작한 흰 점</b>입니다. 해와 헤드라이트가 만듭니다.
        _SpecularStrength ("하이라이트 세기", Range(0, 2)) = 0.6
        _SpecularSize ("하이라이트 크기 (클수록 작아짐)", Range(1, 200)) = 90

        [Header(Fresnel)]
        // 비스듬히 볼수록 유리가 <b>하얗게 서고 진해집니다.</b> 실제 유리가 그렇고,
        // 그것이 없으면 색만 옅은 판으로 보입니다.
        _FresnelColor ("비껴볼 때의 색", Color) = (1, 1, 1, 1)
        _FresnelStrength ("비껴볼 때 밝아지는 정도", Range(0, 2)) = 0.35
        _FresnelOpacity ("비껴볼 때 진해지는 정도", Range(0, 1)) = 0.45
        _FresnelPower ("비껴보는 각도의 좁기 (클수록 가장자리만)", Range(1, 16)) = 4

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
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

            #include "CarDriveToonLighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half   _MidPoint;
                half   _Softness;
                half   _ShadowSoftness;
                half   _StepSoftness;
                half   _Steps;
                half4  _ShadowTint;
                half   _ShadowStrength;
                half   _Ambient;
                half   _SpecularStrength;
                half   _SpecularSize;
                half4  _FresnelColor;
                half   _FresnelStrength;
                half   _FresnelOpacity;
                half   _FresnelPower;
                half   _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 nrm = normalize(input.normalWS);
                float3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                ToonSurface s;
                s.albedo = _BaseColor.rgb;
                s.normalWS = nrm;
                s.positionWS = input.positionWS;
                s.viewDirWS = viewDirWS;

                ToonParams p = DefaultToonParams();
                p.midPoint = _MidPoint;
                p.softness = _Softness;
                p.shadowSoftness = _ShadowSoftness;
                p.stepSoftness = _StepSoftness;
                p.steps = _Steps;
                p.shadowTint = _ShadowTint.rgb;
                p.shadowStrength = _ShadowStrength;
                p.ambient = _Ambient;
                p.specularStrength = _SpecularStrength;
                p.specularSize = _SpecularSize;

                // 외곽 빛(rim)은 <b>쓰지 않습니다.</b> 아래 프레넬이 같은 자리를 칠하는데
                // 둘 다 켜면 가장자리가 두 번 밝아져 테두리가 두껍게 도드라집니다.
                p.rimStrength = 0.0h;

                // 높이 그라데이션도 끕니다. 차는 늘 카메라 앞이라 원경을 누를 일이 없습니다.
                p.heightStrength = 0.0h;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half3 color = ToonShade(s, p, shadowCoord);

                // ── 프레넬 ──
                //
                // 면을 <b>얼마나 비껴 보고 있는가</b>입니다. 정면이면 0, 스칠수록 1 입니다.
                // 뒷면을 그리는 머티리얼도 있으므로 절댓값으로 접어 둡니다 —
                // 그러지 않으면 안쪽에서 본 창이 통째로 프레넬 0 이 됩니다.
                half ndv = 1.0h - saturate(abs(dot(nrm, viewDirWS)));
                half fresnel = pow(ndv, max(1.0h, _FresnelPower));

                color += _FresnelColor.rgb * (fresnel * _FresnelStrength);

                // 밝아지는 만큼 <b>진해지기도</b> 합니다. 밝기만 올리면 유리가 아니라
                // 허공에 뜬 빛으로 보입니다.
                half alpha = saturate(_BaseColor.a + fresnel * _FresnelOpacity);

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
