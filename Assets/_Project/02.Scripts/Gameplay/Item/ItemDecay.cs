using UnityEngine;
using VContainer;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 월드에 놓인 채 <b>하루가 지난 물건</b>을 치웁니다.
    ///
    /// <b>왜 필요한가.</b> 이 게임은 병을 창밖으로 던지고 봉투에서 물건을 꺼내 흘립니다.
    /// 아무것도 치우지 않으면 물건이 무한히 쌓여 두 가지가 함께 나빠집니다 —
    /// 물리 오브젝트가 계속 늘고, <b>세이브 파일이 끝없이 커집니다.</b>
    /// 하루가 지나면 사라진다는 규칙이 그 둘을 한꺼번에 막습니다.
    ///
    /// <b>실제 시간이 아니라 게임 시간으로 셉니다.</b> 수면이나 기절로 시간을 건너뛰면
    /// 그동안 놓여 있던 것도 함께 사라져야 합니다. 잠을 자고 일어났는데 어제 흘린 병이
    /// 그대로 있으면 시간이 흐른 것으로 보이지 않습니다.
    ///
    /// <b>드는 중이거나 차에 실린 것은 세지 않습니다.</b> 손에 든 물건이 하루가 지났다고
    /// 사라지면 그것은 규칙이 아니라 고장으로 보입니다. 판정은
    /// <see cref="SaveableItem.Placement"/> 가 합니다.
    /// </summary>
    public class ItemDecay : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>물건이 월드에 남아 있을 수 있는 게임 시간(분)입니다.</summary>
        [Header("수명")]
        [Tooltip("월드에 놓인 물건이 남아 있는 게임 시간(분). 1440이 하루입니다.")]
        public float lifetimeGameMinutes = 1440f;

        /// <summary>확인 주기(실제 초)입니다.</summary>
        [Tooltip("확인 주기(실제 초). 매 프레임 확인할 이유가 없습니다.")]
        public float sweepIntervalSeconds = 5f;

        /// <summary>체크를 해제하면 아무것도 치우지 않습니다. (디버그용)</summary>
        [Tooltip("체크를 해제하면 아무것도 치우지 않습니다. (디버그용)")]
        public bool decayEnabled = true;

        // --- Private Member Variables ---

        /// <summary>
        /// 시간을 읽어 올 시계입니다.
        ///
        /// 주입되지 않으면 <see cref="NullGameClock"/> 이 들어 있고, 그 시계는 시각이
        /// 늘 0이라 <b>아무것도 사라지지 않습니다.</b> 시계가 없을 때 물건이 갑자기
        /// 사라지는 것보다 남아 있는 편이 안전합니다.
        /// </summary>
        private IGameClock clock = NullGameClock.Instance;

        /// <summary>다음 확인까지 남은 시간(실제 초)입니다.</summary>
        private float sweepTimer;

        // --- Injection ---

        /// <summary>게임 시계를 받습니다.</summary>
        /// <param name="gameClock">게임 시계</param>
        [Inject]
        public void Construct(IGameClock gameClock)
        {
            if (gameClock != null) clock = gameClock;
        }

        // --- Unity Event Functions ---

        /// <summary>자신을 등록합니다. 디버그 도구가 찾아 쓸 수 있습니다.</summary>
        void Awake()
        {
            GameContext.Register(this);
        }

        /// <summary>등록을 해제합니다.</summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
        }

        /// <summary>주기가 되면 한 번 훑습니다.</summary>
        void Update()
        {
            if (!decayEnabled) return;

            sweepTimer -= Time.deltaTime;
            if (sweepTimer > 0f) return;

            sweepTimer = Mathf.Max(0.1f, sweepIntervalSeconds);
            Sweep();
        }

        // --- Public Methods ---

        /// <summary>
        /// 놓인 물건의 상태를 갱신하고 하루가 지난 것을 치웁니다.
        ///
        /// <b>밖에서도 부를 수 있게 열어 두었습니다.</b> 테스트가 프레임 없이 확인하고,
        /// 세이브를 불러온 직후에도 한 번 부릅니다. (불러온 물건 중에 이미 수명이
        /// 다한 것이 있을 수 있습니다)
        /// </summary>
        /// <returns>이번에 치운 물건의 수</returns>
        public int Sweep()
        {
            float now = clock.TotalMinutes;
            int removed = 0;

            // 뒤에서부터 훑습니다. 치우면 등록부에서 빠지기 때문입니다.
            for (int i = SaveableItem.All.Count - 1; i >= 0; i--)
            {
                if (i >= SaveableItem.All.Count) continue;

                SaveableItem item = SaveableItem.All[i];
                if (item == null) continue;

                // 들고 있거나 실려 있으면 시계를 멈춥니다.
                // 다시 놓이면 그때부터 새로 셉니다 — 주웠다 놓으면 수명이 되돌아갑니다.
                if (item.Placement != ItemPlacement.Loose)
                {
                    item.SetLooseSince(SaveableItem.NotLoose);
                    continue;
                }

                // 방금 놓였다면 지금을 시작점으로 잡습니다.
                if (item.LooseSinceMinute <= SaveableItem.NotLoose)
                {
                    item.SetLooseSince(now);
                    continue;
                }

                if (item.LooseFor(now) < lifetimeGameMinutes) continue;

                Despawn(item.gameObject);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// 물건 하나를 치웁니다.
        ///
        /// <b>왜 모드를 가르는가.</b> <c>Object.Destroy</c> 는 에디터 테스트에서
        /// "Destroy may not be called from edit mode" 오류가 됩니다. 그렇다고 늘
        /// <c>DestroyImmediate</c> 를 쓰면 실행 중에 물리·렌더 중간에 오브젝트가 사라져
        /// 위험합니다. <b>실행 중에는 풀로, 편집 중에는 즉시</b>가 양쪽 모두 옳은 답입니다.
        /// </summary>
        /// <param name="go">치울 오브젝트</param>
        internal static void Despawn(GameObject go)
        {
            if (go == null) return;

            // 풀에서 나온 것이면 풀로 돌아가고, 아니면 파괴됩니다.
            if (Application.isPlaying)
            {
                PrefabPool.Release(go);
                return;
            }

            Object.DestroyImmediate(go);
        }
    }
}
