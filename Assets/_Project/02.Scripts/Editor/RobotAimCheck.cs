using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CarDrive.Gameplay;

/// <summary>
/// 포탑이 <b>정말로 겨누는지</b>를 잽니다. 프리팹에 마디가 생긴 것과 그 마디가 목표를
/// 향하는 것은 다른 문제입니다.
///
/// 목표를 로봇 둘레의 여러 자리에 놓고, 겨눈 방향과 목표 방향이 이루는 각을 잽니다.
/// 한계 밖의 자리도 일부러 넣습니다 — 그때는 <b>한계에서 멈춰야</b> 하고, 각이 남는 것이
/// 정상입니다. 멈추지 않고 넘어가면 그것이 버그입니다.
///
/// ⚠ 재생 모드와 렌더링이 필요하므로 <c>-nographics</c> 를 붙이면 안 됩니다.
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod RobotAimCheck.Run
/// </code>
/// </summary>
public static class RobotAimCheck
{
    // --- Constants ---

    private const string DefaultPrefab = "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab";
    private const string SessionKey = "RobotAimCheck.Armed";
    private const string OutputDirectory = "Logs/RobotAim";

    private const float SettleSeconds = 1.5f;
    private const float HoldSeconds = 1.6f;
    private const int ShotSize = 720;

    /// <summary>목표를 놓을 자리입니다. 로봇 기준 로컬 좌표이고, 마지막 둘은 한계 밖입니다.</summary>
    private static readonly (string name, Vector3 local)[] Stations =
    {
        ("front", new Vector3(0f, 6f, 40f)),
        ("left", new Vector3(-30f, 6f, 22f)),
        ("right", new Vector3(30f, 6f, 22f)),
        ("high", new Vector3(0f, 34f, 16f)),
        ("behind", new Vector3(0f, 6f, -40f)),
    };

    // --- Private Member Variables ---

    private static GameObject _robot;
    private static Transform _target;
    private static Camera _shotCamera;
    private static RobotTurret[] _turrets;

    private static float _startTime;
    private static int _lastFrame = -1;
    private static int _station = -1;
    private static int _reported = -1;
    private static string _tag = "strider";

    private static readonly StringBuilder Report = new StringBuilder();

    // --- Public Methods ---

    public static void Run()
    {
        Directory.CreateDirectory(OutputDirectory);

        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        BuildWorld();

        SessionState.SetBool(SessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    // --- Private Methods ---

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static string PrefabPath
    {
        get
        {
            string raw = System.Environment.GetEnvironmentVariable("AIM_PREFAB");
            return string.IsNullOrEmpty(raw) ? DefaultPrefab : raw;
        }
    }

    private static void BuildWorld()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject ground = new GameObject("Ground") { layer = 11 };
        BoxCollider floor = ground.AddComponent<BoxCollider>();
        floor.size = new Vector3(400f, 4f, 400f);
        ground.transform.position = new Vector3(0f, -2f, 0f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Plane);
        visual.transform.localScale = new Vector3(40f, 1f, 40f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        robot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // 걸어 다니면 조준을 재기 어렵습니다. 제자리에 세워 두고 포탑만 봅니다.
        RobotDriver driver = robot.GetComponent<RobotDriver>();
        if (driver != null) driver.enabled = false;

        GameObject target = new GameObject("AimTarget");
        foreach (RobotTurret turret in robot.GetComponentsInChildren<RobotTurret>(true))
        {
            turret.target = target.transform;
        }

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

        GameObject camObject = new GameObject("Shot");
        Camera cam = camObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.15f, 0.17f);
        cam.fieldOfView = 42f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;
        cam.enabled = false;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        if (!SessionState.GetBool(SessionKey, false)) return;

        SessionState.SetBool(SessionKey, false);

        Scene scene = SceneManager.GetActiveScene();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<WalkerRobot>() != null) _robot = root;
            if (root.name == "AimTarget") _target = root.transform;
            if (root.name == "Shot") _shotCamera = root.GetComponent<Camera>();
        }

