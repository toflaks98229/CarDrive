using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 세계에 서 있는 소품의 <b>메시만</b> 블렌더가 만든 것으로 갈아 끼웁니다.
///
/// <b>왜 갈아야 하는가.</b> 집과 바위는 코드로 만든 <c>.asset</c> 메시인데, 만들던
/// 도구는 커밋 <c>1170605</c> 가 지웠습니다. 즉 <b>다시 만들 길이 없는 동결 자산</b>이고,
/// 아트 기조가 브루탈리즘으로 정해진 뒤에도 손댈 수가 없었습니다. 블렌더 경로
/// (스크립트 → FBX → 셋업)는 스크립트가 저장소에 남으므로 언제든 다시 만듭니다.
///
/// <b>치수는 바꿀 수 없습니다.</b> 씬에 소품의 위치·회전이 이미 박혀 있고 월드는
/// 동결입니다. 새 메시가 옛 것보다 크면 이웃을 파고들고 작으면 뜹니다. 그래서 먼저
/// <see cref="Survey"/> 로 옛 봉투를 재고, 블렌더는 <b>그 봉투 안에서</b> 만듭니다.
///
/// <b>프리팹은 새로 만들지 않습니다.</b> 씬 인스턴스가 프리팹을 GUID 로 참조합니다.
/// 같은 경로에 새로 저장하면 GUID 가 바뀌어 씬의 소품이 전부 사라집니다. 그래서
/// 프리팹 내용을 열어 메시·머티리얼·콜라이더만 바꾸고 뿌리는 건드리지 않습니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod PropMeshSetup.Survey
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod PropMeshSetup.Run
/// </code>
/// </summary>
public static class PropMeshSetup
{
    // --- Constants ---

    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";
    private const string ParentMaterialPath = MaterialDir + "/MartWall.mat";

    /// <summary>콜라이더를 어떻게 둘지입니다. 옛 프리팹이 쓰던 것을 그대로 지킵니다.</summary>
    private enum Hull
    {
        /// <summary>상자. 차가 부딪히는 것은 벽이고 5 m 위의 처마는 어차피 안 닿습니다.</summary>
        Box,

        /// <summary>볼록 메시. 바위처럼 모난 것은 상자로 감싸면 헛부딪힘이 생깁니다.</summary>
        Convex,
    }

    private const string InteriorDir = "Assets/_Project/04.Art/02.Models/Interior";

    /// <summary>
    /// 씬에 <b>직접 지어진</b> 방과 그것을 대신할 메시입니다.
    ///
    /// 상점과 집은 프리팹이 아니라 씬 오브젝트라 프리팹 교체가 통하지 않습니다.
    /// 껍데기(바닥·벽·천장·지붕)만 갈고 <b>안에 있는 것은 손대지 않습니다</b> —
    /// 계산대·진열대·점원·침대는 벽에 맞춰 놓여 있으므로, 안쪽 치수를 그대로 두는
    /// 한 그대로 쓸 수 있습니다.
    /// </summary>
    private static readonly (string group, string mesh)[] Rooms =
    {
        ("VillageMart", "SM_Room_Mart"),
        ("PlayerHome", "SM_Room_Home"),
    };

    /// <summary>새 껍데기가 대신하는, 씬에 있던 부품 이름입니다.</summary>
    private static readonly string[] ShellParts =
    {
        "Floor", "Ceiling", "Roof", "Wall_N", "Wall_S", "Wall_E", "Wall_W",
    };

    /// <summary>갈아 끼울 소품 무리입니다. 프리팹 폴더 · FBX 폴더 · 콜라이더 방식.</summary>
    private static readonly (string prefabs, string models, Hull hull)[] Families =
    {
        ("Assets/_Project/05.Prefabs/Prop/House", "Assets/_Project/04.Art/02.Models/Building", Hull.Box),
        ("Assets/_Project/05.Prefabs/Prop/Rock", "Assets/_Project/04.Art/02.Models/Rock", Hull.Convex),
    };

