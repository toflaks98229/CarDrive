#ifndef CARDRIVE_TOON_LIGHTING_INCLUDED
#define CARDRIVE_TOON_LIGHTING_INCLUDED

// CarDrive 툰 조명 라이브러리
//
// 기법은 ColinLeung-NiloCat 의 UnityURPToonLitShaderExample (MIT) 을 참고했습니다.
//   https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
// 그 예제의 핵심 아이디어 셋을 가져왔습니다.
//   1. N·L 을 그대로 쓰지 않고 '경계점(midPoint)' 기준으로 밝음/어둠을 가릅니다.
//   2. 경계를 완전히 끊지 않고 아주 좁은 폭으로 부드럽게 이어, 계단이 지글거리지 않게 합니다.
//   3. 그림자와 추가 광원도 같은 방식으로 눌러, 한 화면에 여러 종류의 경계가 섞이지 않게 합니다.
// 코드는 이 프로젝트에 맞춰 새로 썼습니다. (URP 17 / Unity 6, 낮밤 연동, 지면과 공용)
//
// ── 이 프로젝트만의 사정 두 가지 ──
//
// <b>화면은 픽셀화와 팔레트 양자화를 거쳐 나옵니다.</b> (PixelizeFeature · PaletteFeature)
// 그래서 그라데이션은 어차피 계단으로 뭉개집니다. 애초에 평평한 색으로 칠하는 편이
// 결과를 예측할 수 있고, 양자화 뒤에도 경계가 지저분해지지 않습니다.
//
// <b>해가 하루 종일 돕니다.</b> (TimeSystem 이 각도와 세기를 함께 바꿉니다)
// 밤에는 주광이 약해지는데, 툰 음영을 그대로 곱하면 화면이 통째로 검게 죽습니다.
// 그래서 그림자 쪽 색을 <b>주광 세기에 비례해서만</b> 어둡게 하고, 바닥은 환경광이 받칩니다.
//
// ── 램프를 쓸 때 반드시 알아야 하는 것 ──
//
// 이 프로젝트의 팔레트 후처리는 <b>휘도만</b> 양자화하고 색상은 그대로 둡니다.
// (PixelizePalette.shader: 휘도를 단계로 끊은 뒤 그 비율을 RGB 에 곱합니다)
//
// 그래서 <b>밝기만 다른 램프는 의미가 없습니다.</b> 어차피 후처리가 같은 단계로 뭉갤 것을
// 셰이더가 먼저 계산하는 셈입니다. 램프가 값을 하는 지점은 <b>색조</b>입니다.
// 그늘을 그냥 어둡게 하지 않고 푸른 쪽으로 <b>돌리면</b>, 그 색상은 양자화를 통과해
// 화면에 그대로 남습니다. 밤길에 그림자가 남색으로 도는 인상이 여기서 나옵니다.
//
// 램프 텍스처는 점 필터(Point)에 낮은 해상도로 만드세요. 보간이 들어가면 띠 사이가
// 흐려지고, 그 흐린 구간을 후처리가 다시 단계로 끊어 경계가 두 번 생깁니다.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// 램프 텍스처는 여기서 한 번만 선언합니다.
// 지면과 메시 셰이더가 같은 이름을 쓰므로, 각 셰이더는 프로퍼티만 노출하면 됩니다.
TEXTURE2D(_ToonRampMap);
SAMPLER(sampler_ToonRampMap);

// ── 구름 그림자 ──
//
// 재질마다 설정하지 않습니다. 구름은 <b>세계 전체에 걸리는 하나의 현상</b>이라
// 땅·풀·차가 서로 다른 구름 밑에 있으면 안 됩니다.
// 그래서 CloudShadows 컴포넌트가 Shader.SetGlobal 로 한 번만 넣어 줍니다.
// (GrassPushField 가 풀 밀림 좌표를 넣는 것과 같은 방식입니다)
TEXTURE2D(_CloudShadowMap);
SAMPLER(sampler_CloudShadowMap);

// x = 타일 크기(미터), y = 세기, z = 경계 부드러움, w = 사용 여부(0/1)
float4 _CloudShadowParams;

// xy = 흘러온 거리. 바람 방향과 세기로 CloudShadows 가 누적합니다.
float4 _CloudShadowScroll;

