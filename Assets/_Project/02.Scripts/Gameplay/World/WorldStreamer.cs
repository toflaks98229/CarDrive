using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 마을에서 뻗어 나가는 길 하나입니다. 타일을 이 방향으로 이어 붙입니다.
    /// </summary>
    [System.Serializable]
    public class WorldRoute
    {
        /// <summary>길 이름입니다. 타일 이름과 디버그 표시에 쓰입니다.</summary>
        [Tooltip("길 이름 (디버그·안내용)")]
        public string displayName = "길";

        /// <summary>마을 중심을 기준으로 이 길이 시작되는 위치입니다.</summary>
        [Tooltip("마을 기준 시작 위치")]
        public Vector3 startOffset = Vector3.zero;

        /// <summary>
        /// 길이 뻗어 나갈 방향입니다. 정규화해서 씁니다.
        /// Unity Terrain은 회전을 무시하므로 축에 맞춘 방향(+Z, -Z, +X, -X)만 쓰세요.
        /// </summary>
        [Tooltip("뻗어 나갈 방향. 정규화해서 씁니다. " +
                 "Unity Terrain은 회전을 무시하므로 축에 맞춘 방향(+Z, -Z, +X, -X)만 쓰세요.")]
        public Vector3 direction = Vector3.forward;

        /// <summary>이 길에 이어 붙일 타일 수입니다.</summary>
        [Tooltip("이 길에 깔 타일 수")]
        [Range(1, 40)]
        public int tileCount = 5;

        /// <summary>길 끝에 만들 장소의 이름입니다. 비워두면 장소를 만들지 않습니다.</summary>
        [Tooltip("길 끝에 만들 장소의 이름. 비워두면 장소를 만들지 않습니다.")]
        public string endPlaceName = "";
    }

    /// <summary>
    /// 고정 월드를 깔고 거리 기반으로 켜고 끕니다.
    ///
    /// 이전 RoadManager는 플레이어 앞에 타일을 이어 붙이고 뒤를 Destroy했습니다.
    /// 그 구조에서는 <b>돌아갈 마을이 존재할 수 없습니다.</b> 이 컴포넌트는 대신
    ///  - 시작할 때 정해진 배치를 한 번만 깔고
    ///  - 무엇도 파괴하지 않으며
    ///  - 멀리 있는 타일만 비활성화해 성능을 관리합니다.
    ///
    /// 배치는 시드로 고정되므로 실행할 때마다 같은 세계가 나옵니다.
    /// 세이브를 붙일 때 지형을 저장할 필요가 없다는 뜻이기도 합니다.
    /// </summary>
    public class WorldStreamer : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>길에 깔 지형 타일 프리팹 목록입니다. 매번 이 중에서 무작위로 하나를 고릅니다.</summary>
        [Header("타일")]
        [Tooltip("길에 깔 지형 타일 프리팹")]
        public List<GameObject> tilePrefabs = new List<GameObject>();

        /// <summary>타일 안에서 다음 타일이 이어질 지점의 오브젝트 이름입니다.</summary>
        [Tooltip("타일 안에서 다음 타일이 이어질 지점의 이름")]
        public string nextPointName = "NextSpawnPoint";

        /// <summary>다음 지점을 찾지 못했을 때 쓸 타일 한 변의 길이입니다.</summary>
        [Tooltip("다음 지점을 못 찾았을 때 쓸 타일 한 변의 길이")]
        public float fallbackTileSize = 100f;

        /// <summary>마을(중심)이 될 위치입니다. 비워두면 이 오브젝트의 위치를 씁니다.</summary>
        [Header("배치")]
        [Tooltip("마을(중심)이 될 위치. 비워두면 이 오브젝트의 위치를 씁니다.")]
        public Transform origin;

        /// <summary>중심에 만들 마을 장소의 이름입니다.</summary>
        [Tooltip("마을 장소의 이름")]
        public string villageName = "마을";

        /// <summary>마을 장소의 판정 반경입니다.</summary>
        [Tooltip("마을 반경")]
        public float villageRadius = 60f;

        /// <summary>마을에서 뻗어 나가는 길 목록입니다.</summary>
        [Tooltip("마을에서 뻗어 나가는 길들")]
        public List<WorldRoute> routes = new List<WorldRoute>();

        /// <summary>한 번 확인할 때 새로 켤 수 있는 타일의 최대 수입니다.</summary>
        [Tooltip("한 번 확인할 때 새로 켤 수 있는 타일의 최대 수. " +
                 "타일을 켜는 일은 지형이 풀 조각을 다시 짜는 비용이 있어, 여러 장이 한꺼번에 몰리면 " +
                 "화면이 끊깁니다. 나눠서 켜면 그 끊김이 흩어집니다. 끄는 것은 즉시 합니다.")]
        [Range(1, 8)]
        public int maxTileActivationsPerCheck = 1;

        /// <summary>
        /// 한 번의 검사에서 켤 수 있는 <b>총</b> 타일 수입니다. 안쪽(instantDistance) 타일도 지킵니다.
        ///
        /// <see cref="maxTileActivationsPerCheck"/>는 <b>아직 안 보이는</b> 타일에만 걸리는 예산이라,
        /// 안쪽 타일이 한꺼번에 들어오는 순간(시작 직후·불러오기 직후)에는 아무것도 막지 못했습니다.
        /// 이 값이 그 천장입니다. 넘친 것은 <b>다음 프레임에 곧바로</b> 이어서 켜므로
        /// 빈 곳이 오래 남지 않습니다.
        /// </summary>
        [Tooltip("한 번의 검사에서 켤 수 있는 총 타일 수. 안쪽 타일도 이 천장을 지킵니다. " +
                 "넘친 것은 다음 프레임에 이어서 켜므로 빈 곳이 오래 남지 않습니다.")]
        [Range(1, 16)]
        public int maxInstantActivationsPerCheck = 4;

        /// <summary>배치 무작위 시드입니다. 같은 값이면 항상 같은 세계가 깔립니다.</summary>
        [Tooltip("배치 무작위 시드. 같은 값이면 항상 같은 세계가 깔립니다.")]
        public int layoutSeed = 20260817;

        /// <summary>
        /// 에디터 도구(WorldTerrainBaker)로 미리 구운 타일들의 부모입니다.
        ///
        /// 지정하면 타일을 <b>새로 만들지 않고</b> 이 아래에 이미 있는 것을 그대로 씁니다.
        /// 터레인은 프리팹을 복제해서 쓸 수 없습니다. 복제본이 모두 같은 TerrainData를
        /// 가리켜 지형이 그대로 반복되고, 회전도 되지 않아 길 방향을 맞출 수 없기 때문입니다.
        /// 그래서 터레인 월드는 미리 구워 두고 여기서는 켜고 끄기만 합니다.
        /// </summary>
        [Header("미리 구운 월드")]
        [Tooltip("에디터 도구로 미리 구운 타일들의 부모. 지정하면 타일을 새로 만들지 않고 " +
                 "이 아래에 있는 것을 그대로 씁니다. (CarDrive > World > 터레인 월드 굽기)")]
        public Transform bakedRoot;

        /// <summary>거리 판정의 기준이 될 대상입니다. 비워두면 메인 카메라를 씁니다.</summary>
        [Header("스트리밍")]
        [Tooltip("따라다닐 대상. 비워두면 메인 카메라를 씁니다.")]
        public Transform followTarget;

        /// <summary>
        /// 이 거리 안의 타일만 켭니다. 타일 크기의 2배 이상으로 두세요.
        /// 너무 작으면 달리는 중에 발밑 지형이 꺼집니다.
        /// </summary>
        [Tooltip("이 거리 안의 타일만 켭니다. 타일 크기의 2배 이상으로 두세요. " +
                 "너무 작으면 달리는 중에 발밑 지형이 꺼집니다.")]
        public float activeDistance = 360f;

        /// <summary>
        /// 이 거리 안쪽의 타일은 <b>예산을 무시하고 즉시</b> 켭니다.
        ///
        /// 켜는 것을 나눠서 하는 이유는 비용을 흩기 위해서인데, 그 미룸이 허용되는 것은
        /// <b>아직 보이지 않는 타일</b>일 때뿐입니다. 그보다 가까운 타일이 꺼져 있으면
        /// 그 자리에 빈 곳이 그대로 보입니다.
        ///
        /// <b>안개가 어디서 끝나는지는 씬 설정이 아니라 날씨가 정합니다.</b>
        /// (WeatherRig 가 매 프레임 RenderSettings.fog 를 다시 씁니다)
        /// 그 경계를 가리는 일은 ViewRangeScaler 가 맡습니다.
        ///
        /// 그래서 이 안쪽은 미루지 않습니다. 한 프레임 무거워지는 편이,
        /// 눈앞에서 땅과 나무가 튀어나오는 것보다 낫습니다.
        /// </summary>
        [Tooltip("이 거리 안쪽 타일은 예산을 무시하고 즉시 켭니다. 안개가 끝나는 거리로 두세요.")]
        public float instantDistance = 340f;

        /// <summary>거리 검사 주기(초)입니다. 매 프레임 할 필요가 없습니다.</summary>
        [Tooltip("거리 검사 주기(초). 매 프레임 할 필요가 없습니다.")]
        public float checkInterval = 0.25f;

        /// <summary>체크를 해제하면 모든 타일을 항상 켜 둡니다. (디버그용)</summary>
        [Tooltip("체크를 해제하면 모든 타일을 항상 켜 둡니다. (디버그용)")]
        public bool streamingEnabled = true;

        // --- Public Properties ---

        /// <summary>깔린 타일 수입니다.</summary>
        public int TileCount { get { return tiles.Count; } }

        /// <summary>지금 켜져 있는 타일 수입니다.</summary>
        public int ActiveTileCount { get; private set; }

        // --- Private Member Variables ---

        /// <summary>깔아 둔 모든 타일입니다. 파괴하지 않고 활성 상태만 바꿉니다.</summary>
        private readonly List<GameObject> tiles = new List<GameObject>();

        /// <summary>
        /// 이번 검사에서 켜야 할 타일들입니다. 거리와 함께 담아 <b>가까운 것부터</b> 켭니다.
        ///
        /// 매번 새로 만들지 않고 비워서 다시 씁니다. 0.25초마다 도는 코드라
        /// 여기서 할당하면 쓰레기가 꾸준히 쌓입니다.
        /// </summary>
        private readonly List<PendingTile> pending = new List<PendingTile>();

        /// <summary>
        /// 가까운 순서로 세우는 비교자입니다.
        ///
        /// 메서드 이름을 그대로 Sort 에 넘기면 <b>호출할 때마다 델리게이트가 새로 생깁니다.</b>
        /// 0.25초마다 도는 코드라 그 쓰레기가 꾸준히 쌓입니다. 한 번 만들어 두고 씁니다.
        /// </summary>
        private static readonly System.Comparison<PendingTile> ByDistance = CompareByDistance;

        /// <summary>타일들을 담아 두는 부모 트랜스폼입니다. 하이어라키가 어지러워지지 않게 묶어 둡니다.</summary>
        private Transform tileRoot;

        /// <summary>다음 거리 검사까지 남은 시간(초)입니다.</summary>
        private float checkTimer;

        /// <summary>
        /// 지난 검사에서 천장에 걸려 켜지 못한 타일이 남았는지입니다.
        /// 남았으면 0.25초를 기다리지 않고 다음 프레임에 이어서 켭니다.
        /// </summary>
        private bool backlog;

        // --- Unity Event Functions ---

        /// <summary>
        /// 배치에 필요한 설정을 확인한 뒤 월드를 한 번 깔고 첫 스트리밍을 적용합니다.
        /// 타일 프리팹이 없으면 경고를 남기고 이 컴포넌트를 끕니다.
        /// </summary>
        void Start()
        {
            // 미리 구운 월드를 쓸 때는 타일을 만들지 않으므로 프리팹이 필요 없습니다.
            if (bakedRoot == null && (tilePrefabs == null || tilePrefabs.Count == 0))
            {
                GameLog.Error(GameLog.Channel.World, "WorldStreamer: 타일 프리팹이 없어 월드를 깔 수 없습니다. " +
                               "미리 구운 월드를 쓰려면 bakedRoot를 연결하세요.", this);
                enabled = false;
                return;
            }

            if (routes == null || routes.Count == 0)
            {
                GameLog.Warn(GameLog.Channel.World, "WorldStreamer: 길이 하나도 없습니다. 마을만 만들어집니다.", this);
            }

            // 이 두 수치가 시야 사다리의 기준입니다. 계산은 ViewDistances 가 합니다.
            Systems.ViewDistances.SetTileBases(activeDistance, instantDistance);

            BuildWorld();
            UpdateStreaming(true);
        }

        /// <summary>
        /// checkInterval 주기마다 타일의 활성 범위를 다시 계산합니다.
        /// </summary>
        void Update()
        {
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f) return;

            // 켤 것이 남아 있으면 주기를 기다리지 않고 <b>다음 프레임에</b> 이어서 합니다.
            //
            // 예전에는 안쪽 타일을 한 프레임에 전부 켜서 그 프레임이 통째로 늘어졌습니다.
            // 이제는 천장(maxInstantActivationsPerCheck)만큼만 켜는 대신, 남으면 곧바로 다시 옵니다.
            // 0.25초마다 두 장이 아니라 <b>매 프레임 네 장</b>이 되므로 메워지는 속도는 오히려 빠릅니다.
            checkTimer = backlog ? 0f : Mathf.Max(0.02f, checkInterval);
            UpdateStreaming(false);
        }

        /// <summary>
        /// 씬 뷰에서 마을 반경과 각 길이 뻗어 나갈 경로를 미리 보여 줍니다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 center = origin != null ? origin.position : transform.position;

            Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(center, villageRadius);

            if (routes == null) return;

            Gizmos.color = new Color(0.9f, 0.8f, 0.3f, 0.8f);
            for (int i = 0; i < routes.Count; i++)
            {
                WorldRoute r = routes[i];
                Vector3 dir = r.direction.sqrMagnitude > 0.0001f ? r.direction.normalized : Vector3.forward;
                Vector3 start = center + r.startOffset;
                Vector3 end = start + dir * (fallbackTileSize * r.tileCount);

                Gizmos.DrawLine(start, end);
                Gizmos.DrawWireCube(end, Vector3.one * 10f);
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// 월드를 다시 깝니다. (편집 중 배치를 바꿔 보고 싶을 때)
        /// </summary>
        [ContextMenu("월드 다시 깔기")]
        public void Rebuild()
        {
            ClearWorld();
            BuildWorld();

            // 지형이 통째로 바뀌었습니다. 주기를 기다리면 그 사이 <b>사라진 지형</b>을
            // 가리키거나 <b>새 지형을 빠뜨립니다.</b> 목록을 쓰는 쪽 모두에게 영향을 줍니다.
            Systems.TerrainRegistry.Invalidate();

            UpdateStreaming(true);
        }

        // --- Private Methods : 배치 ---

        /// <summary>
        /// 마을과 모든 길을 한 번에 깝니다.
        /// 배치 동안에는 난수 시드를 layoutSeed로 고정했다가 원래 상태로 되돌립니다.
        /// </summary>
        private void BuildWorld()
        {
            Vector3 center = origin != null ? origin.position : transform.position;

            // 시드를 고정해 매번 같은 세계가 나오게 합니다.
            Random.State savedState = Random.state;
            Random.InitState(layoutSeed);

            CreatePlace(villageName, LocationKind.Village, center, villageRadius);

            if (bakedRoot != null)
            {
                // 미리 구운 터레인을 그대로 씁니다. 타일은 만들지 않고 장소만 세웁니다.
                AdoptBakedTiles();

                for (int r = 0; r < routes.Count; r++)
                {
                    CreateRouteEndPlace(center, routes[r]);
                }
            }
            else
            {
                tileRoot = new GameObject("WorldTiles").transform;
                tileRoot.SetParent(transform, false);
                tileRoot.position = Vector3.zero;

                for (int r = 0; r < routes.Count; r++)
                {
                    BuildRoute(center, routes[r]);
                }
            }

            Random.state = savedState;

            GameLog.Info(GameLog.Channel.World, "WorldStreamer: 타일 " + tiles.Count + "개, 장소 " + WorldLocation.All.Count + "곳을 깔았습니다.");
        }

        /// <summary>
        /// 미리 구워 둔 타일들을 스트리밍 목록에 담습니다. 무엇도 새로 만들지 않습니다.
        /// </summary>
        private void AdoptBakedTiles()
        {
            tileRoot = bakedRoot;

            for (int i = 0; i < bakedRoot.childCount; i++)
            {
                Transform child = bakedRoot.GetChild(i);
                if (child != null) tiles.Add(child.gameObject);
            }

            if (tiles.Count == 0)
            {
                GameLog.Warn(GameLog.Channel.World, "WorldStreamer: bakedRoot 아래에 타일이 없습니다. " +
                                 "CarDrive > World > 터레인 월드 굽기 를 먼저 실행하세요.", this);
            }
        }

        /// <summary>
        /// 길 끝의 장소만 만듭니다. 타일은 이미 구워져 있으므로 깔지 않습니다.
        /// </summary>
        /// <param name="center">마을 중심 위치</param>
        /// <param name="route">장소를 만들 길</param>
        private void CreateRouteEndPlace(Vector3 center, WorldRoute route)
        {
            if (string.IsNullOrEmpty(route.endPlaceName)) return;

            Vector3 dir = route.direction.sqrMagnitude > 0.0001f ? route.direction.normalized : Vector3.forward;
            Vector3 end = center + route.startOffset + dir * (fallbackTileSize * route.tileCount);

            CreatePlace(route.endPlaceName, LocationKind.Site, end - dir * (fallbackTileSize * 0.5f), 45f);
        }

        /// <summary>
        /// 길 하나를 깔고, 끝에 장소를 만듭니다.
        /// </summary>
        /// <param name="center">마을 중심 위치. 길의 시작 오프셋이 여기에 더해집니다.</param>
        /// <param name="route">깔아야 할 길의 설정</param>
        private void BuildRoute(Vector3 center, WorldRoute route)
        {
            Vector3 dir = route.direction.sqrMagnitude > 0.0001f ? route.direction.normalized : Vector3.forward;

            Vector3 cursor = center + route.startOffset;

            for (int i = 0; i < route.tileCount; i++)
            {
                GameObject prefab = tilePrefabs[Random.Range(0, tilePrefabs.Count)];

                // 타일은 절대 회전시키지 않습니다.
                // Unity Terrain과 TerrainCollider는 트랜스폼 회전을 무시하기 때문에,
                // 회전을 주면 보이는 지형과 실제 충돌 지형이 어긋납니다.
                // 타일이 정사각형이라 위치만 옮겨도 어느 방향으로든 이어 붙습니다.
                GameObject tile = Instantiate(prefab, cursor, prefab.transform.rotation, tileRoot);
                tile.name = route.displayName + "_Tile_" + i;
                tiles.Add(tile);

                cursor += dir * GetTileLength(tile);
            }

            if (!string.IsNullOrEmpty(route.endPlaceName))
            {
                // 마지막 타일 한 칸 앞을 현장 중심으로 잡습니다.
                CreatePlace(route.endPlaceName, LocationKind.Site, cursor - dir * (GetStepFallback() * 0.5f), 45f);
            }
        }

        /// <summary>
        /// 타일 안의 다음 지점까지 거리를 재서 타일 길이를 구합니다.
        /// 지점이 없으면 설정된 기본값을 씁니다.
        /// </summary>
        /// <param name="tile">길이를 잴 타일 인스턴스</param>
        /// <returns>다음 타일까지 커서를 옮길 거리</returns>
        private float GetTileLength(GameObject tile)
        {
            Transform next = FindDeep(tile.transform, nextPointName);
            if (next == null) return fallbackTileSize;

            float length = Vector3.Distance(tile.transform.position, next.position);
            return length > 0.01f ? length : fallbackTileSize;
        }

        /// <summary>
        /// 타일 길이를 알 수 없는 자리에서 쓸 기본 보폭을 돌려줍니다.
        /// </summary>
        /// <returns>설정된 기본 타일 크기</returns>
        private float GetStepFallback()
        {
            return fallbackTileSize;
        }

        /// <summary>
        /// 이름을 가진 장소를 이 오브젝트의 자식으로 만듭니다.
        /// </summary>
        /// <param name="displayName">장소 이름. 비어 있으면 아무것도 만들지 않습니다.</param>
        /// <param name="kind">장소의 종류 (마을·현장·지형지물)</param>
        /// <param name="position">장소의 월드 좌표</param>
        /// <param name="radius">장소의 판정 반경</param>
        private void CreatePlace(string displayName, LocationKind kind, Vector3 position, float radius)
        {
            if (string.IsNullOrEmpty(displayName)) return;

            GameObject go = new GameObject("Place_" + displayName);
            go.transform.SetParent(transform, false);
            go.transform.position = position;

            WorldLocation loc = go.AddComponent<WorldLocation>();
            loc.displayName = displayName;
            loc.kind = kind;
            loc.radius = radius;
        }

        /// <summary>
        /// 깔아 둔 타일과 장소를 모두 지웁니다. 다시 깔기 전에만 호출합니다.
        /// </summary>
        private void ClearWorld()
        {
            // 미리 구운 월드는 에디터에서 만든 씬 오브젝트입니다. 여기서 파괴하면 안 됩니다.
            // 목록에서 놓기만 하면 AdoptBakedTiles가 다시 담습니다.
            if (bakedRoot != null)
            {
                tiles.Clear();
                tileRoot = null;
            }
            else
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    if (tiles[i] != null) DestroyImmediate(tiles[i]);
                }
                tiles.Clear();

                if (tileRoot != null) DestroyImmediate(tileRoot.gameObject);
            }

            // 장소도 함께 정리합니다.
            WorldLocation[] places = GetComponentsInChildren<WorldLocation>(true);
            for (int i = 0; i < places.Length; i++)
            {
                if (places[i] != null) DestroyImmediate(places[i].gameObject);
            }
        }

        // --- Private Methods : 스트리밍 ---

        /// <summary>
        /// 멀리 있는 타일을 끄고 가까운 것만 켭니다. 무엇도 파괴하지 않습니다.
        /// </summary>
        /// <param name="force">true면 활성 상태가 같더라도 SetActive를 다시 호출합니다. (첫 적용·재배치용)</param>
        private void UpdateStreaming(bool force)
        {
            if (followTarget == null)
            {
                followTarget = GameContext.MainCameraTransform;
                if (followTarget == null) return;
            }

            Vector3 p = followTarget.position;

            // 장소 판정(WorldLocation.UpdateCurrent)은 여기서 하지 않습니다.
            //
            // 예전에는 이 줄에 있었습니다. 그래서 위의 "따라다닐 대상을 못 찾으면 반환"이
            // <b>장소 판정까지 데리고 나갔고</b>, 기준도 카메라라 타고 내릴 때마다 흔들렸습니다.
            // 무엇보다 앞으로의 의뢰·상점·대화가 전부 지형 스트리밍의 부산물에 매달리게 됩니다.
            // 이제 WorldLocationTracker 가 자기 주기로 돕니다. 여기에 다시 넣지 마세요 —
            // 양쪽에서 돌면 진입·이탈 이벤트가 두 번씩 터집니다.

            if (!streamingEnabled)
            {
                SetAllActive(true);
                return;
            }

            // 거리는 ViewDistances 가 한곳에서 계산합니다. 여기서 배율을 곱하지 않습니다.
            // 인스펙터의 기준 수치(activeDistance · instantDistance)는 Start 에서 등록해 두었습니다.
            Systems.ViewDistances.Ladder ladder = Systems.ViewDistances.Current;

            // <b>켜는 거리와 끄는 거리가 다릅니다.</b>
            //
            // 예전에는 이 둘이 같은 값 하나였습니다. 그래서 경계에 걸친 타일은 플레이어가
            // 그 선을 오갈 때마다 <c>SetActive</c> 를 반복했습니다. 길을 따라 왕복하거나
            // 경계 근처에서 방향을 바꾸면 <b>같은 타일이 몇 초 간격으로 계속 껐다 켜졌습니다.</b>
            //
            // 그 토글이 이 프로젝트에서 가장 비싼 일입니다. 지형이 렌더링 시스템에서 빠졌다가
            // 다시 등록되고 렌더 데이터가 통째로 재구성됩니다. 예산제(아래)는 <b>여러 장이
            // 한꺼번에 몰리는 것</b>은 막아 주지만, <b>같은 한 장이 반복되는 것</b>은 막지 못합니다.
            // 그건 애초에 토글이 일어나지 않게 해야 합니다.
            //
            // <see cref="TerrainChunkCuller"/> 는 훨씬 싼 토글에 이미 이 장치를 쓰고 있었습니다.
            // 비싼 쪽에만 빠져 있었습니다.
            float sqrActivate = ladder.TerrainActive * ladder.TerrainActive;
            float sqrRelease = ladder.TerrainActiveRelease * ladder.TerrainActiveRelease;
            int active = 0;

            // <b>한 번에 켜는 타일 수를 제한합니다.</b>
            //
            // 타일을 켜는 일은 싸지 않습니다. 지형이 풀 조각을 다시 짜고 콜라이더를 준비하는데,
            // 빠르게 달리면 여러 장이 같은 순간에 범위로 들어와 그 비용이 한꺼번에 몰립니다.
            // 그때 화면이 끊기고, 끊긴 프레임은 조작감까지 흔듭니다.
            //
            // 끄는 것은 싸므로 미루지 않습니다. <b>켜는 것만</b> 나눠서 합니다.
            // 늦게 켜지는 타일은 활성 거리 안쪽이라 아직 화면 가장자리에도 오지 않았습니다.
            int budget = force ? int.MaxValue : Mathf.Max(1, maxTileActivationsPerCheck);

            // 1단계 — 끌 것은 바로 끄고, 켤 것은 거리와 함께 모아 둡니다.
            pending.Clear();

            for (int i = 0; i < tiles.Count; i++)
            {
                GameObject tile = tiles[i];
                if (tile == null) continue;

                float sqr = SqrDistanceToTile(tile, p);

                // 이미 켜져 있으면 <b>더 멀리</b> 가야 꺼집니다. 꺼져 있으면 원래 거리에서 켜집니다.
                // 이 한 줄이 경계에서 같은 타일이 반복 토글되는 것을 막습니다.
                bool wasActive = tile.activeSelf;
                bool shouldBeActive = wasActive ? sqr <= sqrRelease : sqr <= sqrActivate;

                if (force)
                {
                    // 첫 적용에는 히스테리시스를 쓰지 않습니다. 아직 "지난 상태"가 없으므로
                    // 켜는 거리 하나로 판정해야 시작 시점의 켜진 범위가 설정 그대로가 됩니다.
                    shouldBeActive = sqr <= sqrActivate;
                    tile.SetActive(shouldBeActive);
                }
                else if (tile.activeSelf != shouldBeActive)
                {
                    if (!shouldBeActive)
                    {
                        tile.SetActive(false);
                        Systems.WorldProfiler.Count(Systems.WorldProfiler.Counter.TileDeactivated);
                    }
                    else pending.Add(new PendingTile(tile, sqr));
                }

                if (tile.activeSelf) active++;
            }

            // 2단계 — <b>가까운 것부터</b> 켭니다.
            //
            // 예전에는 목록에 담긴 순서대로 켰습니다. 그 순서는 구울 때 정해진 것이라
            // 거리와 아무 상관이 없고, 한 번에 한 장만 켜다 보니 <b>같은 타일이 계속 밀렸습니다.</b>
            // 순서가 고정이라 늘 같은 타일이 뒤로 밀려, 결국 눈앞에 와서야 켜졌습니다.
            pending.Sort(ByDistance);

            float instantSqr = ladder.TerrainInstant * ladder.TerrainInstant;

            // <b>한 번의 검사에서 켜는 총량에 천장을 둡니다.</b>
            //
            // 예전에는 안쪽(instantDistance) 타일이 예산을 무시하고 <b>전부</b> 켜졌습니다.
            // 평소 주행에서는 문제가 없습니다. 한 번에 한두 장씩만 들어오기 때문입니다.
            // 그런데 여럿이 동시에 들어오는 순간이 있습니다 — 시작 직후, 세이브 불러오기 직후,
            // 스트리밍을 껐다 켠 직후. 이 월드는 400m 안에 타일이 <b>52장</b>이고
            // 그중 44장이 340m 안이라, 그때 한 프레임에 44장을 켜게 됩니다.
            // 프로파일러에 남은 436ms · 1708ms 프레임이 그 모양입니다.
            //
            // 그래서 안쪽도 천장을 지키게 하고, 대신 <b>남으면 다음 프레임에 곧바로</b>
            // 이어서 켭니다. (아래 backlog 와 Update 를 보세요)
            // 0.25초를 기다리며 두 장씩 켜던 것이 매 프레임 두 장씩으로 바뀌므로,
            // 빈 곳이 메워지는 속도는 오히려 <b>빨라집니다.</b>
            int ceiling = Mathf.Max(budget, Mathf.Max(1, maxInstantActivationsPerCheck));
            int opened = 0;

            for (int i = 0; i < pending.Count; i++)
            {
                bool visibleNow = pending[i].sqrDistance <= instantSqr;

                // 예산이 남았거나, 이미 보이는 거리면 켭니다. 다만 천장은 둘 다 지킵니다.
                if (!visibleNow && i >= budget) break;
                if (opened >= ceiling) break;

                pending[i].tile.SetActive(true);
                Systems.WorldProfiler.Count(Systems.WorldProfiler.Counter.TileActivated);
                opened++;
                active++;
            }

            // 아직 켤 것이 남았으면 다음 프레임에 이어서 합니다.
            backlog = opened < pending.Count && opened >= ceiling;

            ActiveTileCount = active;
        }

        /// <summary>켜기를 기다리는 타일 하나입니다.</summary>
        private readonly struct PendingTile
        {
            /// <summary>켤 타일</summary>
            public readonly GameObject tile;

            /// <summary>기준 위치까지의 거리 제곱</summary>
            public readonly float sqrDistance;

            /// <summary>대기 항목 하나를 만듭니다.</summary>
            /// <param name="tile">켤 타일</param>
            /// <param name="sqrDistance">거리의 제곱</param>
            public PendingTile(GameObject tile, float sqrDistance)
            {
                this.tile = tile;
                this.sqrDistance = sqrDistance;
            }
        }

        /// <summary>가까운 것이 앞에 오도록 비교합니다.</summary>
        /// <param name="a">왼쪽</param>
        /// <param name="b">오른쪽</param>
        /// <returns>정렬 순서</returns>
        private static int CompareByDistance(PendingTile a, PendingTile b)
        {
            return a.sqrDistance.CompareTo(b.sqrDistance);
        }

        /// <summary>
        /// 타일에서 <b>가장 가까운 점</b>까지의 거리 제곱입니다. 높이는 무시합니다.
        ///
        /// 예전에는 타일의 원점 모서리까지 쟀습니다. 그런데 타일은 그 모서리에서
        /// 한 변만큼 더 뻗어 있어서, 활성 거리가 320m 여도 가까운 쪽 끝은 220m 에서
        /// 나타났습니다. 그 거리의 안개는 절반뿐이라 <b>땅이 눈앞에서 튀어나왔습니다.</b>
        ///
        /// 가장 가까운 점까지 재면 활성 거리가 곧 "이 거리 안에는 빈 곳이 없다"는 약속이
        /// 됩니다. 그 거리를 안개가 끝나는 거리보다 멀리 두면 켜지는 순간이 보이지 않습니다.
        /// </summary>
        /// <param name="tile">확인할 타일</param>
        /// <param name="point">기준 위치</param>
        /// <returns>거리의 제곱</returns>
        private float SqrDistanceToTile(GameObject tile, Vector3 point)
        {
            Vector3 origin = tile.transform.position;

            // 타일은 원점에서 한 변만큼 +X, +Z 로 뻗은 정사각형입니다.
            float size = fallbackTileSize;

            float dx = Mathf.Max(0f, Mathf.Max(origin.x - point.x, point.x - (origin.x + size)));
            float dz = Mathf.Max(0f, Mathf.Max(origin.z - point.z, point.z - (origin.z + size)));

            return dx * dx + dz * dz;
        }

        /// <summary>
        /// 모든 타일의 활성 상태를 한 번에 맞춥니다. (스트리밍을 껐을 때 씁니다)
        /// </summary>
        /// <param name="active">모든 타일을 켤지 끌지 여부</param>
        private void SetAllActive(bool active)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] != null && tiles[i].activeSelf != active) tiles[i].SetActive(active);
            }
            ActiveTileCount = active ? tiles.Count : 0;
        }

        /// <summary>
        /// 자식 계층을 깊이 우선으로 훑어 이름이 일치하는 트랜스폼을 찾습니다.
        /// </summary>
        /// <param name="parent">탐색을 시작할 트랜스폼. 자기 자신도 검사 대상입니다.</param>
        /// <param name="name">찾을 오브젝트 이름</param>
        /// <returns>찾은 트랜스폼. 없으면 null입니다.</returns>
        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform f = FindDeep(parent.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
