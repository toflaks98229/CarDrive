using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <c>WalkerRobot_Strider</c> 를 빈 씬에 세우고 네 방향에서 찍습니다.
/// 프리팹 YAML 만 봐서는 메시가 제자리에 붙었는지 알 수 없습니다.
///
/// <b>⚠ <c>-nographics</c> 를 붙이면 안 됩니다.</b> 카메라가 그리지 못합니다.
/// <code>Unity.exe -batchmode -projectPath . -executeMethod StriderCapture.Run</code>
/// </summary>
public static class StriderCapture
{
    // --- Constants ---

    private const string DefaultPrefab = "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab";

    /// <summary>STRIDER_PREFAB 으로 다른 기계를 재도록 바꿀 수 있습니다.</summary>
    private static string PrefabPath
    {
        get
        {
            string raw = System.Environment.GetEnvironmentVariable("STRIDER_PREFAB");
            return string.IsNullOrEmpty(raw) ? DefaultPrefab : raw;
        }
    }

    private const int Size = 900;

    /// <summary>찍을 방향입니다. 이름과 로봇을 도는 각도(도)입니다.</summary>
    private static readonly (string name, float yaw, float pitch)[] Views =
    {
        ("front", 0f, 8f),
        ("side", 90f, 8f),
        ("quarter", 35f, 14f),
        ("above", 35f, 55f),
    };

    // --- Public Methods ---

    public static void Run()
    {
        string outDir = System.Environment.GetEnvironmentVariable("STRIDER_SHOT_DIR");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "strider");
        Directory.CreateDirectory(outDir);

        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("StriderCapture: 프리팹을 찾지 못했습니다: " + PrefabPath);
            if (Application.isBatchMode) EditorApplication.Exit(2);
            return;
        }

        GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        robot.transform.position = Vector3.zero;

        // STRIDER_STATIC=1 이면 살아 있는 컴포넌트를 재웁니다. 프리팹에 적힌 자세를 그대로 봅니다.
        if (System.Environment.GetEnvironmentVariable("STRIDER_STATIC") == "1")
        {
            foreach (MonoBehaviour behaviour in robot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }
        }

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(8f, 1f, 8f);

        GameObject sun = new GameObject("Sun");
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.40f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.20f, 0.18f);
        RenderSettings.fog = false;

        Bounds bounds = Measure(robot);
        Debug.Log($"StriderCapture: 크기 {bounds.size} 중심 {bounds.center} 바닥 {bounds.min.y:F3} 꼭대기 {bounds.max.y:F3}");

        GameObject camObject = new GameObject("Shot");
        Camera cam = camObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.15f, 0.17f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;

        float distance = bounds.extents.magnitude * 2.6f;

        foreach ((string name, float yaw, float pitch) in Views)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            camObject.transform.position = bounds.center - rotation * Vector3.forward * distance;
            camObject.transform.rotation = rotation;

            Shoot(cam, Path.Combine(outDir, "strider_" + name + ".png"));
        }

        Debug.Log("StriderCapture: " + outDir);

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    private static Bounds Measure(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private static void Shoot(Camera cam, string path)
    {
        RenderTexture rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;

        for (int i = 0; i < 3; i++)
        {
            cam.Render();
            while (ShaderUtil.anythingCompiling) Thread.Sleep(50);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D shot = new Texture2D(Size, Size, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        shot.Apply();

        RenderTexture.active = previous;
        cam.targetTexture = null;

        File.WriteAllBytes(path, shot.EncodeToPNG());

        Object.DestroyImmediate(shot);
        rt.Release();
        Object.DestroyImmediate(rt);
    }
}
