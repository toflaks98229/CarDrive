using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 터레인 나무가 <b>실제로 어떻게 구성되어 있는지</b> 덤프합니다. 확인용 임시 도구입니다.
///
/// <b>왜 필요한가.</b> 나무가 멀어질 때 디더가 걸리지 않는 이유를 찾는 중인데,
/// 프로토타입에 LOD 가 있는지·빌보드가 있는지·어떤 셰이더를 쓰는지를
/// <b>추측으로 다루면 계속 헛다리를 짚습니다.</b> TerrainData 는 바이너리라
/// 파일을 읽어서는 알 수 없으므로 유니티에게 직접 물어봅니다.
///
/// 쓰는 법:
///   Unity.exe -batchmode -nographics -projectPath . -executeMethod TreeSetupDump.Run
/// </summary>
public static class TreeSetupDump
{
    /// <summary>확인할 씬입니다.</summary>
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

    /// <summary>씬을 열고 지형과 나무 구성을 찍습니다.</summary>
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,
                                                               FindObjectsSortMode.None);

        Debug.Log("TREEDUMP 지형 " + terrains.Length + "장");

        int totalInstances = 0;
        bool firstReported = false;

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain t = terrains[i];
            if (t == null || t.terrainData == null) continue;

            totalInstances += t.terrainData.treeInstances.Length;

            if (firstReported) continue;
            firstReported = true;

            Debug.Log("TREEDUMP 지형 설정  treeDistance=" + t.treeDistance +
                      "  billboardStart=" + t.treeBillboardDistance +
                      "  crossFade=" + t.treeCrossFadeLength +
                      "  maxFullLOD=" + t.treeMaximumFullLODCount +
                      "  drawInstanced=" + t.drawInstanced +
                      "  drawTreesAndFoliage=" + t.drawTreesAndFoliage);

            TreePrototype[] protos = t.terrainData.treePrototypes;
            Debug.Log("TREEDUMP 프로토타입 " + protos.Length + "종");

            for (int p = 0; p < protos.Length; p++)
            {
                GameObject prefab = protos[p].prefab;
                if (prefab == null)
                {
                    Debug.Log("TREEDUMP   [" + p + "] 프리팹 없음");
                    continue;
                }

                LODGroup lod = prefab.GetComponent<LODGroup>();
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);

                Debug.Log("TREEDUMP   [" + p + "] " + prefab.name +
                          "  LODGroup=" + (lod != null ? lod.lodCount.ToString() + "단계" : "없음") +
                          "  렌더러=" + renderers.Length +
                          "  bendFactor=" + protos[p].bendFactor);

                for (int r = 0; r < renderers.Length; r++)
                {
                    Material m = renderers[r].sharedMaterial;

                    Debug.Log("TREEDUMP        렌더러[" + r + "] " + renderers[r].GetType().Name +
                              "  재질=" + (m != null ? m.name : "없음") +
                              "  셰이더=" + (m != null && m.shader != null ? m.shader.name : "없음") +
                              "  DITHER_FADE=" + (m != null && m.IsKeywordEnabled("_DITHER_FADE")) +
                              "  FadeScatter=" + (m != null && m.HasProperty("_FadeScatter")
                                                    ? m.GetFloat("_FadeScatter").ToString()
                                                    : "프로퍼티 없음"));
                }
            }
        }

        Debug.Log("TREEDUMP 나무 인스턴스 총 " + totalInstances + "그루");
        Debug.Log("TREEDUMP 끝");

        EditorApplication.Exit(0);
    }
}
