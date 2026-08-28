using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 자국이 실제로 차지하는 미터를 재서 판(quad)의 크기와 자리를 정합니다.
    ///
    /// <b>왜 따로 떼었는가.</b> 예전에는 <see cref="UrineSplatter"/>와 SplatCapture 가
    /// 판을 늘이는 비율을 <b>각자 적어 두고</b> "같아야 합니다" 라고 주석으로만 묶여 있었습니다.
    /// 그러면 캡처로 눈으로 골라 둔 값이 게임에서 다른 그림이 되는 날이 옵니다.
    /// 순수 함수 하나로 모으면 어긋날 자리가 없고, 씬도 그래픽 장치도 없이 테스트할 수 있습니다.
    ///
    /// <b>판은 종이일 뿐입니다.</b> 여기서 정하는 것은 "그릴 자리가 얼마나 필요한가" 뿐이고,
    /// 무엇을 어떻게 그릴지는 전부 셰이더가 <b>앵커 기준 미터</b>로 정합니다.
    /// 그래서 판이 커져도 이미 그려진 것은 한 픽셀도 안 움직입니다.
    /// </summary>
    public static class SplatQuadLayout
    {
        /// <summary>
        /// 줄기 하나가 도달할 수 있는 가장 먼 배수입니다.
        /// 셰이더의 <c>tipAt = _DripReach * _DripAmount * (0.3 + lane * 1.2)</c> 에서
        /// lane 이 1 일 때의 값입니다. <b>셰이더와 함께 고쳐야 합니다.</b>
        /// </summary>
        public const float DripReachPeak = 1.5f;

        /// <summary>줄기 끝이 판 밖으로 잘리지 않게 두는 여유(m)입니다.</summary>
        public const float TailMargin = 0.06f;

        /// <summary>
        /// 몸통이 테두리 갉기로 밖으로 밀릴 수 있는 배수입니다.
        /// 셰이더 <c>smoothstep(0.30, 1.05, d)</c> 의 상한에, 잡음이 d 를 낮출 수 있는
        /// 최댓값(<c>_EdgeBite / 2</c>)을 더한 것입니다.
        ///
        /// <b>이 여유가 없으면 자국이 네모로 잘립니다.</b> 예전 코드가 정확히 그랬습니다 —
        /// 다 자란 자국의 판 반폭이 몸통 반지름과 같아서, 잡음 골짜기 쪽 테두리가
        /// 아직 진하게 남은 채로 판 변에 부딪혀 직선으로 끊겼습니다.
        /// </summary>
        public static float OuterFactor(float edgeBite)
        {
            return 1.05f + edgeBite * 0.5f;
        }

        /// <summary>
        /// 앵커에서 사방으로 얼마나 뻗어야 하는지 잽니다.
        /// </summary>
        /// <param name="bodyRadius">몸통 반지름(m)</param>
        /// <param name="edgeBite">재질의 _EdgeBite</param>
        /// <param name="dripStart">줄기 머리 깊이(m)</param>
        /// <param name="dripReach">줄기 길이(m)</param>
        /// <param name="dripAmount">흘러내림 정도(0~1)</param>
        /// <param name="side">앵커에서 위·좌·우로 뻗는 거리(m)</param>
        /// <param name="down">앵커에서 아래로 뻗는 거리(m)</param>
        /// <param name="bodyOffsetY">셰이더에 넘길 _BodyOffsetY (판 좌표에서 앵커의 y)</param>
        public static void Measure(float bodyRadius, float edgeBite,
                                   float dripStart, float dripReach, float dripAmount,
                                   out float side, out float down, out float bodyOffsetY)
        {
            side = Mathf.Max(bodyRadius, 0.005f) * OuterFactor(edgeBite);

            // 아래쪽만 줄기 몫을 더 받습니다. 위·좌·우는 몸통이면 충분합니다.
            float tail = dripStart + dripReach * Mathf.Clamp01(dripAmount) * DripReachPeak + TailMargin;
            down = Mathf.Max(side, dripAmount > 0.001f ? tail : side);

            // 판 좌표는 -1~1 이고 세로 길이가 (side + down) 이므로, 앵커의 y 는 이 비율입니다.
            bodyOffsetY = (down - side) / Mathf.Max(side + down, 1e-4f);
        }

        /// <summary>
        /// 판의 크기와, 앵커에서 판 중심까지의 위쪽 오프셋(m)을 냅니다.
        ///
        /// 판 중심은 <c>앵커 + 판위쪽 * centerLift</c> 입니다. centerLift 가 음수면
        /// 판 중심이 앵커보다 아래에 놓입니다 — 줄기가 자랄 자리를 아래로 벌었다는 뜻입니다.
        /// </summary>
        public static void Resolve(float bodyRadius, float edgeBite,
                                   float dripStart, float dripReach, float dripAmount,
                                   out Vector2 size, out float centerLift, out float bodyOffsetY)
        {
            float side, down;
            Measure(bodyRadius, edgeBite, dripStart, dripReach, dripAmount,
                    out side, out down, out bodyOffsetY);

            size = new Vector2(side * 2f, side + down);
            centerLift = (side - down) * 0.5f;
        }
    }
}
