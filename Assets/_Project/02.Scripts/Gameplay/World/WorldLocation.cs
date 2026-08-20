using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CarDrive.Gameplay
{
    /// <summary>장소의 종류입니다.</summary>
    public enum LocationKind
    {
        Village,    // 마을 (hub)
        Site,       // 의뢰 현장 (spoke 끝)
        Landmark    // 그 밖의 지형지물
    }

    /// <summary>WorldLocation 하나를 넘기는 인스펙터 연결용 이벤트입니다.</summary>
    [System.Serializable]
    public class WorldLocationEvent : UnityEvent<WorldLocation> { }

    /// <summary>
    /// 월드에서 이름을 가진 장소를 표시합니다.
    ///
    /// "지금 마을에 있는가"를 묻는 것이 앞으로 만들 거의 모든 것의 전제입니다.
    /// 의뢰 수주도, 주민과의 대화도, 상점도 장소를 알아야 성립합니다.
    /// 그래서 월드 전환의 결과물은 지형뿐 아니라 이 <b>장소 개념</b>입니다.
    /// </summary>
    public class WorldLocation : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>UI와 안내에 표시할 장소 이름입니다.</summary>
        [Header("장소")]
        [Tooltip("UI와 안내에 표시할 장소 이름")]
        public string displayName = "이름 없는 장소";

        /// <summary>이 장소의 종류입니다. 마을·현장 판별과 기즈모 색상에 쓰입니다.</summary>
        [Tooltip("이 장소의 종류")]
        public LocationKind kind = LocationKind.Landmark;

        /// <summary>이 반경 안에 들어오면 "이 장소에 있다"고 봅니다. 높이 차이는 무시합니다.</summary>
        [Tooltip("이 반경 안에 들어오면 '이 장소에 있다'고 봅니다.")]
        public float radius = 40f;

        /// <summary>플레이어가 이 장소에 들어오는 순간 한 번 호출됩니다.</summary>
        [Header("이벤트")]
        [Tooltip("플레이어가 이 장소에 들어왔을 때")]
        public WorldLocationEvent onPlayerEntered;

        /// <summary>플레이어가 이 장소를 벗어나는 순간 한 번 호출됩니다.</summary>
        [Tooltip("플레이어가 이 장소를 벗어났을 때")]
        public WorldLocationEvent onPlayerExited;

        // --- Public Properties ---

        /// <summary>등록된 모든 장소입니다.</summary>
        public static IReadOnlyList<WorldLocation> All { get { return all; } }

        /// <summary>플레이어가 지금 안에 있는 장소입니다. 아무 데도 아니면 null입니다.</summary>
        public static WorldLocation Current { get; private set; }

        /// <summary>플레이어가 지금 이 장소 안에 있는지 여부입니다.</summary>
        public bool IsPlayerInside { get; private set; }

        // --- Private Member Variables ---

        /// <summary>
        /// 활성화된 모든 장소의 등록부입니다.
        /// OnEnable/OnDisable에서 스스로 넣고 빼므로 씬을 오가도 유령 항목이 남지 않습니다.
        /// </summary>
        private static readonly List<WorldLocation> all = new List<WorldLocation>();

        // --- Unity Event Functions ---

        /// <summary>
        /// 이 장소를 전역 등록부에 넣습니다.
        /// </summary>
        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        /// <summary>
        /// 이 장소를 전역 등록부에서 빼고, 현재 장소로 잡혀 있었다면 그 참조도 지웁니다.
        /// </summary>
        void OnDisable()
        {
            all.Remove(this);
            if (Current == this) Current = null;
            IsPlayerInside = false;
        }

        /// <summary>
        /// 씬 뷰에서 이 장소의 판정 반경을 종류별 색상의 와이어 구로 그립니다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            switch (kind)
            {
                case LocationKind.Village: Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.6f); break;
                case LocationKind.Site: Gizmos.color = new Color(0.9f, 0.4f, 0.35f, 0.6f); break;
                default: Gizmos.color = new Color(0.6f, 0.7f, 0.9f, 0.6f); break;
            }

            Gizmos.DrawWireSphere(transform.position, radius);
        }

        // --- Public Methods ---

        /// <summary>플레이어 위치를 넣어 현재 장소를 갱신합니다. (WorldStreamer가 주기적으로 호출)</summary>
        /// <param name="playerPosition">판정에 쓸 플레이어의 월드 좌표</param>
        public static void UpdateCurrent(Vector3 playerPosition)
        {
            WorldLocation best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                WorldLocation loc = all[i];
                if (loc == null) continue;

                // 높이 차이는 무시합니다. (언덕 위 마을도 같은 마을입니다)
                Vector3 delta = loc.transform.position - playerPosition;
                delta.y = 0f;
                float distance = delta.magnitude;

                bool inside = distance <= loc.radius;

                if (inside && distance < bestDistance)
                {
                    best = loc;
                    bestDistance = distance;
                }

                // 개별 장소의 진입/이탈 이벤트
                if (inside != loc.IsPlayerInside)
                {
                    loc.IsPlayerInside = inside;
                    if (inside)
                    {
                        if (loc.onPlayerEntered != null) loc.onPlayerEntered.Invoke(loc);
                    }
                    else
                    {
                        if (loc.onPlayerExited != null) loc.onPlayerExited.Invoke(loc);
                    }
                }
            }

            Current = best;
        }

        /// <summary>지정한 종류 중 가장 가까운 장소를 찾습니다. (길 안내·의뢰 배치에 씁니다)</summary>
        /// <param name="from">거리를 잴 기준 위치</param>
        /// <param name="kind">찾을 장소의 종류</param>
        /// <returns>가장 가까운 장소. 해당 종류가 하나도 없으면 null입니다.</returns>
        public static WorldLocation FindNearest(Vector3 from, LocationKind kind)
        {
            WorldLocation best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null || all[i].kind != kind) continue;

                float sqr = (all[i].transform.position - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = all[i]; }
            }

            return best;
        }

        /// <summary>플레이어가 마을에 있는지 여부입니다.</summary>
        /// <returns>현재 장소가 있고 그 종류가 마을이면 true를 반환합니다.</returns>
        public static bool IsPlayerInVillage()
        {
            return Current != null && Current.kind == LocationKind.Village;
        }
    }
}
