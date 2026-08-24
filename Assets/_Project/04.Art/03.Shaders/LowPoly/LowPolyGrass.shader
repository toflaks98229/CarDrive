// 로우폴리 · 코지 룩의 풀 셰이더입니다.
//
// 터레인 디테일 메시로 심어 인스턴싱으로 그립니다.
// 지오메트리 셰이더를 쓰지 않습니다. Unity 6의 URP에서는 권장되지 않고 일부 기기에서 아예 못 씁니다.
//
// <b>거리에 따라 다르게 칠합니다.</b>
//
//   가까운 풀 — 위에서 빛을 받아 밑동은 그늘지고 끝은 밝은 그라데이션이 집니다.
//               잎이 크게 보이는 자리라 이 명암이 잎의 부피감을 만듭니다.
//   먼 풀    — 단색으로 눕습니다.
//               멀리서는 잎 하나가 몇 픽셀이라, 명암이 남아 있으면 부피가 아니라
//               <b>자글거리는 잡음</b>으로 보입니다. 그래서 색을 하나로 눕힙니다.
//
// <b>멀어지면 잎이 하나씩 사라집니다.</b>
//
// 그리는 거리(<c>Terrain.detailObjectDistance</c>)는 하드 컷이라, 그 앞에서 다 지워져 있지 않으면
// 풀이 통짜로 튀어나옵니다. 지워지는 구간은 <see cref="ViewRangeScaler"/> 가 전역으로 넘깁니다 —
// 실제 그리는 거리는 rangeScale 과 속도 단계에 따라 실행 중에 49m·36.8m·24.5m 로 바뀌고,
// 재질에 숫자로 적어 두면 그 변화를 따라갈 수 없습니다.
//
// 지우는 방식은 <b>나무와 같은 Bayer 행렬</b>인데 단위가 다릅니다. 나무는 화면 픽셀마다,
// 풀은 <b>잎마다</b> 문턱값을 뽑습니다. 이유는 GrassBladeSeed 위에 적었습니다.
//
// 그라데이션의 기준은 잎 자신의 위아래 비율이 아니라 <b>지면에서 잰 실제 높이</b>입니다.
// 잎 자신의 비율로 정하면 키 작은 잎의 끝과 키 큰 잎의 중간이 같은 눈높이에서 색이 갈라져,
// 그 차이가 곧 잎의 윤곽선이 됩니다.
//
// <b>계산은 거의 전부 정점에서 합니다.</b>
// 풀은 화면을 겹겹이 덮기 때문에 픽셀 하나를 여러 번 그립니다. 픽셀에서 하는 계산은
// 그 겹친 횟수만큼 되풀이되므로, 정점으로 옮기면 그만큼 그대로 절약됩니다.
// 잎은 정점이 넷뿐인 납작한 판이라 정점에서 계산해도 눈에 띄는 차이가 없습니다.
Shader "CarDrive/LowPoly Grass"
{
    Properties
    {
        [Header(Ground Colors)]
        _GrassColorA ("잔디 (어두운 쪽)", Color) = (0.694, 0.494, 0.180, 1)
        _GrassColorB ("잔디 (밝은 쪽)",   Color) = (0.855, 0.667, 0.290, 1)
        _ColorNoiseScale ("색 얼룩 크기", Range(0.002, 0.2)) = 0.022

        [Header(Blade)]
        _TipColor    ("잎 끝 색",   Color) = (0.855, 0.667, 0.290, 1)
        _RootTint    ("잎 밑동 그늘", Range(0, 1)) = 0.26
        _TipBlend    ("잎 끝 색 섞기", Range(0, 1)) = 0.35
        _NormalUp    ("법선 눕히기", Range(0, 1)) = 1
        _CanopyHeight ("풀밭 높이 기준 (m)", Range(0.1, 2)) = 0.75

        [Header(Gradient Distance)]
        _GradientNear ("그라데이션 유지 거리 (m)", Float) = 10
        _GradientFar  ("단색이 되는 거리 (m)", Float) = 30

        [Header(Trample)]
        _PushLay    ("눕는 정도", Range(0, 1)) = 1
        _PushSpread ("바깥으로 밀리는 거리 (m)", Range(0, 1)) = 0.35
        _PushHeightReach ("위아래로 닿는 높이 (m)", Range(0.2, 8)) = 2

        [Header(Wind)]
        _WindStrength ("바람 세기", Range(0, 1)) = 0.18
        _WindSpeed    ("바람 속도", Range(0, 5)) = 1.1
        _WindScale    ("바람 물결 크기", Range(0.005, 0.5)) = 0.08

        [Header(Distance)]
        _FadeStart ("가라앉기 시작 (m)", Float) = 35
        _FadeEnd   ("완전히 눕는 거리 (m)", Float) = 69
        _FadeScatter ("잎이 흩어져 사라지는 정도", Range(0, 0.9)) = 0.6

        [Header(Cozy)]
        _ShadowColor  ("그늘 색", Color) = (0.596, 0.514, 0.494, 1)

        [Header(Toon Ramp)]
        [Toggle(_TOON_RAMP)] _UseRamp ("램프 텍스처 쓰기", Float) = 0
        [NoScaleOffset] _ToonRampMap ("램프 (가로축 = 밝기)", 2D) = "white" {}
        _MidPoint ("명암 경계", Range(0, 1)) = 0.42
        _Softness ("경계 부드러움", Range(0, 0.5)) = 0.06
        _ShadowStrength ("그림자 세기", Range(0, 1)) = 0.7
        _ShadeSteps   ("밝기 단계 수", Range(1, 8)) = 1
        _AmbientBoost ("주변광 보정", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // 잎은 얇은 판이라 뒷면도 그려야 어느 쪽에서 봐도 보입니다.
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // GPU 구동 경로. 이 변형에서만 아래 Setup 이 컴파일됩니다.
            #pragma instancing_options procedural:Setup
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            // 아래 둘이 없어서 그림자가 제대로 들어오지 않았습니다.
            //  - _SHADOWS_SOFT 는 URP 가 fragment 키워드로 다루므로 반드시 이 형태여야 합니다.
            //  - _ADDITIONAL_LIGHTS 가 없으면 헤드라이트가 풀을 비추지 못합니다.
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma shader_feature_local_fragment _TOON_RAMP

            // 그림자를 부드럽게 하는 여러 번 샘플링은 넣지 않습니다.
            // 풀은 지면과 같이 어두워지기만 하면 되고, 그림자 경계가 잎 위에서
            // 부드러운지 아닌지는 보이지도 않습니다. (_SHADOWS_SOFT 를 뺐습니다)

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "LowPolyGround.hlsl"
            #include "../Toon/CarDriveToonLighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _GrassColorA;
                half4  _GrassColorB;
                half4  _TipColor;
                half4  _ShadowColor;
                half   _UseRamp;
                half   _MidPoint;
                half   _Softness;
                half   _ShadowStrength;
                float  _ColorNoiseScale;
                float  _RootTint;
                float  _TipBlend;
                float  _NormalUp;
                float  _CanopyHeight;
                float  _GradientNear;
                float  _GradientFar;
                float  _PushLay;
                float  _PushSpread;
                float  _PushHeightReach;
                float  _WindStrength;
                float  _WindSpeed;
                float  _WindScale;
                float  _FadeStart;
                float  _FadeEnd;
                float  _FadeScatter;
                float  _ShadeSteps;
                float  _AmbientBoost;
            CBUFFER_END

            // 그리는 거리에서 유도한 페이드 구간입니다. ViewRangeScaler 가 매 프레임 씁니다.
            //
            // <b>왜 전역인가.</b> 위의 _FadeStart/_FadeEnd 는 재질에 구워져 있습니다(35~68.6m).
            // detailDistance 70m 에 0.5 와 0.98 을 곱해 에디터 도구가 적어 넣은 값인데,
            // 그 도구는 rangeScale 도 속도 단계도 몰랐습니다. 실제로 그리는 거리는
            // 49m 이고 속도가 붙으면 36.8m·24.5m 까지 줄어듭니다.
            // <b>시속 90 이상에서는 풀이 100% 키로 서 있는 자리에서 그대로 잘렸습니다.</b>
            // (나무가 똑같은 이유로 한 번 튀었고, 그래서 나무도 전역으로 옮겼습니다)
            //
            // 재질 값을 실행 중에 고치면 에디터에서 그 변경이 에셋에 저장됩니다.
            // 전역은 그런 일이 없고 재질을 복제할 필요도 없습니다.
            //
            // 설정되지 않으면 0 이므로, 그때는 재질 값으로 물러섭니다.
            // (셰이더만 열어 보는 에디터 미리보기가 그렇습니다)
            float _CarDriveGrassFadeStart;
            float _CarDriveGrassFadeEnd;

            // 풀을 밟고 지나가는 것들입니다. GrassPushField 가 매 프레임 채웁니다.
            // xyz 가 자리, w 가 반경입니다.
            //
            // 이 수는 <b>GrassPushField.MaxPushers 와 반드시 같아야 합니다.</b>
            // 한쪽만 고치면 넘긴 자리 일부가 조용히 버려집니다.
            #define GRASS_PUSHER_MAX 16

            // 머티리얼마다 다른 값이 아니라 게임 전체가 공유하는 값이라
            // UnityPerMaterial 바깥에 둡니다.
            float4 _GrassPushers[GRASS_PUSHER_MAX];
            float  _GrassPusherCount;

            // 지나간 길에 남은 자국을 담아 둔 지도입니다. GrassTrampleMap 이 매 프레임 그립니다.
            // R 이 얼마나 눌려 있는지입니다. (G는 수명이라 여기서는 쓰지 않습니다)
            //
            // 위의 배열은 <b>지금 겹쳐 있는 것</b>만 다룹니다. 지나간 길이 남으려면
            // 궤적을 전부 들고 있어야 하는데 그건 배열로 감당이 안 됩니다.
            TEXTURE2D(_GrassTrampleMap);
            SAMPLER(sampler_GrassTrampleMap);

            // xy 가 지도가 덮는 땅의 한가운데, z 가 한 변의 길이(m)입니다.
            float4 _GrassTrampleBounds;

            // --- GPU 구동 경로 ---
            //
            // 터레인 디테일 대신 GpuGrassRenderer 가 간접 드로우로 그릴 때만 켜집니다.
            // 터레인 디테일로 그릴 때는 아래가 통째로 컴파일에서 빠지므로,
            // <b>기존 경로는 이 변경의 영향을 전혀 받지 않습니다.</b>
            //
            // 요점은 unity_ObjectToWorld 를 버퍼에서 만들어 준다는 것뿐입니다.
            // 밟힘·바람·색은 전부 그 행렬과 셰이더 전역만 보고 있어서 손댈 것이 없습니다.
        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<float4> _GrassInstances;
            StructuredBuffer<uint>   _GrassVisibleIndices;

            // 포기 크기의 아래·위입니다. 자리 해시로 이 사이를 고릅니다.
            float2 _GrassScaleRange;

            // 자리에서 0~1 값을 하나 뽑습니다. 버퍼에 크기를 담지 않으려고 씁니다.
            // (담으면 인스턴스당 바이트가 늘고, 이 게임은 포기가 백만 단위입니다)
            float GrassHash01(float3 p)
            {
                return frac(sin(dot(p.xz, float2(12.9898, 78.233))) * 43758.5453);
            }

            void Setup()
            {
                uint index = _GrassVisibleIndices[unity_InstanceID];
                float4 packed = _GrassInstances[index];

                float3 origin = packed.xyz;
                float yaw = packed.w;

                float s = lerp(_GrassScaleRange.x, _GrassScaleRange.y, GrassHash01(origin));

                float sn, cs;
                sincos(yaw, sn, cs);

                // y축 회전 + 균일 배율 + 이동. 열 우선으로 씁니다.
                unity_ObjectToWorld  = float4x4(
                    cs * s, 0,     sn * s, origin.x,
                    0,      s,     0,      origin.y,
                    -sn * s, 0,    cs * s, origin.z,
                    0,      0,     0,      1);

                // 법선을 쓰려면 역행렬도 필요합니다. 회전과 균일 배율뿐이라
                // 전치 후 배율로 나누면 됩니다. 일반 역행렬을 구할 이유가 없습니다.
                float inv = 1.0 / max(s, 1e-5);
                unity_WorldToObject = float4x4(
                    cs * inv,  0,        -sn * inv, 0,
                    0,         inv,       0,        0,
                    sn * inv,  0,         cs * inv, 0,
                    0,         0,         0,        1);

                // 이동분은 회전·배율을 되돌린 뒤 빼야 합니다.
                unity_WorldToObject._m03_m13_m23 = -mul((float3x3)unity_WorldToObject, origin);
            }
        #endif

            // ── 잎마다 다른 문턱값 ──
            //
            // 나무는 <b>화면 픽셀</b>마다 문턱값을 뽑아 성기게 버립니다(CarDriveToonLit).
            // 풀에 그대로 쓰면 곤란합니다. 40m 앞의 풀잎은 화면에서 두어 픽셀이라
            // 픽셀 단위로 버리면 잎이 있다 없다 하며 <b>반짝입니다.</b> 게다가 clip 이
            // 들어가면 이 패스가 얼리-Z 를 잃는데, 풀은 화면을 겹겹이 덮는 쪽이라 손해가 큽니다.
            //
            // 그래서 <b>같은 행렬을 잎 단위로</b> 씁니다. 문턱값을 넘긴 잎은 정점 셋을
            // 밑동 한 점으로 모아 버립니다. 삼각형이 찌그러져 사라지므로 픽셀이 아예 나오지 않고,
            // 멀어질수록 그리는 삼각형이 <b>실제로 줄어듭니다.</b>

            /// <summary>
            /// 이 잎만의 씨앗을 뽑습니다. <b>한 잎의 정점 셋에서 반드시 같아야 합니다.</b>
            ///
            /// 잎의 법선이 그 표식입니다. VegetationBuilder 가 잎마다 다른 방향(face)으로 굽고,
            /// 한 잎의 정점 셋은 그 값을 그대로 나눠 갖습니다. 메시에 잎 번호를 따로 담을 수도
            /// 있지만, 포기가 수십만이라 정점 스트림을 늘리고 싶지 않습니다.
            ///
            /// 포기 자리를 섞는 이유가 있습니다. 한 종의 포기는 모두 <b>같은 메시</b>라
            /// 법선만 쓰면 어느 포기에서든 같은 잎이 동시에 사라져 격자가 보입니다.
            /// </summary>
            /// <param name="normalOS">잎의 오브젝트 공간 법선. 잎마다 다릅니다.</param>
            /// <param name="baseWS">포기가 심어진 자리. 포기마다 다릅니다.</param>
            /// <returns>0 이상 1 미만</returns>
            float GrassBladeSeed(float3 normalOS, float3 baseWS)
            {
                // 월드 좌표를 그대로 sin 에 넣으면 먼 곳에서 정밀도가 무너져 이웃한 포기가
                // 같은 값을 받습니다. frac 으로 0~1 에 접어 넣고 씁니다.
                float2 p = frac(baseWS.xz * 0.017) + normalOS.xz * 0.37;

                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                // 예전에는 여기에 <b>계산이 끝난 색</b>을 담았습니다. 그래서 그림자도 정점에서만
                // 샘플됐고, 풀 한 포기의 몇 안 되는 정점 사이를 보간하니 사실상 그림자가
                // 들어오지 않았습니다. 이제 재료를 넘기고 픽셀에서 빛을 계산합니다.
                half3  albedo     : COLOR;
                float  fogFactor  : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                // 이 포기가 심어진 자리입니다. 바람과 거리를 여기 기준으로 정해야
                // 한 포기 안의 잎들이 따로 놀지 않습니다.
                float3 baseWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // 바람을 휘게 할 때 쓰는 값입니다. 밑동을 땅에 붙여 두려면
                // 잎 자신의 위아래 비율이어야 합니다.
                float bend = saturate(input.texcoord.y);

                float dist = distance(baseWS, GetCameraPositionWS());

                // --- 멀어지면 사라지기 ---
                //
                // 전역이 들어와 있으면 그것을 씁니다. 그리는 거리와 함께 움직여야
                // <b>잘리기 전에</b> 페이드가 끝납니다. 없으면 재질 값으로 물러섭니다.
                bool useGlobal = _CarDriveGrassFadeEnd > 0.001;
                float fadeStart = useGlobal ? _CarDriveGrassFadeStart : _FadeStart;
                float fadeEnd = useGlobal ? _CarDriveGrassFadeEnd : _FadeEnd;

                // 이 포기가 얼마나 남을지입니다. 1이면 그대로, 0이면 사라집니다.
                // 나무와 같은 곡선을 씁니다. (CarDriveToonLighting.hlsl)
                float fade = CarDriveFadeCurve(dist, fadeStart, fadeEnd);

                // <b>잎마다 사라지는 때를 어긋냅니다.</b>
                //
                // 어긋내지 않으면 한 포기의 잎 열여덟이 <b>같이</b> 가라앉고, 그러면 풀밭이
                // 카메라를 둘러싼 원 모양으로 낮아집니다. 그 원이 차를 따라다니는 것이
                // 예전 코드가 "구간을 길게 잡아야 원형 자국이 안 남는다"고 적어 둔 문제입니다.
                // 구간을 늘리는 것은 그 자국을 흐리게 할 뿐 없애지는 못합니다.
                //
                // 잎마다 문턱값을 달리 주면 사라지는 순서가 흩어져, 원 대신 <b>성겨지는 결</b>이
                // 됩니다. 문턱값이 큰 잎일수록 먼저 갑니다.
                //
                // _FadeScatter 가 0 이면 아래 식은 sink = fade 가 되어 예전 동작 그대로입니다.
                half band = CarDriveOrderedThreshold(GrassBladeSeed(input.normalOS, baseWS)) * (half)_FadeScatter;
                float sink = saturate((fade - band) / max(1.0h - band, 0.05h));

                // 사라진 잎은 정점 셋이 밑동 한 점으로 모여 삼각형이 없어집니다.
                // 픽셀이 아예 나오지 않으므로 멀어질수록 그리는 양이 실제로 줄어듭니다.
                positionWS = lerp(baseWS, positionWS, sink);

                // --- 밟힘 ---
                //
                // 지나가는 것의 자리마다 얼마나 눌렸는지 재고, 가장 세게 눌린 값을 씁니다.
                // 더하지 않고 가장 큰 값을 쓰는 이유가 있습니다. 차처럼 여러 개를 겹쳐
                // 붙인 경우, 더하면 겹치는 자리만 두 배로 눌려 얼룩이 집니다.
                float pressed = 0.0;
                float2 shove = float2(0.0, 0.0);

                int pushCount = (int)_GrassPusherCount;

                [loop]
                for (int pi = 0; pi < pushCount; pi++)
                {
                    float4 pusher = _GrassPushers[pi];
                    if (pusher.w <= 0.001) continue;

                    float2 away = baseWS.xz - pusher.xz;
                    float toward = length(away);

                    // 위아래로 멀리 떨어져 있으면 누르지 않습니다.
                    // 언덕 위를 지나는 차가 비탈 아래 풀까지 눕히면 이상해 보입니다.
                    float reach = max(_PushHeightReach, 0.01);
                    float vertical = 1.0 - saturate((abs(baseWS.y - pusher.y) - reach) / reach);

                    // <b>안쪽 절반은 완전히 눕습니다.</b>
                    // 가장자리만 부드럽게 일어서게 두면 차 밑이 깨끗하게 비지 않아,
                    // 남은 풀이 차 바닥을 뚫고 실내로 올라옵니다.
                    float w = (1.0 - smoothstep(pusher.w * 0.55, pusher.w, toward)) * vertical;
                    if (w <= 0.0) continue;

                    pressed = max(pressed, w);
                    shove += (toward > 0.001 ? away / toward : float2(1.0, 0.0)) * w;
                }

                // 지나간 길에 남은 자국도 함께 봅니다.
                // 지금 아무도 서 있지 않아도, 아까 지나간 자리라면 아직 눌려 있습니다.
                if (_GrassTrampleBounds.z > 0.001)
                {
                    float2 mapUV = (baseWS.xz - _GrassTrampleBounds.xy) / _GrassTrampleBounds.z + 0.5;

                    if (all(mapUV > 0.0) && all(mapUV < 1.0))
                    {
                        float mark = SAMPLE_TEXTURE2D_LOD(_GrassTrampleMap, sampler_GrassTrampleMap, mapUV, 0).r;
                        pressed = max(pressed, mark);
                    }
                }

                if (pressed > 0.0)
                {
                    // 밑동은 땅에 붙어 있으므로 끝으로 갈수록 세게 밀립니다.
                    positionWS.xz += shove * _PushSpread * bend * pressed;
                    positionWS.y = lerp(positionWS.y, baseWS.y, saturate(pressed * _PushLay));
                }

                // 바람. 넓은 물결이 지나가듯 흔들리게 좌표와 시간을 함께 씁니다.
                // 눌린 풀은 흔들리지 않습니다. 눌려 있는데 흔들리면 밟힌 느낌이 사라집니다.
                float phase = (baseWS.x + baseWS.z) * _WindScale + _Time.y * _WindSpeed;
                float2 sway = float2(sin(phase), cos(phase * 0.73)) * _WindStrength;
                positionWS.xz += sway * bend * bend * sink * (1.0 - pressed);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                // --- 여기서부터 색 ---

                GroundPalette palette;
                palette.grassA = _GrassColorA.rgb;
                palette.grassB = _GrassColorB.rgb;
                palette.dirtA  = _GrassColorA.rgb;
                palette.dirtB  = _GrassColorB.rgb;
                palette.roadA  = _GrassColorA.rgb;
                palette.roadB  = _GrassColorB.rgb;
                palette.noiseScale = _ColorNoiseScale;

                // 아래 지면과 같은 함수, 같은 좌표로 뽑습니다.
                // 이래야 풀밭이 시작되는 자리에 선이 생기지 않습니다.
                half3 ground = SampleGroundAlbedo(palette, positionWS.xz, half4(1, 0, 0, 0));

                // 지면에서 잰 실제 높이. 같은 높이의 잎은 키에 상관없이 같은 색이 됩니다.
                float height = saturate((positionWS.y - baseWS.y) / max(_CanopyHeight, 0.01));

                // <b>가까울수록 그라데이션이 살고, 멀수록 단색이 됩니다.</b>
                float grad = 1.0 - saturate((dist - _GradientNear) / max(_GradientFar - _GradientNear, 0.001));

                half root = (half)(_RootTint * grad);
                half tip  = (half)(_TipBlend * grad);

                half3 albedo = ground * lerp(1.0h - root, 1.0h, (half)height);
                albedo = lerp(albedo, _TipColor.rgb, (half)(height * height) * tip);

                // 잎의 진짜 법선을 쓰면 잎마다 각도가 달라 밝기가 튀고, 그 차이가
                // 잎의 윤곽선이 됩니다. 위로 눕혀 지면과 같이 빛을 받게 합니다.
                float3 normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS),
                                                 float3(0, 1, 0), _NormalUp));

                // 빛 계산은 프래그먼트로 넘깁니다.
                // 그림자 좌표를 정점에서 잡으면 캐스케이드 경계에서 어긋나고,
                // 무엇보다 풀 한 포기의 정점이 너무 적어 그림자가 통째로 들어오거나
                // 통째로 빠집니다. 픽셀마다 재야 잎에 그림자가 걸칩니다.
                output.albedo = albedo;
                output.positionWS = positionWS;
                output.normalWS = normalWS;

                return output;
            }

            /// <summary>인스펙터 값을 툰 설정 묶음으로 모읍니다.</summary>
            ToonParams BuildGrassToonParams()
            {
                ToonParams p = DefaultToonParams();

                // 기존 재질에 이미 들어 있는 값들을 그대로 씁니다.
                // 이름만 바뀌었을 뿐이라 지금까지 맞춰 둔 풀 색이 그대로 유지됩니다.
                p.steps = _ShadeSteps;
                p.shadowTint = _ShadowColor.rgb;
                p.ambient = _AmbientBoost;

                p.midPoint = _MidPoint;
                p.softness = _Softness;
                p.shadowStrength = _ShadowStrength;

                // 풀잎에 하이라이트와 외곽 빛을 얹으면 잎마다 반짝여 지저분합니다.
                p.specularStrength = 0.0h;
                p.rimStrength = 0.0h;
                p.heightStrength = 0.0h;

                return p;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 예전에는 여기서 안개만 섞었습니다. 값싸긴 했지만 그 대가로
                // <b>그림자를 받지 못했습니다.</b> 이제 픽셀마다 그림자를 샘플합니다.
                // 지면과 같은 ToonShade 를 쓰므로 풀밭과 땅의 명암 경계가 이어집니다.
                ToonSurface s;
                s.albedo = input.albedo;
                s.normalWS = normalize(input.normalWS);
                s.positionWS = input.positionWS;
                s.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half3 color = ToonShade(s, BuildGrassToonParams(), shadowCoord);

                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }

        // <b>주의 — 이 패스는 ForwardLit 의 정점 계산을 따라 하지 않습니다.</b>
        //
        // 위에서는 밟힘·바람·거리 페이드로 정점을 월드 공간에서 옮기는데, 여기서는
        // 그냥 클립 공간으로 보냅니다. 그래서 사라진 잎도 깊이를 씁니다.
        //
        // 지금은 문제가 되지 않습니다. URP 에셋 셋(Performant·Balanced·HighFidelity) 모두
        // <c>m_RequireDepthTexture: 0</c> 이라 이 패스가 <b>한 번도 돌지 않습니다.</b>
        //
        // 깊이 텍스처가 필요한 기능(소프트 파티클·화면 공간 안개·외곽선 등)을 켜게 되면
        // 그때 위의 계산을 여기로 옮겨야 합니다. 옮기지 않으면 풀이 <b>보이지 않는 자리에서</b>
        // 뒤를 가립니다.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupDepth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<float4> _GrassInstances;
            StructuredBuffer<uint>   _GrassVisibleIndices;
            float2 _GrassScaleRange;

            float GrassHash01Depth(float3 p)
            {
                return frac(sin(dot(p.xz, float2(12.9898, 78.233))) * 43758.5453);
            }

            // ForwardLit 의 Setup 과 같은 행렬을 만들어야 합니다.
            // 다르면 깊이와 색이 어긋나 풀이 자기 그림자에 잘립니다.
            void SetupDepth()
            {
                uint index = _GrassVisibleIndices[unity_InstanceID];
                float4 packed = _GrassInstances[index];

                float3 origin = packed.xyz;
                float s = lerp(_GrassScaleRange.x, _GrassScaleRange.y, GrassHash01Depth(origin));

                float sn, cs;
                sincos(packed.w, sn, cs);

                unity_ObjectToWorld = float4x4(
                    cs * s, 0, sn * s, origin.x,
                    0,      s, 0,      origin.y,
                    -sn * s, 0, cs * s, origin.z,
                    0,      0, 0,      1);
            }
        #endif

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);

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

    Fallback Off
}
