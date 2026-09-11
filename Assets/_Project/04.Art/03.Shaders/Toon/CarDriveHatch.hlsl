#ifndef CARDRIVE_HATCH_INCLUDED
#define CARDRIVE_HATCH_INCLUDED

// <b>왜 따로 떼어 두었는가.</b> 원래는 <c>CarDriveToonLighting.hlsl</c> 안에 있었습니다.
// 그런데 그 파일은 <c>GetMainLight</c>·<c>SampleSH</c> 같은 URP 조명 심볼을 쓰기 때문에
// <b>UI 셰이더가 포함할 수 없습니다.</b> 니즈 게이지가 세계와 같은 빗금으로 그늘을 그리려면
// 이 부분만 따로 있어야 했습니다. <c>CarDriveDither.hlsl</c> 을 떼어낸 것과 같은 이유이고
// 같은 방식입니다 — UI 쪽에 복사해 두면 한쪽만 고쳐지는 날이 옵니다.
//
// 여기 있는 것은 <b>조명에 기대지 않습니다.</b> 텍스처 샘플과 산술뿐이라
// TEXTURE2D 매크로를 아는 셰이더면 어디서든 포함할 수 있습니다.
//
// <b>2D 에서 쓰는 법.</b> 아래 삼중평면은 법선이 축에 정렬되면 평면 하나로 자연히
// 붕괴합니다. 법선에 (0,0,1) 을 주면 <c>uvZ = positionWS.xy</c> 하나만 샘플되므로,
// 위치에 화면 픽셀 좌표를 넣으면 그대로 2D 빗금이 됩니다. 함수를 고칠 필요가 없습니다.

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

// w = 전역이 유효한가(0/1). x,y,z 는 월드 음영이 쓰던 자리로 지금은 아무도 안 읽습니다.
//
// ⚠ <b>w 만 보고 갈라야 합니다.</b> TAM 은 기본값으로 물러설 수 없습니다 — 전역이
// 안 들어왔는데 샘플하면 바인딩되지 않은 텍스처를 읽어 플랫폼마다 다른 색이 나옵니다.
// 그래서 HatchingRig 가 씬에 없으면 부르는 쪽이 디더로 물러섭니다.
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

#endif // CARDRIVE_HATCH_INCLUDED