// ── 멀어지는 것을 디더로 지우기 ──
//
// 멀어지는 물체를 <b>알파로 흐리게</b> 하려면 반투명으로 그려야 하고, 그러면 정렬 문제가
// 생기고 깊이도 못 씁니다. 나무가 수천 그루, 풀이 수십만 포기면 감당이 안 됩니다.
//
// 대신 <b>성기게 버립니다.</b> 대상마다 정해진 문턱값을 두고, 남을 정도가 그보다 작으면
// 버립니다. 멀어질수록 더 많이 버려져 서서히 성글어지다 사라집니다.
// 불투명 그대로라 값이 싸고 정렬도 필요 없습니다.
//
// 이 게임에는 특히 잘 맞습니다. 화면이 어차피 픽셀화를 거치므로
// 디더 무늬가 <b>결점이 아니라 시대 표현</b>으로 읽힙니다.
//
// <b>여기 있는 이유.</b> 원래는 <c>CarDriveToonLit.shader</c> 안에만 있었습니다.
// 풀도 같은 방식이 필요해졌는데, 복사해 두면 한쪽만 고쳐지는 날이 옵니다.
// 나무는 <b>화면 픽셀</b>마다, 풀은 <b>잎</b>마다 문턱값을 뽑는다는 것만 다르고
// 행렬도 곡선도 같습니다.

// <b>행렬과 문턱값 함수는 CarDriveDither.hlsl 로 옮겼습니다.</b>
// UI 게이지도 같은 디더를 써야 하는데, 이 파일은 URP 조명 심볼에 기대고 있어
// UI 셰이더가 포함할 수 없었습니다. 조명에 기대지 않는 부분만 내렸습니다.
#include "CarDriveDither.hlsl"


/// <summary>
/// 거리에 따라 얼마나 남을지 구합니다. 1이면 그대로, 0이면 다 지웁니다.
/// </summary>
/// <param name="viewDistance">카메라까지의 거리(m). 이름이 <c>distance</c> 면 동명의 내장 함수를 가립니다.</param>
/// <param name="start">지워지기 시작하는 거리(m)</param>
/// <param name="end">다 지워지는 거리(m)</param>
/// <returns>0~1 남을 정도</returns>
half CarDriveFadeCurve(float viewDistance, float start, float end)
{
    return (half)saturate(1.0 - (viewDistance - start) / max(0.001, end - start));
}


// ── 손그림 빗금 (Tonal Art Map) ──
//
// 기법은 nkihrk 의 HatchingShader (MIT) 와 그것을 다듬은 songrise/Unity-Sketch-Style-Shader
// 를 따랐습니다. 원전은 Praun 외 "Real-Time Hatching" (SIGGRAPH 2001) 입니다.
//   https://github.com/nkihrk/HatchingShader
//   https://github.com/songrise/Unity-Sketch-Style-Shader
// TAM 텍스처도 nkihrk 의 것을 씁니다.
//   Assets/_Project/04.Art/01.Images/Hatching/LICENSE-nkihrk.txt
//
// 코드는 이 프로젝트에 맞춰 새로 썼습니다. 원본과 다른 점 셋 —
// 월드 삼중평면으로 샘플하고, 여섯 단계를 두 장에 채널로 묶고,
// 안개·구름 그림자·툰 조명과 같은 자리에서 돕니다.
//
// <b>왜 절차적으로 긋지 않는가.</b> 한 번 그렇게 만들어 봤고 기계처럼 보였습니다.
// 계산으로 그은 선은 간격도 굵기도 각도도 균일합니다. 사람이 그은 획은 필압이 변하고
// 끝이 갈라지고 겹칠 때 잉크가 뭉칩니다. <b>그 성격은 계산이 아니라 그림에서 옵니다.</b>
//
// <b>왜 월드 공간인가.</b> UV 로 하면 이 프로젝트가 무너집니다 — 터레인은 100m 타일에
// 0~1 UV 라 256px 획이 100m 로 늘어나고, 풀은 잎마다 쓸 만한 UV 가 없습니다.
// 월드 좌표로 하면 획 크기를 <b>미터로</b> 정할 수 있고 땅·풀·건물이 같은 크기로 그어집니다.
//
// <b>거리 보정은 밉맵이 합니다.</b> 멀어지면 UV 미분이 커져 밉이 올라가고 획이 흐려집니다.
// TAM 이 애초에 밉 단계와 밝기 단계를 맞춰 만드는 이유가 이것입니다.

// R,G,B = 밝은 쪽 세 단계(0,1,2)
TEXTURE2D(_CarDriveTamBright); SAMPLER(sampler_CarDriveTamBright);

// R,G,B = 어두운 쪽 세 단계(3,4,5)
TEXTURE2D(_CarDriveTamDark);

// x = 획 크기(m), y = 세기(0~1), z = 빗금이 시작되는 밝기, w = 전역이 유효한가(0/1)
float4 _CarDriveHatchParams;

