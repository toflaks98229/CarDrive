using System.Collections.Generic;
using UnityEngine;
using VContainer;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// <b>밤이 되면 세계에 나오는 귀신</b>입니다. 차와 무관하게 돕니다.
    ///
    /// <b>왜 따로 필요한가.</b> <see cref="GhostSpawner"/> 는 차량의 부품이고
    /// <b>시동이 켜져 있을 때만</b> 돕니다. 그래서 차에서 내리는 순간, 또는 시동을 끄고
    /// 서 있는 순간 이 게임에서 <b>위협이 통째로 사라집니다.</b> 밤이 낮보다 무서울
    /// 이유가 헤드라이트 사거리밖에 없었습니다.
    ///
    /// 이쪽은 반대입니다 — <b>시계가 밤이라고 말하면</b> 돕니다. 플레이어가 운전 중이든
    /// 걷고 있든 상관하지 않고, 플레이어 주위의 고리 안에 배회 귀신을 놓습니다.
    ///
    /// <b>둘은 겹치지 않습니다.</b> 달라붙는 귀신은 차의 자식으로 태어나 내구도를 갉고,
    /// 이 귀신은 세계에 서서 <b>걸어옵니다.</b> 앙크로 걷어내는 것과 차로 치거나 도망가는
    /// 것은 다른 대처이므로, 같이 나와도 할 일이 겹치지 않습니다.
    ///
    /// <b>활동량은 이미 있는 것을 씁니다.</b> <see cref="IGhostActivity"/> 는 날씨에 밤
    /// 배율까지 곱한 값이라, 폭우 치는 밤에는 이쪽 간격도 함께 좁아집니다. 여기서 밤을
    /// 다시 곱하지 않습니다 — 밤은 <b>나오느냐 마느냐</b>를 정하고, 배율은 <b>얼마나
    /// 자주</b>를 정합니다.
    ///
    /// <b>보이는 데서 태어나지 않습니다.</b> 눈앞에서 생기면 유령이 아니라 스폰입니다.
    /// 카메라가 보는 반대쪽 부채꼴 안에서, 최소 거리 바깥에 놓습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NightGhostSpawner : MonoBehaviour
    {
        // --- Public Member Variables : 프리팹 ---

        /// <summary>
        /// 꺼내 쓸 귀신 프리팹입니다. <see cref="EnemyController"/> 가 붙어 있어야 합니다.
        /// 여럿이면 그중 하나가 무작위로 나옵니다.
        /// </summary>
        [Header("귀신 프리팹")]
        [Tooltip("배회할 귀신 프리팹들 (EnemyController 필요). 여럿이면 무작위로 하나가 나옵니다.")]
        public List<GameObject> ghostPrefabs = new List<GameObject>();

        // --- Public Member Variables : 타이밍 ---

        /// <summary>스폰 간격의 하한(초)입니다.</summary>
        [Header("스폰 타이밍")]
        [Tooltip("최소 스폰 간격 (초)")]
        public float minSpawnInterval = 14f;

        /// <summary>스폰 간격의 상한(초)입니다.</summary>
        [Tooltip("최대 스폰 간격 (초)")]
        public float maxSpawnInterval = 32f;

        /// <summary>날씨·밤이 반영된 활동량으로 간격을 좁힐지 여부입니다.</summary>
        [Tooltip("체크하면 날씨가 사나울수록 자주 나옵니다. (IGhostActivity)")]
        public bool useWeatherActivity = true;

        /// <summary>활동량 배율의 상한입니다. 너무 높으면 감당할 수 없이 몰려옵니다.</summary>
        [Tooltip("활동량 배율의 상한")]
        [Range(1f, 5f)]
        public float maxActivityMultiplier = 3f;

        // --- Public Member Variables : 자리 ---

        /// <summary>이 거리보다 가까이에는 태어나지 않습니다.</summary>
        [Header("나타나는 자리")]
        [Tooltip("이 거리(m)보다 가까이에는 태어나지 않습니다")]
        public float minSpawnDistance = 30f;

        /// <summary>이 거리보다 멀리에는 태어나지 않습니다.</summary>
        [Tooltip("이 거리(m)보다 멀리에는 태어나지 않습니다")]
        public float maxSpawnDistance = 60f;

        /// <summary>이 거리보다 멀어지면 조용히 거둡니다. 뒤에 줄줄이 남지 않게 합니다.</summary>
        [Tooltip("이 거리(m)를 넘어가면 조용히 거둡니다")]
        public float despawnDistance = 110f;

        /// <summary>
        /// 카메라가 보는 반대쪽 <b>부채꼴</b>의 각도입니다. 이 안에서 자리를 고릅니다.
        /// 360 이면 사방 아무 데나 나옵니다.
        /// </summary>
        [Tooltip("카메라 반대쪽 부채꼴의 각도(°). 360이면 사방 아무 데나 나옵니다.")]
        [Range(30f, 360f)]
        public float behindArc = 200f;

        /// <summary>동시에 살아 있을 수 있는 최대 마릿수입니다.</summary>
        [Tooltip("동시에 살아 있을 수 있는 최대 마릿수")]
        [Range(1, 12)]
        public int maxAlive = 4;

        // --- Public Member Variables : 땅 ---

        /// <summary>바닥으로 인정할 레이어입니다.</summary>
        [Header("땅 찾기")]
        [Tooltip("바닥으로 인정할 레이어")]
        public LayerMask groundMask = ~0;

        /// <summary>바닥을 찾을 때 위로 얼마나 올라가서 내려다볼지입니다.</summary>
        [Tooltip("바닥을 찾을 때 위로 올라가는 거리(m)")]
        public float groundProbeUp = 60f;

        /// <summary>거기서 아래로 얼마나 훑을지입니다.</summary>
        [Tooltip("바닥을 찾을 때 아래로 훑는 거리(m)")]
        public float groundProbeDown = 160f;

        /// <summary>찾은 바닥에서 이만큼 띄워 놓습니다. 땅에 박힌 채로 태어나지 않게 합니다.</summary>
        [Tooltip("찾은 바닥에서 띄워 놓을 높이(m)")]
        public float spawnHeightOffset = 0.6f;

        // --- Public Properties ---

        /// <summary>지금 이 스포너가 내놓아 살아 있는 귀신의 수입니다.</summary>
        public int AliveCount { get { return alive.Count; } }

        /// <summary>지금 밤이라 이 스포너가 도는 중인지 여부입니다.</summary>
        public bool IsActiveNight { get { return clock != null && clock.IsRunning && clock.IsNight; } }

        // --- Private Member Variables : 주입 ---

        /// <summary>밤인지 물어보는 곳입니다. 없으면 아무것도 나오지 않습니다.</summary>
        private IGameClock clock = NullGameClock.Instance;

        /// <summary>날씨·밤이 반영된 활동량입니다. 주입되지 않으면 1이 돌아옵니다.</summary>
        private IGhostActivity activity = NullWeather.Instance;

        // --- Private Member Variables ---

        /// <summary>지금 살아 있는 귀신들입니다. 풀로 돌아간 것은 훑을 때 걸러집니다.</summary>
        private readonly List<EnemyController> alive = new List<EnemyController>();

        /// <summary>다음 스폰까지 남은 시간(초)입니다.</summary>
        private float spawnTimer;

        /// <summary>플레이어가 지금 어디에 있는지 알려 주는 쪽입니다.</summary>
        private PlayerModeController player;

        /// <summary>다음에 플레이어를 다시 찾을 시각입니다.</summary>
        private float nextPlayerSearchTime;

        /// <summary>지난 프레임에 밤이었는지입니다. 날이 밝는 순간을 한 번만 잡습니다.</summary>
        private bool wasNight;

        // --- Constants ---

        /// <summary>플레이어를 다시 찾는 간격(초)입니다.</summary>
        private const float PlayerSearchInterval = 1f;

        /// <summary>자리를 못 찾았을 때 다시 시도할 횟수입니다.</summary>
        private const int PlacementAttempts = 6;

        // --- Injection ---

        /// <summary>시계와 활동량을 받습니다. 둘 다 없어도 조용히 놉니다.</summary>
        /// <param name="gameClock">밤인지 알려 주는 시계</param>
        /// <param name="ghostActivity">날씨·밤이 반영된 활동량</param>
        [Inject]
        public void Construct(IGameClock gameClock, IGhostActivity ghostActivity)
        {
            if (gameClock != null) clock = gameClock;
            if (ghostActivity != null) activity = ghostActivity;
        }

        // --- Unity Event Functions ---

        /// <summary>시계가 있는지 확인하고 첫 타이머를 겁니다.</summary>
        private void Start()
        {
            if (ghostPrefabs == null || ghostPrefabs.Count == 0)
            {
                GameLog.Warn(GameLog.Channel.Enemy,
                    "NightGhostSpawner: 귀신 프리팹이 하나도 없습니다. 밤에 아무것도 나오지 않습니다.", this);
            }

            // 시계가 없으면 밤이 없습니다. 조용히 노는 것보다 한 번 말하는 편이 낫습니다.
            if (!clock.IsRunning)
            {
                GameLog.Warn(GameLog.Channel.Enemy,
                    "NightGhostSpawner: 시계가 돌지 않아 밤을 알 수 없습니다. 아무것도 나오지 않습니다.", this);
            }

            ResetSpawnTimer();
        }

        /// <summary>밤이면 세고, 날이 밝으면 거둡니다.</summary>
        private void Update()
        {
            Prune();

            bool night = IsActiveNight;

            if (!night)
            {
                // 날이 밝는 순간에 한 번만 거둡니다. 매 프레임 훑을 이유가 없습니다.
                if (wasNight) RetireAll();
                wasNight = false;
                return;
            }

            wasNight = true;

            Transform anchor = ResolveAnchor();
            if (anchor == null) return;

            CullFar(anchor);

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f) return;

            if (alive.Count < maxAlive) TrySpawn(anchor);

            ResetSpawnTimer();
        }

        // --- Private Methods ---

        /// <summary>
        /// 풀로 돌아간 귀신을 목록에서 지웁니다.
        ///
        /// 귀신은 앙크에 맞거나 차에 받히면 <b>스스로</b> 풀로 돌아갑니다. 이쪽에 알려 주는
        /// 경로가 없으므로, 꺼져 있는지를 보고 판단합니다. 스폰 전에 반드시 먼저 돌려야
        /// 같은 오브젝트가 재사용되어 목록에 두 번 들어가는 일이 없습니다.
        /// </summary>
        private void Prune()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                EnemyController ghost = alive[i];
                if (ghost == null || !ghost.gameObject.activeInHierarchy) alive.RemoveAt(i);
            }
        }

        /// <summary>거리 기준이 될 플레이어의 몸을 돌려줍니다. 주행이면 차, 도보면 사람입니다.</summary>
        /// <returns>기준 트랜스폼. 플레이어를 못 찾으면 null.</returns>
        private Transform ResolveAnchor()
        {
            if (player == null && Time.time >= nextPlayerSearchTime)
            {
                nextPlayerSearchTime = Time.time + PlayerSearchInterval;
                player = GameContext.Get<PlayerModeController>();
            }

            if (player != null) return player.PickupAnchor;

            // 플레이어가 없는 씬(검사용 씬 등)에서는 카메라라도 기준으로 씁니다.
            // 거리 기준으로 카메라가 불안정한 것은 맞지만, 기준이 아예 없는 것보다는 낫습니다.
            return GameContext.MainCameraTransform;
        }

        /// <summary>다음 스폰까지의 시간을 다시 뽑습니다. 활동량이 높을수록 짧아집니다.</summary>
        private void ResetSpawnTimer()
        {
            float interval = Random.Range(minSpawnInterval, maxSpawnInterval);

            if (useWeatherActivity)
            {
                float multiplier = Mathf.Clamp(activity.Activity, 0.1f, maxActivityMultiplier);
                interval /= multiplier;
            }

            spawnTimer = interval;
        }

        /// <summary>너무 멀어진 귀신을 조용히 거둡니다.</summary>
        /// <param name="anchor">거리를 잴 기준</param>
        private void CullFar(Transform anchor)
        {
            float limit = despawnDistance * despawnDistance;

            for (int i = alive.Count - 1; i >= 0; i--)
            {
                EnemyController ghost = alive[i];
                if (ghost == null) { alive.RemoveAt(i); continue; }

                if ((ghost.transform.position - anchor.position).sqrMagnitude <= limit) continue;

                Retire(ghost);
                alive.RemoveAt(i);
            }
        }

        /// <summary>날이 밝았습니다. 남은 것을 전부 거둡니다.</summary>
        private void RetireAll()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (alive[i] != null) Retire(alive[i]);
            }

            alive.Clear();
        }

        /// <summary>
        /// 죽이지 않고 <b>사라지게</b> 합니다.
        ///
        /// <see cref="EnemyBase.Die"/> 를 부르면 사망음·파티클·재화 드롭이 따라옵니다.
        /// 날이 밝아서 없어지는 것은 죽은 것이 아니므로 그 연출이 붙으면 안 됩니다.
        /// 풀로 직접 돌려보냅니다.
        /// </summary>
        /// <param name="ghost">거둘 귀신</param>
        private void Retire(EnemyController ghost)
        {
            PrefabPool.Release(ghost.gameObject);
        }

        /// <summary>귀신 하나를 플레이어 뒤쪽 고리 안에 놓아 봅니다.</summary>
        /// <param name="anchor">플레이어의 몸</param>
        private void TrySpawn(Transform anchor)
        {
            GameObject prefab = PickPrefab();
            if (prefab == null) return;

            Vector3 spot;
            if (!FindSpot(anchor, out spot)) return;

            GameObject obj = PrefabPool.Get(prefab, spot, Quaternion.identity, null);
            if (obj == null) return;

            EnemyController ghost = obj.GetComponent<EnemyController>();
            if (ghost == null)
            {
                GameLog.Error(GameLog.Channel.Enemy,
                    prefab.name + " 에 EnemyController 가 없습니다. 배회 귀신으로 쓸 수 없습니다.", prefab);
                PrefabPool.Release(obj);
                return;
            }

            alive.Add(ghost);
        }

        /// <summary>프리팹 목록에서 하나를 고릅니다. 비어 있는 칸은 건너뜁니다.</summary>
        /// <returns>고른 프리팹. 쓸 것이 없으면 null.</returns>
        private GameObject PickPrefab()
        {
            if (ghostPrefabs == null || ghostPrefabs.Count == 0) return null;

            // 비어 있는 칸이 섞여 있어도 몇 번 더 뽑아 보고 포기합니다.
            for (int i = 0; i < 4; i++)
            {
                GameObject candidate = ghostPrefabs[Random.Range(0, ghostPrefabs.Count)];
                if (candidate != null) return candidate;
            }

            return null;
        }

        /// <summary>
        /// 카메라 반대쪽 부채꼴 안에서 땅이 있는 자리를 찾습니다.
        ///
        /// 방향의 기준은 <b>카메라</b>입니다. 차의 앞이 아니라 <b>보고 있는 쪽</b>의 반대에
        /// 놓아야 눈앞에서 생기지 않습니다 — 후진 중이거나 두리번거릴 때 차의 앞과
        /// 시선이 갈라집니다.
        /// </summary>
        /// <param name="anchor">플레이어의 몸</param>
        /// <param name="spot">찾은 자리</param>
        /// <returns>찾았으면 true</returns>
        private bool FindSpot(Transform anchor, out Vector3 spot)
        {
            spot = Vector3.zero;

            Transform eye = GameContext.MainCameraTransform;
            Vector3 look = eye != null ? eye.forward : anchor.forward;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f) look = Vector3.forward;
            look.Normalize();

            float baseAngle = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg + 180f;
            float half = behindArc * 0.5f;

            for (int i = 0; i < PlacementAttempts; i++)
            {
                float angle = baseAngle + Random.Range(-half, half);
                float distance = Random.Range(minSpawnDistance, maxSpawnDistance);

                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 around = anchor.position + direction * distance;

                Vector3 point, normal;
                if (!GroundProbe.Sample(around, groundProbeUp, groundProbeDown, groundMask,
                                        out point, out normal))
                {
                    continue;
                }

                spot = point + Vector3.up * spawnHeightOffset;
                return true;
            }

            // 여섯 번 다 실패하면 이번 차례는 거릅니다. 억지로 허공에 놓지 않습니다.
            return false;
        }

        // --- Gizmos ---

#if UNITY_EDITOR
        /// <summary>고른 오브젝트에 스폰 고리와 거두는 거리를 그립니다.</summary>
        private void OnDrawGizmosSelected()
        {
            Transform anchor = Application.isPlaying && player != null ? player.PickupAnchor : transform;
            if (anchor == null) return;

            Vector3 center = anchor.position;

            Gizmos.color = new Color(0.5f, 0.4f, 0.9f, 0.8f);
            DrawRing(center, minSpawnDistance);
            DrawRing(center, maxSpawnDistance);

            Gizmos.color = new Color(0.9f, 0.5f, 0.35f, 0.5f);
            DrawRing(center, despawnDistance);
        }

        /// <summary>수평 고리 하나를 그립니다.</summary>
        /// <param name="center">고리의 중심</param>
        /// <param name="radius">고리의 반지름</param>
        private static void DrawRing(Vector3 center, float radius)
        {
            const int Segments = 48;
            Vector3 previous = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= Segments; i++)
            {
                float t = i / (float)Segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
