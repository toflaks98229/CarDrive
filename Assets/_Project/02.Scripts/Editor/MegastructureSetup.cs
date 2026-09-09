using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 블렌더가 만든 <b>메가프레임 베이</b>를 프로젝트에 세우고, 그것을 이어 스파인을 만듭니다.
///
/// <c>build_megastructure.py</c> 가 베이 하나짜리 FBX 를 씨앗마다 냅니다. 베이는
/// <b>이어 붙는 모듈</b>이지 완성된 건물이 아닙니다 — 메가스트럭처의 정의(Wilcoxon)가
/// 모듈 단위와 무한 연장이므로, 여기서 할 일의 절반은 <b>실제로 이어 붙이는 것</b>입니다.
///
/// <b>레이어는 Default(0) 입니다.</b> Prop(9) 은 Ground 하고만 부딪히므로 거기에 두면
/// 차와 플레이어가 콘크리트를 그대로 통과합니다. 집들도 같은 이유로 0 을 씁니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod MegastructureSetup.Run
/// Unity.exe -batchmode -projectPath . -executeMethod MegastructureSetup.Preview
/// </code>
/// </summary>
public static class MegastructureSetup
{
    // --- Constants ---

    private const string ModelDir = "Assets/_Project/04.Art/02.Models/Megastructure";
    private const string PrefabDir = "Assets/_Project/05.Prefabs/Megastructure";
    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";
    private const string ParentMaterialPath = MaterialDir + "/MartWall.mat";
    private const string SpinePath = PrefabDir + "/MegaSpine.prefab";
    private const string ShotDir = "Logs/Megastructure";

    /// <summary>
    /// 스파인이 덮어야 할 최소 길이(m)입니다.
    ///
    /// 실측한 지형이 1100 × 1200 m 입니다. 그보다 짧으면 세계 안에서 <b>양 끝이
    /// 보이고</b>, 끝이 보이면 "무한히 연장 가능"이 거짓말이 됩니다. 프리셋마다 길이가
    /// 다르므로 개수가 아니라 <b>길이</b>로 정합니다.
    /// </summary>
    private const float SpineLength = 1340f;

    private const string ManifestPath = ModelDir + "/presets.json";

    private const string LodSuffix = "_LOD1";

    /// <summary>
    /// 거친 판본으로 갈아타는 <b>판단 기준 크기</b>(m)입니다. 실제 크기가 아닙니다.
    ///
    /// 유니티는 화면 높이 비율 = <c>size / (거리 x 2 tan(화각/2))</c> 로 LOD 를
    /// 고릅니다. 시타델의 진짜 크기는 505 m 라 <b>어느 거리에서도 화면을 가득
    /// 채웁니다</b> - 파클립 482 m 에서도 0.81 이어서, 임계값을 1 로 두어도 갈아탈
    /// 일이 없습니다. 그래서 size 는 "이 거리에서 갈아탄다" 를 적는 자리로 씁니다.
    ///
    /// 117 = 0.30(임계값) x 300 m x 1.30(화각 66° 의 2 tan(33°)). 곧 <b>300 m</b>
    /// 에서 갈아탑니다. 안개가 257 m 에서 닫히고 이 구조물은 그 뒤로도 45% 만
    /// 남으므로(_FogScale), 300 m 밖의 격자와 갤러리는 읽히지 않습니다.
    /// </summary>
    private const float LodSize = 117f;

    // --- Manifest ---

    /// <summary>
    /// 블렌더가 낸 프리셋 목록입니다. <b>길이는 여기서 읽습니다.</b>
    ///
    /// C# 에 42 라고 적어 두면 블렌더 쪽 상수가 바뀌는 순간 조용히 틈이 생기거나
    /// 겹칩니다. 리그 JSON 과 같은 원칙입니다 — 치수는 만든 쪽이 적고 쓰는 쪽은 읽습니다.
    /// </summary>
    [Serializable]
    private class Manifest
    {
        public float bay;
        public float width;
        public float burial;
        public float deckTop;
        public int seamPoints;

