Shader "CarDrive/Post/Palette"
{
    // ── 화면 전체의 색 수를 줄이고 그 자리를 디더로 메웁니다 ──
    //
    // <b>왜 화면 전체인가.</b> 재질마다 색을 줄이면 각자 줄어들 뿐, <b>조명·안개·하늘이
    // 만드는 그라데이션</b>은 그대로 남습니다. 화면이 하나로 안 묶입니다. 마지막에 한 번
    // 줄이면 그 셋까지 같은 색 수 안으로 들어옵니다.
    //
    // <b>디더는 이미 이 프로젝트의 것입니다.</b> 나무·바위·건물·풀·게이지·얼룩이 전부
    // <c>CarDriveDither.hlsl</c> 의 4x4 Bayer 를 씁니다. 그 파일 주석대로 "디더 무늬가
    // 결점이 아니라 시대 표현으로 읽히기" 때문입니다. 같은 행렬을 여기서도 씁니다 —
    // 다른 난수를 쓰면 화면의 무늬가 두 종류로 갈립니다.
    //
    // <b>계단을 감마 쪽에서 냅니다.</b> 선형값을 그대로 자르면 단계가 밝은 쪽에 몰려
    // 어두운 부분이 뭉갭니다. 제곱근으로 옮겨 자르고 되돌리면 눈이 보는 대로 고르게 나뉩니다.
    Properties
    {
        _Levels ("색 단계 수 (채널당)", Range(2, 32)) = 6
        _Strength ("세기 (0이면 원래 화면)", Range(0, 1)) = 1
        _DitherStrength ("디더 세기", Range(0, 1)) = 1
        _Desaturate ("채도 빼기", Range(0, 1)) = 0
        _DitherPixel ("디더 한 칸의 화면 화소 수 (1080p 기준)", Range(1, 6)) = 1

        // ── 빗금 판 ──────────────────────────────────────────────────────
        //
        // <b>디더 무늬를 손그림 획으로 바꿉니다.</b> 지금까지 Bayer 4x4 가 하던 일을
        // 획 모양 행렬이 대신합니다.
        //
        // <b>왜 여기인가.</b> 재질에 건 빗금은 이 후처리를 통과하지 못합니다 —
        // 밝기를 여섯 단계로 자르고 2 화소로 디더하면 획이 그 계단에 묻힙니다.
        // 실측으로 확인했습니다(조사-손그림_빗금.md). 그런데 <b>디더 자신은 이미
        // 화면 공간</b>이고 이 게임은 그것을 시대 표현으로 받아들이고 있습니다.
        // 그러니 그 무늬를 획으로 바꾸는 것은 새 거짓말을 더하는 것이 아니라
        // <b>이미 있는 무늬의 결을 바꾸는 것</b>입니다.
        //
        // ⚠ 문턱 행렬은 <b>히스토그램이 고와야</b> 합니다. 손그림을 그대로 쓰면
        // 화면 전체가 치우칩니다. 그래서 TAM 여섯 단계를 순위로 펴서 구운 것을
        // 씁니다 — 무늬는 획인데 값은 0~1 에 고르게 흩어져 있습니다.
        [NoScaleOffset] _HatchDitherTex ("빗금 디더 행렬 (순위로 편 것)", 2D) = "gray" {}
        _HatchDither ("빗금 판 섞기 (0=Bayer, 1=획)", Range(0, 1)) = 0
        _HatchDitherScale ("획 한 판이 덮는 화면 화소 (1080p 기준)", Range(64, 1024)) = 256

        // ── 밝기를 따라가는 잉크 ─────────────────────────────────────────
        //
        // <b>왜 문턱만으로는 부족한가.</b> 문턱은 계단이 갈리는 <b>그 자리</b>에서만
        // 듣습니다. 여섯 단계 사이의 넓은 구간에서는 아무 일도 일어나지 않아서,
        // 밝기가 계단을 넘을 때만 획이 <b>튀어나왔다 사라집니다.</b>
        // 손그림은 그렇지 않습니다 — 어두워질수록 획이 <b>차츰</b> 늘어납니다.
        //
        // 여기서는 화면 밝기를 그대로 읽어 <b>그보다 일찍 그어지는 획</b>만 남깁니다.
        // 행렬의 값이 "이 화소가 몇 번째로 잉크가 되는가" 이므로, 어두울수록 더 많은
        // 값이 문턱 아래로 내려와 획이 촘촘해집니다. 계단과 무관하게 이어집니다.
        _HatchInk ("밝기를 따라가는 잉크 세기", Range(0, 1)) = 0
        // (0 이면 획을 안 얹습니다. 아래 두 값이 모양을 정합니다.)

        // ⚠ <b>이것이 없으면 획이 딱딱 잘립니다.</b> 잉크와 종이가 한 화소 사이에서
        // 갈리면 밝기가 조금만 변해도 획 하나가 통째로 나타났다 사라져 <b>기어다니는
        // 것처럼</b> 보입니다. 그 경계를 넓혀 서로 물리게 합니다.
        _HatchSoft ("획이 나타나는 자리의 보간 폭", Range(0.005, 0.4)) = 0.12

        // 획 하나가 <b>덮어 가는 밝기</b>. 곱하는 값이 아니라 빼는 값입니다.
        //
        // ⚠ <b>0.5 같은 값을 넣지 마십시오.</b> 여기는 감마 공간의 밝기라
        // 0.1 만 빼도 화면에서는 20% 가까이 어두워집니다. 0.08~0.16 이 쓸 만합니다.
        _HatchDepth ("획 하나가 덮는 밝기", Range(0, 0.5)) = 0.10

        // ── 채도를 획으로 ────────────────────────────────────────────────
        //
        // <b>손그림에는 흐린 색이 없습니다.</b> 색이 빠진 자리는 옅은 회색이 아니라
        // <b>획이 촘촘한 자리</b>입니다. 잉크 한 자루로 그리는 그림에서 "칙칙함" 은
        // 색을 죽여서가 아니라 선을 더 그어서 만들어집니다.
        //
        // 그래서 화소의 채도를 읽어, 빠진 만큼 <b>획의 문턱을 올립니다.</b>
        // 문턱이 오르면 더 많은 획이 그 아래로 들어와 촘촘해집니다 —
        // 획이 <b>진해지는</b> 것이 아니라 <b>많아집니다.</b> 그것이 종이 위의 잉크가
        // 어두움을 만드는 방식입니다.
        //
        // ⚠ <b>곱해서 올립니다.</b> 더하면 <b>밝고 색 없는 하늘</b>에도 획이 그어집니다.
        // 곱하면 이미 어두운 자리에서만 늘어나, 흰 곳은 흰 채로 남습니다.
        _HatchChroma ("채도가 빠진 만큼 획을 더함", Range(0, 2)) = 0

        // ── 원본 방식: TAM 여섯 단계 ─────────────────────────────────────
        //
        // 여기까지의 획은 <b>순위 행렬</b> 한 장으로 그었습니다. TAM 여섯 단계를 더해
        // 순위로 편 흑백 한 장이라, 무늬는 획인데 <b>단계별 그림은 잃었습니다.</b>
        //
        // 원본(nkihrk/HatchingShader · songrise)은 그렇게 하지 않습니다.
        // 한 장의 <b>R·G·B 채널에 서로 다른 단계의 획</b>을 나눠 담고, 밝기가 그중
        // 어느 단계를 쓸지 고릅니다. 밝은 장에 0·1·2, 어두운 장에 3·4·5 —
        // 그래서 단계마다 <b>사람이 그은 그 획 그대로</b>가 나옵니다.
        //
        // 이 갈래를 켜면 <c>CarDriveHatchValue</c> 를 그대로 부릅니다. 세계에 긋던 것과
        // <b>같은 함수</b>입니다 — 법선에 (0,0,1) 을 주면 삼중평면이 평면 하나로 붕괴해
        // 화면 좌표 빗금이 됩니다(헤더 머리말 참고).
        //
        // ⚠ <c>HatchingRig</c> 이 씬에 있어야 합니다. TAM 두 장을 그것이 넣습니다.
        // 없으면 아래 순위 행렬로 물러섭니다.
        [Toggle] _HatchTam ("원본 방식(TAM 여섯 단계) 쓰기", Float) = 0

        // 이 밝기부터 획이 시작됩니다. 원본의 Density 에 해당합니다.
        _HatchTamTop ("TAM 방식에서 획이 시작되는 밝기", Range(0.2, 1)) = 0.75

        // ── 단계 사이를 잇는 보간 ────────────────────────────────────────
        //
        // <b>획이 단계를 갈아타는 자리가 계단으로 보입니다.</b> 원인은 획이 아니라
        // 획이 읽는 <b>밝기</b>입니다 — 지금은 팔레트가 32 단계로 끊어 놓은 값을
        // 읽습니다. 그 32 가지 중 획이 쓰는 구간에 들어오는 것은 <b>18 가지뿐</b>이라,
        // 여섯 단계를 건너는 동안 밟을 수 있는 자리가 단계당 <b>세 칸</b>입니다.
        // 한 벌의 획에서 다음 벌로 <b>세 번에 나눠 뛰어넘습니다.</b>
        //
        // 계단 나기 <b>전</b>의 밝기에서 뽑으면 그 자리가 연속이 됩니다.
        //
        // ⚠ <b>그런데 화면에서는 거의 티가 안 납니다.</b> 실측입니다 — 1 로 올려도
        // 화면이 0.45/255 밖에 안 바뀝니다. 위 산수는 <b>화소 하나</b>를 두고 한
        // 것인데, 팔레트의 디더가 이웃 화소마다 다른 자리를 골라 주고 있어서
        // 눈에 보이는 밀도는 이미 이어져 있습니다.
        //
        // 그래도 남겨 둡니다. <c>_Levels</c> 를 6~8 로 내리면(그 편이 원래 이 게임의
        // 팔레트였습니다) 밟을 자리가 단계당 한 칸 아래로 떨어져 그때는 이 손잡이가
        // 필요해집니다. 지금은 0 으로 둡니다.
        _HatchToneSmooth ("단계 사이 보간 (0=끊긴 값, 1=원래 값)", Range(0, 1)) = 0

        // ── 보일 (리미티드 프레임 작화) ──────────────────────────────────
        //
        // <b>손으로 그린 그림은 초당 60 장이 아닙니다.</b> 작화는 보통 2s·3s —
        // 초당 8~12 장입니다. 그래서 매 컷 선이 미세하게 다르고, <b>그 흔들림이
        // 사람이 그렸다는 신호</b>가 됩니다.
        //
        // 지금 우리 획은 화면에 완벽하게 고정되어 있습니다. 정지 화면에서는
        // 손그림인데 차가 달리면 <b>유리창에 낀 무늬</b>처럼 보입니다.
        // 획을 월드로 되돌리는 길은 막혀 있으므로(조사-손그림_빗금.md 1~6차),
        // 남은 길은 화면에 둔 채 <b>시간으로 푸는 것</b>입니다.
        //
        // 계단 시계로 획 판을 조금씩 옮깁니다. 몇 분의 1초마다 <b>다시 그린 것</b>이
        // 됩니다. 히스토리 버퍼도 모션 벡터도 필요 없습니다.
        _HatchBoilRate ("작화 초당 장수 (0 이면 끔)", Range(0, 24)) = 0

        // ⚠ <b>이 손잡이는 생각보다 힘이 약합니다.</b> 실측하고 알았습니다 —
        // 폭을 0.20 에서 0.08 로 <b>절반 이하</b>로 줄여도 박자끼리 달라지는 양은
        // 63% → 54% 밖에 안 내려갑니다. 획 하나가 5~10 화소인데 0.08 만 해도
        // 10 화소를 옮기므로, 어느 쪽이든 획은 <b>완전히 다른 자리</b>에 놓입니다.
        //
        // 그래서 실제로 고를 것은 "조금 떨리는가"가 아니라 <b>다시 그리는가 마는가</b>
        // 이고, 세기를 정하는 손잡이는 이것이 아니라 <b>초당 장수</b>입니다.
        // 아주 작게(0.02 이하) 주면 획이 옮겨지는 대신 제자리에서 떠는데,
        // 그것은 손그림이 아니라 <b>영상 노이즈</b>로 보입니다.
        _HatchBoilJump ("작화마다 옮기는 폭 (판의 몇 분의 1)", Range(0, 0.5)) = 0.2

        // ── 한발 늦은 드로잉 ─────────────────────────────────────────────
        //
        // 보일은 작화가 <b>뚝 바뀝니다.</b> 한 박자에 획이 통째로 다른 자리로 갑니다
        // (실측 63%). 실제 작화도 매 장 새로 긋지만, 사람 눈에는 <b>펜이 옮겨 가는</b>
        // 것으로 읽힙니다 — 종이가 순간이동하지는 않으니까요.
        //
        // 그래서 넘어가는 자리를 겹칩니다. 박자가 바뀐 직후에는 <b>직전 작화</b>가
        // 아직 남아 있고, 이 값만큼의 시간에 걸쳐 새 작화로 넘어갑니다.
        // 잉크가 화면을 한 박자 늦게 따라오는 것처럼 보입니다.
        //
        // ⚠ <b>히스토리 버퍼를 쓰지 않습니다.</b> 처음에는 직전 <b>프레임</b>을 들고
        // 섞으려 했는데, 그러려면 렌더러 기능을 새로 써야 하고 모션 벡터가 없어
        // 카메라가 돌 때 <b>화면 전체가 번집니다.</b> 여기서 늦출 것은 화면이 아니라
        // <b>획</b>이므로, 획의 자리를 두 번 읽어 섞는 편이 싸고 정확합니다.
        //
        // ⚠ 겹치는 동안에는 TAM 을 <b>두 번</b> 읽습니다. 다만 이 분기는 시간에만
        // 걸리므로 <b>화면 전체가 같이</b> 갈립니다 — 화소마다 갈리는 분기가 아니라서
        // 미분(ddx/ddy)이 무너지지 않습니다.
        _HatchBoilBlend ("작화가 넘어가며 겹치는 시간 (박자의 몇 분의 1)", Range(0, 1)) = 0

        // ── 종이 ─────────────────────────────────────────────────────────
        //
        // <b>지금까지 이 그림은 아무 데도 안 그려져 있었습니다.</b> 획은 있는데
        // 그 획이 놓인 바탕이 없습니다. 종이 결과 가장자리 어둠을 얹으면 화면이
        // 비로소 <b>한 장의 그림</b>이 됩니다.
        //
        // ⚠ <b>종이는 화면에 고정되어야 맞습니다.</b> 획이 화면에 못 박힌 것은
        // 결함이라 보일로 풀지만, 종이는 원래 안 움직입니다. 여기서는 고정이
        // 정답이라 보일의 <c>penShift</c> 를 쓰지 않습니다.
        //
        // 결 텍스처는 0.5 를 가운데로 구웠습니다. 셰이더가 <c>(값-0.5)*2*세기</c> 로
        // 읽으므로 세기가 0 이면 화면이 한 톨도 안 바뀝니다.
        [NoScaleOffset] _PaperTex ("종이 결 (0.5 가 가운데)", 2D) = "grey" {}
        _PaperGrain ("종이 결이 비치는 정도", Range(0, 1)) = 0
        _PaperScale ("결 한 판이 덮는 화면 화소 (1080p 기준)", Range(64, 1024)) = 512

        // ⚠ <b>밤 운전과 부딪힙니다.</b> 가장자리를 어둡게 하면 그림은 그림다워지는데
        // 헤드라이트 밖을 살피는 일이 어려워집니다. 세게 주지 마십시오.
        _PaperEdge ("가장자리 어둠 (비네트)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Palette"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "../Toon/CarDriveDither.hlsl"

            // 원본 방식(TAM 여섯 단계)을 부르기 위해서입니다. 조명에 기대지 않는
            // 순수 텍스처·산술이라 후처리에서도 그대로 포함할 수 있습니다.
            #include "../Toon/CarDriveHatch.hlsl"

            SAMPLER(sampler_BlitTexture);

            half  _Levels;
            half  _Strength;
            half  _DitherStrength;
            half  _Desaturate;
            half  _DitherPixel;

            TEXTURE2D(_HatchDitherTex);
            SAMPLER(sampler_PointRepeat_HatchDitherTex);
            half  _HatchDither;
            half  _HatchDitherScale;
            half  _HatchInk;
            half  _HatchSoft;
            half  _HatchDepth;
            half  _HatchChroma;
            half  _HatchTam;
            half  _HatchTamTop;
            half  _HatchToneSmooth;
            half  _HatchBoilRate;
            half  _HatchBoilJump;
            half  _HatchBoilBlend;

            // ── 지금 벌어지는 일 ──
            //
            // <c>LookMood</c> 가 밀어 넣는 값입니다. x = 획에 곱할 덤,
            // y = 채도 밀도에 더할 값. <b>0 이 중립</b>이라 그 컴포넌트가 씬에
            // 없으면(또는 꺼져 있으면) 아무 일도 일어나지 않습니다.
            //
            // ⚠ <b>재질이 아니라 전역인 이유.</b> 재질은 에셋이라 에디터에서 값을 쓰면
            // 파일이 바뀌어 남습니다. 재질에 적힌 값은 사람이 정한 기준선으로 두고,
            // 상황에 따라 흔드는 것은 여기서만 합니다.
            float4 _CarDriveLookMood;

            TEXTURE2D(_PaperTex);
            SAMPLER(sampler_LinearRepeat_PaperTex);
            half  _PaperGrain;
            half  _PaperScale;
            half  _PaperEdge;

            /// 박자마다 다른 자리로 흩습니다. 무리수 둘을 쓰는 것은 유리수 비율이면
            /// 몇 장 만에 같은 자리로 돌아와 <b>박자가 보이기</b> 때문입니다.
            float2 Wobble(float beat)
            {
                return frac(float2(beat * 0.6180339887, beat * 0.4142135624)) - 0.5;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord);
                half3 colour = source.rgb;

                // 채도를 빼는 것은 색 수 줄이기와 다른 축입니다. 콘크리트 쪽으로 밀고 싶을 때
                // 쓰라고 따로 두었습니다. 0 이면 아무 일도 하지 않습니다.
                half grey = dot(colour, half3(0.2126h, 0.7152h, 0.0722h));

                // 채도는 <b>회색으로 밀기 전</b>에 잽니다. 뒤에 재면 _Desaturate 가
                // 이미 눌러 놓은 값을 읽게 되어, 손잡이 둘이 같은 일을 두 번 합니다.
                //
                // HSV 의 S 와 같은 식입니다 — 가장 밝은 채널과 가장 어두운 채널의
                // 차이가 곧 색기입니다. 회색은 셋이 같아 0 이 됩니다.
                half hi = max(colour.r, max(colour.g, colour.b));
                half lo = min(colour.r, min(colour.g, colour.b));
                half colourless = 1.0h - (hi - lo) / max(hi, 0.0001h);

                colour = lerp(colour, grey.xxx, _Desaturate);

                // 눈에 고르게 나뉘도록 감마 쪽으로 옮겨 자릅니다.
                half3 encoded = sqrt(max(colour, 0.0h));

                // 상황이 미는 만큼 계단 수를 줄입니다. 지치면 그림이 거칠어집니다.
                // z 가 0 이면 재질에 적힌 그대로입니다.
                half steps = max(_Levels - 1.0h - (half)_CarDriveLookMood.z, 1.0h);
                // ⚠ <b>한 칸이 화면 화소 하나면 1080p 에서 안 보입니다.</b> 참조 화면
                // (White Knuckle)의 가로 자기상관 최소가 k=2,3 에 있어 디더 한 칸이
                // 화면 화소 두셋을 덮습니다. 그쪽은 내부 해상도를 낮춰 그리고 확대해서
                // 그렇게 되는데, 이 게임은 주행 게임이라 해상도를 내리면 노면 차선과
                // 먼 지표가 함께 뭉개집니다. 기하는 또렷이 두고 <b>디더 칸만</b> 키워
                // 같은 인상을 냅니다.
                // ── 해상도에 매이지 않게 ──────────────────────────────────
                //
                // ⚠ <b>이것이 없으면 빌드에서 그림이 달라집니다.</b> 아래 두 무늬는
                // <b>화면 화소</b>로 크기를 잽니다. 그런데 이 게임은 전체화면·네이티브
                // 해상도로 뜹니다(ProjectSettings). 에디터 게임뷰가 960x540 일 때
                // 128 화소짜리 획 한 판은 화면 높이의 <b>23.7%</b> 인데, 1080p 에서는
                // 11.9%, 1440p 에서는 8.9% 입니다. 같은 값인데 획이 <b>두세 배 가늘어져</b>
                // 선이 아니라 잡티로 읽히고, 그러면 명암을 따라가는 것도 안 보입니다.
                //
                // 그래서 <b>1080 을 기준</b>으로 삼아 실제 화면 높이에 맞춰 늘립니다.
                // 이제 값은 "1080p 에서 몇 화소" 라는 뜻이고, 어느 해상도에서도
                // 화면에서 차지하는 <b>몫</b>이 같습니다. 렌더 스케일도 <c>_ScreenParams</c>
                // 에 이미 들어 있으므로 따로 볼 필요가 없습니다.
                half pixelScale = max(_ScreenParams.y, 1.0h) * (1.0h / 1080.0h);

                // ── 보일 ──
                //
                // 획만 옮깁니다. <b>디더는 그대로 둡니다</b> — 그쪽은 그림이 아니라
                // 색을 끊는 장치라, 흔들면 화면 전체가 지글거립니다.
                //
                // 두 무리수로 흩어 놓습니다. 유리수 비율을 쓰면 몇 장 만에 같은 자리로
                // 돌아와 <b>박자가 보입니다.</b>
                float2 penShift = float2(0.0, 0.0);
                float2 penShiftWas = float2(0.0, 0.0);

                // 0 이면 직전 작화를 안 씁니다. 1 이면 새 작화가 다 차오른 것입니다.
                half settled = 1.0h;

                [branch] if (_HatchBoilRate > 0.001h)
                {
                    float clock = _Time.y * _HatchBoilRate;
                    float beat = floor(clock);
                    float reach = max(_HatchDitherScale * pixelScale, 1.0h) * (2.0h * _HatchBoilJump);

                    penShift = Wobble(beat) * reach;
                    penShiftWas = Wobble(beat - 1.0) * reach;

                    // 박자 안에서 얼마나 지났는가. 겹치는 시간을 지나면 1 이 됩니다.
                    half blend = max(_HatchBoilBlend, 0.0001h);
                    settled = smoothstep(0.0h, blend, (half)frac(clock));
                }

                float2 inkPos = input.positionCS.xy + penShift;
                float2 inkPosWas = input.positionCS.xy + penShiftWas;

                float2 cell = floor(input.positionCS.xy / max(_DitherPixel * pixelScale, 1.0h));
                half pattern = CarDriveDitherThreshold(cell);

                // ⚠ <b>점 샘플이어야 합니다.</b> 행렬을 이중선형으로 읽으면 값이
                // 뭉개져 히스토그램의 고름이 깨지고, 그러면 계단이 치우칩니다.
                // 텍스처 임포트를 Point · Repeat · 밉 없음으로 두십시오.
                //
                // 한 번만 읽습니다 — 문턱과 잉크가 <b>같은 행렬</b>을 씁니다.
                // 둘이 다른 무늬를 쓰면 같은 화면에 두 종류의 결이 겹칩니다.
                // 상황이 미는 만큼 획을 더 긋습니다. 둘 다 0 이면 기준선 그대로입니다.
                // <b>여기서 구합니다</b> — 아래 useHatch 가 이미 이 값을 봅니다.
                half inkNow = _HatchInk * (1.0h + (half)_CarDriveLookMood.x);
                half chromaNow = _HatchChroma + (half)_CarDriveLookMood.y;

                half rank = 0.5h;
                bool useHatch = (_HatchDither > 0.001h) || (inkNow > 0.001h);

                [branch] if (useHatch)
                {
                    float2 uv = inkPos / max(_HatchDitherScale * pixelScale, 1.0h);
                    rank = SAMPLE_TEXTURE2D_LOD(_HatchDitherTex,
                                                sampler_PointRepeat_HatchDitherTex, uv, 0).r;

                    // 넘어가는 중이면 직전 작화도 읽어 섞습니다.
                    // 분기가 시간에만 걸려 화면 전체가 같이 갈립니다.
                    [branch] if (settled < 0.999h)
                    {
                        float2 uvWas = inkPosWas / max(_HatchDitherScale * pixelScale, 1.0h);
                        half was = SAMPLE_TEXTURE2D_LOD(_HatchDitherTex,
                                                        sampler_PointRepeat_HatchDitherTex, uvWas, 0).r;
                        rank = lerp(was, rank, settled);
                    }
                    pattern = lerp(pattern, rank, _HatchDither);
                }

                half threshold = (pattern - 0.5h) * _DitherStrength;

                half3 quantised = floor(encoded * steps + 0.5h + threshold) / steps;

                // ── 밝기를 따라가는 잉크 ──
                //
                // 계단을 낸 <b>뒤에</b> 얹습니다. 앞에 얹으면 한 계단보다 얕은 획이
                // 통째로 사라집니다 — 여섯 단계 사이의 미세한 명암은 살아남지 못합니다.
                // 잉크는 종이 위에 얹히는 것이지 종이의 색을 고르는 것이 아닙니다.
                bool drawn = false;

                [branch] if (inkNow > 0.001h)
                {
                    // 밝기를 어디서 뽑는가. 끊긴 값에서 뽑으면 획이 색 띠와 함께 움직이고,
                    // 원래 값에서 뽑으면 획이 단계 사이를 <b>연속으로</b> 건넙니다.
                    half toneStep = dot(quantised, half3(0.2126h, 0.7152h, 0.0722h));
                    half toneFlow = dot(encoded, half3(0.2126h, 0.7152h, 0.0722h));
                    half tone = lerp(toneStep, toneFlow, _HatchToneSmooth);
                    half dark = 1.0h - tone;

                    // 색이 빠진 만큼 획을 더 긋습니다. 이미 어두운 자리에서만
                    // 늘어나도록 <b>곱합니다</b> — 밝고 색 없는 하늘은 그대로 둡니다.
                    dark = saturate(dark * (1.0h + chromaNow * colourless));

                    // ── 원본 방식 ──
                    //
                    // 밝기가 여섯 단계 중 하나를 고르고, <b>그 단계에 그려진 획이 그대로</b>
                    // 나옵니다. 합치는 법도 원본과 같은 <b>곱하기</b>입니다 —
                    // 획 자리에서 값이 0 에 가까워지므로 곱하기가 곧 덮는 것입니다.
                    [branch] if (_HatchTam > 0.5h && _CarDriveHatchParams.w >= 0.5h)
                    {
                        half tamScale = max(_HatchDitherScale * pixelScale, 1.0h);

                        half ink = CarDriveHatchValue(1.0h - dark, float3(inkPos, 0.0),
                                                      float3(0, 0, 1), tamScale, _HatchTamTop);

                        [branch] if (settled < 0.999h)
                        {
                            half was = CarDriveHatchValue(1.0h - dark, float3(inkPosWas, 0.0),
                                                          float3(0, 0, 1), tamScale, _HatchTamTop);
                            ink = lerp(was, ink, settled);
                        }

                        quantised *= lerp(1.0h, ink, saturate(inkNow));
                        drawn = true;
                    }

                    // <c>rank</c> 가 <c>dark</c> 보다 작으면 이미 그어진 획입니다.
                    // 그 경계를 <c>_HatchSoft</c> 만큼 벌려 서로 물리게 합니다.
                    half soft = max(_HatchSoft, 0.001h);
                    half bare = smoothstep(dark - soft, dark + soft, rank);

                    // 순위 행렬 갈래는 <b>빼서</b> 덮습니다. 곱하기가 나빠서가 아니라
                    // (원본의 <c>col *= hatch</c> 는 획 자리에서 0 에 가까워지므로 그
                    // 곱하기가 곧 완전히 덮는 것입니다) 여기 잉크가 <c>_HatchDepth</c> 로
                    // <b>얕게 잘려</b> 있어서입니다. 얕은 곱셈은 바탕이 어두울수록 빼는
                    // 양도 줄어 <b>그늘에서 획이 가장 안 보이는</b> 거꾸로 된 그림이 됩니다
                    // — 실측했습니다(어두운 대역 2.9, 밝은 대역 5.2/255). 빼기는 바탕과
                    // 무관하게 같은 양을 가져가므로 그 뒤집힘이 없습니다.
                    [branch] if (!drawn)
                    {
                        half amount = saturate(inkNow) * _HatchDepth * (1.0h - bare);
                        quantised = max(quantised - amount, 0.0h);
                    }
                }

                quantised = quantised * quantised;

                half3 drawing = lerp(source.rgb, quantised, _Strength);

                // ── 종이 ──
                //
                // <b>맨 마지막입니다.</b> 종이는 그림이 놓이는 바탕이지 그림의 일부가
                // 아니므로 팔레트와 획이 다 끝난 뒤에 곱합니다. 그래서 <c>_Strength</c> 를
                // 0 으로 내려 팔레트를 꺼도 종이는 남습니다.
                [branch] if (_PaperGrain > 0.001h)
                {
                    float2 uv = input.positionCS.xy / max(_PaperScale * pixelScale, 1.0h);
                    half fibre = SAMPLE_TEXTURE2D(_PaperTex, sampler_LinearRepeat_PaperTex, uv).r;

                    // 0.5 가 가운데라 세기 0 이면 정확히 1 이 곱해집니다.
                    drawing *= 1.0h + (fibre - 0.5h) * 2.0h * _PaperGrain;
                }

                // 가장자리 어둠. 한가운데는 건드리지 않고 모서리만 내립니다.
                [branch] if (_PaperEdge > 0.001h)
                {
                    float2 fromMid = input.texcoord - 0.5;
                    half reach = (half)saturate(length(fromMid) * 1.41421356);
                    drawing *= 1.0h - _PaperEdge * smoothstep(0.45h, 1.0h, reach);
                }

                return half4(drawing, source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
