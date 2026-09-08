using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 세계에 서 있는 <b>집</b>을 블렌더가 만든 것으로 갈아 끼웁니다.
///
/// <b>왜 갈아야 하는가.</b> 지금 집들은 코드로 만든 <c>.asset</c> 메시인데, 만들던
/// 도구는 커밋 <c>1170605</c> 가 지웠습니다. 즉 <b>다시 만들 길이 없는 동결 자산</b>이고,
/// 아트 기조가 브루탈리즘으로 정해진 뒤에도 손댈 수가 없습니다. 메가스트럭처에서
/// 쓴 경로(블렌더 스크립트 → FBX → 셋업)는 스크립트가 저장소에 남으므로 언제든
/// 다시 만들 수 있습니다.
///
/// <b>치수는 바꿀 수 없습니다.</b> 씬에 집의 위치·회전이 이미 박혀 있습니다. 새 메시가
/// 옛 메시와 다른 크기면 땅에 묻히거나 뜨고, 문이 벽을 향합니다. 그래서 먼저
/// <see cref="Survey"/> 로 옛 치수를 재고, 블렌더는 <b>그 봉투 안에서</b> 만듭니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod BuildingSetup.Survey
/// </code>
/// </summary>
public static class BuildingSetup
{
    // --- Constants ---

    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string HouseDir = "Assets/_Project/05.Prefabs/Prop/House";
    private const string ModelDir = "Assets/_Project/04.Art/02.Models/Building";
    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";
    private const string ParentMaterialPath = MaterialDir + "/MartWall.mat";

    /// <summary>
    /// FBX 안의 이름 → 프로젝트 머티리얼입니다.
    ///
    /// <b>메가스트럭처와 같은 것을 씁니다.</b> 새 머티리얼을 만들면 같은 콘크리트가
    /// 건물일 때와 구조물일 때 갈라질 수 있고, 드로우 호출도 그만큼 늘어납니다.
    /// 값은 <see cref="MegastructureSetup"/> 이 정하고 결은
    /// <see cref="BrutalistTextureSetup"/> 이 물립니다.
    /// </summary>
    private static readonly (string fbx, string asset, Color value)[] Materials =
    {
        ("M_Mega_Concrete", "MegaConcrete", new Color(0.58f, 0.57f, 0.53f)),
        ("M_Mega_Steel", "MegaSteel", new Color(0.26f, 0.28f, 0.31f)),
        ("M_Mega_Dark", "MegaDark", new Color(0.09f, 0.10f, 0.11f)),
    };

    // --- Public Methods ---

    /// <summary>옛 집들의 봉투와 씬에서 쓰이는 수를 잽니다. 블렌더가 맞춰야 할 값입니다.</summary>
    public static void Survey()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Dictionary<GameObject, int> used = Count(scene);

        Debug.Log("BuildingSetup: 옛 집의 봉투 — 블렌더가 이 안에서 만들어야 합니다");

        foreach (string path in Directory.GetFiles(HouseDir, "*.prefab").OrderBy(p => p))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            if (prefab == null) continue;

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) continue;

            Bounds local = Local(filters);
            int tris = filters.Where(f => f.sharedMesh != null).Sum(f => f.sharedMesh.triangles.Length / 3);

            string parts = string.Join(", ", filters
                .Where(f => f.sharedMesh != null)
                .Select(f => $"{f.name}({f.sharedMesh.vertexCount}v)"));

            string mats = string.Join(", ", prefab.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null).Select(m => m.name).Distinct());

            used.TryGetValue(prefab, out int count);

            Debug.Log($"  {prefab.name,-10} {local.size.x,5:F2} × {local.size.z,5:F2} × {local.size.y,5:F2} m · " +
                      $"바닥 {local.min.y,6:F2} · {tris,4} tris · 씬에 {count,2} 채 · [{parts}] · {mats}");
        }

        Debug.Log($"  콜라이더·레이어는 프리팹이 갖고 있으므로 메시만 갈면 됩니다.");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>
    /// 여섯 채의 <b>알맹이만</b> 갈아 끼웁니다.
    ///
    /// <b>프리팹은 새로 만들지 않습니다.</b> 씬에 인스턴스 13 개가 이 프리팹을 GUID 로
    /// 참조하고 있습니다. 같은 경로에 새로 저장하면 GUID 가 바뀌어 씬의 집이 전부
    /// 사라집니다. 그래서 프리팹 내용을 <b>열어서</b> 메시·머티리얼·콜라이더만 바꾸고
    /// 뿌리 이름·레이어는 건드리지 않습니다.
    /// </summary>
    public static void Run()
    {
        int errors = 0;

        try
        {
            Dictionary<string, Material> materials = EnsureMaterials();

            foreach (string path in Directory.GetFiles(HouseDir, "*.prefab").OrderBy(p => p))
            {
                Swap(path.Replace('\\', '/'), materials);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("BuildingSetup: 완료");
        }
        catch (Exception e)
        {
            Debug.LogError("BuildingSetup: " + e);
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

        foreach ((string fbx, string asset, Color value) in Materials)
        {
            string path = MaterialDir + "/" + asset + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(parent.shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.parent = parent;
            material.SetColor("_BaseColor", value);
            material.SetColor("_Color", value);
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            map[fbx] = material;
        }

        return map;
    }

    private static void Swap(string prefabPath, Dictionary<string, Material> materials)
    {
        string name = Path.GetFileNameWithoutExtension(prefabPath);
        string modelPath = ModelDir + "/SM_" + name + ".fbx";

        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) throw new Exception("FBX 를 찾지 못했습니다: " + modelPath);

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

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        MeshFilter source = model.GetComponentInChildren<MeshFilter>(true);
        Renderer sourceRenderer = model.GetComponentInChildren<Renderer>(true);

        if (source == null || source.sharedMesh == null) throw new Exception("메시가 없습니다: " + modelPath);

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            // 옛 지붕은 따로 있던 오브젝트입니다. 새 메시는 지붕까지 하나이므로 걷습니다.
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

            // 콜라이더는 상자 그대로 둡니다. 차가 부딪히는 것은 벽이고, 5 m 위의
            // 처마는 어차피 닿지 않습니다. 메시 콜라이더로 바꾸면 13 채의 물리
            // 비용만 늘고 얻는 것이 없습니다.
            BoxCollider box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();

            Bounds bounds = source.sharedMesh.bounds;
            box.center = bounds.center;
            box.size = bounds.size;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            Debug.Log($"BuildingSetup: {name} ← {Path.GetFileName(modelPath)} · " +
                      $"{source.sharedMesh.triangles.Length / 3} tris · " +
                      $"{bounds.size.x:F2} × {bounds.size.z:F2} × {bounds.size.y:F2} m");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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
            Vector3 offset = filter.transform.localPosition;
            Bounds moved = new Bounds(mesh.center + offset, mesh.size);

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