        /// <summary>베이마다 놓이는 <b>공용 뼈대</b>의 메시 이름입니다.</summary>
        public string core;

        public Preset[] presets;
    }

    [Serializable]
    private class Preset
    {
        public string name;
        public string mesh;
        public int bays;
        public float length;
        public int weight;
        public float top;

        /// <summary>멀리서 쓸 판본의 메시 이름입니다. 없으면 빈 문자열입니다.</summary>
        public string lod1;

        public int lod1Tris;
    }

    /// <summary>
    /// FBX 안의 이름 → 프로젝트 머티리얼 이름과 <b>설계한 명도</b>입니다.
    ///
    /// <b>값이 두 수명을 가릅니다.</b> 콘크리트는 골조(영구), 어두운 것은 꽂아 넣은
    /// 캡슐(임시). 둘이 같은 명도면 메가스트럭처가 아니라 그냥 큰 건물로 보입니다.
    ///
    /// 결(텍스처)은 여기서 물리지 않습니다. <see cref="BrutalistTextureSetup"/> 이
    /// 로봇과 같은 표에서 물립니다 — 같은 콘크리트여야 같은 세계로 보입니다.
    /// </summary>
    /// <summary>
    /// 색·결·발광·안개는 <see cref="BrutalistTextureSetup.Palette"/> 가 정합니다.
    ///
    /// 여기에도 같은 표가 있었습니다. 그리고 <see cref="PropMeshSetup"/> 에도 또
    /// 있었는데 그쪽만 옛 값에 멈춰 있어서, 그것을 마지막에 돌린 날에는 <b>건물 안
    /// 방만 옛 콘크리트 색</b>이 되었습니다. 표를 지우고 읽기만 합니다.
    /// </summary>
    private static IEnumerable<BrutalistTextureSetup.Surface> Surfaces
    {
        get
        {
            return BrutalistTextureSetup.Palette
                .Where(s => s.Fbx != null && s.Fbx.StartsWith("M_Mega_"));
        }
    }

    // --- Public Methods ---

