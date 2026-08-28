using CarDrive.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 전역 젖음 지도를 씬에 세웁니다. 한 번만 돌리면 됩니다.
///
/// <b>왜 손이 아니라 도구인가.</b> 이 저장소의 다른 셋업(UrineSplatSetup, UrineStagingSetup)과
/// 같은 이유입니다 — 배치모드로 씬을 열어 붙여야 하고, 값이 여럿이라 손으로 하면
/// 무엇을 왜 그렇게 걸었는지 기록이 남지 않습니다. 다시 돌려도 같은 결과입니다.
///
/// 쓰는 법:
///   Unity.exe -batchmode -nographics -projectPath . -executeMethod SplatMapSetup.Run
/// </summary>
public static class SplatMapSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string HostName = "SplatMap";

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        SplatManager manager = Object.FindAnyObjectByType<SplatManager>(FindObjectsInactive.Include);

        if (manager == null)
        {
            GameObject go = new GameObject(HostName);
            manager = go.AddComponent<SplatManager>();
            Debug.Log("SPLATMAP " + HostName + " 을 씬에 세움");
        }
        else
        {
            Debug.Log("SPLATMAP 이미 있음: " + manager.gameObject.name);
        }

        // <b>기본값을 여기서 못박습니다.</b> 인스펙터에서만 맞춰 두면 도구를 다시 돌렸을 때
        // 절반만 되돌아가 서로 어긋납니다.
        //
        // 창 128m / 2048제곱 = 6.2cm/텍셀, R8 로 4MB.
        // 지형 전체(1100 x 1200m)를 덮으면 같은 4MB 에 53.7cm/텍셀이라 14cm 자국이
        // <b>0.3 텍셀</b>입니다 — 형체가 남지 않습니다. 그래서 창으로 둡니다.
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("coverage").enumValueIndex = (int)SplatManager.CoverageMode.FollowTarget;
        so.FindProperty("windowMeters").floatValue = 128f;
        so.FindProperty("windowRecenterDistance").floatValue = 40f;
        so.FindProperty("resolution").intValue = 2048;
        so.FindProperty("dryDuration").floatValue = 26f;
        so.FindProperty("fadeInterval").floatValue = 0.1f;
        so.FindProperty("maxRequestsPerFrame").intValue = 64;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);

        Debug.Log("SPLATMAP 끝.");
        EditorApplication.Exit(0);
    }
}
