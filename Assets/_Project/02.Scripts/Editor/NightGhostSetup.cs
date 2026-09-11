using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CarDrive.Gameplay;

/// <summary>
/// <see cref="NightGhostSpawner"/> 를 씬에 세우고 값을 채웁니다.
///
/// <b>왜 손으로 놓지 않는가.</b> 이 컴포넌트가 제대로 돌려면 프리팹·바닥 레이어·거리
/// 셋이 동시에 맞아야 하는데, 셋 중 하나만 틀려도 <b>조용히 아무것도 안 나옵니다.</b>
/// 밤에 안 나오는 것과 잘못 설정된 것을 눈으로 구별할 수 없으므로, 설정을 코드에
/// 적어 두고 다시 돌릴 수 있게 합니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod NightGhostSetup.Run -quit
/// </code>
/// </summary>
public static class NightGhostSetup
{
    // --- Constants ---

    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

    /// <summary>배회 귀신으로 쓸 프리팹입니다. <see cref="EnemyController"/> 가 붙어 있는 쪽입니다.</summary>
    private const string RoamingGhostPath = "Assets/_Project/05.Prefabs/Monster/Monster_1.prefab";

    /// <summary>시스템들이 모여 있는 오브젝트입니다.</summary>
    private const string SystemsRoot = "_GameSystems";

    /// <summary>이 컴포넌트를 담을 자식 오브젝트의 이름입니다.</summary>
    private const string HolderName = "NightGhosts";

    /// <summary>지형 타일이 쓰는 레이어입니다. 바닥으로 인정할 유일한 레이어입니다.</summary>
    private const int GroundLayer = 11;

    // --- Public Methods ---

    /// <summary>씬에 세우고 값을 채웁니다. 실패하면 종료코드 2 로 나갑니다.</summary>
    [MenuItem("CarDrive/Gameplay/밤 귀신 세우기")]
    public static void Run()
    {
        int errors = 0;

        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoamingGhostPath);
            if (prefab == null) throw new Exception("배회 귀신 프리팹을 찾지 못했습니다: " + RoamingGhostPath);

            if (prefab.GetComponent<EnemyController>() == null)
            {
                throw new Exception(RoamingGhostPath + " 에 EnemyController 가 없습니다. " +
                                    "달라붙는 귀신(AttachedGhostController)은 이 자리에 쓸 수 없습니다.");
            }

            Transform holder = FindOrCreate(scene);

            NightGhostSpawner spawner = holder.GetComponent<NightGhostSpawner>();
            if (spawner == null) spawner = holder.gameObject.AddComponent<NightGhostSpawner>();

            spawner.ghostPrefabs.Clear();
            spawner.ghostPrefabs.Add(prefab);

            // 바닥은 지형 타일뿐입니다. Default 까지 넣으면 지붕 위에서도 태어납니다.
            spawner.groundMask = 1 << GroundLayer;

            // 헤드라이트 사거리 바깥에서 태어나 다가오게 합니다.
            spawner.minSpawnDistance = 30f;
            spawner.maxSpawnDistance = 60f;
            spawner.despawnDistance = 110f;
            spawner.behindArc = 200f;
            spawner.maxAlive = 4;
            spawner.minSpawnInterval = 14f;
            spawner.maxSpawnInterval = 32f;

            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("NightGhostSetup: 완료 — " + Path(holder) +
                      " 에 NightGhostSpawner 를 세웠습니다. 프리팹 " + spawner.ghostPrefabs.Count + "개.");
        }
        catch (Exception e)
        {
            Debug.LogError("NightGhostSetup: " + e);
            errors++;
        }

        AssetDatabase.SaveAssets();

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    // --- Private Methods ---

    /// <summary>계층 경로를 문자열로 만듭니다. 어디에 세웠는지 로그가 정확해야 합니다.</summary>
    /// <param name="t">경로를 만들 트랜스폼</param>
    /// <returns>루트부터의 경로</returns>
    private static string Path(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    /// <summary>담을 오브젝트를 찾고, 없으면 시스템 루트 밑에 만듭니다.</summary>
    /// <param name="scene">작업할 씬</param>
    /// <returns>컴포넌트를 붙일 트랜스폼</returns>
    private static Transform FindOrCreate(Scene scene)
    {
        // 최상위만 훑으면 안 됩니다. 이 씬에서 _GameSystems 는 다른 것의 자식으로
        // 들어가 있어, 루트만 보던 첫 판이 못 찾고 최상위에 세웠습니다.
        Transform systems = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != SystemsRoot) continue;
                systems = t;
                break;
            }

            if (systems != null) break;
        }

        if (systems == null)
        {
            // 시스템 루트가 없으면 최상위에 둡니다. 없는 것보다는 돕니다.
            Debug.LogWarning("NightGhostSetup: '" + SystemsRoot + "' 를 찾지 못해 최상위에 세웁니다.");
            GameObject loose = GameObject.Find(HolderName) ?? new GameObject(HolderName);
            return loose.transform;
        }

        Transform existing = systems.Find(HolderName);
        if (existing != null) return existing;

        GameObject holder = new GameObject(HolderName);
        holder.transform.SetParent(systems, false);
        return holder.transform;
    }
}