    /// <summary>
    /// FBX 안의 이름 → 프로젝트 머티리얼입니다.
    ///
    /// <b>값이 있으면 이 도구가 주인이고, 없으면 빌려 쓰는 것입니다.</b> 콘크리트 세 층은
    /// 메가스트럭처와 같은 것을 여기서도 씁니다 — 새로 만들면 같은 콘크리트가 건물일
    /// 때와 구조물일 때 갈라지고 드로우 호출도 늘어납니다. 반대로 <c>RockLowPoly</c> 는
    /// 이미 조정된 값을 갖고 있으므로 <b>덮어쓰지 않고 물리기만</b> 합니다 — 메시를
    /// 바꿔 달라는 것이지 바위 색을 바꿔 달라는 것이 아니었습니다.
    /// </summary>
    /// <summary>
    /// 색은 <see cref="BrutalistTextureSetup.Palette"/> 가 정합니다.
    ///
    /// 여기에 같은 표를 따로 두고 있었는데 <b>1군 이전 값에 멈춰</b> 있었습니다.
    /// 셋 다 머티리얼에 색을 쓰므로 나중에 도는 쪽이 이기고, 이것을 마지막에
    /// 돌린 날에는 건물 안 방만 옛 콘크리트가 되었습니다.
    /// </summary>
    private static IEnumerable<BrutalistTextureSetup.Surface> Surfaces
    {
        get { return BrutalistTextureSetup.Palette.Where(s => s.Fbx != null); }
    }

    // --- Public Methods ---

    /// <summary>옛 소품의 봉투와 씬에서 쓰이는 수를 잽니다. 블렌더가 맞춰야 할 값입니다.</summary>
    public static void Survey()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Dictionary<GameObject, int> used = Count(scene);

        foreach ((string prefabs, string models, Hull hull) in Families)
        {
            Debug.Log($"PropMeshSetup: {prefabs} — 블렌더가 이 봉투 안에서 만들어야 합니다");

            foreach (string path in Directory.GetFiles(prefabs, "*.prefab").OrderBy(p => p))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                if (prefab == null) continue;

                MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length == 0) continue;

                Bounds local = Local(filters);
                int tris = filters.Where(f => f.sharedMesh != null)
                    .Sum(f => f.sharedMesh.triangles.Length / 3);