        if (_robot == null || _target == null)
        {
            Debug.LogError("RobotAimCheck: 로봇이나 목표를 찾지 못했습니다");
            EditorApplication.Exit(2);
            return;
        }

        _turrets = _robot.GetComponentsInChildren<RobotTurret>(true);
        _tag = _robot.name.Replace("WalkerRobot_", "").Replace("(Clone)", "");

        Line($"== 조준 실측 [{_tag}] · 포탑 {_turrets.Length} 개 ==");
        foreach (RobotTurret t in _turrets)
        {
            Line($"  {t.name}: 선회 {t.yawRange.x:F0}~{t.yawRange.y:F0}° · " +
                 $"부앙 {t.pitchRange.x:F0}~{t.pitchRange.y:F0}° · 총구 {(t.muzzle != null ? t.muzzle.name : "없음")}");
        }
        Line("");

        Time.captureDeltaTime = 1f / 60f;
        _startTime = Time.time;

        EditorApplication.update += Sample;
    }

    private static void Sample()
    {
        if (!EditorApplication.isPlaying) return;
        if (Time.frameCount == _lastFrame) return;
        _lastFrame = Time.frameCount;

        float t = Time.time - _startTime;
        if (t < SettleSeconds) return;

        int station = Mathf.FloorToInt((t - SettleSeconds) / HoldSeconds);

        if (station >= Stations.Length)
        {
            EditorApplication.update -= Sample;
            Finish();
            return;
        }

        if (station != _station)
        {
            _station = station;
            _target.position = _robot.transform.TransformPoint(Stations[station].local);
        }

        // 자리를 옮긴 직후는 아직 돌아가는 중입니다. 다 돌아간 끝에서만 잽니다.
        // 측정 창이 프레임보다 넓으면 같은 자리를 두 번 찍으므로, 찍은 자리를 기억합니다.
        float held = (t - SettleSeconds) - station * HoldSeconds;
        if (held < HoldSeconds - (1f / 60f) * 1.5f) return;
        if (station == _reported) return;
        _reported = station;

        Line($"목표 {Stations[station].name} — 로봇 기준 {Stations[station].local}");

        foreach (RobotTurret turret in _turrets)
        {
            Line($"  {turret.name,-16} 선회 {turret.Yaw,7:F1}° 부앙 {turret.Pitch,6:F1}° " +
                 $"→ 남은 각 {turret.AimError,6:F1}°");
        }

        Shoot(Stations[station].name);
        Line("");
    }

    private static void Shoot(string name)
    {
        Bounds bounds = new Bounds(_robot.transform.position, Vector3.one);
        Renderer[] renderers = _robot.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        }

        float distance = bounds.extents.magnitude * 2.4f;
        Quaternion rotation = Quaternion.Euler(16f, 35f, 0f);
        _shotCamera.transform.position = bounds.center - rotation * Vector3.forward * distance;
        _shotCamera.transform.rotation = rotation;

        RenderTexture rt = new RenderTexture(ShotSize, ShotSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        _shotCamera.targetTexture = rt;

        for (int i = 0; i < 3; i++)
        {
            _shotCamera.Render();
            while (ShaderUtil.anythingCompiling) Thread.Sleep(50);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D shot = new Texture2D(ShotSize, ShotSize, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, ShotSize, ShotSize), 0, 0);
        shot.Apply();

        RenderTexture.active = previous;
        _shotCamera.targetTexture = null;

        File.WriteAllBytes(Path.Combine(OutputDirectory, $"aim_{_tag}_{name}.png"), shot.EncodeToPNG());

        Object.DestroyImmediate(shot);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    private static void Finish()
    {
        string path = Path.Combine(OutputDirectory, "aim_" + _tag + ".txt");
        File.WriteAllText(path, Report.ToString());

        Debug.Log("RobotAimCheck 결과\n" + Report);
        EditorApplication.Exit(0);
    }

    private static void Line(string text)
    {
        Report.Append(text).Append('\n');
    }
}
