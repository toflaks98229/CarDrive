#ifndef CARDRIVE_DITHER_INCLUDED
#define CARDRIVE_DITHER_INCLUDED

// ── 디더 원시 함수 ──
//
// 이 프로젝트는 <b>사라지고 나타나는 일을 전부 같은 4x4 Bayer 행렬로</b> 합니다.
// 나무·바위·건물은 화면 픽셀마다, 풀은 잎마다, 니즈 게이지는 채움 경계에서.
// 디더 무늬가 <b>결점이 아니라 시대 표현</b>으로 읽히기 때문입니다.
//
// <b>왜 따로 떼어 두었는가.</b> 원래는 <c>CarDriveToonLighting.hlsl</c> 안에 있었습니다.
// 그런데 그 파일은 <c>GetMainLight</c>·<c>SampleSH</c> 같은 URP 조명 심볼을 쓰기 때문에
// <b>UI 셰이더가 포함할 수 없습니다.</b> 그렇다고 UI 쪽에 행렬을 복사해 두면
// 한쪽만 고쳐지는 날이 옵니다 — 그 걱정이 이 행렬을 처음 한곳으로 모은 이유였습니다.
//
// 그래서 <b>조명에 기대지 않는 부분만</b> 여기로 내렸습니다. 이 파일은 순수 HLSL 이라
// 어떤 셰이더에서든 포함할 수 있습니다.

/// 4x4 Bayer 행렬입니다. 값이 고르게 흩어져 있어 무늬가 뭉치지 않습니다.
static const half CarDriveBayer4x4[16] =
{
     0.0h / 16.0h,  8.0h / 16.0h,  2.0h / 16.0h, 10.0h / 16.0h,
    12.0h / 16.0h,  4.0h / 16.0h, 14.0h / 16.0h,  6.0h / 16.0h,
     3.0h / 16.0h, 11.0h / 16.0h,  1.0h / 16.0h,  9.0h / 16.0h,
    15.0h / 16.0h,  7.0h / 16.0h, 13.0h / 16.0h,  5.0h / 16.0h,
};

/// <summary>이 화면 픽셀의 문턱값을 구합니다. 나무·바위·건물이 씁니다.</summary>
/// <param name="pixelPos">화면 픽셀 좌표</param>
/// <returns>0~1 문턱값</returns>
half CarDriveDitherThreshold(float2 pixelPos)
{
    int2 cell = int2(fmod(abs(pixelPos), 4.0));
    return CarDriveBayer4x4[cell.y * 4 + cell.x];
}

/// <summary>
/// 대상마다 하나씩 주어진 씨앗으로 문턱값을 구합니다. 풀이 <b>잎마다</b>,
/// 나무가 <b>그루마다</b> 씁니다.
///
/// 화면 픽셀 대신 씨앗을 쓰는 이유가 있습니다. 40m 앞의 풀잎은 화면에서 두어 픽셀이라,
/// 픽셀마다 버리면 잎이 통째로 있다 없다 하며 <b>반짝입니다.</b> 잎을 단위로 버리면
/// 어느 잎이 사라질지가 카메라가 움직여도 바뀌지 않고, 풀밭이 성겨질 뿐입니다.
///
/// 같은 행렬을 그대로 쓰는 것이 중요합니다. 16 단계가 0~1 에 고르게 흩어져 있어서,
/// 남을 정도가 0.5 면 <b>정확히 절반</b>이 남습니다. 난수로 뽑으면 그 보장이 없어
/// 어느 구간에서는 뭉텅 사라지고 어느 구간에서는 그대로입니다.
/// </summary>
/// <param name="seed">대상마다 다르고, 프레임 사이에 <b>변하지 않아야 하는</b> 0~1 값</param>
/// <returns>0~1 문턱값</returns>
half CarDriveOrderedThreshold(float seed)
{
    // 비트 연산과 uint 를 피합니다. 이 파일은 <c>#pragma target 3.0</c> 셰이더도 포함하는데,
    // 그쪽에서는 둘 다 쓸 수 없습니다. 위의 픽셀 판이 쓰는 방식과 같게 맞췄습니다.
    return CarDriveBayer4x4[(int)(saturate(seed) * 15.999)];
}

#endif // CARDRIVE_DITHER_INCLUDED
