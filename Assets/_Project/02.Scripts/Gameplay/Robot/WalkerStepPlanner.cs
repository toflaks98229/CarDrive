using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 걸음을 정할 때 다리 하나에 대해 알아야 하는 전부입니다.
    ///
    /// <b>Transform 도 컴포넌트도 들어 있지 않습니다.</b> 그것이 요점입니다 —
    /// 이 다섯 개 숫자만 있으면 "다음에 어느 다리가 나갈지"가 정해지므로,
    /// 규칙을 씬 없이 검증할 수 있습니다.
    /// </summary>
    public struct WalkerLegState
    {
        /// <summary>발에서 이번에 가고 싶은 자리까지의 거리(m)입니다. 걸음을 부르는 값입니다.</summary>
        public float strideError;

        /// <summary>발에서 제자리까지의 거리(m)입니다. 끌려나간 다리를 구할 때 봅니다.</summary>
        public float homeDistance;

        /// <summary>이 다리가 발을 놓을 수 있는 반경(m)입니다. 다리마다 다를 수 있습니다.</summary>
        public float strideRadius;

        /// <summary>지금 발을 떼고 있는지입니다.</summary>
        public bool isStepping;

        /// <summary>이 다리가 속한 보행 묶음입니다. 같은 번호끼리 함께 나갑니다.</summary>
        public int group;
    }

    /// <summary>지금 속도·보행에서 나온 걸음의 성질입니다. 매 프레임 <see cref="WalkerRobot"/> 이 채웁니다.</summary>
    public struct WalkerStepTuning
    {
        /// <summary>작업 반경 중 실제로 쓸 비율입니다. 1이면 한계까지 씁니다.</summary>
        public float strideUsage;

        /// <summary>계획 보폭의 몇 할이 벌어지면 걸음을 시작할지입니다.</summary>
        public float triggerFraction;

        /// <summary>묶음 안에서 다리끼리 벌어지는 시차입니다. 0이면 함께, 1이면 걸음 하나만큼 차례로 나갑니다.</summary>
        public float stagger;

        /// <summary>걸음 하나에 걸리는 시간(초)입니다.</summary>
        public float stepDuration;
    }

    /// <summary>
    /// <b>다음에 어느 다리가 나갈지</b>를 정하는 규칙입니다.
    ///
    /// <b>왜 떼어냈는가.</b> 이 규칙은 이 게임의 보행이 절뚝이지 않는 이유 전부인데,
    /// 2,000줄짜리 <see cref="WalkerRobot"/> 안에 있어서 <b>검증할 방법이 없었습니다.</b>
    /// 다리 넷이 대각선으로 나가는지, 예약이 겹쳐 쌓이지 않는지, 끌려나간 다리가 구조되는지 —
    /// 전부 눈으로 보고 판단해야 했고, 회귀가 나도 알아챌 수 없었습니다.
    /// 여기에는 <see cref="Transform"/> 도 <see cref="WalkerLeg"/> 도 없으므로
    /// <see cref="Powertrain"/> 과 같은 방식으로 씬 없이 검증합니다.
    ///
    /// <b>이 클래스는 다리를 움직이지 않습니다.</b> 나갈 다리의 번호만 돌려주고,
    /// 실제로 발을 떼는 것은 <see cref="WalkerRobot"/> 입니다. 그래야 이 규칙이
    /// 유니티를 몰라도 됩니다.
    ///
    /// 규칙은 셋입니다.
    /// <code>
    /// 1. 예약된 걸음이 남아 있으면 새 묶음을 부르지 않는다  (묶음이 겹쳐 쌓이지 않게)
    /// 2. 모든 발이 땅에 있을 때만 새 묶음이 나간다          (묶음이 어긋난 채 굳지 않게)
    /// 3. 2를 못 지키는 사이에 작업 반경 밖으로 끌려나간 다리는 구한다 (다리가 찢어지지 않게)
    /// </code>
    /// </summary>
    public sealed class WalkerStepPlanner
    {
        // --- Constants ---

        /// <summary>예약이 비어 있음을 나타내는 값입니다. 0초 뒤 출발과 구분해야 하므로 음수를 씁니다.</summary>
        private const float NoPending = -1f;

        /// <summary>문턱 거리의 하한(m)입니다. 0이 되면 나눗셈이 폭발합니다.</summary>
        private const float MinTrigger = 0.01f;

        /// <summary>구조를 위해 땅에 남겨 둘 최소 발 개수입니다.</summary>
        private const int MinPlantedForRescue = 2;

        // --- Private Member Variables ---

        /// <summary>다리마다 남은 대기 시간(초)입니다. 음수면 예약이 없다는 뜻입니다.</summary>
        private float[] pending = new float[0];

        // --- Public Properties ---

        /// <summary>아직 출발하지 않은 예약이 하나라도 남아 있는지입니다.</summary>
        public bool HasPending
        {
            get
            {
                for (int i = 0; i < pending.Length; i++)
                {
                    if (pending[i] >= 0f) return true;
                }

                return false;
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// 다리 수에 맞춰 자리를 잡습니다. 같은 수로 다시 부르면 예약을 비우기만 합니다.
        /// </summary>
        /// <param name="legCount">다리 개수</param>
        public void Resize(int legCount)
        {
            if (legCount < 0) legCount = 0;

            if (pending.Length != legCount) pending = new float[legCount];

            ClearPending();
        }

        /// <summary>
        /// 예약을 모두 비웁니다.
        ///
        /// 순간이동하거나 쓰러졌다 일어난 직후에 부릅니다. 남겨 두면 <b>이미 없어진 상황을 위해
        /// 잡아 둔 걸음</b>이 뒤늦게 나가, 발이 엉뚱한 자리로 한 번 튑니다.
        /// </summary>
        public void ClearPending()
        {
            for (int i = 0; i < pending.Length; i++) pending[i] = NoPending;
        }

        /// <summary>
        /// 이 다리가 걸음을 시작하는 문턱 거리입니다.
        ///
        /// 다리마다 작업 반경이 다를 수 있으므로 <b>절대 거리가 아니라 그 다리의 반경에 대한 비율</b>로
        /// 정합니다. 그래야 앞다리가 짧고 뒷다리가 긴 리그에서도 같은 시점에 걸음이 나갑니다.
        /// </summary>
        /// <param name="strideRadius">이 다리의 작업 반경(m)</param>
        /// <param name="strideUsage">반경 중 실제로 쓸 비율</param>
        /// <param name="triggerFraction">계획 보폭의 몇 할에서 걸음을 시작할지</param>
        /// <returns>문턱 거리(m). 0으로 내려가지 않습니다.</returns>
        public static float StepTrigger(float strideRadius, float strideUsage, float triggerFraction)
        {
            return Mathf.Max(strideRadius * strideUsage * triggerFraction, MinTrigger);
        }

        /// <summary>
        /// 시간을 <paramref name="dt"/> 만큼 흘리고, <b>이번 프레임에 출발할 다리</b>를 정합니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        /// <param name="legs">다리 상태. 길이가 <see cref="Resize"/> 로 잡은 수와 같아야 합니다.</param>
        /// <param name="tuning">지금 속도·보행에서 나온 걸음의 성질</param>
        /// <param name="begin">출발할 다리 번호를 담을 곳. 부르는 쪽이 비워서 넘깁니다.</param>
        public void Plan(float dt, WalkerLegState[] legs, WalkerStepTuning tuning, List<int> begin)
        {
            if (legs == null || begin == null) return;

            int count = Mathf.Min(legs.Length, pending.Length);
            if (count == 0) return;

            DispatchPending(dt, legs, count, begin);

            // 아직 나갈 차례를 기다리는 다리가 있으면 새 묶음을 부르지 않습니다.
            // 그러지 않으면 시차가 클 때 묶음이 겹쳐 쌓입니다.
            if (HasPending) return;

            if (!AllPlanted(legs, count))
            {
                Rescue(legs, count, begin);
                return;
            }

            int bestGroup = FindReadyGroup(legs, count, tuning);
            if (bestGroup < 0) return;

            Launch(legs, count, tuning, bestGroup, begin);
        }

        // --- Private Methods ---

        /// <summary>
        /// 예약된 걸음의 시각을 흘려보내고, 때가 된 다리를 내보냅니다.
        ///
        /// <b>내보낸 다리를 그 자리에서 "떼는 중"으로 표시합니다.</b> 이것이 없으면 아래의
        /// "모든 발이 땅에 있나"와 구조의 발 개수 세기가 <b>방금 출발한 다리를 여전히
        /// 딛고 있는 것으로</b> 봅니다. 부르는 쪽에서는 걸음이 시작되는 순간
        /// <c>WalkerLeg.IsStepping</c> 이 곧바로 참이 되므로, 여기서도 같아야 합니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        /// <param name="legs">다리 상태. 출발한 다리를 표시하려고 함께 받습니다.</param>
        /// <param name="count">다리 개수</param>
        /// <param name="begin">출발할 다리 번호를 담을 곳</param>
        private void DispatchPending(float dt, WalkerLegState[] legs, int count, List<int> begin)
        {
            for (int i = 0; i < count; i++)
            {
                if (pending[i] < 0f) continue;

                pending[i] -= dt;
                if (pending[i] > 0f) continue;

                pending[i] = NoPending;
                begin.Add(i);
                legs[i].isStepping = true;
            }
        }

        /// <summary>모든 발이 땅에 있는지 봅니다.</summary>
        /// <param name="legs">다리 상태</param>
        /// <param name="count">다리 개수</param>
        /// <returns>모두 땅에 있으면 true</returns>
        private static bool AllPlanted(WalkerLegState[] legs, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (legs[i].isStepping) return false;
            }

            return true;
        }

        /// <summary>
        /// 문턱을 가장 크게 넘긴 다리를 찾아 <b>그 다리가 속한 묶음</b>을 돌려줍니다.
        /// </summary>
        /// <param name="legs">다리 상태</param>
        /// <param name="count">다리 개수</param>
        /// <param name="tuning">걸음의 성질</param>
        /// <returns>나갈 묶음 번호. 아무도 문턱을 넘지 않았으면 -1</returns>
        private static int FindReadyGroup(WalkerLegState[] legs, int count, WalkerStepTuning tuning)
        {
            int bestGroup = -1;

            // 1을 넘겨야 문턱을 넘은 것입니다. 비율로 견주므로 다리 길이가 달라도 공평합니다.
            float bestScore = 1f;

            for (int i = 0; i < count; i++)
            {
                float trigger = StepTrigger(legs[i].strideRadius, tuning.strideUsage, tuning.triggerFraction);
                float score = legs[i].strideError / trigger;
                if (score <= bestScore) continue;

                bestGroup = legs[i].group;
                bestScore = score;
            }

            return bestGroup;
        }

        /// <summary>
        /// 묶음을 내보냅니다. 첫 다리는 지금 나가고 나머지는 시차만큼 기다립니다.
        /// 시차가 0이면 예약 없이 전부 지금 나갑니다.
        /// </summary>
        /// <param name="legs">다리 상태</param>
        /// <param name="count">다리 개수</param>
        /// <param name="tuning">걸음의 성질</param>
        /// <param name="group">내보낼 묶음 번호</param>
        /// <param name="begin">출발할 다리 번호를 담을 곳</param>
        private void Launch(WalkerLegState[] legs, int count, WalkerStepTuning tuning, int group, List<int> begin)
        {
            float lag = tuning.stagger * tuning.stepDuration;
            int order = 0;

            for (int i = 0; i < count; i++)
            {
                if (legs[i].group != group) continue;

                float delay = order * lag;
                order++;

                if (delay <= 0f) begin.Add(i);
                else pending[i] = delay;
            }
        }

        /// <summary>
        /// 보행 표가 막고 있는 사이에 <b>작업 반경 밖으로 끌려나간</b> 다리를 구합니다.
        ///
        /// 걸음 시간을 기하에서 뽑고 계획 속도로 미리 맞추므로 보통은 여기까지 오지 않습니다.
        /// 그래도 지형이 갑자기 꺼지거나 누군가 로봇을 밀면 생길 수 있고, 그때는
        /// <b>보행의 규칙보다 다리가 찢어지지 않는 것</b>이 우선입니다. 다만 두 발은 남깁니다.
        /// </summary>
        /// <param name="legs">다리 상태</param>
        /// <param name="count">다리 개수</param>
        /// <param name="begin">출발할 다리 번호를 담을 곳</param>
        private static void Rescue(WalkerLegState[] legs, int count, List<int> begin)
        {
            int planted = 0;
            for (int i = 0; i < count; i++)
            {
                if (!legs[i].isStepping) planted++;
            }

            if (planted < MinPlantedForRescue) return;

            int worst = -1;
            float worstScore = 1f;

            for (int i = 0; i < count; i++)
            {
                if (legs[i].isStepping) continue;

                // 목표까지의 거리가 아니라 <b>제자리에서 벗어난 거리</b>로 봅니다.
                float score = legs[i].homeDistance / Mathf.Max(legs[i].strideRadius, MinTrigger);
                if (score <= worstScore) continue;

                worst = i;
                worstScore = score;
            }

            if (worst < 0) return;

            begin.Add(worst);
        }
    }
}
