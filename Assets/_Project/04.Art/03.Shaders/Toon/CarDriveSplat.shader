// 오줌이 닿은 자리에 남는 <b>젖은 자국</b>입니다. 땅에도 벽에도 붙습니다.
//
// <b>왜 데칼이 아닌가.</b> URP 의 Decal Renderer Feature 는 깊이 텍스처를 요구하는데
// 이 프로젝트는 세 URP 에셋 모두 m_RequireDepthTexture 가 0 입니다. 켜면 불투명 전체를
// 다시 그리는 프리패스가 생기고, 드로우 제출이 이미 병목이라 그 값을 치를 수 없습니다.
// 그래서 닿은 면에 맞춰 눕힌 판 하나로 그립니다.
//
// ── 좌표계 ─────────────────────────────────────────────────────────────
//
// <b>판은 종이일 뿐입니다.</b> 무늬는 판이 아니라 <b>앵커에서 잰 미터</b>로 그립니다.
//
// 처음에는 전부 판의 정규화 UV(0~1)로 그렸습니다. 그런데 자국이 자랄 때 런타임이 판도
// 함께 키우므로, UV 로 계산한 모든 것이 물리적으로 확대됐습니다. 그 결과 <b>벽에 이미
// 흘러내린 줄기가 웅덩이를 따라 굵어지고 길어지고 자리까지 옮겼습니다.</b> 줄기 칸 간격이
// 판 폭에 매여 있어, 몸통이 자라면 앵커를 중심으로 방사 팽창했기 때문입니다.
//
// 고친 방식은 상업 사용 가능한 구현들에서 가져왔습니다.
//   - mixandjam/Splatoon-Ink (MIT) — TexturePainter.shader 가 붓을 판 UV 가 아니라
//     distance(_PainterPosition, worldPos) 와 미터 _Radius 로만 표현합니다. 반지름을
//     키워도 이미 칠한 자리가 안 움직이는 이유가 이것입니다.
//     https://github.com/mixandjam/Splatoon-Ink
//   - EsProgram/InkPainter (MIT) — HeightDrip.shader 의 줄기는 면의 텍셀에 <b>기록된</b>
//     값이라 원본을 더 부어도 안 움직입니다. 줄기는 상태를 가져야 한다는 것.
//     https://github.com/EsProgram/InkPainter
//   - Godot Shaders "Bloody Pool" (dip000, MIT) — 자국마다 좌표·크기를 따로 들고 있어
//     하나가 자라도 다른 것이 안 움직입니다. 우리는 버퍼를 못 쓰므로 같은 성질을
//     <b>머리 고정 + 꼬리만 단조 증가</b>로 얻습니다(_DripStart / _DripReach).
//
// <b>몸통은 반대로 형태에 맵니다.</b> 테두리 갉는 잡음까지 미터로 고정하면, 갓 찍힌
// 7cm 자국은 형체가 뭉개지고 70cm 자국은 완벽한 동그라미가 됩니다. 10배 범위를 한
// 미터값으로 못 덮습니다. 잡음 좌표를 몸통 반지름으로 나누면 실루엣이 제자리에서
// <b>닮은꼴로만</b> 넓어집니다.
//   - keijiro/BloodFx (CC0) — Bloodstain.shader 의 snoise(uv * 8 / radius) 와 같은 뜻.
//     그 셰이더도 판 크기를 한 번도 바꾸지 않고 안의 radius 만 키웁니다.
//     https://github.com/keijiro/BloodFx
//
// 요약하면 요소마다 매는 곳이 다릅니다 —
//   줄기(자리·머리·굵기·길이)  → 앵커 기준 <b>미터</b>. 판에도 몸통에도 안 매임.
//   몸통(테두리·심)            → <b>몸통 반지름</b> 상대. 닮은꼴로만 자람.
//   빗금(사라짐)               → <b>월드 좌표</b>. 잉크가 땅에 박혀 있음.
//
// ── 그리는 것 ───────────────────────────────────────────────────────────
//
// <b>모양.</b> 가운데는 진하게 고이고 가장자리는 잡음으로 흩어집니다. 물이 튀어 고인
// 자국은 원이 아니라 중심이 짙고 테두리가 너덜너덜한 얼룩입니다.
//
// <b>벽에서는 흘러내립니다.</b> 세운 면에 붙으면 자국 아래로 줄기가 자랍니다.
// 스플래툰의 물감이 벽을 타고 흐르는 것과 같은 생각입니다.
//
// <b>마르는 것은 손그림 획입니다.</b> Bayer 디더로 지우면 기계가 지운 자국이 됩니다 —
// 디더는 멀어서 사라지는 나무와 풀의 문법입니다. 자국은 지면·건물의 그늘을 긋는 그
// TAM 을 그대로 문턱으로 삼아 획이 성겨지며 마릅니다.
Shader "CarDrive/Splat"
{
    Properties
    {
        _Color ("자국 색", Color) = (0.78, 0.68, 0.32, 1)

        _Age ("나이 (0 갓 튄 것 ~ 1 다 마름)", Range(0, 1)) = 0

        // 몸통의 <b>월드 반지름(m)</b>입니다. 판 크기와 무관합니다.
        // 예전의 _Grow 는 판 상대값이라, 판이 함께 커지면 배율이 두 겹이 됐습니다.
        _BodyRadius ("몸통 반지름(m)", Range(0.01, 1.0)) = 0.07

        // 정의역이 몸통 반지름으로 정규화된 좌표(-1~1)입니다. 판 UV(0~1)일 때보다
        // 주기가 두 배로 잡히므로 값이 절반쯤으로 내려갑니다.
        _NoiseScale ("테두리 잡음 잘기 (몸통 지름당 주기)", Range(0.5, 12)) = 3.5
        _EdgeBite ("테두리를 갉는 정도", Range(0, 1)) = 0.55

        // 몸통 반지름 대비 비율입니다.
        _CoreSize ("가운데 심 크기 (몸통 반지름 대비)", Range(0.02, 0.8)) = 0.35
        _CoreBoost ("가운데 심 진하기", Range(0, 1)) = 0.85

        _DripAmount ("흘러내림 (벽에서만)", Range(0, 1)) = 0

        // <b>스탬프 때 정하고 다시 안 바꿉니다.</b> 예전에는 셰이더가 매 프레임
        // radius*0.4 로 다시 셈해서, 몸통이 자랄 때마다 이미 그어진 줄기의 머리가
        // 함께 내려갔습니다.
        _DripStart ("줄기 머리 깊이(m)", Range(0, 0.6)) = 0.03

        // 런타임이 <b>젖은 시간</b>으로만 올립니다. 몸통 크기를 참조하지 않습니다.
        _DripReach ("줄기 길이(m)", Range(0, 2.5)) = 0

        // 칸 간격이 미터로 고정이라 몸통이 자라도 줄기가 안 벌어집니다.
        // (nomand/RevealShader, MIT — 무늬의 잘기를 바운즈가 아니라 미터에 매라)
        _DripPitch ("줄기 칸 간격(m)", Range(0.01, 0.2)) = 0.035

        // 굵기·진하기가 <b>절대 깊이</b>로 정해지게 하는 값입니다. 예전에는 길이 대비
        // 비율이라, 길이가 늘면 이미 그어진 부분까지 굵어졌습니다.
        _DripTaper ("줄기가 가늘어지는 거리(m)", Range(0.05, 2)) = 0.6

        // 늘인 판에서 앵커가 놓인 판 좌표의 y 입니다. 런타임(SplatQuadLayout)이 셉니다.
        // <b>상한을 좁히면 안 됩니다</b> — 작은 몸통에 긴 줄기면 0.95 까지 올라가는데,
        // MaterialPropertyBlock 은 Range 를 안 자르고 Material 은 자릅니다. 그러면
        // 게임은 멀쩡한데 캡처 도구만 틀린 그림을 냅니다.
        _BodyOffsetY ("앵커의 판 좌표 y", Range(0, 1)) = 0

        _Seed ("자국마다 다른 씨앗", Float) = 0

        // 획 한 판이 덮는 거리(m)입니다. 전역 빗금(_CarDriveHatchParams.x)은 지면과 건물에
        // 맞춘 큰 값이라 30cm 짜리 자국에 쓰면 획이 한두 개밖에 안 걸립니다.
        _HatchScale ("획 한 판이 덮는 거리(m)", Range(0.05, 2)) = 0.6
        _HatchBite ("획이 자국을 갉는 정도", Range(0, 1)) = 1

        // HatchingRig 가 씬에 없을 때만 물러설 자리입니다.
        _DitherPixelSize ("디더 격자 (화면 픽셀, 물러설 때만)", Range(1, 12)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest+50"
        }

        Pass
        {
            Name "Splat"
            Tags { "LightMode" = "UniversalForward" }

            // 면에 얹히는 자국이라 깊이는 쓰지 않습니다. 바닥과 z 다툼을 피하려고
            // 살짝 띄워 두는데(런타임에서 법선 방향으로 offset), 그래도 남는 다툼은
            // 여기서 눌러 줍니다.
            Offset -1, -1
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CarDriveHatch.hlsl"
            #include "CarDriveDither.hlsl"
            #include "../LowPoly/CarDriveNoise.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                // <b>앵커 기준 미터.</b> 판이 커져도 같은 월드점은 같은 값입니다.
                // 프래그먼트에는 판 UV 가 아예 오지 않습니다 — 그래야 되돌아갈 수 없습니다.
                float2 posMS      : TEXCOORD0;

                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Age;
                float _BodyRadius;
                float _NoiseScale;
                float _EdgeBite;
                float _CoreSize;
                float _CoreBoost;
                float _DripAmount;
                float _DripStart;
                float _DripReach;
                float _DripPitch;
                float _DripTaper;
                float _BodyOffsetY;
                float _Seed;
                float _HatchScale;
                float _HatchBite;
                float _DitherPixelSize;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);

                // 판의 월드 변 길이(m). <b>단위 quad 를 localScale 로만 늘여 쓰므로</b>
                // 월드 행렬의 열 길이가 곧 변의 길이입니다.
                // (BuildQuad 가 1x1 인 것이 전제입니다. SplatLayoutTests 가 그것을 고정합니다.)
                float4x4 m = GetObjectToWorldMatrix();
                float halfW = 0.5 * length(float3(m._m00, m._m10, m._m20));
                float halfH = 0.5 * length(float3(m._m01, m._m11, m._m21));

                // 앵커(물이 실제로 닿은 자리)를 원점으로 한 미터 좌표입니다.
                // 정점 위치에 대해 선형이라 보간해도 정확합니다.
                float2 p = input.uv * 2.0 - 1.0;
                o.posMS = float2(p.x * halfW, (p.y - _BodyOffsetY) * halfH);

                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 mm = input.posMS;                 // 앵커 기준 미터
                float rBody = max(_BodyRadius, 0.005);

                // 몸통이 테두리 갉기로 밀려날 수 있는 가장 먼 거리(m)와, 가장 긴 줄기의 끝(m).
                // 이 밖은 무엇으로도 안 채워지므로 값잡음 네 번을 통째로 건너뜁니다.
                float outer = rBody * (1.05 + _EdgeBite * 0.5);
                float lowest = max(outer, _DripStart + _DripReach * _DripAmount * 1.5);
                bool outside = (mm.y > outer) || (mm.y < -lowest) || (abs(mm.x) > outer);

                float raw = 0.0;

                // <b>여기서 discard 하면 안 됩니다.</b> 아래 CarDriveHatchValue 안에 ddx/ddy 가
                // 있어서, 한 쿼드에서 일부 레인만 빠져나가면 밉 단계가 무너져 원경이 지글거립니다
                // (CarDriveHatch.hlsl 머리말이 경고하는 그 함정입니다).
                // 이 분기 안에는 텍스처가 하나도 없으므로 건너뛰어도 안전합니다.
                [branch] if (!outside)
                {
                    // ── 몸통: 형태에 맵니다 (keijiro/BloodFx, CC0) ──
                    //
                    // 잡음 좌표를 몸통 반지름으로 나눕니다. 그래서 자국이 자라면 실루엣이
                    // 제자리에서 <b>닮은꼴로</b> 넓어질 뿐, 무늬가 미끄러지거나 뒤섞이지 않습니다.
                    float2 nb = mm / rBody;
                    float2 seedOffset = float2(_Seed * 13.7, _Seed * 7.31);

                    float n = ValueNoise(nb * _NoiseScale + seedOffset) * 0.68
                            + ValueNoise(nb * _NoiseScale * 2.6 + seedOffset) * 0.32;

                    float r0 = length(nb);

                    // 테두리를 잡음으로 갉습니다. 원 그대로 두면 도장 자국이 됩니다.
                    float d = r0 + (n - 0.5) * _EdgeBite;

                    // <b>1 을 넘게 여유를 둡니다.</b> 몸통이 정확히 1 이면 나이가 조금만 들어도
                    // 곧바로 문턱 아래로 내려가 몸통 전체가 한꺼번에 성겨집니다.
                    float body = (1.0 - smoothstep(0.30, 1.05, d)) * 1.25;

                    // 물이 고이는 가운데만 따로 진하게. 이게 없으면 얼룩이 균일해서
                    // "젖었다"가 아니라 "칠했다"로 보입니다. 심도 잡음으로 흔들어
                    // 마른 뒤 완벽한 동그라미가 남지 않게 합니다.
                    float core = 1.0 - smoothstep(0.0, max(_CoreSize, 0.02),
                                                  r0 + (n - 0.5) * _CoreSize * 0.7);

                    // <b>saturate 하지 않고 들고 갑니다.</b> 가운데는 1 을 넘게 쌓여 있어야
                    // 마를 때 테두리부터 사라지고 심이 끝까지 남습니다.
                    raw = body + core * _CoreBoost;

                    // ── 흘러내림: 앵커 기준 미터에만 맵니다 ──
                    //
                    // 여기서는 판도 몸통도 기준이 아닙니다. (mixandjam/Splatoon-Ink, MIT —
                    // 붓을 월드 좌표 + 미터 반지름으로 표현하면 반지름을 키워도 이미 칠한
                    // 자리가 안 움직인다는 원칙.)
                    [branch] if (_DripAmount > 0.001 && _DripReach > 0.0001)
                    {
                        // 머리는 <b>스탬프 때 못 박은 미터 깊이</b>입니다. 몸통이 자라도 안 내려갑니다.
                        float below = -mm.y - _DripStart;

                        [branch] if (below > 0.0)
                        {
                            float pitch = max(_DripPitch, 0.005);

                            // 흔들림도 미터입니다. 폭이 완벽히 일정하면 자로 그은 막대가 됩니다.
                            float wobble = (ValueNoise(float2(_Seed * 2.3, below * 2.5)) - 0.5) * pitch * 0.25;

                            // 줄기 자리 = 앵커에서 잰 미터 / 칸 간격(m).
                            // 칸 간격이 미터로 고정이라 <b>몸통이 자라도 줄기가 안 벌어집니다.</b>
                            float lane = ValueNoise(float2((mm.x + wobble) / pitch + _Seed * 3.1, 0.37));

                            // 이 줄기의 끝(m). <b>시간을 타는 것은 여기 하나뿐입니다.</b>
                            float tipAt = _DripReach * _DripAmount * (0.3 + lane * 1.2);

                            // 굵기와 진하기는 <b>절대 깊이</b>의 함수입니다. 예전에는
                            // below/reach 라는 <b>비율</b>이었기 때문에, 길이가 늘면 이미 그어진
                            // 부분까지 함께 굵어지고 진해졌습니다. 절대 깊이로 바꾸면
                            // 늘어나는 것은 끝뿐입니다.
                            float t = saturate(below / max(_DripTaper, 0.01));

                            // 문턱을 내려갈수록 올리면 잡음이 문턱을 넘는 x 구간이 좁아져
                            // 줄기가 저절로 뾰족해집니다.
                            float taper = step(0.5 + t * 0.34, lane);
                            float fade = saturate(1.0 - t * 0.85);

                            // 끝만 4cm 로 눕힙니다. 자라는 것은 이 창이 내려가는 것뿐입니다.
                            float tip = saturate((tipAt - below) / 0.04);

                            // 몸통 폭 안에서만 시작합니다. 밖에서 시작하면 허공에서 흐릅니다.
                            // <b>줄기 수식에서 몸통을 타는 것은 여기 하나뿐이고, 단조 증가만</b>
                            // 합니다 — 있던 줄기가 사라지거나 옮겨지는 일은 불가능합니다.
                            float within = 1.0 - smoothstep(0.55, 1.05, abs(mm.x) / rBody);

                            raw += taper * within * fade * tip * 1.15;
                        }
                    }

                    // <b>빼서 말립니다.</b> 곱하면 전체가 고르게 옅어져 한꺼번에 사라지는데,
                    // 젖은 자국은 얇은 테두리부터 마르고 고인 가운데가 끝까지 남습니다.
                    raw -= _Age * 1.35;
                }

                float coverage = saturate(raw);

                [branch] if (_CarDriveHatchParams.w >= 0.5)
                {
                    // <b>손그림 획을 문턱으로 씁니다.</b> 세계의 그늘을 긋는 그 TAM 이 그대로
                    // 자국을 갉습니다. 잉크가 뭉친 자리는 늦게까지 젖어 있고, 종이가 드러난
                    // 자리부터 마릅니다. 나무·풀의 Bayer 디더와 달리 <b>사람이 지운 자국</b>이
                    // 됩니다.
                    //
                    // 무늬는 <b>월드 좌표</b>로 읽습니다. 판 좌표로 읽으면 자국이 번져 커질 때
                    // 무늬까지 함께 늘어나 고무 도장이 늘어난 꼴이 됩니다.
                    //
                    // 톤은 <b>한 단계로 고정</b>합니다(게이지와 같은 이유). 위치에 따라 톤을
                    // 흔들면 마르는 동안 무늬 자체가 바뀌어 획이 기어다녀 보입니다.
                    half pattern = CarDriveHatchValue(0.6h, input.positionWS, input.normalWS,
                                                      max(_HatchScale, 0.01), 1.0h);

                    // 게이지와 같은 꼴입니다. 덮임에 (1 + 갉기) 를 곱해 두면 고인 가운데는
                    // 어떤 무늬값이라도 넘어 통짜로 남고, 테두리로 갈수록 짙은 획만 버팁니다.
                    // 이 곱이 없으면 종이(1)인 자리가 가운데에도 구멍을 뚫습니다.
                    half bite = (half)_HatchBite;
                    clip(coverage * (1.0h + bite) - pattern * bite - 0.0001);
                }
                else
                {
                    // HatchingRig 가 없으면 획을 그을 수 없습니다. 그냥 두면 테두리가
                    // 매끈한 원이 되어 이 셰이더의 이유가 사라지므로 디더로 물러섭니다.
                    float2 cell = input.positionCS.xy / max(_DitherPixelSize, 1.0);
                    clip(coverage - CarDriveDitherThreshold(cell) - 0.0001);
                }

                return half4(_Color.rgb, _Color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
