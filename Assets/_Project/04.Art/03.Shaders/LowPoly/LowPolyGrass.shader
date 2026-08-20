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
            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
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
                float  _ShadeSteps;
                float  _AmbientBoost;
            CBUFFER_END

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

                // 멀수록 키를 줄여 지면에 눕힙니다.
                // 구간을 길게 잡아야 풀밭이 원형으로 잘린 자국이 남지 않습니다.
                float sink = 1.0 - saturate((dist - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
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
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
