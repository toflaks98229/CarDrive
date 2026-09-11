using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// <see cref="InteractionOutline"/> 을 씬에 붙이고, 실제로 어떻게 보이는지 찍습니다.
///
/// <b>왜 도구로 붙이는가.</b> 손으로 붙이면 다음에 씬을 다시 만들 때 빠집니다.
/// 어디에 붙어야 하는지가 코드에 적혀 있으면 그 판단이 남습니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod InteractionOutlineSetup.Run
/// Unity.exe -batchmode            -projectPath . -executeMethod InteractionOutlineSetup.Preview
/// </code>
/// </summary>
public static class InteractionOutlineSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string ShotDirectory = "Logs/Outline";

    /// <summary>
    /// <see cref="PlayerInteractor"/> 와 <b>같은 오브젝트에</b> 붙입니다.
    ///
    /// 조준을 재는 쪽과 선을 두르는 쪽이 붙어 있어야, 한쪽이 프리팹에서 빠질 때
    /// 다른 쪽만 남아 조용히 아무것도 안 하는 일이 생기지 않습니다.
    /// </summary>
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        PlayerInteractor interactor = Object.FindAnyObjectByType<PlayerInteractor>(FindObjectsInactive.Include);

        if (interactor == null)
        {
            Debug.Log("OUTLINE ⚠ PlayerInteractor 를 씬에서 찾지 못했습니다");
            EditorApplication.Exit(1);
            return;
        }

        InteractionOutline outline = interactor.GetComponent<InteractionOutline>();

        if (outline == null)
        {
            outline = interactor.gameObject.AddComponent<InteractionOutline>();
            Debug.Log("OUTLINE 붙였습니다 — " + Path(interactor.transform));
        }
        else
        {
            Debug.Log("OUTLINE 이미 붙어 있습니다 — " + Path(interactor.transform));
        }

        outline.interactor = interactor;

        EditorSceneManager.MarkSceneDirty(interactor.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("OUTLINE 두께 " + outline.width.ToString("F4")
                  + " · 페이드 " + outline.fadeSeconds.ToString("F2") + "s");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 씬의 상호작용 대상 하나에 선을 켜고 그 앞에서 한 장 찍습니다.
    ///
    /// ⚠ <b>조준은 세우지 않습니다.</b> 여기서 보려는 것은 "선이 어떻게 보이는가" 이지
    /// "조준이 맞는가" 가 아닙니다. <c>Tick</c> 에 대상을 직접 넘겨 선을 끝까지 채웁니다.
    ///
    /// ⚠ <c>-nographics</c> 를 붙이면 안 됩니다. 그리질 못합니다.
    /// </summary>
    public static void Preview()
    {
        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        Directory.CreateDirectory(ShotDirectory);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 어느 것을 찍을지는 고를 수 있어야 합니다. 검은 차에 선을 둘러 놓고
        // "안 보인다" 고 판단할 뻔했습니다 — 대상에 따라 그림이 전혀 다릅니다.
        string want = System.Environment.GetEnvironmentVariable("CARDRIVE_OUTLINE_TARGET");
        Component door = PickTarget(want);

        if (door == null)
        {
            Debug.Log("OUTLINE ⚠ 찍을 상호작용 대상을 찾지 못했습니다 — " + (want ?? "(아무거나)"));
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("OUTLINE 찍을 대상 — " + door.GetType().Name + " · " + Path(door.transform));

        GameObject host = new GameObject("OutlinePreview");
        InteractionOutline outline = host.AddComponent<InteractionOutline>();

        // 안 보일 때 "안 그려지는 것" 과 "얇아서 안 보이는 것" 을 가르기 위한 문입니다.
        string thick = System.Environment.GetEnvironmentVariable("CARDRIVE_OUTLINE_WIDTH");
        if (!string.IsNullOrEmpty(thick)
            && float.TryParse(thick, System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out float w))
        {
            outline.width = w;
            Debug.Log("OUTLINE 두께를 덮어씀 — " + w.ToString("F4"));
        }

        Camera camera = new GameObject("OutlineCamera").AddComponent<Camera>();

        try
        {
            // 대상을 화면에 꽉 채웁니다. 선은 카메라 거리에 비례해 두꺼워지므로
            // 얼마나 떨어져서 보느냐가 그림을 바꿉니다.
            Bounds box = Box(door.transform);
            float reach = Mathf.Max(box.extents.magnitude * 2.2f, 1.5f);
            camera.transform.position = box.center + new Vector3(reach * 0.8f, reach * 0.35f, reach * 0.8f);
            camera.transform.LookAt(box.center);

            // ⚠ <b>진단용 갈래.</b> 선이 안 보일 때 원인이 둘입니다 —
            // 프로퍼티 블록이 안 먹거나, 외곽선 패스 자체가 안 돌거나.
            // 이 갈래는 재질 사본에 값을 직접 넣어 <b>패스가 도는지</b>만 봅니다.
            bool direct = System.Environment.GetEnvironmentVariable("CARDRIVE_OUTLINE_DIRECT") == "1";

            foreach (string shot in new[] { "off", "on" })
            {
                if (shot == "on")
                {
                    if (direct) Direct(door.transform, outline.width, outline.color);
                    else for (int i = 0; i < 30; i++) outline.Tick(door.transform, 0.05f);
                }

                Save(camera, ShotDirectory + "/" + door.GetType().Name + "_" + shot + ".png");
            }

            Debug.Log("OUTLINE 찍었습니다 — " + ShotDirectory);
        }
        finally
        {
            outline.Tick(null, 10f);           // 선을 걷고 나갑니다
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(host);
        }

        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>
    /// 재질 <b>사본</b>에 두께를 직접 넣습니다. 에셋은 건드리지 않습니다.
    ///
    /// 프로퍼티 블록을 거치지 않으므로, 이것으로 보이는데 블록으로 안 보이면
    /// 블록이 범인이고, 이것으로도 안 보이면 패스가 안 도는 것입니다.
    /// </summary>
    private static void Direct(Transform root, float width, Color color)
    {
        Transform at = root;
        Renderer[] found = at.GetComponentsInChildren<Renderer>(false);

        while (found.Length == 0 && at.parent != null)
        {
            at = at.parent;
            found = at.GetComponentsInChildren<Renderer>(false);
        }

        int touched = 0;

        for (int i = 0; i < found.Length; i++)
        {
            Material shared = found[i].sharedMaterial;
            if (shared == null || !shared.HasProperty("_OutlineWidth")) continue;

            Material copy = new Material(shared);
            copy.SetFloat("_OutlineWidth", width);
            copy.SetColor("_OutlineColor", color);
            found[i].material = copy;      // 사본이므로 에셋은 그대로입니다
            touched++;
        }

        Debug.Log("OUTLINE 직접 넣은 렌더러 " + touched + " 개 (" + at.name + " 아래)");
    }

    /// <summary>
    /// 씬에서 상호작용 대상을 하나 고릅니다.
    ///
    /// <paramref name="typeName"/> 이 있으면 그 타입 이름이 들어간 첫 번째 것을,
    /// 없으면 <b>렌더러가 달린</b> 첫 번째 것을 고릅니다. 렌더러가 없으면 찍어도
    /// 보이지 않으므로 거릅니다.
    /// </summary>
    private static Component PickTarget(string typeName)
    {
        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Component fallback = null;

        for (int i = 0; i < all.Length; i++)
        {
            MonoBehaviour m = all[i];
            if (m == null || !(m is CarDrive.Common.IInteractable)) continue;

            // ⚠ 이름을 준 경우에는 <b>렌더러가 있는지 묻지 않습니다.</b> 차 문이 바로
            // 렌더러 없는 대상이라, 여기서 걸러 버리면 정작 보고 싶은 것을 못 찍습니다.
            // (컴포넌트는 위로 올라가 부모의 렌더러를 씁니다)
            if (!string.IsNullOrEmpty(typeName))
            {
                if (m.GetType().Name.IndexOf(typeName, System.StringComparison.OrdinalIgnoreCase) >= 0) return m;
                continue;
            }

            if (m.GetComponentInChildren<Renderer>() != null) return m;
            if (fallback == null) fallback = m;
        }

        return fallback;
    }

    /// <summary>
    /// <b>실제로 선이 그어질 렌더러</b>가 차지하는 자리입니다.
    ///
    /// ⚠ 상호작용 컴포넌트의 위치로 잡으면 안 됩니다. 차 문의 경우 그것은 렌더러 없는
    /// 콜라이더라 기본 크기가 나오고, 카메라가 <b>반대편 문</b>을 바라보게 됩니다.
    /// 실제로 그렇게 찍고서 "선이 안 그려진다" 고 판단할 뻔했습니다.
    /// 컴포넌트가 위로 올라가 렌더러를 찾는 것과 <b>같은 규칙</b>을 씁니다.
    /// </summary>
    private static Bounds Box(Transform root)
    {
        Transform at = root;
        Renderer[] parts = at.GetComponentsInChildren<Renderer>();

        while (parts.Length == 0 && at.parent != null)
        {
            at = at.parent;
            parts = at.GetComponentsInChildren<Renderer>();
        }

        if (parts.Length == 0) return new Bounds(root.position, Vector3.one);

        Bounds box = parts[0].bounds;
        for (int i = 1; i < parts.Length; i++) box.Encapsulate(parts[i].bounds);

        return box;
    }

    private static void Save(Camera camera, string path)
    {
        RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        Texture2D shot = new Texture2D(1280, 720, TextureFormat.RGB24, false);

        camera.targetTexture = target;

        // 셰이더가 아직 컴파일 중이면 단색으로 찍힙니다. 다 될 때까지 돌립니다.
        for (int i = 0; i < 3; i++)
        {
            camera.Render();
            while (ShaderUtil.anythingCompiling) System.Threading.Thread.Sleep(50);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        shot.Apply();
        RenderTexture.active = previous;

        File.WriteAllBytes(path, shot.EncodeToPNG());

        camera.targetTexture = null;
        Object.DestroyImmediate(shot);
        target.Release();
        Object.DestroyImmediate(target);
    }

    private static string Path(Transform t)
    {
        string name = t.name;
        while (t.parent != null) { t = t.parent; name = t.name + "/" + name; }
        return name;
    }
}
