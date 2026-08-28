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


// ── 벽 자국과 같은 손그림 얼룩으로 ──
//
// 위의 <c>CarDriveSplatWetness</c> 는 매끈한 0~1 값입니다. 그대로 쓰면 땅에 <b>에어브러시로
// 뿌린 것 같은</b> 부드러운 얼룩이 생기는데, 벽에 붙는 자국(CarDriveSplat.shader)은
// 너덜너덜한 테두리에 손그림 획으로 지워집니다. 같은 오줌인데 <b>땅과 벽이 다른 물건</b>으로
// 보입니다.
//
// <b>지도는 낮은 주파수만 담습니다.</b> 텍셀이 6cm 라 얼룩의 잔결을 담을 수 없습니다.
// 그래서 지도는 "얼마나 젖었나" 만 들고, 너덜너덜함과 획은 여기서 <b>픽셀 해상도로</b>
// 만듭니다. 벽 자국이 앵커 기준 미터로 하는 것과 같은 생각입니다.
//
// <b>자르지 않고 마스크로 씁니다.</b> 벽에서는 획이 문턱을 못 넘으면 <c>clip</c> 으로
// 픽셀을 버립니다. 땅에서 같은 짓을 하면 <b>지면에 구멍이 뚫립니다</b> — 자국은 얹히는
// 것이지 도려내는 것이 아닙니다. 그래서 여기서는 획이 "이 픽셀이 얼룩인가" 를 정하고,
// 얼룩이 아닌 픽셀은 그냥 원래 땅색으로 둡니다. 화면에 보이는 결과는 같습니다.
#include "CarDriveHatch.hlsl"
#include "../LowPoly/CarDriveNoise.hlsl"

/// <summary>
/// 이 자리가 <b>손그림 얼룩</b>인지 봅니다. 0 이면 마른 땅, 1 이면 얼룩입니다.
///
/// 값이 중간으로 나오지 않습니다 — 획이 문턱을 넘거나 못 넘거나 둘뿐입니다.
/// 벽 자국이 남거나 없거나인 것과 같아야 <b>한 사람이 그린 것</b>으로 읽힙니다.
/// </summary>
/// <param name="positionWS">월드 위치</param>
/// <param name="normalWS">월드 법선. 획의 삼중평면이 씁니다.</param>
/// <param name="noiseScale">테두리 잡음의 잘기(1m 당 주기)</param>
/// <param name="edgeBite">테두리를 갉는 정도. 벽 자국의 _EdgeBite 와 같은 뜻입니다.</param>
/// <param name="edgeSharp">덮임의 경사를 세우는 정도. 클수록 얼룩의 테두리가 또렷해집니다.</param>
/// <param name="hatchScale">획 한 판이 덮는 거리(m)</param>
/// <param name="hatchBite">획이 얼룩을 갉는 정도</param>
half CarDriveSplatStain(float3 positionWS, float3 normalWS,
                        float noiseScale, half edgeBite, half edgeSharp,
                        float hatchScale, half hatchBite)
{
    half w = CarDriveSplatWetness(positionWS);
    if (w <= 0.0h) return 0.0h;

    // 테두리를 잡음으로 갉습니다. 원 그대로 두면 스프레이 자국이 됩니다.
    // 좌표는 <b>월드 미터</b>라, 자국이 넓어져도 잔결이 함께 늘어나지 않습니다.
    float2 np = positionWS.xz * noiseScale;
    float n = ValueNoise(np) * 0.68 + ValueNoise(np * 2.6) * 0.32;

    // ── 잡음은 <b>가장자리에서만</b> 흔듭니다 ──
    //
    // 벽 자국은 <b>거리</b>를 흔든 뒤 감쇠를 먹입니다. 그래서 몸통 밖은 무슨 잡음이 와도
    // 0 이고, 테두리에서만 너덜너덜해집니다. 여기는 감쇠 <b>결과</b>(w)만 손에 쥐고 있어
    // 그 순서를 쓸 수 없습니다.
    //
    // 그대로 더했더니 완전히 마른 땅(w=0)까지 최대 +0.275 들려 <b>화면 전체에 잉크가
    // 흩뿌려졌습니다.</b> 그래서 경사를 세워 눌렀더니 이번에는 흔들림까지 같이 눌려
    // <b>테두리가 매끈한 원</b>이 됐습니다 — 흔들리는 폭은 잡음 세기를 기울기로 나눈 값이라,
    // 기울기를 5배로 세우면 흔들림도 1/5 이 됩니다. 둘 다 틀렸습니다.
    //
    // 답은 흔드는 <b>세기 자체</b>를 가장자리에서만 살리는 것입니다.
    // w(1-w)x4 는 안팎(0, 1)에서 0 이고 딱 중간(0.5)에서 1 입니다.
    // 마른 땅은 아무리 잡음이 세도 안 들리고, 다 젖은 가운데는 안 갉히며,
    // 테두리만 흔들립니다 — 벽 자국이 거리를 흔들 때 얻는 성질과 같습니다.
    half band = w * (1.0h - w) * 4.0h;
    half coverage = saturate((w + (half)((n - 0.5) * edgeBite) * band - 0.5h) * edgeSharp + 0.5h);

    [branch] if (_CarDriveHatchParams.w >= 0.5)
    {
        // 세계의 그늘을 긋는 그 TAM 입니다. 톤은 한 단계로 고정합니다 —
        // 위치에 따라 흔들면 마르는 동안 무늬가 바뀌어 획이 기어다녀 보입니다.
        half pattern = CarDriveHatchValue(0.6h, positionWS, normalWS,
                                          max(hatchScale, 0.01), 1.0h);

        // 벽 자국의 clip 과 같은 식입니다. 진한 가운데는 어떤 무늬값이라도 넘어 통짜로
        // 남고, 테두리로 갈수록 짙은 획만 버팁니다.
        // 0.0001 을 더해 <b>덮임이 0 인 마른 땅</b>이 무늬가 0 인 자리에서 얼룩이 되는 것을 막습니다.
        return step(pattern * hatchBite + 0.0001h, coverage * (1.0h + hatchBite));
    }

    // HatchingRig 가 없으면 획을 못 긋습니다. 그냥 두면 테두리가 매끈해져
    // 이 함수의 이유가 사라지므로, 갉은 덮임을 그대로 문턱으로 씁니다.
    return step(0.5h, coverage);
}

#endif // CARDRIVE_SPLAT_MAP_INCLUDED
