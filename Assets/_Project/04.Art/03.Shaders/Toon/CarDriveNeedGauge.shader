// 니즈 게이지를 <b>디더로 차오르고 줄어들게</b> 그립니다.
//
// <b>왜 UI 에까지 디더인가.</b> 이 게임은 사라지고 나타나는 일을 전부 같은 4x4 Bayer 로
// 합니다 — 나무도, 풀도. 그런데 게이지만 매끈한 직선으로 차올랐습니다.
// 화면에서 가장 자주 보는 것이 게이지인데 <b>거기만 다른 시대의 물건</b>처럼 보였습니다.
//
// <b>어떻게 하는가.</b> 채움 경계를 딱 자르지 않고, 그 언저리 좁은 띠 안에서 픽셀을
// Bayer 순서로 버립니다. 게이지가 차오를 때 그 띠가 <b>점점이 메워지며</b> 지나갑니다.
// 줄어들 때는 반대로 점점이 흩어집니다.
//
// <b>기하가 아니라 픽셀로 자릅니다.</b> Image 의 Filled 타입은 정점을 잘라 내므로
// 경계 바깥에 그릴 픽셀이 남지 않습니다. 그래서 채움은 <c>_Fill</c> 로 받고
// 이 셰이더가 판 전체를 받아 스스로 자릅니다. (NeedsUI 가 채움을 넘깁니다)
Shader "CarDrive/UI Need Gauge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Fill ("채움 (0~1)", Range(0, 1)) = 1

        _DitherBand ("디더 띠 너비 (게이지 폭 대비)", Range(0.002, 0.4)) = 0.08
        _DitherPixelSize ("디더 격자 크기 (화면 픽셀)", Range(1, 12)) = 3

        // --- UI 보일러플레이트 ---
        // Unity UI 는 마스크와 스텐실을 이 프로퍼티들로 다룹니다.
        // 하나라도 빠지면 Mask 안에서 게이지가 사라지거나 마스크가 새어 나갑니다.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CarDriveDither.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 texcoord   : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float  _Fill;
                float  _DitherBand;
                float  _DitherPixelSize;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord) * input.color;

                // <b>채움 경계까지 얼마나 남았는가.</b> 1이면 확실히 안쪽, 0이면 확실히 바깥,
                // 그 사이가 디더로 흩어지는 띠입니다.
                //
                // 0.5 를 더하는 것은 띠를 경계에 <b>걸치게</b> 하기 위해서입니다.
                // 더하지 않으면 띠가 전부 안쪽에 생겨 게이지가 실제보다 짧아 보입니다.
                float band = max(_DitherBand, 0.0001);
                float inside = saturate((_Fill - input.texcoord.x) / band + 0.5);

                // <b>격자를 키웁니다.</b> 4x4 를 화면 픽셀 그대로 쓰면 요즘 해상도에서는
                // 너무 잘아 그냥 흐릿한 경계로 보입니다. 나누면 점이 굵어져
                // 이 게임의 픽셀 룩과 같은 결이 됩니다.
                float2 cell = input.positionCS.xy / max(_DitherPixelSize, 1.0);

                // <b>완전히 안쪽은 문턱을 보지 않습니다.</b> inside 가 1일 때 문턱값 1인 픽셀이
                // 걸려 <b>채워진 구간에 구멍</b>이 뚫리는 것을 막습니다.
                clip(inside >= 1.0 ? 1.0 : inside - CarDriveDitherThreshold(cell) - 0.0001);

                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