/// <summary>
/// 이 픽셀에 얼마나 잉크가 얹힐지 구합니다. 1 이면 종이 그대로, 0 에 가까울수록 덮입니다.
///
/// <b>샘플링과 섞기를 한 함수에 둔 이유.</b> 밝기를 먼저 알면 <b>어느 텍스처가 필요한지</b>도
/// 알 수 있습니다. 밝은 쪽만 쓰는 픽셀은 어두운 쪽 장을 읽지 않아도 되고, 그 반대도 같습니다.
/// 나눠 두면 늘 둘 다 읽어야 해서 그 절약을 못 합니다.
///
/// <b>기울기를 미리 구해 넘깁니다(SAMPLE_TEXTURE2D_GRAD).</b> 아래에서 분기 안에 샘플이
/// 들어가는데, 분기가 픽셀마다 갈리면 하드웨어가 밉 단계를 정할 미분을 잃습니다.
/// 그러면 원경이 지글거립니다. 미분은 분기 <b>밖에서</b> 한 번 구해 두고 넘겨야 합니다.
/// </summary>
/// <param name="tone">이 픽셀이 받은 밝기 (0~1)</param>
/// <param name="positionWS">월드 위치</param>
/// <param name="normalWS">월드 법선</param>
/// <param name="scale">획 한 판이 덮는 거리(m)</param>
/// <param name="top">이 밝기부터 빗금이 시작됩니다</param>
half CarDriveHatchValue(half tone, float3 positionWS, float3 normalWS, float scale, half top)
{
    // 종이(0) 에서 가장 어두운 단계(6) 까지의 자리입니다.
    half x = saturate((top - tone) / max(top, 1e-3h)) * 6.0h;

    // ── 섞기 ──
    //
    // 삼각형 창은 인접한 둘만 남기지만 <b>꼭짓점에서 꺾입니다.</b> 그 꺾임이 단계 경계에
    // 가느다란 띠로 보입니다. smoothstep 을 통과시키면 양 끝에서 기울기가 0 이 되어
    // 경계가 사라집니다. 계산은 곱셈 두 번 더할 뿐입니다.
    #define CD_W(d) smoothstep(0.0h, 1.0h, saturate(1.0h - abs(x - (d))))

    half wPaper = CD_W(0.0h);
    half w0 = CD_W(1.0h);
    half w1 = CD_W(2.0h);
    half w2 = CD_W(3.0h);
    half w3 = CD_W(4.0h);
    half w4 = CD_W(5.0h);
    half w5 = CD_W(6.0h);

    #undef CD_W

    // ── 어느 장이 필요한가 ──
    //
    // 밝은 장은 단계 0·1·2 를, 어두운 장은 3·4·5 를 담고 있습니다.
    // 경계(x 가 3~4) 에서만 둘 다 필요하고, 그 밖에서는 한 장이면 됩니다.
    // 화면 대부분이 어느 한쪽에 몰려 있어 <b>평균 샘플 수가 절반으로</b> 떨어집니다.
    bool needBright = (w0 + w1 + w2) > 0.0h;
    bool needDark = (w3 + w4 + w5) > 0.0h;

    // ── 삼중평면 ──
    float3 n = abs(normalWS);
    n /= max(n.x + n.y + n.z, 1e-4);

    float inv = 1.0 / max(scale, 0.01);
    float2 uvX = positionWS.zy * inv;
    float2 uvY = positionWS.xz * inv;
    float2 uvZ = positionWS.xy * inv;

    // 분기 밖에서 미리 구합니다. 위의 주석대로, 안에서 구하면 밉이 무너집니다.
    float2 dX = ddx(uvX), dYx = ddy(uvX);
    float2 dY = ddx(uvY), dYy = ddy(uvY);
    float2 dZ = ddx(uvZ), dYz = ddy(uvZ);

    // 기여가 이보다 작은 축은 건너뜁니다. <b>땅과 풀은 사실상 Y 하나로 끝납니다</b> —
    // 세 번 읽던 것이 한 번이 됩니다. 벽은 X·Z 가 받습니다.
    // 문턱을 0 으로 두면 절약이 없고, 크게 두면 비스듬한 면에서 이음매가 보입니다.
    const float AxisEps = 0.02;

    half3 bright = half3(0, 0, 0);
    half3 dark = half3(0, 0, 0);

    [branch] if (needBright)
    {
        [branch] if (n.x > AxisEps)
            bright += SAMPLE_TEXTURE2D_GRAD(_CarDriveTamBright, sampler_CarDriveTamBright, uvX, dX, dYx).rgb * (half)n.x;
        [branch] if (n.y > AxisEps)
            bright += SAMPLE_TEXTURE2D_GRAD(_CarDriveTamBright, sampler_CarDriveTamBright, uvY, dY, dYy).rgb * (half)n.y;
        [branch] if (n.z > AxisEps)
            bright += SAMPLE_TEXTURE2D_GRAD(_CarDriveTamBright, sampler_CarDriveTamBright, uvZ, dZ, dYz).rgb * (half)n.z;
    }

    // 어두운 장도 <b>같은 샘플러</b>를 씁니다. 두 장이 다른 설정으로 읽히면 획이 어긋납니다.
    [branch] if (needDark)
    {
        [branch] if (n.x > AxisEps)
            dark += SAMPLE_TEXTURE2D_GRAD(_CarDriveTamDark, sampler_CarDriveTamBright, uvX, dX, dYx).rgb * (half)n.x;
        [branch] if (n.y > AxisEps)
            dark += SAMPLE_TEXTURE2D_GRAD(_CarDriveTamDark, sampler_CarDriveTamBright, uvY, dY, dYy).rgb * (half)n.y;
        [branch] if (n.z > AxisEps)
            dark += SAMPLE_TEXTURE2D_GRAD(_CarDriveTamDark, sampler_CarDriveTamBright, uvZ, dZ, dYz).rgb * (half)n.z;
    }

    // 창의 합이 1 이 아닐 수 있으므로(smoothstep 을 통과시켰으므로) 나눠 정규화합니다.
    // 하지 않으면 단계 사이에서 전체가 살짝 밝아졌다 어두워졌다 합니다.
    half sum = wPaper + w0 + w1 + w2 + w3 + w4 + w5;

    half ink = wPaper * 1.0h
             + w0 * bright.r + w1 * bright.g + w2 * bright.b
             + w3 * dark.r   + w4 * dark.g   + w5 * dark.b;

    return ink / max(sum, 1e-4h);
}

