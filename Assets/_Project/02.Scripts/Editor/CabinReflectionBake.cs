using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 차 실내용 <b>리플렉션 프로브를 굽고 차 프리팹에 붙입니다.</b>
///
/// <b>왜 필요한가.</b> 씬에 리플렉션 프로브가 하나도 없어서, 매끈한 차 표면
/// (<c>_Smoothness: 1</c>, <c>_Metallic: 0</c> 인 URP Lit)이 <b>스카이박스를 그대로 반사</b>합니다.
/// 그래서 대시보드에 차가운 하늘색 광택이 얹혀 따뜻한 실내등과 싸웁니다.
/// 화면의 60% 가 운전석이라 이 광택이 실내의 인상을 지배합니다.
///
/// <b>왜 Custom 인가.</b> Baked 로 두면 사용자가 라이팅을 다시 구울 때 함께 갱신되거나
/// 무효가 됩니다. 차는 움직이는 물체라 어차피 구운 자리의 그림을 들고 다니는 것이므로,
/// 한 번 구워 에셋으로 박아 두는 편이 안정적입니다. 굽는 것은 여기서
/// <c>Lightmapping.BakeReflectionProbe</c> 로 합니다 — 직접 RenderToCubemap 하면
/// 거칠기용 밉 컨볼루션이 없어 거친 표면이 선명한 밉을 읽습니다.
///
/// <b>실내를 기준으로 잡습니다.</b> 차체와 실내가 한 렌더러라 프로브를 실내에만
/// 한정할 수 없습니다. 그러면 어느 쪽을 기준으로 삼을지 골라야 하는데, 이 게임은
/// 운전석에서 보내는 시간이 압도적이라 실내를 택했습니다. 바깥 면은 창을 통해 들어온
/// 하늘 부분을 상자 투영으로 읽습니다.
///
/// 쓰는 법 (그래픽 장치가 필요하므로 -nographics 를 붙이지 않습니다):
///   Unity.exe -batchmode -projectPath . -executeMethod CabinReflectionBake.Run
/// </summary>
public static class CabinReflectionBake
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string CarPrefabPath = "Assets/_Project/05.Prefabs/Player/PlayerCar.prefab";
    private const string CubemapPath = "Assets/_Project/04.Art/01.Images/RenderTextures/CabinReflection.exr";
    private const string ProbeName = "CabinReflectionProbe";

    /// <summary>캐빈 안쪽만 담는 상자입니다. 차 전체를 담으면 바깥 면까지 실내를 반사합니다.</summary>
    private static readonly Vector3 BoxSize = new Vector3(2.0f, 1.5f, 2.6f);

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject car = GameObject.Find("PlayerCar");
        if (car == null)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "PlayerCar") { car = t.gameObject; break; }
        }
        if (car == null) { Debug.Log("PROBE 씬에서 PlayerCar 를 찾지 못함"); EditorApplication.Exit(1); return; }

        // 캐빈 기준점: 실내등 자리가 캐빈 한가운데입니다. 눈높이로 조금 내립니다.
        Transform interior = FindDeep(car.transform, "InteriorLight");
        Vector3 center = interior != null
            ? interior.position + Vector3.down * 0.35f
            : car.transform.position + Vector3.up * 1.0f;

        Debug.Log("PROBE 차=" + car.name + " 캐빈중심=" + center.ToString("F3"));

        // 굽기용 임시 프로브. 차 프리팹에는 다 구운 뒤 Custom 으로 붙입니다.
        GameObject holder = new GameObject("__BakeProbe");
        holder.transform.position = center;
        ReflectionProbe probe = holder.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Baked;
        probe.resolution = 128;
        probe.hdr = true;
        probe.boxProjection = true;
        probe.size = BoxSize;
        probe.center = Vector3.zero;
        probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        probe.cullingMask = ~(1 << 5);   // UI 는 반사에 들어가면 안 됩니다.
        probe.nearClipPlane = 0.05f;
        probe.farClipPlane = 60f;

        Directory.CreateDirectory(Path.GetDirectoryName(CubemapPath));
        bool ok = Lightmapping.BakeReflectionProbe(probe, CubemapPath);
        Debug.Log("PROBE 굽기 " + (ok ? "성공" : "실패") + " -> " + CubemapPath);
        Object.DestroyImmediate(holder);

        if (!ok) { EditorApplication.Exit(1); return; }

        AssetDatabase.ImportAsset(CubemapPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Cubemap baked = AssetDatabase.LoadAssetAtPath<Cubemap>(CubemapPath);
        if (baked == null) { Debug.Log("PROBE 구운 큐브맵을 못 읽음"); EditorApplication.Exit(1); return; }
        Debug.Log("PROBE 큐브맵 " + baked.width + "px 포맷=" + baked.format);

        AttachToPrefab(baked, center - car.transform.position);

        Debug.Log("PROBE 끝");
        EditorApplication.Exit(0);
    }

    /// <summary>구운 큐브맵을 차 프리팹에 Custom 프로브로 붙입니다.</summary>
    private static void AttachToPrefab(Cubemap baked, Vector3 localCenter)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CarPrefabPath);
        try
        {
            Transform existing = FindDeep(root.transform, ProbeName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(ProbeName);
            if (existing == null) go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = Quaternion.identity;

            ReflectionProbe p = go.GetComponent<ReflectionProbe>();
            if (p == null) p = go.AddComponent<ReflectionProbe>();

            p.mode = ReflectionProbeMode.Custom;
            p.customBakedTexture = baked;
            p.boxProjection = true;
            p.size = BoxSize;
            p.center = Vector3.zero;
            p.hdr = true;
            p.intensity = 1f;
            p.importance = 1;
            p.blendDistance = 0.5f;

            PrefabUtility.SaveAsPrefabAsset(root, CarPrefabPath);
            Debug.Log("PROBE 프리팹에 붙임 local=" + localCenter.ToString("F3") + " 상자=" + BoxSize);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
