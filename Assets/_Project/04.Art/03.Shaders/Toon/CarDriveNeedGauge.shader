// 니즈 게이지의 채움 끝을 <b>세계와 같은 손그림 빗금으로</b> 그립니다.
//
// <b>왜 빗금인가.</b> 예전에는 여기도 4x4 Bayer 디더였습니다. 나무와 풀이 디더로
// 사라지니 게이지도 디더로 차오르게 한 것이었는데, 게이지는 <b>사라지는 물건이 아니라
// 그려지는 눈금</b>입니다. 그래서 지면·건물·소품의 그늘을 긋는 그 빗금과 같은 편에
// 세웠습니다. 같은 TAM 을 같은 세기로 읽으므로 화면 어디를 봐도 한 사람이 그은 획입니다.
//
// <b>어떻게 하는가.</b> 안쪽은 획이 빽빽해 꽉 차 보이고, 채움 경계로 갈수록 획이
// 성겨집니다. 연필이 종이에서 떨어지듯 끝납니다.
//
// <b>획이 곧 게이지입니다.</b> 색을 어둡게 하지 않고 <b>덮임을 알파로</b> 몰았습니다.
// 색만 곱하면 획 사이의 종이가 불투명한 채로 남아 어두운 판 위에 획이 얹힌 꼴이 됩니다.
// 알파로 몰면 종이는 비고 획만 남아, 게이지가 제 색을 가진 획 다발로 흩어집니다.
// 얼마나 비울지는 <c>_HatchAlphaBite</c> 가 정합니다. 1 이 기본입니다 — 낮추면 알파에
// 바닥이 남아 획 뒤로 반투명한 판이 비쳐, 획만 남기려던 것이 회색 배경 위의 획이 됩니다.
//
// <b>월드 빗금을 2D 로 쓰는 법.</b> 삼중평면은 법선이 축에 정렬되면 평면 하나로
// 붕괴합니다. 법선에 (0,0,1) 을 주고 위치에 화면 픽셀을 넣으면 2D 빗금이 되고
// 샘플도 셋이 아니라 하나입니다. 빗금 함수는 한 줄도 고치지 않았습니다.
//
// <b>리그가 없을 때는 디더로 물러섭니다.</b> 빗금은 TAM 텍스처가 있어야 하고
// HatchingRig 가 씬에 없으면 아무것도 하지 않습니다. 그때 그냥 두면 경계가 매끈한
// 직선이 되어, 애초에 이 셰이더를 만든 이유가 사라집니다. 디더는 수치만으로 도니
// 그 자리를 메웁니다.
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

        _EdgeBand ("경계 띠 너비 (게이지 폭 대비)", Range(0.002, 0.4)) = 0.16
        _HatchPixelSize ("획 한 판이 덮는 화면 픽셀", Range(8, 512)) = 192

        // 0 이면 예전처럼 꽉 찬 판, 1 이면 획만 남고 사이가 완전히 비칩니다.
        // <b>1 이 기본입니다.</b> 1 보다 낮추면 알파에 바닥이 남아 획 뒤로 반투명한 판이
        // 비쳐 보입니다 — 획만 남기려던 것이 회색 배경 위의 획이 됩니다.
        // 성겨지는 정도는 이 값이 아니라 띠 안의 톤 그라데이션이 만듭니다.
        _HatchAlphaBite ("획이 알파를 먹는 정도", Range(0, 1)) = 1

        // 빗금을 쓸 수 없을 때(HatchingRig 없음) 물러설 디더의 격자 크기입니다.
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
            #include "CarDriveHatch.hlsl"
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
                float  _EdgeBand;
                float  _HatchPixelSize;
                float  _HatchAlphaBite;
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
                // 그 사이가 획이 겹치는 띠입니다.
                //
                // 0.5 를 더하는 것은 띠를 경계에 <b>걸치게</b> 하기 위해서입니다.
                // 더하지 않으면 띠가 전부 안쪽에 생겨 게이지가 실제보다 짧아 보입니다.
                float band = max(_EdgeBand, 0.0001);
                float inside = saturate((_Fill - input.texcoord.x) / band + 0.5);

                // 채움 바깥은 그리지 않습니다. 획이 성겨지는 것과 <b>어디서 끝나는가</b>는
                // 다른 일이라, 끝은 빗금이 아니라 이 자름이 정합니다.
                clip(inside - 0.0001);

                [branch] if (_CarDriveHatchParams.w >= 0.5)
                {
                    // 위치에 화면 픽셀을, 법선에 (0,0,1) 을 줍니다. 삼중평면이 Z 한 장으로
                    // 붕괴해 2D 가 되고 샘플도 하나입니다. (CarDriveHatch.hlsl 머리말 참고)
                    //
                    // <b>획 무늬를 문턱값으로 씁니다.</b> 디더가 Bayer 격자로 하던 일을
                    // 손그림 획이 그대로 이어받습니다 — 무늬가 진한 자리는 늦게까지 남고
                    // 옅은 자리(종이)부터 먼저 지워집니다.
                    //
                    // <b>왜 알파에 섞지 않고 자르는가.</b> 알파로 섞어 봤더니 획 사이의 종이가
                    // 반투명하게 남아 <b>회색 판 위에 획이 얹힌</b> 꼴이 됐습니다. 안쪽을
                    // 채우려고 넣은 보간이 띠 전체에 알파 바닥을 만들었기 때문입니다.
                    // 자르면 남거나 없거나 둘뿐이라 뒤에 아무것도 깔리지 않습니다.
                    //
                    // 무늬는 <b>한 단계로 고정</b>합니다. 톤을 위치에 따라 흔들면 지워지는 동안
                    // 무늬 자체가 바뀌어 획이 기어다니는 것처럼 보입니다. Bayer 격자가
                    // 늘 같은 격자인 것과 같은 이유입니다.
                    half pattern = CarDriveHatchValue(0.6h,
                                                      float3(input.positionCS.xy, 0.0),
                                                      float3(0, 0, 1),
                                                      max(_HatchPixelSize, 1.0), 1.0h);

                    // <b>안쪽을 문턱 위로 들어 올립니다.</b> 그냥 <c>inside - 무늬</c> 로 자르면
                    // 안쪽에서도 무늬가 종이(1)인 자리가 문턱을 못 넘어 <b>바 전체에 구멍</b>이
                    // 뚫립니다. 실제로 그렇게 나왔습니다. inside 에 (1 + 먹기) 를 곱하면
                    // 안쪽에서는 어떤 무늬값이라도 반드시 넘어 통짜로 남고, 바깥으로 갈수록
                    // 짙은 획만 버티다 사라집니다.
                    // _HatchAlphaBite 가 0 이면 무늬가 빠져 그냥 곧게 잘립니다.
                    half bite = (half)_HatchAlphaBite;
                    clip(inside * (1.0h + bite) - pattern * bite - 0.0001);
                }
                else
                {
                    // <b>빗금을 쓸 수 없을 때만</b> 예전처럼 디더로 흩뜨립니다.
                    // 그냥 두면 경계가 매끈한 직선이 되어 이 셰이더의 이유가 사라집니다.
                    float2 cell = input.positionCS.xy / max(_DitherPixelSize, 1.0);
                    clip(inside >= 1.0 ? 1.0 : inside - CarDriveDitherThreshold(cell) - 0.0001);
                }

                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
