using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬의 <b>라이트맵 정책</b>을 봅니다. 그리고 필요하면 맞춥니다.
///
/// <b>왜 필요한가.</b> 굽기가 켜져 있는데 한 번도 굽지 않은 상태는 조용합니다 —
/// 오류도 경고도 없고, 다만 정적으로 표시된 오브젝트마다 "라이트맵이 있어야 하는데
/// 없는" 상태로 남습니다. 나중에 누가 Generate Lighting 을 누르면 그때서야
/// 메시 수백 개의 uv2 를 요구하며 몇 시간을 굽습니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod LightmapPolicySetup.Report
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod LightmapPolicySetup.Apply
/// </code>
/// </summary>
public static class LightmapPolicySetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string SettingsPath = "Assets/_Project/01.Scenes/CarDrive_RealtimeOnly.lighting";

    public static void Report()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        List<GameObject> flagged = Contributors(scene);

        Debug.Log($"LightmapPolicy: 씬 '{scene.name}' · ContributeGI 켜진 오브젝트 {flagged.Count} 개");

        foreach (IGrouping<string, GameObject> group in flagged
                     .GroupBy(SourceLabel)
                     .OrderByDescending(g => g.Count()))
        {
            Debug.Log($"  {group.Count(),4} 개 · {group.Key} · 예: {group.First().name}");
        }

        Debug.Log(Lightmapping.TryGetLightingSettings(out LightingSettings settings) && settings != null
            ? $"  라이팅 설정 자산: {settings.name} · bakedGI {settings.bakedGI} · realtimeGI {settings.realtimeGI}"
            : "  라이팅 설정 자산: 없음 (씬에 박힌 기본값을 씀)");

        // ⚠ 내장 기본 자산이 붙어 있어도 null 이 아닙니다. 경로까지 봐야 진짜 구운 것인지
        //   압니다 — 진짜 결과는 씬 옆 폴더에, 기본값은 Library/unity default resources 에 있습니다.
        LightingDataAsset data = Lightmapping.lightingDataAsset;
        string dataPath = data != null ? AssetDatabase.GetAssetPath(data) : "";
        Debug.Log($"  구운 결과(LightingDataAsset): {(string.IsNullOrEmpty(dataPath) ? "없음(내장 기본값)" : dataPath)}");

        int realtime = 0;
        int baked = 0;
        int mixed = 0;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.lightmapBakeType == LightmapBakeType.Realtime) realtime++;
            else if (light.lightmapBakeType == LightmapBakeType.Baked) baked++;
            else mixed++;
        }

        Debug.Log($"  조명: 실시간 {realtime} · 혼합 {mixed} · 구움 {baked}");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>
    /// 굽기를 끄고 ContributeGI 를 걷습니다.
    ///
    /// <b>왜 uv2 를 만드는 쪽이 아니라 이쪽인가.</b> 이 프로젝트의 셰이더는 라이트맵을
    /// 읽지 않고(<c>SAMPLE_GI</c>·<c>LIGHTMAP_ON</c> 이 한 번도 안 나옵니다), 조명은
    /// 전부 실시간이라 구울 것이 주변광밖에 없으며, 해의 색과 세기는 시계가 돌립니다 —
    /// 구운 값은 어느 한 시각에 얼어붙어 나머지 모든 시각에 틀립니다. 메시 253 개에
    /// uv2 를 붙여도 아무도 읽지 않습니다.
    ///
    /// <b>ContributeGI 만 걷습니다.</b> 배칭·오클루전 플래그는 그대로 둡니다. 드로우
    /// 제출이 렌더 스레드의 87% 를 먹는 프로젝트에서 배칭 정적은 값이 나가는 정보입니다.
    /// </summary>
    public static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 설정을 씬에 박힌 익명 기본값으로 두면 무엇이 정책이고 무엇이 기본값인지
        // 구별되지 않습니다. 이름 붙은 자산으로 꺼내 두면 라이팅 창에서 바로 보입니다.
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);

        if (settings == null)
        {
            settings = new LightingSettings { name = "CarDrive_RealtimeOnly" };
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        settings.bakedGI = false;
        settings.realtimeGI = false;
        EditorUtility.SetDirty(settings);

        Lightmapping.lightingSettings = settings;
        Lightmapping.lightingDataAsset = null;

        int cleared = 0;

        foreach (GameObject go in Contributors(scene))
        {
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.ContributeGI);
            cleared++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"LightmapPolicy: ContributeGI {cleared} 개 걷음 · 굽기 끔 · 설정 자산 {SettingsPath}");
        Debug.Log($"  남은 ContributeGI: {Contributors(scene).Count} 개");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    private static List<GameObject> Contributors(Scene scene)
    {
        List<GameObject> found = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((flags & StaticEditorFlags.ContributeGI) != 0) found.Add(t.gameObject);
            }
        }

        return found;
    }

    /// <summary>이 오브젝트가 씬에 직접 놓인 것인지, 프리팹에서 온 것인지입니다.</summary>
    private static string SourceLabel(GameObject go)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(go)) return "씬에 직접";

        GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
        string path = source != null ? AssetDatabase.GetAssetPath(source) : "";
        return string.IsNullOrEmpty(path) ? "프리팹(경로 불명)" : "프리팹 " + System.IO.Path.GetFileName(path);
    }
}
