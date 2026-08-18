using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 마을과 길로 이루어진 터레인 월드를 한 번에 구워 내는 에디터 도구입니다.
///
/// 왜 도구로 만드는가:
///  - TerrainData는 바이너리 에셋이라 손으로 편집할 수 없습니다.
///  - 타일 14개가 같은 TerrainData 하나를 공유하고 있어 지형이 그대로 반복됩니다.
///  - Unity 터레인은 <b>회전할 수 없어서</b> 같은 타일을 북쪽 길과 동쪽 길에 함께 쓸 수 없습니다.
///  - 타일 경계는 <see cref="Terrain.SetNeighbors"/>로 이어 주지 않으면 균열이 보입니다.
///
/// 해결 방식의 핵심은 <b>높이를 월드 좌표에서 구한다</b>는 것입니다.
/// 타일마다 따로 노이즈를 굴리지 않고, 어느 타일이든 같은 함수 H(worldX, worldZ)를 씁니다.
/// 맞닿은 두 타일의 경계 정점은 <b>월드 좌표가 완전히 같으므로</b> 높이도 저절로 같아집니다.
/// 이음매를 맞추려는 별도의 보정이 필요 없습니다.
///
/// 기존 <c>New Terrain.asset</c>은 건드리지 않습니다. 새 에셋을 Generated 폴더에 따로 굽습니다.
/// </summary>
public class WorldTerrainBaker : EditorWindow
{
    // --- Constants ---

    /// <summary>구워 낸 TerrainData를 모아 둘 폴더입니다.</summary>
    private const string OutputFolder = "Assets/_Project/03.DataAssets/Terrain/Generated";

    /// <summary>씬에 만들 루트 오브젝트의 이름입니다. 다시 구우면 이 오브젝트를 갈아엎습니다.</summary>
    private const string RootName = "BakedWorld";

    /// <summary>Perlin 노이즈를 양수 구간에서만 쓰기 위한 좌표 오프셋입니다.</summary>
    private const float NoiseOrigin = 10000f;

    // --- Settings : 원본 ---

    [Tooltip("배치를 읽어 올 WorldStreamer. 마을 반경과 길 구성을 그대로 씁니다.")]
    private WorldStreamer streamer;

    [Tooltip("터레인 레이어(지면 텍스처)를 복사해 올 원본 TerrainData. 비워두면 단색으로 나옵니다.")]
    private TerrainData layerSource;

    // --- Settings : 타일 ---

    private int tileSize = 100;
    private int heightmapResolution = 129;
    private int alphamapResolution = 128;

    [Tooltip("터레인의 최대 높이(m). 높이 0~1 값이 이 범위로 펼쳐집니다.")]
    private float heightScale = 70f;

    [Tooltip("도로와 마을이 놓일 기준 높이(0~1). 0보다 커야 도로 아래로 골짜기를 팔 수 있습니다.")]
    private float baseHeight = 0.28f;

    // --- Settings : 지형 ---

    private int seed = 20260817;

    [Tooltip("낮을수록 완만하고 넓은 언덕이 됩니다.")]
    private float frequency = 0.0026f;

    [Tooltip("겹칠 노이즈 층 수. 많을수록 잔주름이 늘어납니다.")]
    private int octaves = 4;

    [Tooltip("기준 높이에서 위아래로 얼마나 벗어날 수 있는지(0~1).")]
    private float relief = 0.30f;

    // --- Settings : 마을과 길 ---

    [Tooltip("이 반경 안은 완전히 평탄해집니다. 비워두면 WorldStreamer의 값을 씁니다.")]
    private float villageRadius = 70f;

    [Tooltip("평탄한 구간에서 지형으로 되돌아가는 데 쓰는 여유 폭(m).")]
    private float villageFalloff = 55f;

    [Tooltip("도로 중심선에서 이만큼은 완전히 평탄합니다.")]
    private float roadHalfWidth = 9f;

    [Tooltip("도로 갓길에서 지형으로 되돌아가는 폭(m).")]
    private float roadFalloff = 40f;

    [Tooltip("도로가 오르내리는 높이(0~1). 0이면 완전히 평평합니다.")]
    private float roadUndulation = 0.035f;

    // --- Settings : 마무리 ---

    [Tooltip("마을 주변으로 몇 칸까지 타일을 깔지. 마을 반경이 길보다 넓을 때 생기는 빈 곳을 메웁니다.")]
    private int villagePadTiles = 1;

