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
    private static readonly (string fbx, string asset, Color? value)[] Materials =
    {
        ("M_Mega_Concrete", "MegaConcrete", new Color(0.58f, 0.57f, 0.53f)),
        ("M_Mega_Steel", "MegaSteel", new Color(0.26f, 0.28f, 0.31f)),
        ("M_Mega_Dark", "MegaDark", new Color(0.09f, 0.10f, 0.11f)),
        ("M_Rock", "RockLowPoly", null),
    };

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

        if (Application.isBatchMode) EditorApplication.Exit(0);
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

        foreach ((string fbx, string asset, Color? value) in Materials)
        {
            string path = MaterialDir + "/" + asset + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (value == null)
            {
                // 빌려 쓰는 것. 없으면 만들지 않고 멈춥니다 — 조용히 새로 만들면
                // 조정해 둔 값이 사라진 것을 아무도 모릅니다.
                if (material == null) throw new Exception("머티리얼이 없습니다: " + path);

                map[fbx] = material;
                continue;
            }

            if (material == null)
            {
                material = new Material(parent.shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.parent = parent;
            material.SetColor("_BaseColor", value.Value);
            material.SetColor("_Color", value.Value);
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            map[fbx] = material;
        }

        return map;
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
