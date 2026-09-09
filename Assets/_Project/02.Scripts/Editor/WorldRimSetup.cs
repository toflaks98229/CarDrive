using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 세계가 끝나는 자리에 <b>테두리 골조</b>를 두릅니다.
///
/// 대지가 자연 지형이 아니라 거대 건축물의 인공 지반 한 조각이라면, 끝까지 갔을
/// 때 나와야 하는 것은 낭떠러지가 아니라 <b>잘린 단면</b>입니다. 그 너머의 구름은
/// 스카이맵의 아래쪽 반구가 이미 맡고 있으므로, 여기서 세울 것은 잘린 자리뿐입니다.
///
/// <b>25 m 조각으로 끊습니다.</b> <see cref="WorldEdgeReport"/> 로 재 보니 세계는
/// 100 m 타일 103 장이고 격자가 꽉 차 있지 않아 바깥면이 46 개인 들쭉날쭉한
/// 계단이었고, 한 면 안에서 지형이 중앙값 4.6 m·최대 18.4 m 오르내렸습니다.
/// 통짜로 두르면 어딘가는 뜨고 어딘가는 파묻힙니다. 넷으로 끊어 조각마다 제
/// 높이에 앉히면 남는 높이차가 연석 안에 들어오고, 조각 사이의 단차는 계단으로
/// 읽힙니다.
///
/// <b>조각은 그 자리의 가장 높은 점에 맞춥니다.</b> 평균에 맞추면 높은 쪽에
/// 틈이 생겨 <b>세계 밖이 비칩니다</b> - 낮은 쪽에 연석이 조금 더 서는 편이
/// 훨씬 싼 대가입니다.
///
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod WorldRimSetup.Run -quit
/// </code>
/// </summary>
public static class WorldRimSetup
{
    private const string Fbx = "Assets/_Project/04.Art/02.Models/World/M_WorldRim.fbx";
    private const string PrefabPath = "Assets/_Project/05.Prefabs/World/WorldRim.prefab";
    private const string MaterialDir = "Assets/_Project/04.Art/00.Materials";
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string HolderName = "WorldRim";

    /// <summary>한 바깥면(100 m)을 몇 조각으로 끊을지.</summary>
    private const int Slices = 4;

    public static void Run()
    {
        int errors = 0;

        try
        {
            if (!File.Exists(Fbx))
            {
                Debug.LogError("WorldRimSetup: FBX 가 없습니다 — " + Fbx +
                               " (Art/Blender/build_rim.py 를 먼저 돌립니다)");
                errors++;
            }
            else
            {
                errors += Place(Prefab());
            }
        }
        catch (Exception e)
        {
            Debug.LogError("WorldRimSetup: " + e);
            errors++;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    /// <summary>
    /// FBX 를 프리팹으로 세웁니다. <see cref="SkyColumnSetup"/> 과 같은 길입니다 —
    /// ⚠ <c>materialLocation</c> 이 <c>External</c> 이면 remap 이 덮여 URP Lit 이 붙습니다.
    /// </summary>
    private static GameObject Prefab()
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
                 { "M_Mega_Concrete", "M_Mega_Steel", "M_Mega_Dark", "M_Mega_Signal" })
        {
            string asset = MaterialDir + "/" + name.Replace("M_Mega_", "Mega") + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(asset);

            if (material == null)
            {
                Debug.LogError("WorldRimSetup: 재질이 없습니다 — " + asset);
                continue;
            }

            importer.AddRemap(
                new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
        }

        importer.SaveAndReimport();

        GameObject root = UnityEngine.Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
        root.name = "WorldRim";

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            filter.gameObject.layer = 0;

            // 연석이 차를 멈춰야 합니다. 180 면짜리 정적 메시라 그대로 씁니다.
            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        return saved;
    }

    private static int Place(GameObject prefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Terrain[] tiles = UnityEngine.Object
            .FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (tiles.Length == 0)
        {
            Debug.LogError("WorldRimSetup: 지형이 없습니다");
            return 1;
        }

        Vector3 size = tiles[0].terrainData.size;

        Dictionary<Vector2Int, Terrain> at = new Dictionary<Vector2Int, Terrain>();

        foreach (Terrain t in tiles)
        {
            Vector3 p = t.transform.position;
            at[new Vector2Int(Mathf.RoundToInt(p.x / size.x),
                              Mathf.RoundToInt(p.z / size.z))] = t;
        }

        GameObject old = GameObject.Find(HolderName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        GameObject holder = new GameObject(HolderName);

        Vector2Int[] around =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        int made = 0;
        float lowestTop = float.MaxValue;
        float tallestStep = 0f;

        foreach (KeyValuePair<Vector2Int, Terrain> pair in at)
        {
            foreach (Vector2Int step in around)
            {
                if (at.ContainsKey(pair.Key + step)) continue;

                Terrain tile = pair.Value;

                Vector3 middle = tile.transform.position
                                 + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);

                Vector3 outward = new Vector3(step.x, 0f, step.y);
                Vector3 along = new Vector3(step.y, 0f, step.x);

                // 바깥면의 한가운데.
                Vector3 face = middle + outward * (size.x * 0.5f);

                float last = float.NaN;

                for (int k = 0; k < Slices; k++)
                {
                    float slot = size.x / Slices;
                    Vector3 spot = face + along * (((k + 0.5f) / Slices - 0.5f) * size.x);

                    // <b>조각 안에서 가장 높은 점</b>에 맞춥니다. 평균에 맞추면
                    // 높은 쪽에 틈이 생기고, 그 틈으로 세계 밖이 비칩니다.
                    float top = float.MinValue;

                    for (int s = 0; s <= 6; s++)
                    {
                        // 경계선 위가 아니라 <b>2 m 안쪽</b>을 찍습니다. 타일 밖은
                        // SampleHeight 가 뜻 없는 값을 돌려줍니다.
                        Vector3 probe = spot
                                        + along * ((s / 3f - 1f) * slot * 0.5f)
                                        - outward * 2f;

                        top = Mathf.Max(top, tile.SampleHeight(probe));
                    }

                    top += tile.transform.position.y;

                    GameObject piece = (GameObject)PrefabUtility
                        .InstantiatePrefab(prefab, holder.transform);

                    piece.transform.position = new Vector3(spot.x, top, spot.z);
                    piece.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
                    piece.name = string.Format("Rim_{0}_{1}_{2}{3}",
                        pair.Key.x, pair.Key.y, step.x, step.y);

                    lowestTop = Mathf.Min(lowestTop, top);
                    if (!float.IsNaN(last)) tallestStep = Mathf.Max(tallestStep, Mathf.Abs(top - last));
                    last = top;

                    made++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(string.Format(
            "WorldRimSetup: 조각 {0} 개 · 가장 낮은 윗면 {1:F1} m · " +
            "이웃 조각 사이 최대 단차 {2:F1} m",
            made, lowestTop, tallestStep));

        return made > 0 ? 0 : 1;
    }
}
