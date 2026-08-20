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
    half  softness;      // 경계의 폭. 0 이면 완전히 끊깁니다.
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
half ToonSteps(half lit, half steps)
{
    if (steps < 2.0h) return lit;

    // floor 를 그냥 쓰면 1일 때 단계를 하나 넘깁니다. 위를 눌러 둡니다.
    half top = steps - 1.0h;
    return min(floor(lit * steps), top) / top;
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
    half lit = ToonBand(ndl, p.midPoint, p.softness);

    // 거리·스포트 감쇠와 그림자도 같은 방식으로 끊습니다.
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    lit *= ToonBand(atten, 0.5h, p.softness);

    return ToonSteps(lit, p.steps);
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

    half3 lighting;

    #ifdef _TOON_RAMP
        // 램프가 이미 띠를 갖고 있으므로 여기서는 끊지 않고 날것의 밝기로 읽습니다.
        half raw = ToonLightRaw(mainLight, s.normalWS);
        half3 ramp = ToonRampColor(raw);

        // 밤에는 램프의 색조만 남기고 세기를 눌러야 화면이 죽지 않습니다.
        ramp = lerp(half3(1, 1, 1), ramp, sunPower);
        lighting = ramp * mainLight.color;
    #else
        half lit = ToonLightAmount(mainLight, s.normalWS, p);
        half3 shadowColor = lerp(half3(1, 1, 1), p.shadowTint, p.shadowStrength * sunPower);
        lighting = lerp(shadowColor, half3(1, 1, 1), lit) * mainLight.color;
    #endif

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
            lighting += extra.color * ToonLightAmount(extra, s.normalWS, p);
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

    return color;
}

#endif // CARDRIVE_TOON_LIGHTING_INCLUDED