/// <summary>
/// 빗금을 색에 <b>곱합니다.</b>
///
/// 잉크는 종이 위에 얹히는 것이라 밑색을 어둡게 할 뿐 색을 갈아 끼우지 않습니다.
/// 원본(nkihrk)도 <c>col *= hatch</c> 입니다.
/// </summary>
half3 CarDriveApplyHatch(half3 color, half tone, float3 positionWS, float3 normalWS)
{
    // <b>TAM 은 기본값으로 물러설 수 없습니다.</b> 절차적 빗금은 수치만 있으면 그렸지만
    // 이건 텍스처가 있어야 합니다. 전역이 안 들어왔는데 그냥 샘플하면 바인딩되지 않은
    // 텍스처를 읽어 <b>플랫폼마다 다른 색</b>(대개 흰색이나 검정)이 나옵니다.
    // 그래서 HatchingRig 가 씬에 없으면 아무것도 하지 않고 돌려보냅니다.
    if (_CarDriveHatchParams.w < 0.5) return color;

    half strength = (half)_CarDriveHatchParams.y;
    half top = (half)_CarDriveHatchParams.z;

    // <b>여기서 일찍 빠져나가면 안 됩니다.</b> 밝은 픽셀을 건너뛰고 싶어지지만,
    // 아래 함수 안에 <c>ddx/ddy</c> 가 있습니다. 미분은 <b>균일한 흐름</b>에서만 유효한데
    // tone 은 픽셀마다 달라서, 한 쿼드에서 일부만 들어가면 밉 단계가 무너져 원경이 지글거립니다.
    //
    // 건너뛰는 일은 함수 <b>안에서</b> 이미 일어납니다 — 밝으면 창 가중치가 종이 쪽에만 서고
    // needBright/needDark 가 둘 다 거짓이 되어 <b>텍스처를 한 번도 읽지 않습니다.</b>
    // 아끼려던 것(샘플)은 그대로 아끼고 미분만 지킵니다.
    half ink = CarDriveHatchValue(tone, positionWS, normalWS, _CarDriveHatchParams.x, top);

    // 세기가 0 이면 종이 그대로(1), 1 이면 획 그대로입니다.
    return color * lerp(1.0h, ink, strength);
}

/// <summary>툰 음영을 계산할 때 쓰는 설정 묶음입니다.</summary>
struct ToonSurface
{
    half3 albedo;        // 바탕색
    float3 normalWS;     // 월드 법선
    float3 positionWS;   // 월드 위치
    float3 viewDirWS;    // 표면에서 카메라로 향하는 방향
};

/// <summary>툰 음영의 모양을 정하는 값들입니다.</summary>
struct ToonParams
{
    half  midPoint;      // 밝음과 어둠을 가르는 지점 (0~1). 낮을수록 밝은 면이 넓습니다.
    half  softness;      // 하이라이트·외곽 빛 경계의 폭. 0 이면 완전히 끊깁니다.

