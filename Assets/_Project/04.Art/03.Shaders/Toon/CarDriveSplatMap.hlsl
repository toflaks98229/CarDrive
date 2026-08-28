#ifndef CARDRIVE_SPLAT_MAP_INCLUDED
#define CARDRIVE_SPLAT_MAP_INCLUDED

// ── 전역 자국 맵 읽기 ──
//
// 세계 전체를 위에서 내려다본 <b>한 장의 젖음 지도</b>입니다. 값은 0(마름) ~ 1(흠뻑)이고,
// 컴퓨트 셰이더(SplatPainter.compute)가 여기에 칠하고 서서히 지웁니다.
//
// <b>왜 전역인가.</b> 이걸 재질마다 물려 줄 수가 없습니다. Unity Terrain 은
// MaterialPropertyBlock 을 받지 않고, 지형은 100m 타일이 103장이라 타일마다 다른 값을
// 넣는 방식은 애초에 유지가 안 됩니다. 젖음은 <b>세계 전체에 걸리는 하나의 현상</b>이라
// 구름 그림자(_CloudShadowMap)·풀 밀림(GrassPushField)과 같은 자리에 두었습니다.
//
// <b>왜 따로 떼어 두었는가.</b> <c>CarDriveToonLighting.hlsl</c> 안에 넣지 않았습니다.
// 그 파일은 <c>GetMainLight</c>·<c>SampleSH</c> 같은 URP 조명 심볼에 기대기 때문에
// 조명 없는 셰이더가 포함할 수 없습니다. 젖음은 도로·소품·이펙트처럼 조명을 안 쓰는
// 곳에서도 읽고 싶은 값이고, 그때 파일을 복사해 두면 <b>한쪽만 고쳐지는 날이 옵니다.</b>
// <c>CarDriveDither.hlsl</c> · <c>CarDriveHatch.hlsl</c> 을 떼어낸 것과 같은 이유이고
// 같은 방식입니다. 여기 있는 것은 텍스처 샘플과 산술뿐이라, TEXTURE2D 매크로를 아는
// 셰이더면 어디서든 포함할 수 있습니다.
//
// ── 이름과 좌표 규칙은 C# 과 <b>글자 그대로</b> 같아야 합니다 ──
//
// 아래 세 전역과 UV 식은 C# 쪽(전역을 넣는 시스템)과 공유하는 계약입니다.
// 한쪽만 고치면 컴파일도 되고 화면도 그럴듯한데 자국이 <b>엉뚱한 자리에</b> 찍힙니다.
//   uv.x = (worldX - originX) * invSizeX
//   uv.y = (worldZ - originZ) * invSizeZ

// 단일 채널(R)입니다. R8 을 먼저 쓰고, 안 되면 RHalf → RFloat 로 물러섭니다.
// 어느 쪽이든 읽는 쪽은 .r 하나라 여기는 바뀌지 않습니다.
TEXTURE2D(_GlobalSplatMap);
SAMPLER(sampler_GlobalSplatMap);

/// xy = 맵 구석의 월드 좌표(originX, originZ), zw = 1/가로(m), 1/세로(m).
/// <b>나눗셈이 아니라 역수입니다.</b> 픽셀마다 나누지 않으려고 C# 이 미리 뒤집어 넘깁니다.
float4 _GlobalSplatMapRect;

/// 0 = 시스템이 없거나 이 기기에서 못 씀, 1 = 유효.
float _GlobalSplatMapOn;

/// <summary>
/// 이 월드 좌표가 얼마나 젖었는지 읽습니다. 0 이면 마름, 1 이면 흠뻑입니다.
///
/// <b>맵이 없으면 공짜로 꺼집니다.</b> 안 바인딩된 텍스처를 그냥 샘플하면 플랫폼마다
/// 다른 값(대개 흰색이나 검정)이 나와, 자국 시스템이 없는 기기에서 <b>온 세상이 젖은 것처럼</b>
/// 보입니다. 그래서 전역 스위치로 먼저 가릅니다. <c>SampleCloudShadow</c> 가
/// <c>_CloudShadowParams.w</c> 로 하는 것과 같습니다.
/// 이 스위치는 <b>유니폼</b>이라 한 쿼드의 네 픽셀이 늘 같은 길로 갑니다 — 픽셀마다 갈리는
/// 분기가 아니므로 미분이 무너지는 종류의 문제가 없습니다.
/// </summary>
/// <param name="positionWS">월드 좌표. y 는 쓰지 않습니다 — 위에서 내려다본 투영입니다.</param>
/// <returns>0~1 젖은 정도</returns>
half CarDriveSplatWetness(float3 positionWS)
{
    if (_GlobalSplatMapOn < 0.5) return 0.0h;

    // 계약의 UV 식 그대로입니다. 여기를 고치려거든 C# 쪽도 같이 고쳐야 합니다.
    float2 uv = (positionWS.xz - _GlobalSplatMapRect.xy) * _GlobalSplatMapRect.zw;

    // ── 맵 밖은 0 ──
    //
    // <b>샘플러의 wrap 모드에 기대지 않습니다.</b> Clamp 면 가장자리 텍셀이 맵 밖으로
    // 무한히 번져서, 세계 끝의 웅덩이 하나가 그 방향 전체를 젖게 만듭니다. Repeat 면
    // 반대편 자국이 되풀이돼 나타납니다. 둘 다 틀린 그림이고, 둘 중 무엇이 걸릴지는
    // C# 이 텍스처를 어떻게 만들었느냐에 달려 있습니다. 그래서 <b>읽는 쪽에서</b> 자릅니다.
    //
    // 분기가 아니라 곱으로 자릅니다. 샘플은 어차피 한 번이라 분기로 아낄 것이 없고,
    // 이 판정은 <b>픽셀마다 갈리므로</b> 분기로 두면 맵 경계에 걸친 쿼드에서 흐름이 갈립니다.
    float2 inRange = step(0.0, uv) * step(uv, 1.0);
    half inside = (half)(inRange.x * inRange.y);

    // ── 왜 밉을 안 쓰는가 (LOD 0 고정) ──
    //
    // 이 맵은 매 프레임 컴퓨트가 덧칠하는 대상입니다. 밉 사슬을 두면 <b>칠할 때마다</b>
    // 사슬 전체를 다시 만들어야 하는데, 정작 칠한 곳은 화면의 한 뼘입니다.
    // 그래서 단일 레벨로 둡니다 — 그러면 암시적 LOD 도 어차피 0 을 고르므로,
    // 미분에 기대는 것은 <b>얻는 것 없이 제약만 남기는</b> 셈입니다.
    //
    // LOD 를 못박아 두면 이 함수를 <b>픽셀마다 갈리는 분기 안에서도</b> 부를 수 있습니다.
    // 아무 데서나 포함할 수 있어야 한다는 이 파일의 목적과 맞습니다.
    half wet = (half)SAMPLE_TEXTURE2D_LOD(_GlobalSplatMap, sampler_GlobalSplatMap, uv, 0).r;

    return wet * inside;
}

#endif // CARDRIVE_SPLAT_MAP_INCLUDED
