// 오줌이 닿은 자리에 남는 <b>젖은 자국</b>입니다. 땅에도 벽에도 붙습니다.
//
// <b>왜 데칼이 아닌가.</b> URP 의 Decal Renderer Feature 는 깊이 텍스처를 요구하는데
// 이 프로젝트는 세 URP 에셋 모두 m_RequireDepthTexture 가 0 입니다. 켜면 불투명 전체를
// 다시 그리는 프리패스가 생기고, 드로우 제출이 이미 병목이라 그 값을 치를 수 없습니다.
// 그래서 닿은 면에 맞춰 눕힌 판 하나로 그립니다.
//
// <b>모양.</b> 가운데는 진하게 고이고 가장자리는 잡음으로 흩어집니다. 물이 튀어 고인
// 자국은 원이 아니라 <b>중심이 짙고 테두리가 너덜너덜한</b> 얼룩입니다. 원형 감쇠에
// 값 잡음을 곱해 테두리를 갉아내고, 중심에는 따로 심을 더해 진하게 만듭니다.
//
// <b>벽에서는 흘러내립니다.</b> 세운 면에 붙으면 자국 아래로 줄기가 자랍니다.
// 세로로 늘인 잡음으로 줄기 자리를 고르고, 나이에 따라 아래로 길어집니다.
// 스플래툰의 물감이 벽을 타고 흐르는 것과 같은 생각입니다.
//
// <b>마르는 것은 손그림 획입니다.</b> 알파로 서서히 비우면 이 게임의 다른 것들과 어긋납니다.
// 그렇다고 Bayer 디더로 지우면 기계가 지운 자국이 됩니다 — 디더는 <b>멀어서 사라지는</b>
// 나무와 풀의 문법이지, 손으로 그린 세계의 문법이 아닙니다. 자국은 지면·건물의 그늘을
// 긋는 그 TAM 을 그대로 문턱으로 삼아, <b>획이 성겨지며</b> 마릅니다. 고인 가운데는 끝까지
// 잉크가 뭉쳐 남고 얇은 테두리부터 종이가 드러납니다.
//
// 획은 <b>월드 공간</b>에서 긋습니다. 자국이 번져 커져도 무늬는 땅에 박혀 있어야
// 잉크가 종이에 얹힌 것으로 읽힙니다. 판을 따라 무늬가 늘어나면 고무 도장이 됩니다.
// 같은 이유로 지면·소품과 같은 삼중평면을 쓰므로 획의 결이 서로 맞습니다.
Shader "CarDrive/Splat"
{
    Properties
    {
        _Color ("자국 색", Color) = (0.78, 0.68, 0.32, 1)

        _Age ("나이 (0 갓 튄 것 ~ 1 다 마름)", Range(0, 1)) = 0
        _Grow ("번짐 (0 점 ~ 1 다 퍼짐)", Range(0, 1)) = 1

        _NoiseScale ("테두리 잡음 잘기", Range(1, 24)) = 7
        _EdgeBite ("테두리를 갉는 정도", Range(0, 1)) = 0.55
        _CoreSize ("가운데 심 크기", Range(0.02, 0.6)) = 0.22
        _CoreBoost ("가운데 심 진하기", Range(0, 1)) = 0.85

        _DripAmount ("흘러내림 (벽에서만)", Range(0, 1)) = 0
        _DripLength ("흘러내림 길이 (몸통 반지름 배수)", Range(0, 3)) = 1.4
        _DripWidth ("줄기 굵기 (클수록 가늘고 촘촘)", Range(4, 48)) = 24

        // 벽에서는 판을 세로로 늘여 줄기가 자랄 자리를 만듭니다. 늘인 만큼 여기에
        // 알려 주어야 몸통이 타원으로 찌그러지지 않습니다. (세로 길이 / 가로 길이)
        _Aspect ("판의 세로/가로 비", Range(1, 4)) = 1

        // 늘인 판에서 몸통을 <b>위쪽</b>으로 올립니다. 가운데 두면 줄기가 자랄 아래쪽이
        // 절반밖에 안 남습니다. 판 좌표(-1~1) 기준입니다.
        _BodyOffsetY ("몸통을 위로 올리는 정도", Range(0, 0.8)) = 0

        _Seed ("자국마다 다른 씨앗", Float) = 0

        // 획 한 판이 덮는 거리(m)입니다. 전역 빗금(_CarDriveHatchParams.x)은 지면과 건물에
        // 맞춘 큰 값이라 30cm 짜리 자국에 쓰면 획이 한두 개밖에 안 걸립니다.
        // 자국은 작으므로 따로, 더 잘게 긋습니다.
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
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Age;
                float _Grow;
                float _NoiseScale;
                float _EdgeBite;
                float _CoreSize;
                float _CoreBoost;
                float _DripAmount;
                float _DripLength;
                float _DripWidth;
                float _Aspect;
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
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 판 한가운데를 원점으로. uv 는 0~1 이라 -1~1 로 폅니다.
                float2 p = input.uv * 2.0 - 1.0;

                // <b>몸통은 늘인 판 안에서도 동그래야 합니다.</b> 벽에서는 줄기가 자랄
                // 자리를 만들려고 판을 세로로 늘이는데, 그대로 두면 얼룩까지 세로로
                // 늘어나 계란이 됩니다. 세로를 가로 단위로 되돌리고, 몸통을 위로 올려
                // 아래쪽을 통째로 줄기에게 내줍니다.
                float2 pb = float2(p.x, (p.y - _BodyOffsetY) * _Aspect);

                // 씨앗으로 잡음 자리를 옮깁니다. 옮기지 않으면 자국 열 개가
                // 전부 같은 무늬라 도장을 찍은 것처럼 보입니다.
                float2 seedOffset = float2(_Seed * 13.7, _Seed * 7.31);

                // ── 몸통 ──
                //
                // 번짐(_Grow)이 반지름을 키웁니다. 갓 튀었을 때는 점만 하다가 퍼집니다.
                float radius = max(_Grow, 0.02);
                float d = length(pb) / radius;

                // 테두리를 잡음으로 갉습니다. 원 그대로 두면 도장 자국이 됩니다.
                float n = ValueNoise(input.uv * _NoiseScale + seedOffset) * 0.68
                        + ValueNoise(input.uv * _NoiseScale * 2.6 + seedOffset) * 0.32;
                d += (n - 0.5) * _EdgeBite;

                // 가장자리는 부드럽게, 안쪽은 꽉 차게.
                //
                // <b>1 을 넘게 여유를 둡니다.</b> 몸통이 정확히 1 이면 나이가 조금만 들어도
                // 곧바로 문턱 아래로 내려가 몸통 전체가 한꺼번에 성겨집니다. 여유를 두면
                // 처음 얼마간은 젖은 채로 버티다가 테두리부터 마릅니다.
                float body = (1.0 - smoothstep(0.30, 1.05, d)) * 1.25;

                // ── 가운데 심 ──
                //
                // 물이 고이는 곳이라 여기만 따로 진하게 더합니다. 이게 없으면
                // 얼룩이 균일해서 "젖었다"가 아니라 "칠했다"로 보입니다.
                // <b>심도 잡음으로 흔듭니다.</b> 그냥 두면 마른 뒤 획 한가운데 <b>완벽한 동그라미</b>가
                // 남습니다. 컴퍼스로 그린 자국이 되어, 둘레를 애써 갉아 놓은 것이 무색해집니다.
                float core = 1.0 - smoothstep(0.0, max(_CoreSize, 0.02),
                                              length(pb) + (n - 0.5) * _CoreSize * 0.7);

                // <b>saturate 하지 않고 들고 갑니다.</b> 가운데는 1 을 넘게 쌓여 있어야
                // 마를 때 테두리부터 사라지고 심이 끝까지 남습니다. 여기서 잘라 버리면
                // 나이가 조금만 들어도 가운데까지 한꺼번에 성겨집니다.
                float raw = body + core * _CoreBoost;

                // ── 벽에서 흘러내림 ──
                //
                // uv.y 가 아래쪽인 판으로 세워 붙입니다(런타임이 그렇게 눕힙니다).
                // 세로로 길게 늘인 잡음으로 줄기 자리를 고르고, 아래로만 자랍니다.
                if (_DripAmount > 0.001)
                {
                    // <b>몸통이 끝나는 자리부터</b> 잽니다. 판 한가운데부터 재면 줄기가
                    // 몸통 안에서 시작해 통째로 묻혀 버립니다 — 실제로 그렇게 나왔습니다.
                    // 단위는 몸통과 같은 가로 단위입니다.
                    float below = -(p.y - _BodyOffsetY) * _Aspect - radius * 0.4;

                    // <b>아래로만 흐릅니다.</b> 예전에는 saturate 로 눌러 위쪽이 전부 0 이 되는
                    // 바람에 판의 윗절반이 통째로 채워졌습니다.
                    [branch] if (below > 0.0)
                    {
                        // 줄기 자리는 <b>거의</b> x 에만 달려 있어야 세로로 곧게 흐릅니다.
                        // y 를 크게 섞으면 줄기가 비스듬히 번져 흐른 것으로 안 읽힙니다.
                        // 다만 아주 조금 흔들어 둡니다 — 폭이 완벽히 일정하면 물이 흐른
                        // 자국이 아니라 <b>자로 그은 막대</b>가 됩니다.
                        float wobble = (ValueNoise(float2(_Seed * 2.3, below * 1.7)) - 0.5) * 0.05;
                        float lane = ValueNoise(float2((input.uv.x + wobble) * _DripWidth + _Seed * 3.1, 0.37));

                        // 줄기마다 길이가 달라야 흐른 것처럼 보입니다.
                        float reach = radius * _DripLength * _DripAmount * (0.3 + lane * 1.2);

                        // 자국 몸통 폭 안에서만 시작합니다. 밖에서 시작하면 허공에서 흐릅니다.
                        float within = 1.0 - smoothstep(0.55, 1.05, abs(p.x) / radius);

                        // 절반쯤의 줄기만 실제로 흘러내립니다. 전부 흐르면 커튼이 됩니다.
                        // 1 을 넘겨 더하는 것은 줄기가 <b>젖은 채로</b> 뻗어야 하기 때문입니다.
                        // 성기게 더하면 나이가 붙는 순간 줄기부터 사라집니다.
                        float run = saturate(1.0 - below / max(reach, 0.001));

                        // <b>끝으로 갈수록 가늘어집니다.</b> 문턱을 내려갈수록 올리면 잡음이
                        // 문턱을 넘는 x 구간이 좁아져 줄기가 저절로 뾰족해집니다. 폭이 끝까지
                        // 같으면 흘러내린 물이 아니라 막대를 붙여 놓은 것으로 보입니다.
                        float taper = step(0.5 + (1.0 - run) * 0.34, lane);
                        raw += taper * within * run * 1.15;
                    }
                }

                // ── 마름 ──
                //
                // <b>빼서 말립니다.</b> 곱하면 전체가 고르게 옅어져 한꺼번에 사라지는데,
                // 젖은 자국은 얇은 테두리부터 마르고 고인 가운데가 끝까지 남습니다.
                raw -= _Age * 1.35;

                float coverage = saturate(raw);

                [branch] if (_CarDriveHatchParams.w >= 0.5)
                {
                    // <b>손그림 획을 문턱으로 씁니다.</b> 세계의 그늘을 긋는 그 TAM 이 그대로
                    // 자국을 갉습니다. 잉크가 뭉친 자리는 늦게까지 젖어 있고, 종이가 드러난
                    // 자리부터 마릅니다. 나무·풀의 Bayer 디더와 달리 <b>사람이 지운 자국</b>이
                    // 됩니다.
                    //
                    // 무늬는 <b>월드 좌표</b>로 읽습니다. 판 UV 로 읽으면 자국이 번져 커질 때
                    // 무늬까지 함께 늘어나 고무 도장이 늘어난 꼴이 됩니다. 월드로 읽으면
                    // 잉크가 땅에 박혀 있어 자국만 자랍니다.
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
