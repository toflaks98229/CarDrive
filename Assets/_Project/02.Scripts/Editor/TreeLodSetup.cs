using UnityEditor;
using UnityEngine;

/// <summary>
/// 나무 프리팹에 <see cref="LODGroup"/> 을 달아 <b>콘솔의 나무 경고를 멈춥니다.</b>
///
/// <b>무슨 경고인가.</b>
/// <c>The tree tree01 must use the Nature/Soft Occlusion shader.</c> 가 씬을 열 때마다
/// 다섯 줄씩 쏟아집니다. 터레인은 나무 프로토타입을 <b>세 갈래</b>로 나눠 다루는데
/// (SpeedTree · LOD 가 있는 메시 나무 · 그 외), 셋 중 어디에도 안 들면 옛 "Tree Creator"
/// 규칙으로 보고 <c>Nature/Tree Soft Occlusion</c> 셰이더를 요구합니다.
/// 이 게임의 나무는 <c>MeshRenderer</c> 하나에 툰 머티리얼을 쓰는 평범한 메시라
/// 그 갈래로 떨어집니다.
///
/// ⚠ <b>셰이더를 바꾸는 것은 답이 아닙니다.</b> 소프트 오클루전 셰이더로 갈아타면
/// 나무만 이 게임의 명암 규칙 밖으로 나가고, 거리 디더 페이드도 같이 잃습니다.
/// 대신 <b>LOD 가 있는 메시 나무</b>로 분류되게 만듭니다 — 단 한 단짜리 LOD 라도
/// 있으면 터레인이 그 갈래로 다룹니다.
///
/// <b>왜 한 단인가.</b> 이 월드의 나무는 이미 <c>ViewRangeScaler</c> 가
/// <c>treeDistance</c> 와 <c>treeMaximumFullLODCount</c> 로 거리를 관리하고,
/// 사라질 때는 셰이더의 디더 페이드가 받습니다. 여기에 화면 비율 기반 LOD 전환을
/// 또 넣으면 <b>두 규칙이 같은 일을 서로 모르게</b> 하게 됩니다.
/// 그래서 전환 높이를 0 으로 두어 <b>LOD 가 아무것도 자르지 않게</b> 합니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod TreeLodSetup.Run
/// </code>
/// </summary>
public static class TreeLodSetup
{
    private const string TreeDir = "Assets/_Project/05.Prefabs/Prop/Tree";

    public static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { TreeDir });
        int touched = 0, had = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            if (prefab.GetComponentInChildren<LODGroup>(true) != null)
            {
                had++;
                continue;
            }

            GameObject copy = PrefabUtility.LoadPrefabContents(path);

            try
            {
                Renderer[] parts = copy.GetComponentsInChildren<Renderer>(true);

                if (parts.Length == 0)
                {
                    Debug.Log("TREELOD ⚠ 그릴 것이 없습니다 — " + path);
                    continue;
                }

                LODGroup group = copy.GetComponent<LODGroup>();
                if (group == null) group = copy.AddComponent<LODGroup>();

                group.fadeMode = LODFadeMode.None;
                group.animateCrossFading = false;

                // 전환 높이 0 — 이 LOD 는 끝까지 그립니다. 자르는 일은 터레인이 합니다.
                group.SetLODs(new[] { new LOD(0f, parts) });
                group.RecalculateBounds();

                PrefabUtility.SaveAsPrefabAsset(copy, path);
                touched++;

                Debug.Log("TREELOD 달았습니다 — " + System.IO.Path.GetFileName(path)
                          + " · 렌더러 " + parts.Length + " 개");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(copy);
            }
        }

        int faded = Fade(guids);

        AssetDatabase.SaveAssets();
        Debug.Log("TREELOD 끝 — 새로 단 것 " + touched + " · 이미 있던 것 " + had
                  + " · 페이드 끈 재질 " + faded + " 개");
        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>
    /// 나무 재질의 <b>디더 페이드를 끕니다.</b>
    ///
    /// ⚠ <b>LOD 를 다는 것만으로는 화면이 바뀝니다.</b> 지금까지 나무는 터레인의
    /// 옛 경로로 그려졌고, 그 경로는 <b>프로토타입 재질을 쓰지 않습니다.</b> 그래서
    /// <c>_DITHER_FADE</c> 가 켜져 있어도 나무에는 <b>한 번도 걸린 적이 없었습니다.</b>
    /// LOD 를 달아 재질 경로로 옮기는 순간 그 페이드가 처음으로 살아나고,
    /// 지금 창(시야의 70~97%, 167~231 m)이 <b>마을을 둘러싼 숲 띠를 통째로</b> 덮습니다.
    /// 실제로 찍어 보면 지평선의 나무가 전부 사라집니다.
    ///
    /// 그래서 여기서는 <b>오늘의 화면을 그대로 둡니다</b> — 나무는 잘리는 거리까지
    /// 통짜로 그리고, 페이드는 끕니다. 원래도 그렇게 보이고 있었습니다.
    ///
    /// <b>남겨 둔 결정.</b> 이 페이드는 원래 나무를 위해 쓴 것입니다(셰이더의
    /// <c>CarDriveObjectSeed</c> 주석이 "나무마다 사라지는 때를 어긋낸다" 고 적고 있습니다).
    /// 되살리려면 창을 <b>잘리는 거리 가까이로</b> 밀어야 합니다(예: 시야의 90~97%).
    /// 그 창은 건물·바위와 함께 쓰므로 그쪽 화면도 같이 바뀝니다.
    /// </summary>
    /// <param name="guids">나무 프리팹들</param>
    /// <returns>페이드를 끈 재질 수</returns>
    private static int Fade(string[] guids)
    {
        System.Collections.Generic.HashSet<Material> seen =
            new System.Collections.Generic.HashSet<Material>();
        int off = 0;

        foreach (string guid in guids)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;

            foreach (Renderer part in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in part.sharedMaterials)
                {
                    if (material == null || !seen.Add(material)) continue;
                    if (!material.HasProperty("_UseDitherFade")) continue;
                    if (material.GetFloat("_UseDitherFade") <= 0.5f) continue;

                    material.SetFloat("_UseDitherFade", 0f);
                    material.DisableKeyword("_DITHER_FADE");
                    EditorUtility.SetDirty(material);
                    off++;

                    Debug.Log("TREELOD 페이드를 껐습니다 — " + material.name);
                }
            }
        }

        return off;
    }
}