    // <b>그림자 경계는 자기 폭을 가집니다.</b> 예전에는 위의 softness 하나가
    // 명암 경계·그림자·하이라이트·외곽 빛을 전부 끊었습니다. 그런데 하이라이트는
    // 좁게 끊어야 <b>납작한 점</b>이 되고, 그림자는 넓게 이어야 <b>계단이 보이지 않습니다.</b>
    // 한 값으로는 둘을 함께 만족시킬 수 없어 갈랐습니다.
    half  shadowSoftness;// 빛과 그늘이 갈리는 폭 (명암 경계 + 드리운 그림자)
    half  stepSoftness;  // 단계와 단계 사이를 잇는 폭 (0~1, 한 단계 폭에 대한 비율)
    half  steps;         // 밝은 쪽을 몇 단계로 끊을지. 2 미만이면 끊지 않습니다.
    half3 shadowTint;    // 그림자 쪽에 섞을 색. 회색보다 푸른 기가 도는 편이 자연스럽습니다.
    half  shadowStrength;// 그림자를 얼마나 어둡게 할지 (0~1)
    half  ambient;       // 환경광을 얼마나 받을지
    half  rimStrength;   // 외곽 빛의 세기. 0 이면 끄고, 밤에 실루엣을 살릴 때 올립니다.
    half  rimWidth;      // 외곽 빛의 폭 (클수록 좁아집니다)
    half3 rimColor;      // 외곽 빛의 색
    half  specularStrength; // 하이라이트 세기. 0 이면 끕니다.
    half  specularSize;  // 하이라이트 크기 (클수록 작아집니다)

    // 높이 그라데이션 — 월드 Y 를 따라 색을 덧입힙니다.
    // 원경을 눌러 주거나 지면 바닥을 가라앉힐 때 씁니다. 안개와 달리 거리가 아니라 <b>높이</b>가 기준입니다.
    half3 heightColor;   // 덧입힐 색
    half  heightBottom;  // 이 높이부터
    half  heightTop;     // 이 높이까지
    half  heightStrength;// 얼마나 섞을지. 0 이면 끕니다.
};

/// <summary>인스펙터 값이 없을 때 쓸 기본 설정입니다.</summary>
ToonParams DefaultToonParams()
{
    ToonParams p;
    p.midPoint = 0.35h;
    p.softness = 0.05h;
    p.shadowSoftness = 0.25h;
    p.stepSoftness = 0.35h;
    p.steps = 0.0h;
    p.shadowTint = half3(0.42h, 0.47h, 0.62h);
    p.shadowStrength = 0.75h;
    p.ambient = 0.85h;
    p.rimStrength = 0.0h;
    p.rimWidth = 4.0h;
    p.rimColor = half3(1, 1, 1);
    p.specularStrength = 0.0h;
    p.specularSize = 40.0h;
    p.heightColor = half3(0.30h, 0.34h, 0.48h);
    p.heightBottom = 0.0h;
    p.heightTop = 20.0h;
    p.heightStrength = 0.0h;
    return p;
}

/// <summary>
/// 0~1 값을 툰 밴드로 바꿉니다.
///
/// 경계를 <c>step</c> 으로 딱 끊지 않고 <c>smoothstep</c> 으로 아주 좁게 이어 줍니다.
/// 완전히 끊으면 비스듬한 면에서 경계가 픽셀 단위로 지글거립니다.
/// (원본 예제가 얇은 소프트니스를 두는 이유가 이것입니다)
/// </summary>
/// <param name="value">가를 값 (보통 N·L)</param>
/// <param name="mid">경계 지점</param>
/// <param name="soft">경계의 폭</param>
half ToonBand(half value, half mid, half soft)
{
    half half_ = max(soft, 1e-4h) * 0.5h;
    return smoothstep(mid - half_, mid + half_, value);
}

/// <summary>
/// 밝은 쪽을 여러 단계로 끊습니다. steps 가 2 미만이면 그대로 둡니다.
/// </summary>
/// <param name="lit">밝기 (0~1)</param>
/// <param name="steps">단계 수</param>
half ToonSteps(half lit, half steps, half soft)
{
    if (steps < 2.0h) return lit;

    // floor 를 그냥 쓰면 1일 때 단계를 하나 넘깁니다. 위를 눌러 둡니다.
    half top = steps - 1.0h;

    half scaled = saturate(lit) * steps;
    half index = min(floor(scaled), top);

    // <b>단계 안에서 어디까지 왔는가</b>입니다. 0 이면 이 단계에 막 들어섰고, 1 이면 다음 단계 문턱입니다.
    half within = scaled - index;

    // <b>문턱 언저리에서만 다음 단계로 이어 줍니다.</b>
    //
    // 예전에는 <c>floor</c> 하나로 끝냈습니다. 그러면 단계와 단계 사이가 <b>한 픽셀 폭의
    // 완전한 절벽</b>이라, 완만한 면에서는 그 경계가 굵은 등고선처럼 화면을 가로지릅니다.
    // 툰 룩은 단계가 보이는 것이 목적이지만 <b>계단의 톱니까지 보이는 것</b>은 아닙니다.
    //
    // soft 는 한 단계 폭에 대한 비율입니다. 0 이면 예전 그대로 딱 끊깁니다.
    half band = clamp(soft, 1e-4h, 1.0h);
    half blend = smoothstep(1.0h - band, 1.0h, within);

    return min(index + blend, top) / top;
}