                string mats = string.Join(", ", prefab.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials)
                    .Where(m => m != null).Select(m => m.name).Distinct());

                used.TryGetValue(prefab, out int count);

                Debug.Log($"  {prefab.name,-10} {local.size.x,5:F2} × {local.size.z,5:F2} × {local.size.y,5:F2} m · " +
                          $"바닥 {local.min.y,6:F2} · {tris,4} tris · 씬에 {count,4} 개 · {mats}");
            }
        }

        SurveyRooms(scene);

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>
    /// 씬에 <b>직접 지어진</b> 방들을 잽니다. 프리팹이 아니라 씬 오브젝트입니다.
    ///
    /// 상점과 집 안쪽은 프리팹으로 묶여 있지 않고 벽·바닥·천장이 씬에 흩어져 있습니다.
    /// 갈아 끼우려면 <b>안쪽 치수</b>를 알아야 합니다 — 바깥 봉투만 맞추면 문이 벽을
    /// 향하거나 진열대가 벽에 박힙니다.
    /// </summary>
    private static void SurveyRooms(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "VillageMart" && t.name != "PlayerHome") continue;

                Renderer[] all = t.GetComponentsInChildren<Renderer>(true);
                if (all.Length == 0) continue;

                Bounds box = all[0].bounds;
                for (int i = 1; i < all.Length; i++) box.Encapsulate(all[i].bounds);

                Debug.Log($"PropMeshSetup: {t.name} — 바깥 {box.size.x:F2} × {box.size.z:F2} × " +
                          $"{box.size.y:F2} m · 바닥 {box.min.y:F2} · 자리 " +
                          $"({t.position.x:F1}, {t.position.z:F1}) · 부품 {all.Length} 개");

                // <b>파일로 씁니다.</b> Debug.Log 는 배치모드 로그에서 잘려 나가
                // 부품을 몇 개만 보고 판단하게 됩니다 - 실제로 벽을 못 보고 지나쳤습니다.
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                // <b>손자까지 봅니다.</b> 벽은 문 둘레로 쪼개져 자식에 렌더러를 답니다 -
                // 직계 자식만 보다가 벽을 통째로 놓쳤습니다.
                foreach (Transform part in t.GetComponentsInChildren<Transform>(true))
                {
                    if (part == t) continue;

                    Renderer r = part.GetComponent<Renderer>();
                    MeshFilter f = part.GetComponent<MeshFilter>();
                    if (r == null) continue;

                    Vector3 local = t.InverseTransformPoint(r.bounds.center);

                    string path = part.name;
                    for (Transform up = part.parent; up != null && up != t; up = up.parent)
                    {
                        path = up.name + "/" + path;
                    }

                    sb.AppendLine($"{path,-28} 크기 {r.bounds.size.x,6:F2} × {r.bounds.size.z,6:F2} × " +
                                  $"{r.bounds.size.y,6:F2} · 가운데({local.x,6:F2},{local.y,5:F2},{local.z,6:F2}) · " +
                                  $"{(f != null && f.sharedMesh != null ? f.sharedMesh.vertexCount : 0),4}v " +
                                  $"{(f != null && f.sharedMesh != null ? f.sharedMesh.triangles.Length / 3 : 0),4}tri · " +
                                  $"{(r.sharedMaterial != null ? r.sharedMaterial.name : "-")}");
                }

                System.IO.Directory.CreateDirectory("Logs/Rooms");
                System.IO.File.WriteAllText("Logs/Rooms/" + t.name + ".txt", sb.ToString());
            }
        }
    }

    public static void Run()
    {
        int errors = 0;

        try
        {
            Dictionary<string, Material> materials = EnsureMaterials();

            foreach ((string prefabs, string models, Hull hull) in Families)
            {
                if (!Directory.Exists(models))
                {
                    Debug.Log($"PropMeshSetup: {models} 이 아직 없어 건너뜁니다");
                    continue;
                }

                foreach (string path in Directory.GetFiles(prefabs, "*.prefab").OrderBy(p => p))
                {
                    Swap(path.Replace('\\', '/'), models, hull, materials);
                }
            }

            SwapRooms(materials);

            AssetDatabase.SaveAssets();
            Debug.Log("PropMeshSetup: 완료");
        }
        catch (Exception e)
        {
            Debug.LogError("PropMeshSetup: " + e);
            errors++;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    // --- Private Methods ---

    private static Dictionary<string, Material> EnsureMaterials()
    {
        Material parent = AssetDatabase.LoadAssetAtPath<Material>(ParentMaterialPath);
        if (parent == null) throw new Exception("부모 머티리얼이 없습니다: " + ParentMaterialPath);

        Dictionary<string, Material> map = new Dictionary<string, Material>();

        foreach (BrutalistTextureSetup.Surface surface in Surfaces)
        {
            string path = MaterialDir + "/" + surface.Asset + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (surface.Value == null)
            {
                // 빌려 쓰는 것. 없으면 만들지 않고 멈춥니다 — 조용히 새로 만들면
                // 조정해 둔 값이 사라진 것을 아무도 모릅니다.
                if (material == null) throw new Exception("머티리얼이 없습니다: " + path);

                map[surface.Fbx] = material;
                continue;
            }

            if (material == null)
            {
                material = new Material(parent.shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.parent = parent;

            BrutalistTextureSetup.LoadMap(surface.Texture, out Texture2D grain, out Color gain);
            BrutalistTextureSetup.Paint(material, surface, grain, gain);

            map[surface.Fbx] = material;
        }

        return map;
    }

    /// <summary>
    /// 씬의 방 껍데기를 갈아 끼웁니다.
    ///
    /// 옛 부품은 <b>지우지 않고 끕니다.</b> 지우면 되돌릴 때 씬 파일을 통째로
    /// 되감아야 하는데, 꺼 두면 껍데기 하나만 지우면 원래대로 돌아옵니다. 껍데기가
    /// 마음에 안 들 가능성이 남아 있는 동안에는 그쪽이 낫습니다.
    /// </summary>
    private static void SwapRooms(Dictionary<string, Material> materials)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        bool touched = false;

        foreach ((string group, string mesh) in Rooms)
        {
            string modelPath = InteriorDir + "/" + mesh + ".fbx";

            if (!File.Exists(modelPath))
            {
                Debug.Log($"PropMeshSetup: {mesh} 의 FBX 가 없어 건너뜁니다");
                continue;
            }

            Transform room = Find(scene, group);
            if (room == null)
            {
                Debug.Log($"PropMeshSetup: 씬에 {group} 이 없습니다");
                continue;
            }

            Configure(modelPath, materials);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            MeshFilter source = model.GetComponentInChildren<MeshFilter>(true);
            Renderer sourceRenderer = model.GetComponentInChildren<Renderer>(true);

            foreach (string name in ShellParts)
            {
                Transform part = room.Find(name);
                if (part != null) part.gameObject.SetActive(false);
            }

            Transform shell = room.Find("Shell");
            if (shell == null)
            {
                shell = new GameObject("Shell").transform;
                shell.SetParent(room, false);
            }

            shell.gameObject.layer = room.gameObject.layer;
            shell.localPosition = Vector3.zero;
            shell.localRotation = Quaternion.identity;

            MeshFilter filter = shell.GetComponent<MeshFilter>();
            if (filter == null) filter = shell.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = source.sharedMesh;

            MeshRenderer renderer = shell.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = shell.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = sourceRenderer.sharedMaterials;

            // 안으로 걸어 들어가야 하므로 상자가 아니라 메시 콜라이더입니다.
            MeshCollider collider = shell.GetComponent<MeshCollider>();
            if (collider == null) collider = shell.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = source.sharedMesh;
            collider.convex = false;

            touched = true;

            Debug.Log($"PropMeshSetup: {group} ← {mesh} · " +
                      $"{source.sharedMesh.triangles.Length / 3} tris · 옛 껍데기 끔");
        }

        if (!touched) return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
        }

        return null;
    }

    private static void Swap(string prefabPath, string modelDir, Hull hull,
                             Dictionary<string, Material> materials)
    {
        string name = Path.GetFileNameWithoutExtension(prefabPath);
        string modelPath = modelDir + "/SM_" + name + ".fbx";

        if (!File.Exists(modelPath))
        {
            Debug.Log($"PropMeshSetup: {name} 의 FBX 가 없어 건너뜁니다");
            return;
        }

        Configure(modelPath, materials);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        MeshFilter source = model.GetComponentInChildren<MeshFilter>(true);
        Renderer sourceRenderer = model.GetComponentInChildren<Renderer>(true);

        if (source == null || source.sharedMesh == null) throw new Exception("메시가 없습니다: " + modelPath);

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            // 옛 집은 지붕이 따로 있었습니다. 새 메시는 하나로 나오므로 자식을 걷습니다.
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            MeshFilter filter = root.GetComponent<MeshFilter>();
            if (filter == null) filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = source.sharedMesh;

            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = sourceRenderer.sharedMaterials;

            Bounds bounds = source.sharedMesh.bounds;
            Fit(root, hull, source.sharedMesh, bounds);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            Debug.Log($"PropMeshSetup: {name} ← {Path.GetFileName(modelPath)} · " +
                      $"{source.sharedMesh.triangles.Length / 3} tris · " +
                      $"{bounds.size.x:F2} × {bounds.size.z:F2} × {bounds.size.y:F2} m · {hull}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>옛 프리팹이 쓰던 콜라이더 종류를 그대로 지키며 새 메시에 맞춥니다.</summary>
    private static void Fit(GameObject root, Hull hull, Mesh mesh, Bounds bounds)
    {
        if (hull == Hull.Box)
        {
            MeshCollider stale = root.GetComponent<MeshCollider>();
            if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

            BoxCollider box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();

            box.center = bounds.center;
            box.size = bounds.size;
            return;
        }

        BoxCollider staleBox = root.GetComponent<BoxCollider>();
        if (staleBox != null) UnityEngine.Object.DestroyImmediate(staleBox);

        MeshCollider collider = root.GetComponent<MeshCollider>();
        if (collider == null) collider = root.AddComponent<MeshCollider>();

        collider.sharedMesh = mesh;
        collider.convex = true;
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
        importer.generateSecondaryUV = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

        foreach (KeyValuePair<string, Material> pair in materials)
        {
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        }

        importer.SaveAndReimport();
    }

    /// <summary>프리팹 뿌리 기준의 봉투입니다. 씬의 위치를 빼야 비교가 됩니다.</summary>
    private static Bounds Local(MeshFilter[] filters)
    {
        bool first = true;
        Bounds box = new Bounds();

        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null) continue;

            Bounds mesh = filter.sharedMesh.bounds;
            Bounds moved = new Bounds(mesh.center + filter.transform.localPosition, mesh.size);

            if (first) { box = moved; first = false; }
            else box.Encapsulate(moved);
        }

        return box;
    }

    /// <summary>씬에서 각 프리팹이 몇 번 쓰였는지입니다.</summary>
    private static Dictionary<GameObject, int> Count(Scene scene)
    {
        Dictionary<GameObject, int> map = new Dictionary<GameObject, int>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;

                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source == null) continue;

                map.TryGetValue(source, out int n);
                map[source] = n + 1;
            }
        }

        return map;
    }
}
