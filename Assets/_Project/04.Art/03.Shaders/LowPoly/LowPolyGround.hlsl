#ifndef CARDRIVE_LOWPOLY_GROUND_INCLUDED
#define CARDRIVE_LOWPOLY_GROUND_INCLUDED

// 지면과 풀이 함께 쓰는 계산들입니다.
//
// 둘이 따로 색을 정하면 풀밭 가장자리에 반드시 선이 보입니다.
// 풀은 아래에 깔린 지면과 <b>같은 함수로</b> 색을 뽑아야 서로 녹아듭니다.
// 그래서 색을 정하는 자리를 이 파일 하나로 모아 둡니다.

// --- 잡음 ---

/// <summary>격자점 하나에 대응하는 난수 하나를 만듭니다.</summary>
float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

/// <summary>
/// 값 잡음입니다. 격자마다 난수를 두고 사이를 부드럽게 이어 붙입니다.
/// 텍스처가 아니라 좌표에서 바로 뽑기 때문에 <b>반복 이음매가 없습니다.</b>
/// </summary>
float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    // 직선으로 이으면 격자 자국이 보입니다. 양 끝의 기울기를 0으로 눕혀 줍니다.
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = Hash21(i);
    float b = Hash21(i + float2(1, 0));
    float c = Hash21(i + float2(0, 1));
    float d = Hash21(i + float2(1, 1));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

/// <summary>큰 얼룩과 잔 얼룩을 겹쳐 자연스러운 색 흔들림을 만듭니다.</summary>
float GroundNoise(float2 worldXZ, float scale)
{
    float n = ValueNoise(worldXZ * scale) * 0.65
            + ValueNoise(worldXZ * scale * 2.7) * 0.35;
    return saturate(n);
}

// --- 색 ---

/// <summary>지면 색을 정하는 데 필요한 색들을 한 묶음으로 넘깁니다.</summary>
struct GroundPalette
{
    half3 grassA;
    half3 grassB;
    half3 dirtA;
    half3 dirtB;
    half3 roadA;
    half3 roadB;
    float noiseScale;
};

/// <summary>
/// 스플랫 가중치와 좌표로 지면 색을 뽑습니다.
///
/// 텍스처를 깔지 않습니다. 타일 텍스처는 아무리 잘 만들어도 반복 무늬가 눈에 걸리는데,
/// 색을 좌표에서 바로 계산하면 그 이음매 자체가 생기지 않습니다.
/// 로우폴리 룩이 텍스처를 안 쓰는 이유이기도 합니다.
/// </summary>
/// <param name="palette">쓸 색 묶음</param>
/// <param name="worldXZ">월드 좌표의 가로/세로</param>
/// <param name="control">터레인 스플랫 가중치 (R 잔디 / G 흙 / B 도로)</param>
/// <returns>섞인 지면 색</returns>
half3 SampleGroundAlbedo(GroundPalette palette, float2 worldXZ, half4 control)
{
    float n = GroundNoise(worldXZ, palette.noiseScale);

    half3 grass = lerp(palette.grassA, palette.grassB, n);
    half3 dirt  = lerp(palette.dirtA,  palette.dirtB,  n);
    half3 road  = lerp(palette.roadA,  palette.roadB,  n);

    // 가중치 합이 1이 아니면 지면이 어두워지거나 날아갑니다.
    half total = dot(control, half4(1, 1, 1, 1));
    control /= max(total, 1e-4h);

    return control.r * grass + control.g * dirt + control.b * road;
}

// --- 조명 ---

/// <summary>
/// 아늑한 느낌의 조명입니다.
///
/// 두 가지가 핵심입니다.
///  1. 밝기를 <b>단계로 끊습니다.</b> 그래야 면이 또렷하게 갈라져 로우폴리로 읽힙니다.
///  2. 그늘을 검정이 아니라 <b>서늘한 색</b>으로 물들입니다.
///     그늘이 검게 죽으면 차갑고 딱딱해 보입니다. 하늘빛이 도는 그늘이 코지 룩을 만듭니다.
/// </summary>
/// <param name="albedo">바탕색</param>
/// <param name="normalWS">월드 공간 법선</param>
/// <param name="shadowCoord">그림자 좌표</param>
/// <param name="steps">밝기를 끊을 단계 수. 2 미만이면 끊지 않습니다.</param>
/// <param name="shadowColor">그늘을 물들일 색</param>
/// <param name="ambientBoost">주변광 배율</param>
/// <returns>조명이 적용된 색</returns>
half3 CozyShade(half3 albedo, float3 normalWS, float4 shadowCoord,
                float steps, half3 shadowColor, half ambientBoost)
{
    Light mainLight = GetMainLight(shadowCoord);

    half ndl = saturate(dot(normalWS, mainLight.direction));
    half shade = saturate(ndl * mainLight.shadowAttenuation);

    if (steps >= 2.0)
    {
        // floor 를 그냥 쓰면 shade 가 1일 때 단계를 하나 넘겨 버립니다.
        half top = steps - 1.0;
        shade = min(floor(shade * steps), top) / top;
    }

    half3 tint = lerp(shadowColor, half3(1, 1, 1), shade);
    half3 ambient = SampleSH(normalWS) * ambientBoost;

    return albedo * (tint * mainLight.color + ambient);
}

/// <summary>
/// 화면에 그려진 실제 삼각형의 법선을 구합니다.
///
/// 정점에 들어 있는 법선은 이웃과 평균이 나 있어 표면이 매끈하게 보입니다.
/// 옆 픽셀과의 좌표 차이로 직접 구하면 <b>삼각형 하나하나가 각진 면</b>으로 드러납니다.
/// 지오메트리 셰이더 없이 로우폴리 면을 얻는 방법입니다.
/// </summary>
/// <param name="positionWS">월드 좌표</param>
/// <param name="smoothNormalWS">정점에서 넘어온 매끈한 법선. 방향을 맞추는 데 씁니다.</param>
/// <returns>면 법선</returns>
float3 FaceNormal(float3 positionWS, float3 smoothNormalWS)
{
    float3 n = normalize(cross(ddx(positionWS), ddy(positionWS)));

    // 화면 축의 방향에 따라 부호가 뒤집힐 수 있어 원래 법선 쪽으로 맞춰 줍니다.
    return n * sign(dot(n, smoothNormalWS));
}

#endif