    public static void Run()
    {
        int errors = 0;

        try
        {
            Dictionary<string, Material> materials = EnsureMaterials();

            string[] models = Directory.GetFiles(ModelDir, "*.fbx")
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p)
                .ToArray();

            if (models.Length == 0) throw new Exception("FBX 가 하나도 없습니다: " + ModelDir);

            Directory.CreateDirectory(PrefabDir);

            Manifest manifest = JsonUtility.FromJson<Manifest>(
                System.IO.File.ReadAllText(ManifestPath));

            if (manifest == null || manifest.presets == null || manifest.presets.Length == 0)
            {
                throw new Exception("프리셋 목록을 읽지 못했습니다: " + ManifestPath);
            }

            // <b>부품 이름으로 담습니다.</b> 뼈대와 프리셋 부품이 섞여 있고, 스파인은
            // 둘을 다른 규칙으로 놓습니다 - 뼈대는 베이마다, 부품은 프리셋마다.
            Dictionary<string, GameObject> made = new Dictionary<string, GameObject>();

            // <b>거친 판본은 따로 붙입니다.</b> 그냥 돌리면 그것도 프리팹 하나가
            // 되어 스파인에 <b>두 번째 시타델</b>로 끼어듭니다.
            foreach (string model in models)
            {
                Configure(model, materials);

                if (model.EndsWith(LodSuffix + ".fbx")) continue;

                GameObject piece = BuildBay(model);
                made[piece.name] = piece;
            }

            foreach (string model in models)
            {
                if (!model.EndsWith(LodSuffix + ".fbx")) continue;

                AttachLod(model);
            }

            if (!made.TryGetValue(manifest.core, out GameObject core))
            {
                throw new Exception("공용 뼈대 프리팹이 없습니다: " + manifest.core);
            }

            BuildSpine(manifest, core, made);

            AssetDatabase.SaveAssets();
            Debug.Log($"MegastructureSetup: 부품 {made.Count} 종 · 이음매 {manifest.seamPoints} 점 확인됨");
        }
        catch (Exception e)
        {
            Debug.LogError("MegastructureSetup: " + e);
            errors++;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    /// <summary>
    /// 만든 것을 <b>프로젝트 셰이더로</b> 찍어 봅니다.
    ///
    /// 블렌더 렌더로는 알 수 없는 것이 있습니다 — 툰 램프가 명암을 계단으로 끊고,
    /// 팔레트가 색 수를 줄이고, 안개가 중경을 걷어 갑니다. 그 셋을 지난 뒤에도
    /// 골조와 캡슐이 갈려 보이는지는 여기서만 확인됩니다.
    ///
    /// ⚠ 렌더가 필요하므로 <c>-nographics</c> 를 붙이면 안 됩니다.
    ///
    /// ⚠ <b>얼룩(높이 그라디언트)은 여기서 틀리게 보입니다.</b> 얼룩이 걸리는 높이는
    /// 월드 좌표라 <see cref="MegastructurePlacer"/> 가 구조물을 앉힌 높이(지금
    /// 20.2 m)에 맞춰져 있는데, 이 씬은 원점에 세우므로 20 m 어긋납니다. 여기서
    /// 판단할 것은 <b>무엇이 어디에 있는가</b>이고, 색과 밝기는
    /// <see cref="MegastructureLookCapture"/> 가 답합니다.
    /// </summary>
    public static void Preview()
    {
        Directory.CreateDirectory(ShotDir);

        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(200f, 1f, 200f);

        GameObject spine = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(SpinePath));
        Bounds all = Measure(spine);

        Manifest manifest = JsonUtility.FromJson<Manifest>(
            System.IO.File.ReadAllText(ManifestPath));

        // 차 몇 대. 이것이 없으면 100 m 인지 10 m 인지 그림만 봐서는 알 수 없습니다.
        // <b>한 대로는 부족합니다</b> — 높이가 400 m 를 넘으면 멀리 있는 차 한 대는
        // 점이 되어 사라지고, 그러면 크기를 재 줄 것이 화면에 아무것도 없습니다.
        for (int i = 0; i < 5; i++)
        {
            GameObject car = GameObject.CreatePrimitive(PrimitiveType.Cube);
            car.transform.localScale = new Vector3(1.9f, 1.5f, 4.5f);
            car.transform.position = new Vector3(
                (i - 2) * 26f, 0.75f, all.center.z + all.extents.z * (0.2f + i * 0.14f));
        }

        GameObject sun = new GameObject("Sun");
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        // 알베도를 0.58 → 0.66 으로 올린 뒤 이 빛(1.4)에서 콘크리트가 하얗게
        // 날아가 그늘이 안 보였습니다. 재는 도구가 클리핑하면 아무것도 못 잽니다.
        light.intensity = 1.05f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(38f, -34f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.44f, 0.50f, 0.59f);
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.40f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.20f, 0.18f);
        RenderSettings.fog = false;

