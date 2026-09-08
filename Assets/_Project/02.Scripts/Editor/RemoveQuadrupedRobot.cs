using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 네발 보행 로봇(<c>WalkerRobot_Quadruped</c>)을 프로젝트에서 걷어냅니다.
///
/// <b>씬 인스턴스를 먼저 지웁니다.</b> 프리팹 에셋만 지우면 씬에는 <b>깨진 인스턴스</b>가
/// 남아 열 때마다 경고가 납니다. 목적지 더미(<c>*_Destination</c>)도 그 로봇만 쓰던 것이라 함께 지웁니다.
///
/// 한 번 쓰고 지울 스크립트입니다.
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod RemoveQuadrupedRobot.Run
/// </code>
/// </summary>
public static class RemoveQuadrupedRobot
{
    private const string PrefabPath = "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Quadruped.prefab";
    private const string DestinationName = "WalkerRobot_Quadruped_Destination";

    public static void Run()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (prefab == null)
        {
            Debug.Log("RemoveQuadrupedRobot: 이미 없습니다");
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
        int removed = 0;

        foreach (string scenePath in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project" })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(p => System.IO.File.ReadAllText(p).Contains(guid)))
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            List<GameObject> doomed = new List<GameObject>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject go = t.gameObject;

                    if (go.name == DestinationName) doomed.Add(go);
                    else if (PrefabUtility.IsAnyPrefabInstanceRoot(go)
                             && PrefabUtility.GetCorrespondingObjectFromSource(go) == prefab) doomed.Add(go);
                }
            }

            foreach (GameObject go in doomed)
            {
                Debug.Log($"RemoveQuadrupedRobot: {scenePath} 에서 {go.name} 을 지웁니다");
                Object.DestroyImmediate(go);
                removed++;
            }

            if (doomed.Count > 0) EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.DeleteAsset(PrefabPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"RemoveQuadrupedRobot: 씬에서 {removed} 개, 프리팹 1 개를 지웠습니다");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
