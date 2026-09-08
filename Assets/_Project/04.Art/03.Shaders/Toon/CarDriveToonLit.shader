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
        [Toggle(_GRAIN_ON)] _UseGrain ("바탕 텍스처를 결로 쓰기", Float) = 0
        [HDR] _BaseMapGain ("결 이득 (맵 평균을 1로 맞춤)", Color) = (1, 1, 1, 1)
        _BaseMapStrength ("결 세기 (0이면 바탕색만)", Range(0, 1)) = 1

        // <b>얼룩은 켠 머티리얼만 받습니다.</b> 이 셰이더는 35개 머티리얼이 함께 쓰는데,
        // 하늘·차 내부·UI 처럼 오줌이 닿을 수 없는 것까지 전부 얼룩 코드를 컴파일하면
        // 배리언트가 늘고 셰이더를 고칠 때마다 재컴파일 범위가 그만큼 넓어집니다.
        // 앞으로 벽을 여러 에셋으로 바꿔 나갈 예정이라면 그 마찰이 실제 비용이 됩니다.
        //
        // 새 에셋은 이 체크 하나로 참여합니다. 다른 셰이더라면 CarDriveSplatMap.hlsl 을
        // 포함하고 세 줄만 쓰면 됩니다 — 벽마다가 아니라 <b>셰이더 종류마다 한 번</b>입니다.
        [Toggle(_SPLAT_ON)] _SplatOn ("오줌 얼룩을 받는가", Float) = 0

        // 땅 얼룩(CarDriveToonTerrain)과 <b>같은 이름·같은 기본값</b>입니다.
        // 다르면 같은 오줌인데 땅과 벽이 다른 물건으로 보입니다.
        _SplatColor ("얼룩 색", Color) = (0.78, 0.68, 0.32, 0.85)
        _SplatDarken ("젖으면 어두워지는 정도", Range(0, 1)) = 0.45
        _SplatGloss ("젖으면 생기는 반짝임", Range(0, 1)) = 0.55
        _SplatNoiseScale ("테두리 잡음 잘기 (1m 당 주기)", Range(0.5, 20)) = 3.5
        _SplatEdgeBite ("테두리를 갉는 정도", Range(0, 2)) = 1.7
        _SplatEdgeSharp ("테두리 경사 세우기", Range(1, 12)) = 1.5
        _SplatHatchScale ("획 한 판이 덮는 거리(m)", Range(0.05, 2)) = 0.6
        _SplatHatchBite ("획이 얼룩을 갉는 정도", Range(0, 1)) = 1

        [Header(Toon Shading)]
        _MidPoint ("명암 경계 (낮을수록 밝은 면이 넓음)", Range(0, 1)) = 0.35
        _Softness ("경계 부드러움", Range(0, 0.5)) = 0.05
        _ShadowSoftness ("그림자 경계 부드러움", Range(0, 1)) = 0.25
        _StepSoftness ("단계 사이 부드러움", Range(0, 1)) = 0.35
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


        // 차의 램프처럼 <b>스스로 빛나는 면</b>을 위한 값입니다. 검정이면 아무 일도 하지 않습니다.
        //
        // <b>키워드를 두지 않았습니다.</b> 더하기 한 번뿐이라 끄고 켜서 아낄 것이 없고,
        // 키워드를 두면 이 셰이더를 함께 쓰는 35개 머티리얼의 배리언트가 통째로 두 배가 됩니다.
        [HDR] _EmissionColor ("스스로 내는 빛 (검정이면 끔)", Color) = (0, 0, 0, 1)

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

        [Header(Hand Drawn)]
        [Toggle(_HATCHING)] _UseHatching ("빗금으로 음영 그리기", Float) = 0

        // <b>움직이는 것에는 이것도 켜야 합니다.</b> 빗금은 기본이 월드 좌표라
        // 건물·바위처럼 붙박이인 것에 맞춰져 있습니다. 차처럼 달리는 물체에 그대로 켜면
        // 획이 차체 위를 미끄러져, 그린 것이 아니라 <b>비춘 것</b>으로 보입니다.
        //
        // ⚠ 물체의 스케일이 1 이어야 합니다. 이유는 CarDriveToonLighting.hlsl 에 적어 두었습니다.
        [Toggle(_HATCH_LOCAL)] _HatchLocal ("빗금을 물체에 붙이기 (움직이는 것)", Float) = 0

        [Header(Cutout)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("알파 컷아웃 쓰기 (잎처럼 뚫린 것)", Float) = 0
        _Cutoff ("컷아웃 기준", Range(0, 1)) = 0.5

        [Header(Distance Fade)]
        [Toggle(_DITHER_FADE)] _UseDitherFade ("멀어지면 디더로 지우기", Float) = 0
        _FadeStart ("지워지기 시작하는 거리(m)", Float) = 240
        _FadeEnd ("완전히 지워지는 거리(m)", Float) = 330
        _FadeScatter ("사라지는 때 어긋내기", Range(0, 1)) = 0.6

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

        // 세운 면까지 덮는 전역 젖음 지도입니다. <b>켠 머티리얼만</b> 포함합니다.
        #ifdef _SPLAT_ON
        #include "CarDriveSplatMap.hlsl"
        #endif

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _BaseMapGain;
            half   _BaseMapStrength;
            half4  _SplatColor;
            half   _SplatDarken;
            half   _SplatGloss;
            float  _SplatNoiseScale;
            half   _SplatEdgeBite;
            half   _SplatEdgeSharp;
            float  _SplatHatchScale;
            half   _SplatHatchBite;
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
            half   _RimStrength;
            half   _RimWidth;
            half4  _RimColor;
            half4  _EmissionColor;
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
            float  _FadeScatter;
        CBUFFER_END

        // 시야 거리에서 유도한 디더 페이드 구간입니다. ViewRangeScaler 가 매 프레임 씁니다.
        //
        // <b>왜 전역인가.</b> 위의 _FadeStart/_FadeEnd 는 재질에 구워져 있습니다(240~330m).
        // 시야 거리를 줄이면 그리기 거리가 그보다 앞으로 당겨져, <b>페이드가 시작되기도 전에
        // 나무가 통째로 잘립니다.</b> 이 프로젝트가 한 번 겪은 문제이고 그 기록이
        // TerrainPerformanceSetup 주석에 남아 있습니다.
        //
        // 재질 값을 실행 중에 고치면 에디터에서 그 변경이 에셋에 저장됩니다.
        // 전역은 그런 일이 없고 재질을 복제할 필요도 없습니다.
        //
        // 설정되지 않으면 0 이므로, 그때는 재질 값으로 물러섭니다.
        float _CarDriveFadeStart;
        float _CarDriveFadeEnd;

        // 페이드의 기준이 될 카메라 자리입니다. w 가 1이면 값이 들어와 있다는 뜻입니다.
        //
        // <b>왜 GetCameraPositionWS() 를 쓰지 않는가.</b> 그 함수는 지금 그리고 있는 카메라를
        // 돌려주는데, <b>그림자 패스에서는 그것이 빛의 가상 카메라</b>입니다. 그 자리로 거리를 재면
        // 나무가 해에서 먼 순서로 지워져, 보이는 나무와 그림자가 서로 다른 집합이 됩니다.
        float4 _CarDriveEye;

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
        // <b>행렬과 곡선은 CarDriveToonLighting.hlsl 에 있습니다.</b> 풀도 같은 방식으로
        // 사라지는데(LowPolyGrass), 복사해 두면 한쪽만 고쳐지는 날이 옵니다.
        // 여기 남은 것은 <b>이 재질의 프로퍼티를 읽는 부분</b>뿐입니다.

        /// 페이드의 기준이 될 카메라 자리입니다. 전역이 없으면 예전대로 물러섭니다.
        float3 CarDriveEyePosition()
        {
            return _CarDriveEye.w > 0.5 ? _CarDriveEye.xyz : GetCameraPositionWS();
        }

        /// <summary>
        /// 이 오브젝트만의 0~1 값입니다. <b>나무마다 사라지는 때를 어긋내는 데</b> 씁니다.
        ///
        /// 오브젝트 원점(<c>unity_ObjectToWorld</c> 의 이동 성분)에서 뽑습니다.
        /// 인스턴스마다 다르고 프레임 사이에 변하지 않아야 하므로 화면 좌표나 시간은 쓸 수 없습니다.
        ///
        /// 월드 좌표를 그대로 sin 에 넣으면 먼 곳에서 정밀도가 무너져 이웃한 나무가 같은 값을
        /// 받습니다. frac 으로 0~1 에 접어 넣고 씁니다. (풀의 GrassBladeSeed 와 같은 방식입니다)
        /// </summary>
        float CarDriveObjectSeed()
        {
            float2 p = frac(float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m23) * 0.017);
            return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        /// 거리에 따라 얼마나 남을지 구합니다. 1이면 그대로, 0이면 다 지웁니다.
        half CarDriveFadeAmount(float3 positionWS)
        {
            #if defined(_DITHER_FADE)
                float d = length(CarDriveEyePosition() - positionWS);

                // 전역이 들어와 있으면 그것을 씁니다. 시야 거리와 함께 움직여야
                // 그리기 거리보다 페이드가 먼저 끝납니다.
                bool useGlobal = _CarDriveFadeEnd > 0.001;
                float s = useGlobal ? _CarDriveFadeStart : _FadeStart;
                float e = useGlobal ? _CarDriveFadeEnd : _FadeEnd;

                half fade = CarDriveFadeCurve(d, s, e);

                // <b>나무마다 사라지는 때를 어긋냅니다.</b>
                //
                // 아래 <c>clip</c> 의 문턱값은 <b>화면 픽셀</b>로 뽑는 4x4 격자입니다. 가까운 건물이나
                // 바위처럼 화면을 넓게 덮는 것에는 잘 맞지만, <b>멀어진 나무에는 맞지 않습니다.</b>
                // 200m 앞의 나무는 화면에서 몇 픽셀이라 격자 한 칸 안에 통째로 들어가고,
                // 그러면 문턱 하나를 넘는 순간 <b>나무 전체가 한꺼번에 나타납니다.</b>
                // 멀수록, 저해상도일수록 심해집니다.
                //
                // 그래서 나무마다 다른 값만큼 페이드를 미리 깎아 둡니다. 그러면 같은 거리에서도
                // <b>어떤 나무는 벌써 나타나고 어떤 나무는 아직</b>이라, 나무 하나하나는 여전히
                // 툭 나타나도 <b>숲은 서서히 채워집니다.</b> 그리는 양은 늘지 않습니다.
                //
                // 풀이 잎마다 같은 일을 하고 있고 그 이유도 같습니다. (LowPolyGrass 의 _FadeScatter)
                // 0 으로 두면 아래 식은 fade 그대로가 되어 예전 동작으로 돌아갑니다.
                half band = CarDriveOrderedThreshold(CarDriveObjectSeed()) * (half)_FadeScatter;

                return saturate((fade - band) / max(1.0h - band, 0.05h));
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
            p.shadowSoftness = _ShadowSoftness;
            p.stepSoftness = _StepSoftness;
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
            #pragma shader_feature_local_fragment _HATCHING
            #pragma shader_feature_local_fragment _HATCH_LOCAL
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _DITHER_FADE
            #pragma shader_feature_local_fragment _SPLAT_ON
            #pragma shader_feature_local_fragment _GRAIN_ON
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

                float3 nrm = normalize(input.normalWS);
                #ifdef _GRAIN_ON
                    // <b>맵은 결만, 값은 바탕색이 냅니다.</b> 사진 텍스처는 대개 색조가 있고
                    // 어두워서 그대로 곱하면 팔레트가 물들고 명도가 통째로 내려앉습니다.
                    // _BaseMapGain 이 맵의 채널별 평균을 1 로 끌어올려 그 둘을 상쇄하고,
                    // _BaseMapStrength 가 결의 세기를 정합니다. 툰 램프는 명암을 계단으로
                    // 끊으므로, 사진의 계조를 100% 쓰면 밝은 절반이 하얗게 타 버립니다.
                    //
                    // <b>왜 별도 셰이더가 아닌가.</b> 이 프로젝트는 이미 얼룩(_SPLAT_ON)을
                    // 같은 방식으로 옵트인시킵니다 — 켠 머티리얼만 그 배리언트를 컴파일합니다.
                    // 패스 네 개를 복제하면 600 줄이 갈라져 따로 늙습니다.
                    half3 grain = lerp(half3(1.0h, 1.0h, 1.0h),
                                       baseSample.rgb * _BaseMapGain.rgb, _BaseMapStrength);
                    half3 albedo = grain * _BaseColor.rgb;
                #else
                    half3 albedo = baseSample.rgb * _BaseColor.rgb;
                #endif

                #ifdef _SPLAT_ON
                // ── 오줌 얼룩 ──
                //
                // <b>판(quad)을 소환하지 않습니다.</b> 예전에는 벽에 자국이 튈 때마다 판을 하나씩
                // 눕혔는데, 그러면 자국 수만큼 드로우가 늘고 고정 풀이 한 바퀴 돌면 살아 있는
                // 자국이 그대로 사라졌습니다. 지금은 머티리얼이 지도를 읽습니다 —
                // 자국이 몇이든 드로우는 늘지 않고 상한도 없습니다.
                //
                // <b>삼중평면입니다.</b> 위에서 내려다본 지도 하나로는 벽을 못 덮습니다.
                // 벽의 위아래 기둥이 전부 같은 XZ 좌표라 같은 텍셀을 가리켜, 발밑에 튄 오줌이
                // 벽 꼭대기까지 젖게 만듭니다. 법선으로 세 지도를 섞어 그것을 피합니다.
                //
                // 지도가 없거나 이 자리가 마르면 wet 이 0 이라 아래가 전부 사라집니다 —
                // <b>마른 표면의 그림은 예전과 픽셀 단위로 같습니다.</b>
                half wet = CarDriveSplatStainTriplanar(input.positionWS, nrm,
                                                       _SplatNoiseScale, _SplatEdgeBite, _SplatEdgeSharp,
                                                       _SplatHatchScale, _SplatHatchBite);

                half3 stained = lerp(albedo * (1.0h - _SplatDarken), _SplatColor.rgb, _SplatColor.a);
                albedo = lerp(albedo, stained, wet);
                #endif

                ToonSurface s;
                s.albedo = albedo;
                s.normalWS = nrm;
                s.positionWS = input.positionWS;
                s.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                ToonParams tp = BuildToonParams();
                #ifdef _SPLAT_ON
                tp.specularStrength = max(tp.specularStrength, _SplatGloss * wet);
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half3 color = ToonShade(s, tp, shadowCoord);

                // ── 스스로 내는 빛 ──
                //
                // <b>툰 음영이 끝난 뒤에 더합니다.</b> 등불은 해를 등졌다고 어두워지지 않고,
                // 빗금도 그 위에 그으면 안 됩니다 — 잉크는 물체의 그늘을 그리는 것이지
                // 켜진 등을 덮는 것이 아닙니다. <c>ToonShade</c> 가 빗금까지 마친 자리가 여기입니다.
                //
                // <b>안개보다는 앞입니다.</b> 멀어지는 미등은 안개에 묻혀야 거리가 읽힙니다.
                color += _EmissionColor.rgb;

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
            #pragma shader_feature_local_fragment _DITHER_FADE

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

                // 거리 디더가 쓸 자리입니다. <b>그림자 바이어스를 먹이기 전</b>의 값이어야
                // 색 패스와 같은 거리가 나옵니다.
                float3 positionWS : TEXCOORD1;
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
                output.positionWS = positionWS;
                return output;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                // 잎이 뚫려 있는데 그림자가 통짜로 지면 나무가 아니라 상자 그림자가 됩니다.
                CarDriveApplyAlphaClip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a);

                // <b>지워진 나무는 그림자도 지웁니다.</b>
                //
                // 예전에는 여기를 비워 두고 "그림자는 50m 안쪽, 디더는 240m 부터라 늘 1"이라고
                // 적어 두었습니다. 그때는 맞았지만 <b>그 두 거리가 이제 함께 움직입니다</b> —
                // 그림자 거리는 시야 사다리가 정하고(ViewDistances.Ladder.Shadow),
                // 페이드 구간도 시야에서 나옵니다. 설정의 그림자 거리를 올리면 그림자가
                // 페이드 구간 안까지 들어와, <b>보이지 않는 나무의 그림자만 남습니다.</b>
                //
                // 거리의 기준은 <c>_CarDriveEye</c> 입니다. 이 패스에서
                // <c>GetCameraPositionWS()</c> 는 빛의 가상 카메라라 쓸 수 없습니다.
                CarDriveApplyDitherFade(input.positionWS, input.positionCS.xy);

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