    private Vector2 scroll;
    private string lastReport = "";

    // --- Editor Window ---

    /// <summary>도구 창을 엽니다.</summary>
    [MenuItem("CarDrive/World/터레인 월드 굽기")]
    private static void Open()
    {
        WorldTerrainBaker window = GetWindow<WorldTerrainBaker>(false, "터레인 월드 굽기", true);
        window.minSize = new Vector2(380f, 520f);
        window.TryResolveSources();
        window.Show();
    }

    /// <summary>
    /// 에디터를 띄우지 않고 명령줄에서 바로 굽습니다.
    /// <c>Unity.exe -batchmode -quit -executeMethod WorldTerrainBaker.BakeFromCommandLine</c>
    /// </summary>
    public static void BakeFromCommandLine()
    {
        const string scenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        WorldTerrainBaker baker = CreateInstance<WorldTerrainBaker>();
        baker.TryResolveSources();

        if (baker.streamer == null)
        {
            Debug.LogError("WorldTerrainBaker: 씬에서 WorldStreamer를 찾지 못해 구울 수 없습니다.");
            EditorApplication.Exit(1);
            return;
        }

        baker.Bake();
        EditorSceneManager.SaveScene(scene);

        Debug.Log("WorldTerrainBaker: 명령줄 굽기 완료.");
    }

    /// <summary>
    /// 구워 낸 월드가 실제로 쓸 만한지 실측해 로그로 남깁니다.
    /// 이음매가 맞는지, 도로가 운전할 수 있는 경사인지, 지형에 기복이 있는지를 봅니다.
    /// <c>Unity.exe -batchmode -quit -executeMethod WorldTerrainBaker.VerifyFromCommandLine</c>
    /// </summary>
    public static void VerifyFromCommandLine()
    {
        const string scenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            Debug.LogError("VERIFY: 구운 월드가 없습니다.");
            EditorApplication.Exit(1);
            return;
        }

        Dictionary<Vector2Int, Terrain> map = new Dictionary<Vector2Int, Terrain>();
        Terrain[] all = root.GetComponentsInChildren<Terrain>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string[] parts = all[i].name.Split('_');
            if (parts.Length != 3) continue;

