using UnityEngine;
using MoreMountains.Feedbacks;

namespace CarDrive.Common
{
    /// <summary>
    /// 풀로 돌아갈 때 재생 중이던 피드백을 멈춥니다.
    ///
    /// <b>왜 필요한가.</b> 이 게임의 귀신과 재화 덩어리는 파괴되지 않고
    /// <see cref="PrefabPool"/> 로 돌아갑니다. 그런데 피드백이 재생되는 도중에 회수되면
    /// 그 피드백이 건드리던 값(재질 색·크기·라이트 밝기)이 <b>중간 상태로 굳은 채</b> 남고,
    /// 다음에 꺼낼 때 그 모습으로 나타납니다. 빨갛게 물든 귀신, 찌그러진 덩어리 같은 것들입니다.
    ///
    /// <see cref="EnemyBase"/> 가 렌더러와 라이트를 손으로 되돌리는 것과 같은 이유이고,
    /// 이 컴포넌트는 그 일을 Feel 쪽에 대해 합니다.
    ///
    /// 풀에서 나오는 프리팹에 붙이세요. 그 외의 곳에서는 필요 없습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FeedbackPoolGuard : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>
        /// 멈출 대상입니다. 비워두면 자식까지 뒤져서 모두 찾습니다.
        /// </summary>
        [Tooltip("멈출 MMF_Player 들. 비워두면 자식까지 뒤져서 모두 찾습니다.")]
        public MMF_Player[] players;

        /// <summary>
        /// 멈출 때 진행 중인 연출을 즉시 끊을지 여부입니다.
        /// 꺼진 오브젝트에서 연출이 이어질 이유가 없으므로 기본값은 즉시 끊기입니다.
        /// </summary>
        [Tooltip("체크하면 진행 중인 연출을 즉시 끊습니다. 풀 회수에는 이쪽이 맞습니다.")]
        public bool interruptImmediately = true;

        // --- Unity Event Functions ---

        /// <summary>
        /// 비어 있으면 자기 계층에서 찾습니다. 꺼져 있는 것도 포함합니다.
        /// </summary>
        void Awake()
        {
            if (players == null || players.Length == 0)
            {
                players = GetComponentsInChildren<MMF_Player>(true);
            }
        }

        /// <summary>
        /// 풀로 돌아갈 때 재생 중인 것을 모두 멈춥니다.
        /// </summary>
        void OnDisable()
        {
            if (players == null) return;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                players[i].StopFeedbacks(interruptImmediately);
            }
        }
    }
}