        GameObject camObject = new GameObject("Shot");
        Camera cam = camObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.66f, 0.72f);
        cam.fieldOfView = 38f;
        cam.nearClipPlane = 0.4f;
        cam.farClipPlane = 4000f;
        cam.enabled = false;

        // 카메라 거리를 <b>여기 적어 두면 안 됩니다.</b> 옛 그림은 -320 m 에서 찍게
        // 박혀 있었는데, 높이를 34 m 에서 438 m 로 올린 뒤에도 그 거리 그대로라 화면에
        // 다리 밑동만 들어왔습니다. 화각이 38° 이므로 높이 h 를 담으려면
        // h / (2 tan 19°) ≈ h x 1.45 만큼 떨어져야 합니다.
        float height = all.size.y;
        float span = height * 1.6f;

        // 밑을 지나가며 보는 그림. 운전 게임에서 <b>가장 많이 보게 될 각</b>입니다.
        Shoot(cam, new Vector3(0f, 2.2f, all.center.z - all.extents.z * 0.9f),
              new Vector3(0f, all.min.y + height * 0.22f, all.center.z + all.extents.z * 0.6f),
              "under");

        // 옆에서 본 단면. 골조와 캡슐이 갈려 보이는지는 여기서 판단합니다.
        Shoot(cam, new Vector3(-span, height * 0.5f, all.center.z),
              new Vector3(0f, height * 0.5f, all.center.z), "side");

        // <b>갤러리와 방.</b> 캡슐 안이 방이 되었는지는 여기서만 보입니다 - 멀리서
        // 보면 캡슐이 꽂혔는지까지만 알 수 있고, 그 안이 창고인지 집인지는 모릅니다.
        Renderer gallery = spine.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(r => r.name.Contains("_Room"));

        if (gallery != null)
        {
            Bounds room = gallery.bounds;
            Vector3 back = (room.center - new Vector3(0f, room.center.y, room.center.z));
            back = back.sqrMagnitude > 1f ? back.normalized : Vector3.right;

            Shoot(cam, room.center + back * 52f + Vector3.up * 20f,
                  room.center + back * 4f, "rooms");
        }

        // <b>운전 눈높이의 노면.</b> 이 게임에서 가장 오래 보게 될 화면이고,
        // 차선·연석·등주가 <b>속도를 느끼게 하는지</b>는 여기서만 판단됩니다.
        // 옆에서 본 그림에서는 노면이 검은 띠 하나로만 보여 아무것도 알 수 없습니다.
        // 바운드의 최저점은 <b>파묻힌 다리 끝</b>(-16 m)이지 지면이 아닙니다. 여기에
        // 데크 높이만 더했더니 카메라가 데크 밑 다리 사이에 섰습니다.
        float road = all.min.y + manifest.burial + manifest.deckTop;

        Shoot(cam, new Vector3(-11f, road + 1.4f, all.center.z - all.extents.z * 0.86f),
              new Vector3(-4f, road + 1.1f, all.center.z + all.extents.z * 0.4f), "road");

        // <b>올려다보는 그림.</b> 넓이는 옆에서 보면 알지만 높이는 밑에서 봐야 압니다.
        // 사람이 서는 자리에서 찍어야 층이 몇 겹인지가 화면에 나옵니다.
        Shoot(cam, new Vector3(span * 0.22f, 1.7f, all.center.z + all.extents.z * 0.34f),
              new Vector3(0f, height * 0.7f, all.center.z + all.extents.z * 0.1f), "up");

        Debug.Log($"MegastructureSetup: 미리보기 저장 — {ShotDir}");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>
    /// 세 머티리얼을 <c>MartWall</c> 의 <b>변형</b>으로 둡니다.
    ///
    /// 툰 음영 · 빗금 · 거리 페이드를 부모 한 곳에서 관리하기 위해서입니다. 워커가
    /// 쓰는 것과 같은 부모라, 부모를 만지면 로봇과 구조물이 함께 움직입니다.
    ///
    /// 인스턴싱을 켭니다. 스파인은 같은 메시를 스물네 번 놓으므로, 켜지 않으면
    /// 드로우 호출이 그대로 스물네 배가 됩니다 — 이 프로젝트의 병목이 렌더 스레드의
    /// 드로우 제출입니다.
    /// </summary>
    private static Dictionary<string, Material> EnsureMaterials()
    {
        Material parent = AssetDatabase.LoadAssetAtPath<Material>(ParentMaterialPath);
        if (parent == null) throw new Exception("부모 머티리얼이 없습니다: " + ParentMaterialPath);

        Dictionary<string, Material> map = new Dictionary<string, Material>();

        foreach (BrutalistTextureSetup.Surface surface in Surfaces)
        {
            string path = MaterialDir + "/" + surface.Asset + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(parent.shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.parent = parent;

            // <b>칠하는 것은 팔레트가 합니다.</b> 여기서 색을 쓰면 표가 둘이 됩니다.
            BrutalistTextureSetup.LoadMap(surface.Texture, out Texture2D grain, out Color gain);
            BrutalistTextureSetup.Paint(material, surface, grain, gain);

            map[surface.Fbx] = material;
        }

        return map;
    }

    private static void Configure(string path, Dictionary<string, Material> materials)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new Exception("FBX 를 열지 못했습니다: " + path);

        // 블렌더에서 bake_space_transform 을 켜고 내보내므로 배율은 1 입니다.
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.weldVertices = false;

        // 라이트맵을 굽지 않는 프로젝트이므로 uv2 를 만들지 않습니다.
        // (LightmapPolicySetup 참조 — 셰이더가 라이트맵을 읽지 않습니다.)
        importer.generateSecondaryUV = false;

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

        foreach (KeyValuePair<string, Material> pair in materials)
        {
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        }

        importer.SaveAndReimport();
    }

    /// <summary>
    /// 거친 판본을 <b>원래 프리팹의 LOD 1</b>로 붙입니다.
    ///
    /// 데시메이션이 아니라 <b>안 지은 것</b>입니다 - 상자로 만든 것을 깎으면 각이
    /// 뭉개져 삼각형 죽이 됩니다. 만드는 쪽이 구조를 알므로, 멀리서 안 읽히는
    /// 것(갤러리 · 끝면 격자)을 애초에 빼고 한 번 더 냅니다.
    ///
    /// 콜라이더는 달지 않습니다. LOD 1 은 <b>보이기만</b> 하는 것이고, 부딪히는
    /// 것은 늘 원래 파츠입니다.
    /// </summary>
    private static void AttachLod(string path)
    {
        string coarse = Path.GetFileNameWithoutExtension(path);
        string basePath = PrefabDir + "/" + coarse.Substring(0, coarse.Length - LodSuffix.Length)
                          + ".prefab";

        if (!File.Exists(basePath))
        {
            Debug.LogError("MegastructureSetup: LOD 를 붙일 프리팹이 없습니다: " + basePath);
            return;
        }

        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        MeshFilter source = fbx.GetComponentInChildren<MeshFilter>(true);
        Renderer sourceRenderer = fbx.GetComponentInChildren<Renderer>(true);

        if (source == null || sourceRenderer == null)
        {
            Debug.LogError("MegastructureSetup: 거친 판본에 메시가 없습니다: " + path);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(basePath);

        try
        {
            Transform old = root.transform.Find(LodSuffix);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            Renderer[] near = root.GetComponentsInChildren<Renderer>(true);

            GameObject far = new GameObject(LodSuffix);
            far.transform.SetParent(root.transform, false);
            far.layer = root.layer;

            GameObjectUtility.SetStaticEditorFlags(far,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);

            far.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            MeshRenderer renderer = far.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = sourceRenderer.sharedMaterials;

            LODGroup group = root.GetComponent<LODGroup>();
            if (group == null) group = root.AddComponent<LODGroup>();

            group.SetLODs(new[]
            {
                new LOD(0.30f, near),
                // 두 번째는 <b>사라지지 않게</b> 합니다. 0.30 짜리 하나만 두면
                // 300 m 밖에서 시타델이 통째로 없어집니다.
                new LOD(0.001f, new[] { (Renderer)renderer }),
            });

            // ⚠ RecalculateBounds() 를 부르면 안 됩니다. 진짜 크기(505 m)로 덮어써
            // 갈아타는 거리가 사라집니다.
            group.size = LodSize;

            PrefabUtility.SaveAsPrefabAsset(root, basePath);

            int nearTris = near.Sum(r => r.GetComponent<MeshFilter>() != null
                ? r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3 : 0);

            Debug.Log($"MegastructureSetup: {coarse} → LOD · " +
                      $"{nearTris:N0} → {source.sharedMesh.triangles.Length / 3:N0} tris · " +
                      $"기준 크기 {LodSize:F0} m (화각 66° 에서 300 m 에 갈아탐)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject BuildBay(string path)
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (fbx == null) throw new Exception("FBX 프리팹을 열지 못했습니다: " + path);

        string name = Path.GetFileNameWithoutExtension(path);
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);

        try
        {
            root.name = name;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 0;

                // 굽지 않으므로 ContributeGI 는 빼고, 배칭·오클루전만 켭니다.
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic);

                MeshFilter filter = t.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                // 다리 사이를 지나갈 수 있어야 하므로 상자가 아니라 메시 콜라이더입니다.
                // 상자면 통로가 막혀 <b>밑으로 지나간다</b>는 뜻이 통째로 사라집니다.
                MeshCollider collider = t.GetComponent<MeshCollider>();
                if (collider == null) collider = t.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
            }

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// 프리셋을 <b>순서대로</b> 이어 스파인 하나를 만듭니다.
    ///
    /// <b>변화는 잡음이 아니라 순서에서 옵니다.</b> 맨 골조가 이어지다 거주 구간이 오고,
    /// 설비가 붙고, 분기가 갈라지고, 한 번 초거대 덩어리를 지나 다시 맨 골조로
    /// 돌아갑니다. 값을 흔들어 만든 비슷한 조각을 늘어놓는 것과는 다른 결과입니다.
    ///
    /// 규칙은 셋뿐입니다:
    ///   · 양 끝은 맨 골조입니다. 세계 밖으로 나가는 부분에 프로그램을 쓰면 낭비입니다.
    ///   · 큰 조각(2 베이 이상)끼리는 붙이지 않습니다. 사이에 맨 골조가 들어가야
    ///     각각이 <b>사건</b>으로 읽힙니다.
    ///   · 초거대는 한 번만. 두 번 나오면 초거대가 아닙니다.
    /// </summary>
    private static void BuildSpine(Manifest manifest, GameObject core,
                                   Dictionary<string, GameObject> made)
    {
        List<Preset> order = Sequence(manifest);

        GameObject root = new GameObject("MegaSpine");

        try
        {
            float total = order.Sum(p => p.length);
            float at = -total * 0.5f;
            int cores = 0;

            // 스파인이 z 로 흐르게 돌립니다. 블렌더에서는 x 가 진행 방향이었습니다.
            Quaternion turn = Quaternion.Euler(0f, 90f, 0f);

            foreach (Preset preset in order)
            {
                // <b>뼈대는 베이마다.</b> 같은 메시가 반복되므로 유니티가 인스턴싱할
                // 수 있고, 콜라이더도 베이 단위로 쪼개집니다.
                for (int b = 0; b < preset.bays; b++)
                {
                    GameObject bay = (GameObject)PrefabUtility.InstantiatePrefab(core);
                    bay.transform.SetParent(root.transform, false);
                    bay.transform.localPosition =
                        new Vector3(0f, 0f, at + (b + 0.5f) * manifest.bay);
                    bay.transform.localRotation = turn;
                    cores++;
                }

                // 프리셋이 더하는 것은 그 한가운데에 하나.
                GameObject part = (GameObject)PrefabUtility.InstantiatePrefab(made[preset.mesh]);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = new Vector3(0f, 0f, at + preset.length * 0.5f);
                part.transform.localRotation = turn;

                at += preset.length;
            }

            PrefabUtility.SaveAsPrefabAsset(root, SpinePath);

            Debug.Log($"MegastructureSetup: 스파인 {order.Count} 프리셋 · 뼈대 {cores} 베이 · " +
                      $"{total:F0} m — " + string.Join(" ", order.Select(p => p.name)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static List<Preset> Sequence(Manifest manifest)
    {
        Dictionary<string, Preset> byName = manifest.presets.ToDictionary(p => p.name);

        if (!byName.TryGetValue("Viaduct", out Preset link))
        {
            throw new Exception("이어 주는 Viaduct 프리셋이 없습니다");
        }

        // 길이가 베이의 정수배가 아니면 격자가 깨집니다. 여기서 멈추는 편이 낫습니다.
        foreach (Preset preset in manifest.presets)
        {
            float bays = preset.length / manifest.bay;
            if (Mathf.Abs(bays - Mathf.Round(bays)) > 0.01f)
            {
                throw new Exception($"{preset.name} 의 길이 {preset.length:F2} m 가 " +
                                    $"베이 {manifest.bay:F1} m 의 정수배가 아닙니다");
            }
        }

        // <b>맨 골조도 후보에 남깁니다.</b> 처음에 이것을 빼 두었더니 가운데가 통째로
        // 프로그램으로 차서, 사이가 없어 각 구간이 사건으로 안 읽혔습니다.
        // 무게 5 로 가장 흔한 조각이 되어 사이를 벌립니다.
        //
        // 초거대는 여기서 뽑지 않습니다. 무작위로 뽑으면 끝에 붙어 세계 밖으로
        // 나갈 수 있는데, 한 번뿐인 지표를 못 보게 되는 것은 그냥 손해입니다.
        List<Preset> pool = manifest.presets.Where(p => p.bays < 3).ToList();
        Preset huge = manifest.presets.OrderByDescending(p => p.bays).First();

        // <b>경사로도 자리를 정해 줍니다.</b> 데크로 올라가는 <b>유일한 길</b>인데,
        // 무작위로 뽑았더니 세계 밖으로 나가는 끝자락에 떨어져 올라갈 방법이 하나도
        // 없는 배치가 나왔습니다. 한 번뿐인 지표와 같은 이유입니다.
        Preset way = pool.FirstOrDefault(p => p.name == "Ramp");
        if (way != null) pool.Remove(way);

        // <b>모든 프리셋이 적어도 한 번은 나옵니다.</b> 무게만으로 뽑았더니 열 종을
        // 만들어 두고 일곱 종만 나온 적이 있습니다. 어휘를 만들어 놓고 보여 주지
        // 않는 것은 그냥 손해라, 한 벌을 먼저 깔고 나머지를 무게로 채웁니다.
        List<Preset> picks = pool.Where(p => p.name != "Viaduct").ToList();

        System.Random rng = new System.Random(20260908);
        float budget = SpineLength - huge.length - link.length * 6f;
        float used = picks.Sum(p => p.length);

        while (used < budget)
        {
            int total = pool.Sum(p => Mathf.Max(1, p.weight));
            int roll = rng.Next(total);
            Preset pick = pool[0];

            foreach (Preset candidate in pool)
            {
                roll -= Mathf.Max(1, candidate.weight);
                if (roll < 0) { pick = candidate; break; }
            }

            picks.Add(pick);
            used += pick.length;
        }

        for (int i = picks.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (picks[i], picks[j]) = (picks[j], picks[i]);
        }

        List<Preset> order = new List<Preset> { link, link };

        foreach (Preset pick in picks)
        {
            // 큰 조각끼리는 붙이지 않습니다. 사이에 맨 골조가 들어가야 각각이
            // <b>사건</b>으로 읽힙니다.
            if (pick.bays >= 2 && order[order.Count - 1].bays >= 2) order.Add(link);

            order.Add(pick);
        }

        order.Add(link);
        order.Add(link);

        // 초거대는 <b>한가운데</b>에 끼웁니다. 세계를 가로지르는 스파인의 중간이므로
        // 어느 방향에서 와도 보이고, 양옆에 맨 골조를 붙여 홀로 서게 합니다.
        int middle = order.Count / 2;
        order.InsertRange(middle, new[] { link, huge, link });

        // 경사로는 세계 안쪽, 초거대와 반대편 사분점에 둡니다. 어디서 출발하든
        // 올라갈 자리가 하나는 손에 닿습니다.
        if (way != null) order.InsertRange(order.Count / 4, new[] { link, way, link });

        return order;
    }

    private static Bounds Measure(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static void Shoot(Camera cam, Vector3 from, Vector3 at, string name)
    {
        cam.transform.position = from;
        cam.transform.LookAt(at);

        RenderTexture rt = new RenderTexture(1600, 700, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;

        for (int i = 0; i < 3; i++)
        {
            cam.Render();
            while (ShaderUtil.anythingCompiling) System.Threading.Thread.Sleep(50);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        shot.Apply();

        RenderTexture.active = previous;
        cam.targetTexture = null;

        File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), shot.EncodeToPNG());

        UnityEngine.Object.DestroyImmediate(shot);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }
}
