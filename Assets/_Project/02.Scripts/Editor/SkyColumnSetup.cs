using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <b>대기권을 뚫는 기둥</b>을 지형 위에 세웁니다.
///
/// 하늘을 덮은 천장은 스카이맵이 맡습니다. 그런데 스카이맵은 시차가 없어
/// <b>다가갈 수 없습니다</b> — 차를 몰아 가까이 가도 그대로입니다. 그래서 하나는
/// 진짜 기하여야 합니다. 다가갈 수 있는 기둥이 하나 있으면 하늘의 나머지도
/// 그림이 아니라 <b>같은 세계의 먼 부분</b>으로 읽힙니다.
///
/// <b>파클립을 건드리지 않습니다.</b> 기둥은 451 m 인데 파클립은 482 m 라, 200 m
/// 밖에서 올려다보면 꼭대기가 잘릴 자리입니다. 그런데 이 게임의 안개는 257 m 에서
/// 완전히 닫히므로 <b>잘리는 자리는 이미 안개뿐</b>입니다 — 원경 지표를 고치며
/// 맞춰 둔 안개·클립 사다리를 하나도 안 만지고 끝납니다.
///
/// 먼 기둥이 안개에 지워지는 것은 손해가 아닙니다. 그 자리를 스카이맵의 기둥들이
/// 채우고, 가까워지면 진짜 기둥이 안개에서 걸어 나옵니다.
///
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod SkyColumnSetup.Run -quit
/// </code>
/// </summary>
public static class SkyColumnSetup
{
    private const string Fbx = "Assets/_Project/04.Art/02.Models/World/M_SkyColumn.fbx";
    private const string PrefabPath = "Assets/_Project/05.Prefabs/World/SkyColumn.prefab";
    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string HolderName = "SkyColumns";

    /// <summary>세울 개수입니다. 세계가 1100 x 1200 m 이고 안개가 257 m 에서 닫히므로,
    /// 일곱이면 어디에 서 있어도 대개 하나는 안개 안에 들어옵니다.</summary>
    private const int Count = 7;

    /// <summary>
    /// 기둥끼리 이만큼은 떨어집니다. 붙으면 숲이 아니라 벽이 됩니다.
    ///
    /// 260 으로 두었더니 일곱 중 여섯만 들어갔습니다 — 세계가 직사각형이 아니라
    /// 타일 103 장의 들쭉날쭉한 모양이고, 거기서 시작 지점과 메가스트럭처
    /// 둘레를 빼면 남는 자리가 생각보다 좁습니다.
    /// </summary>
    private const float Apart = 230f;

    /// <summary>플레이어 시작 지점과 메가스트럭처에서 비켜설 거리입니다.</summary>
    private const float Clear = 150f;

    /// <summary>발이 지형에 묻히는 깊이. 지형이 울퉁불퉁해 0 이면 밑동이 뜹니다.</summary>
    private const float Sink = 7f;

    /// <summary>레이어 12. <c>TagManager</c> 의 "Landmark" 입니다.</summary>
    private const int LandmarkLayer = 12;

    /// <summary>없으면 만들어 두는 머티리얼의 본. 셰이더와 변형 부모가 딸려 옵니다.</summary>
    private const string Seed = MaterialDir + "/MegaConcrete.mat";

