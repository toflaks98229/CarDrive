using System.Collections.Generic;
using UnityEngine;

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
    public float activeDistance = 320f;

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

    /// <summary>타일들을 담아 두는 부모 트랜스폼입니다. 하이어라키가 어지러워지지 않게 묶어 둡니다.</summary>
    private Transform tileRoot;

    /// <summary>다음 거리 검사까지 남은 시간(초)입니다.</summary>
    private float checkTimer;

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
            Debug.LogError("WorldStreamer: 타일 프리팹이 없어 월드를 깔 수 없습니다. " +
                           "미리 구운 월드를 쓰려면 bakedRoot를 연결하세요.", this);
            enabled = false;
            return;
        }

        if (routes == null || routes.Count == 0)
        {
            Debug.LogWarning("WorldStreamer: 길이 하나도 없습니다. 마을만 만들어집니다.", this);
        }

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

        checkTimer = Mathf.Max(0.02f, checkInterval);
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

        Debug.Log("WorldStreamer: 타일 " + tiles.Count + "개, 장소 " + WorldLocation.All.Count + "곳을 깔았습니다.");
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
            Debug.LogWarning("WorldStreamer: bakedRoot 아래에 타일이 없습니다. " +
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
            if (Camera.main == null) return;
            followTarget = Camera.main.transform;
        }

        Vector3 p = followTarget.position;
        WorldLocation.UpdateCurrent(p);

        if (!streamingEnabled)
        {
            SetAllActive(true);
            return;
        }

        float sqrRange = activeDistance * activeDistance;
        int active = 0;

        for (int i = 0; i < tiles.Count; i++)
        {
            GameObject tile = tiles[i];
            if (tile == null) continue;

            // 높이는 무시합니다. 언덕 위에 있어도 같은 타일입니다.
            Vector3 delta = tile.transform.position - p;
            delta.y = 0f;

            bool shouldBeActive = delta.sqrMagnitude <= sqrRange;
            if (force || tile.activeSelf != shouldBeActive) tile.SetActive(shouldBeActive);

            if (shouldBeActive) active++;
        }

        ActiveTileCount = active;
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