/// <summary>
/// 주광 하나에 대한 툰 음영을 계산합니다.
///
/// 그림자 감쇠도 같은 밴드를 통과시킵니다. 그러지 않으면 <b>부드러운 그림자 경계와
/// 딱딱한 명암 경계가 한 화면에 섞여</b> 툰 룩이 무너집니다.
/// </summary>
/// <param name="light">계산할 광원</param>
/// <param name="normalWS">월드 법선</param>
/// <param name="p">툰 설정</param>
/// <returns>0(그늘) ~ 1(빛) 사이의 밝기</returns>
half ToonLightAmount(Light light, float3 normalWS, ToonParams p)
{
    half ndl = dot(normalWS, light.direction) * 0.5h + 0.5h;   // 0~1 로 폅니다
    // <b>명암 경계는 그림자 폭을 씁니다.</b> 하이라이트용 좁은 폭으로 끊으면
    // 둥근 면에서 빛과 그늘이 칼로 자른 듯 갈립니다.
    half lit = ToonBand(ndl, p.midPoint, p.shadowSoftness);

    // 드리운 그림자도 같은 폭으로 잇습니다. URP 는 이미 부드러운 그림자를 주는데
    // 여기서 좁게 끊으면 <b>그 부드러움을 도로 없애는 셈</b>이었습니다.
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    lit *= ToonBand(atten, 0.5h, p.shadowSoftness);

    return ToonSteps(lit, p.steps, p.stepSoftness);
}

/// <summary>
/// 툰 하이라이트입니다. 블린-퐁을 밴드로 끊어 <b>납작한 점</b>으로 만듭니다.
/// </summary>
half ToonSpecular(Light light, ToonSurface s, ToonParams p)
{
    if (p.specularStrength <= 0.001h) return 0.0h;

    float3 halfDir = SafeNormalize(light.direction + s.viewDirWS);
    half ndh = saturate(dot(s.normalWS, halfDir));
    half raw = pow(ndh, max(1.0h, p.specularSize));

    return ToonBand(raw, 0.5h, p.softness) * p.specularStrength;
}

/// <summary>
/// 외곽 빛입니다. 밤에 실루엣이 배경에 묻히는 것을 막습니다.
/// </summary>
half ToonRim(ToonSurface s, ToonParams p)
{
    if (p.rimStrength <= 0.001h) return 0.0h;

    half ndv = 1.0h - saturate(dot(s.normalWS, s.viewDirWS));
    half rim = pow(ndv, max(1.0h, p.rimWidth));

    return ToonBand(rim, 0.5h, p.softness) * p.rimStrength;
}

/// <summary>
/// 구름 그림자를 읽습니다. 1이면 햇빛, 0이면 구름 그늘입니다.
///
/// 위에서 내려다보듯 월드 XZ 로 샘플합니다. 구름은 아주 높이 있으므로
/// 물체의 높이는 사실상 영향을 주지 않습니다. 대신 <b>흘러가는 것</b>이 중요합니다.
///
/// 경계는 다른 음영과 같은 방식으로 끊습니다. 구름 그림자만 부드럽게 두면
/// 딱딱한 명암 경계 위에 흐릿한 얼룩이 얹혀 툰 룩이 무너집니다.
/// </summary>
/// <param name="positionWS">월드 좌표</param>
half SampleCloudShadow(float3 positionWS)
{
    if (_CloudShadowParams.w < 0.5h) return 1.0h;

    float tile = max(1.0, _CloudShadowParams.x);
    float2 uv = (positionWS.xz + _CloudShadowScroll.xy) / tile;

    half noise = SAMPLE_TEXTURE2D(_CloudShadowMap, sampler_CloudShadowMap, uv).r;

    // 노이즈를 그대로 곱하면 온 세상이 얼룩덜룩해집니다.
    // 밴드로 끊어 <b>구름이 있는 자리와 없는 자리</b>로 가릅니다.
    half band = ToonBand(noise, 0.5h, _CloudShadowParams.z);

    // 세기가 1이어도 완전히 검게 만들지는 않습니다. 구름 그늘도 하늘빛은 받습니다.
    return lerp(1.0h - _CloudShadowParams.y, 1.0h, band);
}

