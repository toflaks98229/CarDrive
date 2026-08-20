// 풀에 남은 <b>눌린 자국</b>을 담아 두는 지도를 그립니다.
//
// 위에서 내려다본 한 장의 그림입니다. 플레이어를 따라다니며, 한 픽셀이 땅의 한 뼘에 대응합니다.
//   R — 얼마나 눌려 있는지 (1이면 완전히 누움, 0이면 다 일어섬)
//   G — 이 자국이 얼마나 오래 남는 자국인지 (무게에서 나온 값)
//
// <b>자국을 좌표 배열로 들고 있을 수는 없습니다.</b>
// 차가 지나간 길을 담으려면 수백 개가 필요한데, 풀 정점마다 그만큼 훑으면 감당이 안 됩니다.
// 지도에 칠해 두면 풀은 자기 자리의 픽셀 하나만 읽으면 됩니다.
//
// 이 셰이더는 매 프레임 한 번 돌며 세 가지를 합니다.
//   1. 플레이어가 움직인 만큼 지난 장을 밀어서 받아 옵니다. (지도가 따라다니므로)
//   2. 시간이 지난 만큼 옅게 만듭니다. 옅어지는 속도는 G에 적힌 값에 따라 다릅니다.
//   3. 이번에 지나간 자리에 새 자국을 찍습니다.
Shader "CarDrive/Grass Trample Map"
{
    Properties
    {
        _MainTex ("지난 장", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 한 번에 찍을 수 있는 자국의 수입니다.
            // <b>GrassTrampleMap.MaxSegments 와 반드시 같아야 합니다.</b>
            #define TRAMPLE_SEGMENT_MAX 16

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 지나간 자리. xy가 지난 자리, zw가 지금 자리입니다. (월드 XZ)
            float4 _TrampleSegments[TRAMPLE_SEGMENT_MAX];

            // x 반경(m), y 얼마나 오래 남는 자국인지(0~1)
            float4 _TrampleShape[TRAMPLE_SEGMENT_MAX];

            float  _TrampleCount;

            // 지도가 덮는 땅. xy가 한가운데(월드 XZ), z가 한 변의 길이(m)입니다.
            float4 _MapBounds;

            // 지난 장에서 같은 자리를 찾기 위해 옮길 양입니다. (UV 단위)
            float2 _MapShift;

            // 지난 프레임에서 흐른 시간(초)입니다.
            float  _StepSeconds;

            // 자국이 남는 시간의 아래/위 한계(초)입니다.
            float2 _LifeRange;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            /// <summary>점에서 선분까지의 거리입니다.</summary>
            float DistanceToSegment(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-6));

                return distance(p, a + ab * t);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 지난 장에서 같은 <b>땅의 자리</b>를 찾아 옵니다.
                //    지도는 플레이어를 따라다니므로, 화면상 같은 픽셀이 같은 땅이 아닙니다.
                float2 previousUV = input.uv + _MapShift;

                float2 previous = float2(0.0, 0.0);
                if (all(previousUV > 0.0) && all(previousUV < 1.0))
                {
                    previous = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, previousUV, 0).rg;
                }

                // 2. 시간이 지난 만큼 옅어집니다.
                //    얼마나 버티는지는 G에 적혀 있습니다. 무거운 것이 남긴 자국일수록 오래갑니다.
                float life = lerp(_LifeRange.x, _LifeRange.y, previous.g);
                float amount = max(0.0, previous.r - _StepSeconds / max(life, 0.01));

                float remembered = previous.g;

                // 3. 이번에 지나간 자리를 찍습니다.
                float2 worldXZ = _MapBounds.xy + (input.uv - 0.5) * _MapBounds.z;

                int count = (int)_TrampleCount;

                [loop]
                for (int i = 0; i < count; i++)
                {
                    float4 segment = _TrampleSegments[i];
                    float radius = _TrampleShape[i].x;
                    if (radius <= 0.001) continue;

                    float d = DistanceToSegment(worldXZ, segment.xy, segment.zw);

                    // 안쪽 절반은 완전히 눌립니다. 바깥은 부드럽게 풀립니다.
                    float w = 1.0 - smoothstep(radius * 0.5, radius, d);
                    if (w <= amount) continue;

                    // 더 세게 눌린 쪽이 이깁니다. 그 자국의 수명도 함께 덮어씁니다.
                    amount = w;
                    remembered = _TrampleShape[i].y;
                }

                return half4(amount, remembered, 0, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
