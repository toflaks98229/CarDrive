#ifndef CARDRIVE_NOISE_INCLUDED
#define CARDRIVE_NOISE_INCLUDED

// 좌표에서 바로 뽑는 값 잡음입니다.
//
// <b>왜 따로 떼어 두었는가.</b> 원래는 <c>LowPolyGround.hlsl</c> 안에 있었습니다.
// 그런데 그 파일은 아래쪽에서 <c>GetMainLight</c>·<c>SampleSH</c> 를 쓰기 때문에
// 조명이 없는 셰이더(자국·UI 같은 것)가 포함할 수 없습니다.
// <c>CarDriveDither.hlsl</c>·<c>CarDriveHatch.hlsl</c> 을 떼어낸 것과 같은 이유이고
// 같은 방식입니다 — 복사해 두면 한쪽만 고쳐지는 날이 옵니다.

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

#endif // CARDRIVE_NOISE_INCLUDED
