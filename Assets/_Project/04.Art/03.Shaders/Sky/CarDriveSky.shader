// 밤길 주행에 맞춘 하늘입니다.
//
// 정적 큐브맵을 쓰지 않는 이유:
// 이 게임은 TimeSystem이 태양을 360도 돌리는 주야 순환이 있습니다. 밤하늘 사진 한 장을
// 붙여 두면 낮이 깨지고, 낮 하늘을 붙이면 밤에 별이 없습니다. 그래서 시간에 따라
// 색과 별이 함께 변하는 셰이더로 만듭니다.
//
// 별도 그림 파일 없이 방향을 해싱해 별을 찍습니다. 격자로 잘라 해싱하므로 별이
// 네모난 점으로 나오는데, 화면을 세로 215픽셀로 줄이는 이 게임에서는 그 편이 오히려 맞습니다.
//
// 색은 계단으로 잘라 씁니다(_Bands). 픽셀화 뒤에도 하늘에 부드러운 그라데이션이
// 남으면 지면과 톤이 어긋나기 때문입니다.
Shader "CarDrive/Sky"
{
    Properties
    {
        [Header(Day)]
        _DayTop        ("낮 - 천정", Color) = (0.35, 0.52, 0.78, 1)
        _DayHorizon    ("낮 - 지평선", Color) = (0.66, 0.74, 0.82, 1)

        [Header(Night)]
        _NightTop      ("밤 - 천정", Color) = (0.03, 0.04, 0.07, 1)
        _NightHorizon  ("밤 - 지평선", Color) = (0.07, 0.09, 0.13, 1)

        [Header(Dusk)]
        _DuskColor     ("여명 - 지평선 물듦", Color) = (0.85, 0.45, 0.22, 1)
        _DuskWidth     ("여명 퍼짐", Range(1, 24)) = 8

        [Header(Sun)]
        _SunSize       ("해 크기", Range(0.001, 0.2)) = 0.035
        _SunColor      ("해 색", Color) = (1, 0.93, 0.78, 1)

        [Header(Stars)]
        _StarDensity   ("별 격자 촘촘함", Range(50, 600)) = 220
        _StarAmount    ("별 개수", Range(0, 0.2)) = 0.018
        _StarBrightness("별 밝기", Range(0, 3)) = 1.1

        [Header(Look)]
        _Bands         ("색 계단 수", Range(2, 64)) = 18
        _DayFactor     ("낮 정도 (0 밤 ~ 1 낮)", Range(0, 1)) = 1
        _StarFade      ("별 가림 (구름)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Background"
            "Queue" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirOS      : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DayTop;
                float4 _DayHorizon;
                float4 _NightTop;
                float4 _NightHorizon;
                float4 _DuskColor;
                float4 _SunColor;
                float  _DuskWidth;
                float  _SunSize;
                float  _StarDensity;
                float  _StarAmount;
                float  _StarBrightness;
                float  _Bands;
                float  _DayFactor;
                float  _StarFade;
            CBUFFER_END

            // 해가 있는 방향입니다. SkyController가 매 프레임 넣어 줍니다.
            float4 _SunDirection;

            // 격자 한 칸에서 0~1 난수를 뽑습니다. 별을 흩뿌리는 데 씁니다.
            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // 색을 계단으로 잘라 픽셀 룩에 맞춥니다.
            float3 Quantize(float3 c, float steps)
            {
                return floor(c * steps + 0.5) / steps;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // 스카이박스 메시는 원점 중심이라 정점 위치가 곧 바라보는 방향입니다.
                output.dirOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dirOS);

                // 1. 위아래 그라데이션. 지평선에서 천정으로 갈수록 짙어집니다.
                float up = saturate(dir.y);
                float gradient = pow(up, 0.55);

                float3 dayCol   = lerp(_DayHorizon.rgb,   _DayTop.rgb,   gradient);
                float3 nightCol = lerp(_NightHorizon.rgb, _NightTop.rgb, gradient);
                float3 sky = lerp(nightCol, dayCol, _DayFactor);

                // 2. 별. 방향을 격자로 잘라 해싱하므로 고개를 돌려도 같은 자리에 있습니다.
                //    낮에는 보이지 않고, 구름이 끼면 가려집니다.
                float3 cell = floor(dir * _StarDensity);
                float  rnd  = Hash13(cell);

                float star = step(1.0 - _StarAmount, rnd);
                float twinkle = 0.65 + 0.35 * Hash13(cell + 17.0);

                // 별은 해가 조금만 떠도 금방 안 보입니다.
                // 1.6 배로는 아침(낮 0.35)에도 별이 가득 남아 어색했습니다.
                float starVisible = saturate(1.0 - _DayFactor * 4.0) * (1.0 - _StarFade);
                // 지평선 근처는 대기가 두꺼워 별이 흐려집니다.
                starVisible *= smoothstep(-0.05, 0.25, dir.y);

                sky += star * twinkle * _StarBrightness * starVisible;

                // 3. 여명. 해가 지평선 가까이 있을 때 그쪽 하늘만 물듭니다.
                float3 sunDir = normalize(_SunDirection.xyz);
                float towardSun = saturate(dot(dir, float3(sunDir.x, 0, sunDir.z) * rsqrt(max(sunDir.x * sunDir.x + sunDir.z * sunDir.z, 1e-4))));
                float lowSun = 1.0 - saturate(abs(sunDir.y) * 3.0);
                float horizonBand = pow(saturate(1.0 - abs(dir.y) * _DuskWidth), 2.0);

                sky += _DuskColor.rgb * pow(towardSun, 3.0) * horizonBand * lowSun;

                // 4. 해. 지평선 아래로 내려가면 사라집니다.
                float sunDot = dot(dir, sunDir);
                float sunDisc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.4, sunDot);
                sky += _SunColor.rgb * sunDisc * saturate(sunDir.y * 6.0 + 0.2);

                return half4(Quantize(saturate(sky), _Bands), 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