/// <summary>
/// 밴드를 적용하지 않은 <b>날것의</b> 밝기입니다. 램프를 쓸 때 이 값으로 램프를 읽습니다.
///
/// 램프가 이미 띠를 갖고 있으므로 여기서 또 끊으면 경계가 두 번 생깁니다.
/// 감쇠와 그림자는 곱해서 넣습니다. 그늘도 램프의 어두운 쪽을 읽어야 하기 때문입니다.
/// </summary>
/// <param name="light">계산할 광원</param>
/// <param name="normalWS">월드 법선</param>
half ToonLightRaw(Light light, float3 normalWS)
{
    half ndl = dot(normalWS, light.direction) * 0.5h + 0.5h;
    return saturate(ndl * light.distanceAttenuation * light.shadowAttenuation);
}

/// <summary>
/// 램프 텍스처에서 색을 읽습니다. 가로축이 밝기(0=그늘, 1=빛)입니다.
/// </summary>
/// <param name="lit">밝기 (0~1)</param>
half3 ToonRampColor(half lit)
{
    return SAMPLE_TEXTURE2D(_ToonRampMap, sampler_ToonRampMap, float2(saturate(lit), 0.5h)).rgb;
}

/// <summary>
/// 월드 높이를 따라 색을 덧입힙니다.
///
/// 안개는 <b>거리</b>로 멀어지는 것을 누르지만, 이건 <b>높이</b>로 누릅니다.
/// 지면 바닥을 가라앉히거나 먼 언덕 꼭대기를 하늘색으로 뜨게 할 때 씁니다.
/// </summary>
/// <param name="color">덧입힐 대상 색</param>
/// <param name="heightWS">월드 높이 (positionWS.y)</param>
/// <param name="p">툰 설정</param>
half3 ApplyHeightGradient(half3 color, float heightWS, ToonParams p)
{
    if (p.heightStrength <= 0.001h) return color;

    half span = max(1e-4h, p.heightTop - p.heightBottom);
    half t = saturate((heightWS - p.heightBottom) / span);

    return lerp(color, p.heightColor, t * p.heightStrength);
}