    public static void Run()
    {
        int errors = 0;

        try
        {
            if (!File.Exists(Fbx))
            {
                Debug.LogError("SkyColumnSetup: FBX 가 없습니다 — " + Fbx +
                               " (Art/Blender/build_column.py 를 먼저 돌립니다)");
                errors++;
            }
            else
            {
                GameObject prefab = BuildPrefab();
                errors += Place(prefab);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("SkyColumnSetup: " + e);
            errors++;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    /// <summary>
    /// FBX 를 프리팹으로 세웁니다. 재질은 <b>이름으로</b> 짝을 찾습니다.
    ///
    /// ⚠ <c>materialLocation</c> 은 <c>InPrefab</c> 이어야 합니다. <c>External</c> 로 두면
    /// FBX 안의 재질을 모델 옆 폴더로 꺼내 그것을 쓰고, <c>AddRemap</c> 으로 지정한
    /// 짝을 <b>덮어씁니다</b> — 결과는 URP Lit 이 붙은 프리팹입니다.
    /// </summary>
    private static GameObject BuildPrefab()
    {
        ModelImporter importer = AssetImporter.GetAtPath(Fbx) as ModelImporter;

        if (importer == null)
        {
            AssetDatabase.ImportAsset(Fbx, ImportAssetOptions.ForceSynchronousImport);
            importer = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        }

        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.weldVertices = false;
        importer.generateSecondaryUV = false;

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

        foreach (string name in new[]
                 { "M_Column_Concrete", "M_Column_Dark", "M_Column_Signal" })
        {
            string asset = MaterialDir + "/" + name.Replace("M_Column_", "Column") + ".mat";
            Material material = Ensure(asset);

            if (material == null) continue;

            importer.AddRemap(
                new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
        }

        importer.SaveAndReimport();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        GameObject root = UnityEngine.Object.Instantiate(model);
        root.name = "SkyColumn";

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            // <b>랜드마크 레이어입니다.</b> 451 m 짜리라 파클립(482 m) 밖으로
            // 나가는데, 파클립은 평면이라 잘리는 자리가 <b>시선의 상하 각도에 따라
            // 움직입니다</b> — 고개를 들면 기둥 끝이 잘려 나갔습니다.
            // ViewRangeScaler.ReachLandmarks 가 이 레이어만 더 멀리 그립니다.
            filter.gameObject.layer = LandmarkLayer;

            // 차가 통과하면 안 됩니다. 정적이고 618 면뿐이라 볼록화가 필요 없습니다.
            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        Debug.Log("SkyColumnSetup: 프리팹 — " + PrefabPath);

        return saved;
    }

    /// <summary>
    /// 머티리얼 에셋이 없으면 <see cref="Seed"/> 를 복사해 만듭니다.
    ///
    /// <b>색은 여기서 정하지 않습니다.</b> 껍데기만 만들어 두고, 값·결·안개는
    /// <c>BrutalistTextureSetup</c> 의 팔레트가 씁니다 — 이 프로젝트에서 겉모습의
    /// 주인은 한 곳뿐이고, 여기서 색을 쓰면 그 규칙이 깨집니다.
    /// </summary>
    private static Material Ensure(string path)
    {
        Material found = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (found != null) return found;

        if (!AssetDatabase.CopyAsset(Seed, path))
        {
            Debug.LogError("SkyColumnSetup: 머티리얼을 만들지 못했습니다 — " + path);
            return null;
        }

        Debug.Log("SkyColumnSetup: 머티리얼 껍데기 생성 — " + path +
                  " (값은 BrutalistTextureSetup 이 씁니다)");

        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    /// <summary>
    /// 지형 위에 흩습니다.
    ///
    /// <b>고르게가 아니라 떨어뜨려 놓습니다.</b> 격자로 두면 기둥이 아니라 울타리로
    /// 보이고, 완전히 무작위면 두 개가 겹쳐 하나로 보입니다. 최소 거리를 두고
    /// 뽑되 <b>씨앗을 고정</b>해, 다시 돌려도 같은 세계가 나오게 합니다.
    /// </summary>
    private static int Place(GameObject prefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Terrain[] tiles = UnityEngine.Object
            .FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (tiles.Length == 0)
        {
            Debug.LogError("SkyColumnSetup: 지형이 없습니다");
            return 1;
        }

        GameObject old = GameObject.Find(HolderName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        GameObject holder = new GameObject(HolderName);

        Transform spine = GameObject.Find("Megastructure")?.transform;
        Bounds mega = spine != null ? Around(spine) : new Bounds(Vector3.zero, Vector3.zero);

        GameObject player = GameObject.FindWithTag("Player");
        Vector3 start = player != null ? player.transform.position : Vector3.zero;

        System.Random rng = new System.Random(20260909);
        List<Vector3> taken = new List<Vector3>();

        float lowX = tiles.Min(t => t.transform.position.x);
        float lowZ = tiles.Min(t => t.transform.position.z);
        float spanX = tiles.Max(t => t.transform.position.x) + tiles[0].terrainData.size.x - lowX;
        float spanZ = tiles.Max(t => t.transform.position.z) + tiles[0].terrainData.size.z - lowZ;

        // 뽑기는 <b>넉넉히</b> 돌립니다. 최소 거리 규칙 때문에 실패가 잦습니다.
        for (int attempt = 0; attempt < 4000 && taken.Count < Count; attempt++)
        {
            Vector3 spot = new Vector3(
                lowX + (float)rng.NextDouble() * spanX, 0f,
                lowZ + (float)rng.NextDouble() * spanZ);

            Terrain under = Below(tiles, spot);
            if (under == null) continue;

            spot.y = under.SampleHeight(spot) + under.transform.position.y;

            if (Flat(spot, start) < Clear) continue;
            if (mega.size.sqrMagnitude > 0f && mega.SqrDistance(spot) < Clear * Clear) continue;
            if (taken.Any(t => Flat(spot, t) < Apart)) continue;

            taken.Add(spot);

            GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
            made.transform.position = spot + Vector3.down * Sink;
            made.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            made.name = "SkyColumn_" + taken.Count;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(string.Format(
            "SkyColumnSetup: {0} 개 세움 · 가장 가까운 둘 {1:F0} m · 시작 지점에서 {2:F0} m",
            taken.Count,
            taken.Count > 1 ? taken.SelectMany(
                (a, i) => taken.Skip(i + 1).Select(b => Flat(a, b))).Min() : 0f,
            taken.Count > 0 ? taken.Min(t => Flat(t, start)) : 0f));

        // 자리가 모자라 덜 세워지는 것은 <b>실패가 아닙니다.</b> 최소 거리를
        // 지키는 쪽이 개수를 채우는 쪽보다 중요합니다 - 붙은 기둥 둘은 하나로
        // 보입니다. 몇 개가 섰는지는 위에 남겼습니다.
        return 0;
    }

    /// <summary>높이를 뺀 거리입니다. 기둥 사이를 잴 때 높이는 뜻이 없습니다.</summary>
    private static float Flat(Vector3 a, Vector3 b)
    {
        return new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }

    private static Terrain Below(Terrain[] tiles, Vector3 spot)
    {
        foreach (Terrain t in tiles)
        {
            Vector3 p = t.transform.position;
            Vector3 size = t.terrainData.size;

            if (spot.x >= p.x && spot.x < p.x + size.x &&
                spot.z >= p.z && spot.z < p.z + size.z)
            {
                return t;
            }
        }

        return null;
    }

    private static Bounds Around(Transform spine)
    {
        Renderer[] parts = spine.GetComponentsInChildren<Renderer>(true);

        if (parts.Length == 0) return new Bounds(spine.position, Vector3.zero);

        Bounds all = parts[0].bounds;
        foreach (Renderer r in parts) all.Encapsulate(r.bounds);

        return all;
    }
}