            int cx, cz;
            if (!int.TryParse(parts[1], out cx) || !int.TryParse(parts[2], out cz)) continue;
            map[new Vector2Int(cx, cz)] = all[i];
        }

        // 1) 이음매 — 맞닿은 두 타일의 경계 높이가 실제로 같은가
        float worstSeam = 0f;
        int seamPairs = 0;
        foreach (KeyValuePair<Vector2Int, Terrain> e in map)
        {
            Terrain rightN;
            if (map.TryGetValue(new Vector2Int(e.Key.x + 1, e.Key.y), out rightN))
            {
                worstSeam = Mathf.Max(worstSeam, EdgeGap(e.Value, rightN, true));
                seamPairs++;
            }
            Terrain topN;
            if (map.TryGetValue(new Vector2Int(e.Key.x, e.Key.y + 1), out topN))
            {
                worstSeam = Mathf.Max(worstSeam, EdgeGap(e.Value, topN, false));
                seamPairs++;
            }
        }

        // 2) 도로 — 북쪽 길(x=0, z 0..800)과 동쪽 길(z=0, x 0..700)의 경사
        string northRoad = ProfileLine(map, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 800f), "북쪽 길");
        string eastRoad = ProfileLine(map, new Vector3(0f, 0f, 0f), new Vector3(700f, 0f, 0f), "동쪽 길");

        // 3) 지형 — 도로에서 벗어난 곳에 기복이 있는가
        float lo = float.MaxValue, hi = float.MinValue, heightY = 1f;
        foreach (KeyValuePair<Vector2Int, Terrain> e in map)
        {
            heightY = e.Value.terrainData.size.y;
            float[,] h = e.Value.terrainData.GetHeights(0, 0,
                e.Value.terrainData.heightmapResolution, e.Value.terrainData.heightmapResolution);
            for (int z = 0; z < h.GetLength(0); z += 8)
            {
                for (int x = 0; x < h.GetLength(1); x += 8)
                {
                    lo = Mathf.Min(lo, h[z, x]);
                    hi = Mathf.Max(hi, h[z, x]);
                }
            }
        }

        string[] lines =
        {
            "VERIFY ===============================",
            "타일: " + map.Count + "개, 검사한 이웃 쌍: " + seamPairs,
            "이음매 최대 오차: " + worstSeam.ToString("F6") + " m",
            northRoad,
            eastRoad,
            "지형 높이 범위: " + (lo * heightY).ToString("F1") + " ~ " + (hi * heightY).ToString("F1") + " m",
            "======================================"
        };
        Debug.Log(string.Join(System.Environment.NewLine, lines));
    }

    /// <summary>맞닿은 두 타일의 경계 정점 높이 차이 중 가장 큰 값(m)을 구합니다.</summary>
    /// <param name="a">기준 타일</param>
    /// <param name="b">이웃 타일</param>
    /// <param name="alongX">true면 X 방향 이웃, false면 Z 방향 이웃</param>
    /// <returns>가장 큰 높이 차이(m)</returns>
    private static float EdgeGap(Terrain a, Terrain b, bool alongX)
    {
        int res = a.terrainData.heightmapResolution;
        float scale = a.terrainData.size.y;

        float[,] ha = a.terrainData.GetHeights(0, 0, res, res);
        float[,] hb = b.terrainData.GetHeights(0, 0, res, res);

        float worst = 0f;
        for (int i = 0; i < res; i++)
        {
            float va = alongX ? ha[i, res - 1] : ha[res - 1, i];
            float vb = alongX ? hb[i, 0] : hb[0, i];
            worst = Mathf.Max(worst, Mathf.Abs(va - vb) * scale);
        }
        return worst;
    }

    /// <summary>선을 따라 높이를 훑어 최대 경사와 고저차를 문자열로 만듭니다.</summary>
    /// <param name="map">좌표별 터레인</param>
    /// <param name="from">시작 월드 좌표</param>
    /// <param name="to">끝 월드 좌표</param>
    /// <param name="label">표시할 이름</param>
    /// <returns>측정 결과 한 줄</returns>
    private static string ProfileLine(Dictionary<Vector2Int, Terrain> map, Vector3 from, Vector3 to, string label)
    {
        const int Samples = 200;
        float lo = float.MaxValue, hi = float.MinValue, maxGrade = 0f, prev = 0f;
        float spacing = Vector3.Distance(from, to) / Samples;

        for (int i = 0; i <= Samples; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, (float)i / Samples);

            float y = 0f;
            foreach (KeyValuePair<Vector2Int, Terrain> e in map)
            {
                Vector3 tp = e.Value.transform.position;
                Vector3 size = e.Value.terrainData.size;
                if (p.x < tp.x || p.x > tp.x + size.x || p.z < tp.z || p.z > tp.z + size.z) continue;

                y = e.Value.SampleHeight(p);
                break;
            }

            lo = Mathf.Min(lo, y);
            hi = Mathf.Max(hi, y);
            if (i > 0 && spacing > 0.001f) maxGrade = Mathf.Max(maxGrade, Mathf.Abs(y - prev) / spacing);
            prev = y;
        }

        return label + ": 고저차 " + (hi - lo).ToString("F1") + " m, 최대 경사 " +
               (maxGrade * 100f).ToString("F1") + " %";
    }

    /// <summary>창이 열릴 때 비어 있는 참조를 씬과 에셋에서 찾아 둡니다.</summary>
    private void OnEnable()
    {
        TryResolveSources();
    }

    /// <summary>설정 UI를 그립니다.</summary>
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "월드 좌표 기반으로 높이를 구하므로 타일 경계가 저절로 맞습니다.\n" +
            "기존 New Terrain.asset은 건드리지 않고 Generated 폴더에 새로 굽습니다.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("원본", EditorStyles.boldLabel);
        streamer = (WorldStreamer)EditorGUILayout.ObjectField(
            new GUIContent("WorldStreamer", "배치(마을 반경·길 구성)를 읽어 옵니다."),
            streamer, typeof(WorldStreamer), true);
        layerSource = (TerrainData)EditorGUILayout.ObjectField(
            new GUIContent("레이어 원본", "지면 텍스처를 복사해 올 기존 TerrainData"),
            layerSource, typeof(TerrainData), false);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("타일", EditorStyles.boldLabel);
        tileSize = EditorGUILayout.IntField("타일 한 변(m)", tileSize);
        heightmapResolution = ResolutionField("높이맵 해상도", heightmapResolution);
        alphamapResolution = EditorGUILayout.IntField("텍스처 해상도", alphamapResolution);
        heightScale = EditorGUILayout.FloatField("최대 높이(m)", heightScale);
        baseHeight = EditorGUILayout.Slider("기준 높이", baseHeight, 0.05f, 0.9f);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("지형", EditorStyles.boldLabel);
        seed = EditorGUILayout.IntField("시드", seed);
        frequency = EditorGUILayout.Slider("굴곡 주기", frequency, 0.0005f, 0.02f);
        octaves = EditorGUILayout.IntSlider("노이즈 층", octaves, 1, 6);
        relief = EditorGUILayout.Slider("기복", relief, 0f, 0.6f);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("마을과 길", EditorStyles.boldLabel);
        villageRadius = EditorGUILayout.FloatField("마을 반경(m)", villageRadius);
        villageFalloff = EditorGUILayout.FloatField("마을 여유 폭(m)", villageFalloff);
        roadHalfWidth = EditorGUILayout.FloatField("도로 반폭(m)", roadHalfWidth);
        roadFalloff = EditorGUILayout.FloatField("갓길 폭(m)", roadFalloff);
        roadUndulation = EditorGUILayout.Slider("도로 기복", roadUndulation, 0f, 0.15f);
        villagePadTiles = EditorGUILayout.IntSlider("마을 주변 타일", villagePadTiles, 0, 3);

        EditorGUILayout.Space(10f);

        using (new EditorGUI.DisabledScope(streamer == null))
        {
            if (GUILayout.Button("월드 굽기", GUILayout.Height(34f))) Bake();
        }
        if (streamer == null)
        {
            EditorGUILayout.HelpBox("씬에서 WorldStreamer를 찾지 못했습니다. 직접 지정하세요.", MessageType.Warning);
        }

        if (GUILayout.Button("구운 월드 지우기")) ClearBaked(true);

        if (!string.IsNullOrEmpty(lastReport))
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("결과", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(lastReport, MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    // --- Baking ---

    /// <summary>
    /// 배치를 읽어 타일 좌표를 정하고, 타일마다 TerrainData를 구워 씬에 세운 뒤 이웃을 이어 줍니다.
    /// </summary>
    private void Bake()
    {
        if (streamer == null) return;

        Vector3 center = streamer.origin != null ? streamer.origin.position : streamer.transform.position;
        List<Vector2Int> coords = CollectTileCoords(center);
        List<RoadSegment> roads = CollectRoads(center);

        if (coords.Count == 0)
        {
            lastReport = "깔 타일이 없습니다. WorldStreamer의 길 구성을 확인하세요.";
            return;
        }

        EnsureOutputFolder();
        ClearBaked(false);

        Transform root = new GameObject(RootName).transform;
        root.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "터레인 월드 굽기");

        TerrainLayer[] layers = layerSource != null ? layerSource.terrainLayers : null;
        Material material = FindTerrainMaterial();

        Dictionary<Vector2Int, Terrain> built = new Dictionary<Vector2Int, Terrain>();

        try
        {
            for (int i = 0; i < coords.Count; i++)
            {
                EditorUtility.DisplayProgressBar("터레인 월드 굽기",
                    "타일 " + (i + 1) + " / " + coords.Count, (float)i / coords.Count);

                Terrain terrain = BakeTile(coords[i], center, roads, layers, material, root);
                if (terrain != null) built[coords[i]] = terrain;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        StitchNeighbors(built);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 구운 타일을 스트리밍이 그대로 쓰도록 연결합니다.
        Undo.RecordObject(streamer, "구운 월드 연결");
        streamer.bakedRoot = root;
        EditorUtility.SetDirty(streamer);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(streamer.gameObject.scene);

        lastReport =
            "타일 " + built.Count + "개를 구웠습니다.\n" +
            "길 " + roads.Count + "개를 평탄화했습니다.\n" +
            "TerrainData: " + OutputFolder + "\n" +
            "WorldStreamer.bakedRoot 에 " + RootName + " 을 연결했습니다.\n" +
            "씬을 저장해야 유지됩니다.";

        Debug.Log("WorldTerrainBaker: " + lastReport);
    }

    /// <summary>
    /// 타일 하나의 TerrainData를 만들어 에셋으로 저장하고 씬에 세웁니다.
    /// </summary>
    /// <param name="coord">타일 좌표 (타일 크기 단위)</param>
    /// <param name="center">마을 중심 월드 좌표</param>
    /// <param name="roads">평탄화할 길 목록</param>
    /// <param name="layers">복사해 넣을 터레인 레이어. null이면 넣지 않습니다.</param>
    /// <param name="material">터레인 머티리얼. null이면 파이프라인 기본값을 씁니다.</param>
    /// <param name="root">타일을 담을 부모</param>
    /// <returns>만들어진 Terrain. 실패하면 null입니다.</returns>
    private Terrain BakeTile(Vector2Int coord, Vector3 center, List<RoadSegment> roads,
                             TerrainLayer[] layers, Material material, Transform root)
    {
        int res = heightmapResolution;

        TerrainData data = new TerrainData();
        data.heightmapResolution = res;
        data.size = new Vector3(tileSize, heightScale, tileSize);

        Vector3 tileOrigin = new Vector3(coord.x * tileSize, 0f, coord.y * tileSize);

        // 높이맵은 [z, x] 순서로 인덱싱합니다.
        float[,] heights = new float[res, res];
        float step = (float)tileSize / (res - 1);

        for (int zi = 0; zi < res; zi++)
        {
            float wz = tileOrigin.z + zi * step;
            for (int xi = 0; xi < res; xi++)
            {
                float wx = tileOrigin.x + xi * step;
                heights[zi, xi] = SampleHeight(wx, wz, center, roads);
            }
        }
        data.SetHeights(0, 0, heights);

        if (layers != null && layers.Length > 0)
        {
            data.terrainLayers = layers;
            PaintBySlope(data, layers.Length);
        }

        string path = OutputFolder + "/Tile_" + coord.x + "_" + coord.y + ".asset";
        AssetDatabase.CreateAsset(data, path);

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Tile_" + coord.x + "_" + coord.y;
        go.transform.SetParent(root, false);
        go.transform.position = tileOrigin;

        Terrain terrain = go.GetComponent<Terrain>();
        if (terrain != null)
        {
            if (material != null) terrain.materialTemplate = material;
            terrain.allowAutoConnect = true;
            terrain.groupingID = 0;
        }

        return terrain;
    }

    // --- Height field ---

    /// <summary>
    /// 월드 좌표 한 점의 높이를 구합니다. <b>타일과 무관한 순수 함수</b>이므로
    /// 맞닿은 타일의 경계 정점은 저절로 같은 값을 갖습니다.
    /// </summary>
    /// <param name="wx">월드 X</param>
    /// <param name="wz">월드 Z</param>
    /// <param name="center">마을 중심</param>
    /// <param name="roads">평탄화할 길 목록</param>
    /// <returns>0~1 정규화 높이</returns>
    private float SampleHeight(float wx, float wz, Vector3 center, List<RoadSegment> roads)
    {
        float natural = Mathf.Clamp01(baseHeight + (Fbm(wx, wz) - 0.5f) * 2f * relief);

        // 마을과 길이 각각 "이 높이로 눌러라"라고 요구합니다.
        //
        // 예전에는 그중 <b>가장 센 요구 하나만</b> 골라 썼습니다. 그러면 마을과 길이
        // 겹치는 지점에서 고르는 대상이 바뀌는 순간 목표 높이가 툭 튀어, 고저차가
        // 2m밖에 안 되는데도 그 한 지점의 경사가 30%를 넘는 꺾임이 생깁니다.
        //
        // 그래서 목표 높이는 <b>가중 평균</b>으로 섞고, 누르는 세기만 가장 센 값을 씁니다.
        // 그러면 요구가 서로 넘어가는 구간에서도 목표가 이어집니다.
        float maxWeight = 0f;
        float sumWeight = 0f;
        float sumHeight = 0f;

        // 마을 — 원형으로 평탄합니다.
        float toVillage = new Vector2(wx - center.x, wz - center.z).magnitude;
        float villageW = Falloff(toVillage, villageRadius, villageFalloff);
        if (villageW > 0f)
        {
            maxWeight = villageW;
            sumWeight += villageW;
            sumHeight += villageW * baseHeight;
        }

        // 길 — 중심선에서의 거리로 판정하고, 길이 방향으로 완만하게 오르내립니다.
        for (int i = 0; i < roads.Count; i++)
        {
            float t;
            float d = roads[i].DistanceTo(wx, wz, out t);

            float w = Falloff(d, roadHalfWidth, roadFalloff);
            if (w <= 0f) continue;

            if (w > maxWeight) maxWeight = w;
            sumWeight += w;
            sumHeight += w * (baseHeight + RoadOffset(roads[i], t));
        }

        if (maxWeight <= 0f) return Mathf.Clamp01(natural);

        return Mathf.Clamp01(Mathf.Lerp(natural, sumHeight / sumWeight, maxWeight));
    }

    /// <summary>
    /// 길을 따라 완만하게 오르내리는 높이 보정입니다. 운전할 수 있을 만큼만 흔듭니다.
    /// </summary>
    /// <param name="road">대상 길</param>
    /// <param name="t">길 위의 진행 비율(0~1)</param>
    /// <returns>기준 높이에 더할 값</returns>
    private float RoadOffset(RoadSegment road, float t)
    {
        if (roadUndulation <= 0f) return 0f;

        float distance = t * road.Length;

        // 길이 마을에서 출발하는 지점에서는 마을과 높이가 정확히 같아야 합니다.
        // 그러지 않으면 마을 경계에서 도로가 한 단 꺾입니다. 기복을 서서히 붙입니다.
        float ramp = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(distance / Mathf.Max(1f, villageRadius + villageFalloff)));

        float n = Mathf.PerlinNoise(NoiseOrigin + road.NoiseLane, NoiseOrigin + distance * 0.01f);
        return (n - 0.5f) * 2f * roadUndulation * ramp;
    }

    /// <summary>
    /// 여러 층의 Perlin 노이즈를 겹쳐 0~1 값을 만듭니다.
    /// </summary>
    /// <param name="wx">월드 X</param>
    /// <param name="wz">월드 Z</param>
    /// <returns>0~1 노이즈 값</returns>
    private float Fbm(float wx, float wz)
    {
        float sx = NoiseOrigin + (seed % 977) * 13.37f;
        float sz = NoiseOrigin + (seed % 613) * 7.11f;

        float sum = 0f, amp = 1f, freq = frequency, norm = 0f;

        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Mathf.PerlinNoise(sx + wx * freq, sz + wz * freq);
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }

        return norm > 0f ? sum / norm : 0.5f;
    }

    /// <summary>
    /// 중심에서 <paramref name="inner"/>까지는 1, 그 밖으로 <paramref name="fade"/>만큼 부드럽게 0이 됩니다.
    /// </summary>
    /// <param name="distance">중심에서의 거리</param>
    /// <param name="inner">완전히 평탄한 반경</param>
    /// <param name="fade">되돌아가는 데 쓸 여유 폭</param>
    /// <returns>평탄화 가중치(0~1)</returns>
    private static float Falloff(float distance, float inner, float fade)
    {
        if (distance <= inner) return 1f;
        if (fade <= 0.001f) return 0f;

        float t = Mathf.Clamp01((distance - inner) / fade);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
    }

    // --- Layout ---

    /// <summary>
    /// 마을과 모든 길을 덮는 타일 좌표를 모읍니다. 중복은 걸러 냅니다.
    /// </summary>
    /// <param name="center">마을 중심</param>
    /// <returns>타일 좌표 목록</returns>
    private List<Vector2Int> CollectTileCoords(Vector3 center)
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>();

        // 마을은 원형이라 길만으로는 사방이 덮이지 않습니다. 주변을 채워 둡니다.
        int pad = Mathf.Max(villagePadTiles, Mathf.CeilToInt(villageRadius / tileSize));
        Vector2Int c = ToCoord(center);
        for (int dz = -pad; dz <= pad; dz++)
        {
            for (int dx = -pad; dx <= pad; dx++) set.Add(new Vector2Int(c.x + dx, c.y + dz));
        }

        if (streamer.routes != null)
        {
            for (int r = 0; r < streamer.routes.Count; r++)
            {
                WorldRoute route = streamer.routes[r];
                Vector3 dir = route.direction.sqrMagnitude > 0.0001f ? route.direction.normalized : Vector3.forward;
                Vector3 cursor = center + route.startOffset;

                for (int i = 0; i < route.tileCount; i++)
                {
                    set.Add(ToCoord(cursor));

                    // 길이 타일 경계를 비스듬히 지나갈 때 옆 칸이 비지 않도록 한 칸 더 넣습니다.
                    Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
                    set.Add(ToCoord(cursor + side * (tileSize * 0.5f)));
                    set.Add(ToCoord(cursor - side * (tileSize * 0.5f)));

                    cursor += dir * tileSize;
                }
                set.Add(ToCoord(cursor));
            }
        }

        return new List<Vector2Int>(set);
    }

    /// <summary>
    /// 평탄화할 길을 모읍니다. 각 길은 마을 중심에서 끝점까지의 직선입니다.
    /// (길 시작점이 마을에서 떨어져 있어도 중심에서부터 이어 주어야 도로가 끊기지 않습니다)
    /// </summary>
    /// <param name="center">마을 중심</param>
    /// <returns>길 목록</returns>
    private List<RoadSegment> CollectRoads(Vector3 center)
    {
        List<RoadSegment> roads = new List<RoadSegment>();
        if (streamer.routes == null) return roads;

        for (int r = 0; r < streamer.routes.Count; r++)
        {
            WorldRoute route = streamer.routes[r];
            Vector3 dir = route.direction.sqrMagnitude > 0.0001f ? route.direction.normalized : Vector3.forward;

            Vector3 start = center;
            Vector3 end = center + route.startOffset + dir * (route.tileCount * tileSize);

            roads.Add(new RoadSegment(start, end, r * 97.3f));
        }

        return roads;
    }

    /// <summary>월드 좌표를 타일 좌표로 내립니다.</summary>
    /// <param name="world">월드 좌표</param>
    /// <returns>타일 좌표</returns>
    private Vector2Int ToCoord(Vector3 world)
    {
        return new Vector2Int(
            Mathf.FloorToInt(world.x / tileSize),
            Mathf.FloorToInt(world.z / tileSize));
    }

    // --- Finishing ---

    /// <summary>
    /// 이웃 터레인을 서로 알려 줍니다. 이것을 하지 않으면 경계에서 LOD가 어긋나 균열이 보입니다.
    /// </summary>
    /// <param name="built">좌표로 찾을 수 있는 터레인 표</param>
    private void StitchNeighbors(Dictionary<Vector2Int, Terrain> built)
    {
        foreach (KeyValuePair<Vector2Int, Terrain> entry in built)
        {
            Vector2Int c = entry.Key;

            Terrain left, right, top, bottom;
            built.TryGetValue(new Vector2Int(c.x - 1, c.y), out left);
            built.TryGetValue(new Vector2Int(c.x + 1, c.y), out right);
            built.TryGetValue(new Vector2Int(c.x, c.y + 1), out top);
            built.TryGetValue(new Vector2Int(c.x, c.y - 1), out bottom);

            entry.Value.SetNeighbors(left, top, right, bottom);
        }
    }

    /// <summary>
    /// 경사에 따라 지면 텍스처를 칠합니다. 완만한 곳은 첫 번째 레이어, 가파른 곳은 두 번째 레이어입니다.
    /// 레이어가 하나뿐이면 전부 그 레이어로 채웁니다.
    /// </summary>
    /// <param name="data">칠할 대상</param>
    /// <param name="layerCount">사용할 레이어 수</param>
    private void PaintBySlope(TerrainData data, int layerCount)
    {
        int res = Mathf.Max(16, alphamapResolution);
        data.alphamapResolution = res;

        float[,,] maps = new float[res, res, layerCount];

        for (int z = 0; z < res; z++)
        {
            float nz = (float)z / (res - 1);
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1);

                if (layerCount == 1)
                {
                    maps[z, x, 0] = 1f;
                    continue;
                }

                // GetSteepness는 (x, z) 순서의 정규화 좌표를 받습니다.
                float steep = data.GetSteepness(nx, nz) / 90f;
                float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.45f, steep));

                maps[z, x, 0] = 1f - rock;
                maps[z, x, 1] = rock;
                for (int l = 2; l < layerCount; l++) maps[z, x, l] = 0f;
            }
        }

        data.SetAlphamaps(0, 0, maps);
    }

    /// <summary>
    /// 이전에 구운 결과를 지웁니다.
    /// </summary>
    /// <param name="alsoAssets">true면 Generated 폴더의 에셋까지 지웁니다.</param>
    private void ClearBaked(bool alsoAssets)
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        if (!alsoAssets) return;

        if (AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.DeleteAsset(OutputFolder);
            AssetDatabase.Refresh();
        }

        if (streamer != null)
        {
            Undo.RecordObject(streamer, "구운 월드 해제");
            streamer.bakedRoot = null;
            EditorUtility.SetDirty(streamer);
        }

        lastReport = "구운 월드를 지웠습니다.";
    }

    // --- Helpers ---

    /// <summary>비어 있는 참조를 씬과 에셋에서 찾아 채웁니다.</summary>
    private void TryResolveSources()
    {
        if (streamer == null) streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer != null) villageRadius = streamer.villageRadius;

        if (layerSource == null)
        {
            string[] found = AssetDatabase.FindAssets("t:TerrainData", new[] { "Assets/_Project/03.DataAssets/Terrain" });
            for (int i = 0; i < found.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(found[i]);
                if (path.Contains("/Generated/")) continue;

                layerSource = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                break;
            }
        }
    }

    /// <summary>기존 터레인 프리팹이 쓰던 머티리얼을 그대로 씁니다.</summary>
    /// <returns>찾은 머티리얼. 없으면 null(파이프라인 기본값)입니다.</returns>
    private static Material FindTerrainMaterial()
    {
        string[] found = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/05.Prefabs/Map" });
        for (int i = 0; i < found.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(found[i]));
            if (prefab == null) continue;

            Terrain t = prefab.GetComponentInChildren<Terrain>(true);
            if (t != null && t.materialTemplate != null) return t.materialTemplate;
        }
        return null;
    }

    /// <summary>출력 폴더가 없으면 만듭니다.</summary>
    private static void EnsureOutputFolder()
    {
        if (AssetDatabase.IsValidFolder(OutputFolder)) return;

        Directory.CreateDirectory(OutputFolder);
        AssetDatabase.Refresh();
    }

    /// <summary>높이맵 해상도는 2의 거듭제곱 + 1이어야 합니다. 가까운 값으로 맞춰 줍니다.</summary>
    /// <param name="label">표시할 이름</param>
    /// <param name="value">현재 값</param>
    /// <returns>보정된 값</returns>
    private static int ResolutionField(string label, int value)
    {
        int[] options = { 65, 129, 257, 513 };
        string[] names = { "65", "129", "257", "513" };

        int index = 1;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == value) index = i;
        }

        index = EditorGUILayout.Popup(label, index, names);
        return options[index];
    }

    // --- Types ---

    /// <summary>평탄화할 길 하나입니다. 마을 중심에서 끝점까지의 직선입니다.</summary>
    private struct RoadSegment
    {
        private readonly Vector2 a;
        private readonly Vector2 ab;
        private readonly float sqrLength;

        /// <summary>길의 전체 길이(m)입니다.</summary>
        public float Length { get; private set; }

        /// <summary>길마다 다른 기복을 주기 위한 노이즈 좌표입니다.</summary>
        public float NoiseLane { get; private set; }

        /// <summary>
        /// 시작점과 끝점으로 길을 만듭니다.
        /// </summary>
        /// <param name="start">시작 월드 좌표</param>
        /// <param name="end">끝 월드 좌표</param>
        /// <param name="noiseLane">이 길에 쓸 노이즈 좌표</param>
        public RoadSegment(Vector3 start, Vector3 end, float noiseLane)
        {
            a = new Vector2(start.x, start.z);
            ab = new Vector2(end.x - start.x, end.z - start.z);
            sqrLength = Mathf.Max(0.0001f, ab.sqrMagnitude);
            Length = Mathf.Sqrt(sqrLength);
            NoiseLane = noiseLane;
        }

        /// <summary>
        /// 한 점에서 이 길까지의 최단 거리를 구합니다.
        /// </summary>
        /// <param name="wx">월드 X</param>
        /// <param name="wz">월드 Z</param>
        /// <param name="t">길 위의 진행 비율(0~1)이 여기에 담깁니다.</param>
        /// <returns>중심선까지의 거리(m)</returns>
        public float DistanceTo(float wx, float wz, out float t)
        {
            Vector2 ap = new Vector2(wx - a.x, wz - a.y);
            t = Mathf.Clamp01(Vector2.Dot(ap, ab) / sqrLength);

            Vector2 closest = a + ab * t;
            return new Vector2(wx - closest.x, wz - closest.y).magnitude;
        }
    }
}