/// <summary>
/// 툰 음영 전체를 계산합니다. 지면 셰이더와 메시 셰이더가 함께 씁니다.
/// </summary>
/// <param name="s">표면 정보</param>
/// <param name="p">툰 설정</param>
/// <param name="shadowCoord">그림자 좌표</param>
/// <returns>최종 색</returns>
half3 ToonShade(ToonSurface s, ToonParams p, float4 shadowCoord)
{
    Light mainLight = GetMainLight(shadowCoord);

    // 밤에는 주광이 약해집니다. 그림자를 그때도 똑같이 어둡게 하면 화면이 통째로 죽으므로,
    // <b>주광이 셀수록 그림자도 진하게</b> 만듭니다. TimeSystem 이 intensity 를 낮추면
    // 그림자도 함께 옅어져, 밤에는 환경광이 화면을 받칩니다.
    half sunPower = saturate(Luminance(mainLight.color));

    // <b>빗금이 쓸 밝기는 끊지 않은 날것이어야 합니다.</b>
    //
    // 여기에 <c>ToonLightAmount</c> 를 넣으면 안 됩니다. 그 함수는 툰 밴드
    // (<c>smoothstep(mid - soft/2, mid + soft/2, ...)</c>)로 값을 <b>계단으로 끊습니다.</b>
    // 재질의 <c>_Softness</c> 가 0.05 안팎이라 0~1 구간에서 폭 0.05 면 사실상 0 아니면 1 이고,
    // 그 값을 빗금에 넘기면 <b>TAM 여섯 단계 중 둘만 쓰입니다.</b> 그라데이션이 통째로 사라집니다.
    //
    // 그리고 그 함수는 <b>그림자 감쇠까지</b> 같은 밴드에 통과시킵니다.
    // 그러면 주변광이 만드는 부드러운 그늘 경계도 계단이 됩니다.
    //
    // 날것은 <c>ndl × 거리감쇠 × 그림자</c> 를 그대로 곱한 연속값입니다.
    // 빗금은 애초에 여섯 단계로 자기가 끊으므로, 여기서 미리 끊어 줄 이유가 없습니다.
    half rawLit = ToonLightRaw(mainLight, s.normalWS);

    half3 lighting;

    #ifdef _HATCHING
        // ── 단색 그늘을 쓰지 않습니다 ──
        //
        // 예전에는 여기서 면을 둘로 갈라 그늘 쪽에 <c>shadowTint</c> 를 칠했고
        // (램프를 쓰는 재질은 램프에서 색을 읽었고), 그것이 이 게임의 툰 음영이었습니다.
        // <b>이제 그 일을 빗금이 합니다.</b> 같은 그늘을 색으로도 칠하고 획으로도 그으면
        // 경계가 두 번 생겨 지저분해집니다.
        //
        // 주광의 색과 세기는 남깁니다. <b>밤에 어두워지는 것은 그늘이 아니라 해가 지는 것</b>이라,
        // 이것까지 걷으면 낮과 밤이 같아집니다.
        lighting = mainLight.color;
    #else
        // 빗금을 켜지 않은 재질(나무 등)은 예전 방식 그대로입니다.
        // 그늘을 그리는 수단이 아무것도 없으면 통째로 납작해집니다.
        #ifdef _TOON_RAMP
            // 램프가 이미 띠를 갖고 있으므로 여기서도 날것의 밝기로 읽습니다.
            half3 ramp = ToonRampColor(rawLit);

            // 밤에는 램프의 색조만 남기고 세기를 눌러야 화면이 죽지 않습니다.
            ramp = lerp(half3(1, 1, 1), ramp, sunPower);
            lighting = ramp * mainLight.color;
        #else
            // 단색 음영은 <b>끊은</b> 값을 씁니다. 그것이 툰 룩의 딱딱한 경계입니다.
            half lit = ToonLightAmount(mainLight, s.normalWS, p);

            half3 shadowColor = lerp(half3(1, 1, 1), p.shadowTint, p.shadowStrength * sunPower);
            lighting = lerp(shadowColor, half3(1, 1, 1), lit) * mainLight.color;
        #endif
    #endif

    // 받은 빛의 <b>양</b>입니다. 색이 아니라 양이라 빗금이 이걸 읽습니다.
    // 주광은 날것의 밝기에 그날의 광량을 곱한 만큼 들어옵니다.
    half received = rawLit * sunPower;

    // 구름 그림자는 <b>주광에만</b> 곱합니다. 구름이 가리는 것은 해이지
    // 헤드라이트나 귀신 불빛이 아닙니다. 그래서 추가 광원을 더하기 전에 적용합니다.
    //
    // 밤에는 해가 없으니 구름 그림자도 없습니다. sunPower 를 곱해 저절로 사라지게 합니다.
    half cloud = SampleCloudShadow(s.positionWS);
    lighting *= lerp(1.0h, cloud, sunPower);

    // 추가 광원(헤드라이트·귀신 라이트 등)도 같은 밴드를 통과시킵니다.
    #ifdef _ADDITIONAL_LIGHTS
        uint count = GetAdditionalLightsCount();
        for (uint i = 0u; i < count; ++i)
        {
            Light extra = GetAdditionalLight(i, s.positionWS);

            // 칠하는 쪽은 끊어서 툰 룩을 유지하고,
            lighting += extra.color * ToonLightAmount(extra, s.normalWS, p);

            // 빗금 쪽은 날것으로 받습니다. 헤드라이트가 비춘 자리에서 획이 <b>서서히</b>
            // 옅어져야 빛이 번지는 것으로 읽힙니다. 끊으면 원이 오려낸 듯 생깁니다.
            received += ToonLightRaw(extra, s.normalWS) * saturate(Luminance(extra.color));
        }
    #endif

    half3 ambient = SampleSH(s.normalWS) * p.ambient;
    half3 color = s.albedo * (lighting + ambient);

    // 하이라이트와 외곽 빛은 바탕색에 곱하지 않고 더합니다. 어두운 물체에도 얹히도록.
    color += mainLight.color * ToonSpecular(mainLight, s, p);
    color += p.rimColor * ToonRim(s, p);

    // 높이 그라데이션은 조명 뒤에 얹습니다. 빛을 받든 안 받든 같은 높이면 같은 색이 되어야
    // 원경이 고르게 눌립니다.
    color = ApplyHeightGradient(color, s.positionWS.y, p);

    // <b>빗금은 맨 마지막입니다.</b> 잉크는 색 위에 얹히는 것이지 조명을 받는 것이 아닙니다.
    //
    // 밝기는 주광만이 아니라 <b>받은 빛 전부</b>에서 뽑습니다. 헤드라이트가 비춘 자리는
    // 빗금이 옅어져야 빛이 닿았다는 것이 읽힙니다.
    #ifdef _HATCHING
        // <b>최종 색의 휘도를 쓰면 안 됩니다.</b> 단색 그늘을 걷어낸 뒤로 그 휘도에는
        // 방향성이 남아 있지 않아, 해를 등진 면과 마주한 면이 같은 값이 됩니다.
        // 그래서 위에서 따로 모아 둔 <c>received</c> 를 씁니다.
        half hatchTone = saturate(received + Luminance(ambient));
        color = CarDriveApplyHatch(color, hatchTone, s.positionWS, s.normalWS);
    #endif

    return color;
}

#endif // CARDRIVE_TOON_LIGHTING_INCLUDED
