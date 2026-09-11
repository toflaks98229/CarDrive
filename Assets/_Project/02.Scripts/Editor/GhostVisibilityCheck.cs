using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using CarDrive.Common;
using CarDrive.Gameplay;
using CarDrive.Systems;

/// <summary>
/// 귀신이 <b>화면에서 얼마나 보이는지</b> 잽니다.
///
/// <b>왜 재는가.</b> <c>TODO.md</c> 가 "귀신의 시인성이 좋지 않다" 고 적어 두었습니다.
/// 그런데 "안 보인다" 는 <b>눈의 말</b>이라 그대로는 고칠 수 없습니다 — 무엇이
/// 얼마나 안 보이는지 숫자가 나와야 어디를 건드릴지 정할 수 있습니다.
///
/// <b>재는 법.</b> 세 장을 찍습니다 — 귀신을 세운 화면, 치운 화면, 그리고 귀신만
/// 검은 바탕에 찍은 <b>실루엣</b>. 실루엣이 "어디가 귀신인가" 를 말해 주고,
/// 그 자리에서 앞의 두 장을 견주면 눈이 실제로 쓰는 단서인 <b>대비</b>가 나옵니다.
///
/// ⚠ <b>두 장의 차이로 고르면 안 됩니다.</b> 처음에 그렇게 했다가 "귀신이 화면의
/// 55% 를 덮는다" 가 나왔습니다. 이 게임의 후처리는 빗금이 <b>끓어서</b>, 아무것도
/// 안 바꾸고 같은 화면을 두 번 찍어도 화소의 18~62% 가 다릅니다. 차이의 대부분은
/// 귀신이 아니라 그 끓음이었습니다.
///
/// ⚠ <b>본 화면은 후처리를 켜고 찍습니다.</b> 이 게임의 화면은 밝기를 계단으로 끊고
/// 채도를 깎습니다. 귀신이 재질에서 아무리 밝아도 그 계단을 지나며 배경과 <b>같은
/// 칸</b>에 떨어질 수 있고, 그러면 화면에서는 사라집니다. 후처리를 빼고 재면 그
/// 고장이 안 보입니다.
///
/// ⚠ <c>-nographics</c> 를 붙이면 안 됩니다.
///
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod GhostVisibilityCheck.Run
/// </code>
/// 결과: <c>Logs/Ghost/*.png</c> · <c>Logs/Ghost/visibility.txt</c>
/// </summary>
public static class GhostVisibilityCheck
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string OutputDirectory = "Logs/Ghost";

    private const int Width = 960;
    private const int Height = 540;

    /// <summary>
    /// 실루엣을 찍을 때 귀신을 잠시 옮겨 둘 레이어입니다.
    ///
    /// 31 은 이 프로젝트가 안 쓰는 자리입니다. 쓰는 레이어로 옮기면 그 레이어의
    /// 다른 것들까지 실루엣에 섞여 <b>귀신이 아닌 화소</b>를 재게 됩니다.
    /// </summary>
    private const int MaskLayer = 31;

    /// <summary>재 볼 귀신들입니다.</summary>
    private static readonly string[] Ghosts =
    {
        "Assets/_Project/05.Prefabs/Monster/Monster_1.prefab",
        "Assets/_Project/05.Prefabs/Monster/Monster_2.prefab",
        "Assets/_Project/05.Prefabs/Monster/Monster_3.prefab",
    };

    /// <summary>이 거리들에서 잽니다(m). 차 앞 유리 너머로 보이는 거리들입니다.</summary>
    private static readonly float[] Distances = { 8f, 16f, 28f };

    /// <summary>이 밝기들에서 잽니다. 0 이 한밤입니다.</summary>
    private static readonly float[] Moments = { 0f, 0.3f };

    public static void Run()
    {
        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        Directory.CreateDirectory(OutputDirectory);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        SkyController sky = Object.FindAnyObjectByType<SkyController>(FindObjectsInactive.Include);
        Camera main = Camera.main;

        if (sky == null || main == null)
        {
            Debug.Log("GHOST ⚠ 하늘이나 카메라가 없습니다");
            EditorApplication.Exit(1);
            return;
        }

        FieldInfo clockField = typeof(SkyController)
            .GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo lateUpdate = typeof(SkyController)
            .GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo activeSkyField = typeof(SkyController)
            .GetField("activeSky", BindingFlags.NonPublic | BindingFlags.Instance);

        if (clockField == null || lateUpdate == null || activeSkyField == null)
        {
            Debug.Log("GHOST ⚠ SkyController 의 속을 못 찾았습니다 — 이름이 바뀌었는지 보십시오");
            EditorApplication.Exit(1);
            return;
        }

        Clock clock = new Clock();
        object was = clockField.GetValue(sky);
        clockField.SetValue(sky, clock);

        Camera eye = Shot(main);
        Camera mask = Mask(eye);

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        Texture2D withGhost = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        Texture2D without = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        Texture2D silhouette = new Texture2D(Width, Height, TextureFormat.RGB24, false);

        StringBuilder report = new StringBuilder();
        report.AppendLine("== 귀신이 얼마나 보이는가 ==");
        report.AppendLine("재는 법 : 귀신만 따로 찍은 실루엣으로 자리를 고르고, 그 자리에서");
        report.AppendLine("          귀신이 선 화면과 치운 화면을 견줍니다");
        report.AppendLine("대비    : (귀신 밝기 - 그 자리의 배경 밝기). +면 밝게, -면 어둡게 도드라집니다");
        report.AppendLine();

        try
        {
            foreach (float daylight in Moments)
            {
                clock.Daylight = daylight;
                lateUpdate.Invoke(sky, null);

                Material chosen = activeSkyField.GetValue(sky) as Material;
                if (chosen != null) RenderSettings.skybox = chosen;

                Grab(eye, target, without);

                File.WriteAllBytes(Path.Combine(OutputDirectory,
                                                "background_" + daylight.ToString("F1") + ".png"),
                                   without.EncodeToPNG());

                report.AppendLine("[밝기 " + daylight.ToString("F1")
                                  + (daylight <= 0.01f ? " · 한밤" : " · 땅거미") + "]");

                foreach (string path in Ghosts)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;

                    foreach (float away in Distances)
                    {
                        GameObject ghost = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        ghost.transform.position = eye.transform.position
                                                   + eye.transform.forward * away;
                        ghost.transform.rotation =
                            Quaternion.LookRotation(-eye.transform.forward, Vector3.up);

                        // 실루엣을 따로 찍으려고 아무도 안 쓰는 레이어로 옮깁니다.
                        foreach (Transform t in ghost.GetComponentsInChildren<Transform>(true))
                        {
                            t.gameObject.layer = MaskLayer;
                        }

                        Grab(eye, target, withGhost);
                        Grab(mask, target, silhouette);

                        Object.DestroyImmediate(ghost);

                        string tag = prefab.name + "_" + away.ToString("F0") + "m_"
                                     + daylight.ToString("F1");

                        File.WriteAllBytes(Path.Combine(OutputDirectory, tag + ".png"),
                                           withGhost.EncodeToPNG());

                        Measure(prefab.name, away, withGhost, without, silhouette, report);
                    }
                }

                report.AppendLine();

                // ── 진짜 나타나는 자리 ──
                //
                // 위의 거리들은 <b>정면</b>입니다. 실제로 귀신이 서는 자리는 스포너가
                // 쥐고 있는 앵커입니다 — 그 자리에서 운전석을 보면 화면에 들어오기나
                // 하는지가 시인성의 첫 질문입니다.
                Real(eye, mask, target, withGhost, without, silhouette, report);

                report.AppendLine();
            }
        }
        finally
        {
            clockField.SetValue(sky, was);

            Object.DestroyImmediate(eye.gameObject);
            Object.DestroyImmediate(mask.gameObject);
            Object.DestroyImmediate(withGhost);
            Object.DestroyImmediate(without);
            Object.DestroyImmediate(silhouette);

            target.Release();
            Object.DestroyImmediate(target);
        }

        string text = report.ToString();
        File.WriteAllText(Path.Combine(OutputDirectory, "visibility.txt"), text);

        Debug.Log(text);
        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>
    /// <b>스포너가 실제로 쓰는 자리</b>에 세워 놓고 잽니다.
    ///
    /// 앞의 거리들은 정면이라 잘 보이는 것이 당연합니다. 게임에서 귀신은
    /// <c>GhostSpawner</c> 의 앵커에 섭니다 — 뒤에 붙는 것 하나, 옆으로 8 m 떨어진
    /// 것 둘. <b>운전석에서 앞을 볼 때 그 셋이 화면에 들어오는가</b>가 시인성의
    /// 첫 질문이고, 재질이나 대비는 그다음입니다.
    /// </summary>
    private static void Real(Camera eye, Camera mask, RenderTexture target,
                             Texture2D withGhost, Texture2D without, Texture2D silhouette,
                             StringBuilder report)
    {
        GhostSpawner spawner = Object.FindAnyObjectByType<GhostSpawner>(
            FindObjectsInactive.Include);

        if (spawner == null)
        {
            report.AppendLine("  (스포너를 못 찾아 진짜 자리는 못 쟀습니다)");
            return;
        }

        Grab(eye, target, without);

        Place(eye, mask, target, withGhost, without, silhouette, report,
              "뒤에 붙는 것", spawner.rearGhostPrefab, spawner.rearSpawnAnchor);

        Mirrors(spawner.rearGhostPrefab, spawner.rearSpawnAnchor, report);

        Place(eye, mask, target, withGhost, without, silhouette, report,
              "왼쪽 8 m", spawner.sideGhostPrefab, spawner.sideSpawnAnchor1);

        Mirrors(spawner.sideGhostPrefab, spawner.sideSpawnAnchor1, report);

        Place(eye, mask, target, withGhost, without, silhouette, report,
              "오른쪽 8 m", spawner.sideGhostPrefab, spawner.sideSpawnAnchor2);
    }

    /// <summary>앵커 자리에 하나 세우고 잽니다.</summary>
    /// <param name="eye">본 카메라</param>
    /// <param name="mask">실루엣 카메라</param>
    /// <param name="target">그릴 곳</param>
    /// <param name="withGhost">귀신이 선 화면</param>
    /// <param name="without">귀신을 치운 화면</param>
    /// <param name="silhouette">실루엣</param>
    /// <param name="report">적을 곳</param>
    /// <param name="label">자리 이름</param>
    /// <param name="prefab">세울 귀신</param>
    /// <param name="anchor">설 자리</param>
    private static void Place(Camera eye, Camera mask, RenderTexture target,
                              Texture2D withGhost, Texture2D without, Texture2D silhouette,
                              StringBuilder report, string label,
                              GameObject prefab, Transform anchor)
    {
        if (prefab == null || anchor == null)
        {
            report.AppendLine("  " + label + " : 프리팹이나 앵커가 비어 있습니다");
            return;
        }

        GameObject ghost = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        ghost.transform.position = anchor.position;
        ghost.transform.rotation = anchor.rotation;

        foreach (Transform t in ghost.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = MaskLayer;
        }

        Grab(eye, target, withGhost);
        Grab(mask, target, silhouette);

        // 앞을 볼 때 화면 안에 들어오는지부터 적습니다.
        Vector3 view = eye.WorldToViewportPoint(anchor.position);
        bool onScreen = view.z > 0f && view.x >= 0f && view.x <= 1f && view.y >= 0f && view.y <= 1f;

        Object.DestroyImmediate(ghost);

        File.WriteAllBytes(Path.Combine(OutputDirectory, "real_" + label + ".png"),
                           withGhost.EncodeToPNG());

        report.Append("  " + label + " : 앞을 볼 때 화면 "
                      + (onScreen ? "안" : "밖") + " · ");

        Measure(label, 0f, withGhost, without, silhouette, report);
    }

    /// <summary>
    /// <b>거울에는 비치는가.</b>
    ///
    /// 앞을 볼 때 화면 밖이라는 것이 곧 "못 본다" 는 아닙니다 — 이 차에는 거울이
    /// 셋 있고, 뒤와 옆은 원래 거울로 보는 자리입니다. 거울에 비치면 단서는 있는
    /// 것이고, 그러면 고칠 곳은 <b>자리가 아니라 거울</b>입니다.
    /// </summary>
    /// <param name="prefab">세울 귀신</param>
    /// <param name="anchor">설 자리</param>
    /// <param name="report">적을 곳</param>
    private static void Mirrors(GameObject prefab, Transform anchor, StringBuilder report)
    {
        if (prefab == null || anchor == null) return;

        Camera[] mirrors = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,
                                                            FindObjectsSortMode.None);

        foreach (Camera mirror in mirrors)
        {
            if (mirror.targetTexture == null) continue;
            if (!mirror.name.Contains("Mirror")) continue;

            // ⚠ <b>차가 두 대라 거울도 여섯입니다.</b> 어느 차의 거울인지 안 적으면
            // 같은 이름이 두 번씩 나와 무엇을 본 줄인지 알 수 없습니다.
            Vehicle car = mirror.GetComponentInParent<Vehicle>();
            string owner = car != null ? car.name : "어느 차인지 모름";

            int w = mirror.targetTexture.width;
            int h = mirror.targetTexture.height;

            Texture2D before = Read(mirror, w, h);

            GameObject ghost = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            ghost.transform.position = anchor.position;
            ghost.transform.rotation = anchor.rotation;

            Texture2D after = Read(mirror, w, h);
            Object.DestroyImmediate(ghost);

            int moved = Differs(before, after);

            Object.DestroyImmediate(before);
            Object.DestroyImmediate(after);

            report.AppendLine("    거울 " + owner + "/" + mirror.name + " : "
                              + (moved == 0
                                 ? "안 비침"
                                 : "거울의 " + (moved * 100f / (w * h)).ToString("F2") + "%"));
        }
    }

    /// <summary>거울 하나를 그려 읽습니다.</summary>
    /// <param name="mirror">거울 카메라</param>
    /// <param name="w">너비</param>
    /// <param name="h">높이</param>
    private static Texture2D Read(Camera mirror, int w, int h)
    {
        mirror.Render();

        Texture2D shot = new Texture2D(w, h, TextureFormat.RGB24, false);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = mirror.targetTexture;
        shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        shot.Apply();
        RenderTexture.active = previous;

        return shot;
    }

    /// <summary>두 장에서 다른 화소의 수입니다.</summary>
    /// <param name="a">한 장</param>
    /// <param name="b">다른 장</param>
    private static int Differs(Texture2D a, Texture2D b)
    {
        Color[] x = a.GetPixels();
        Color[] y = b.GetPixels();

        int n = 0;

        for (int i = 0; i < x.Length && i < y.Length; i++)
        {
            if (Mathf.Abs(x[i].r - y[i].r) < 0.02f
                && Mathf.Abs(x[i].g - y[i].g) < 0.02f
                && Mathf.Abs(x[i].b - y[i].b) < 0.02f)
            {
                continue;
            }

            n++;
        }

        return n;
    }


    /// <summary>
    /// 실루엣이 가리키는 자리에서 대비를 잽니다.
    /// </summary>
    /// <param name="name">귀신 이름</param>
    /// <param name="away">거리(m)</param>
    /// <param name="withGhost">귀신이 선 화면</param>
    /// <param name="without">귀신을 치운 화면</param>
    /// <param name="silhouette">귀신만 찍은 화면</param>
    /// <param name="report">적을 곳</param>
    private static void Measure(string name, float away, Texture2D withGhost, Texture2D without,
                                Texture2D silhouette, StringBuilder report)
    {
        Color[] a = withGhost.GetPixels();
        Color[] b = without.GetPixels();
        Color[] m = silhouette.GetPixels();

        int seen = 0;
        float ghostSum = 0f;
        float backSum = 0f;
        float best = 0f;

        for (int i = 0; i < a.Length && i < b.Length && i < m.Length; i++)
        {
            // 검은 바탕에 귀신만 그린 그림입니다. 조금이라도 밝으면 그 자리입니다.
            if (Luma(m[i]) <= 0.01f) continue;

            float here = Luma(a[i]);
            float there = Luma(b[i]);

            seen++;
            ghostSum += here;
            backSum += there;

            float gap = Mathf.Abs(here - there);
            if (gap > best) best = gap;
        }

        if (seen == 0)
        {
            report.AppendLine(Head(name, away) + "화면에 한 화소도 안 나타납니다   ← 안 보입니다");
            return;
        }

        float ghost = ghostSum / seen;
        float back = backSum / seen;
        float share = seen * 100f / (Width * Height);

        report.AppendLine(Head(name, away)
                          + "화면의 " + share.ToString("F2") + "% · 귀신 "
                          + ghost.ToString("F3") + " · 배경 " + back.ToString("F3")
                          + " · 대비 " + (ghost - back).ToString("+0.000;-0.000")
                          + " · 가장 센 곳 " + best.ToString("F3"));
    }

    /// <summary>
    /// 한 줄의 머리말입니다. 거리가 0 이면 <b>앵커 자리</b>라 이미 이름이 적혀 있습니다.
    /// </summary>
    /// <param name="name">귀신 이름이나 자리 이름</param>
    /// <param name="away">거리(m). 0 이면 앵커</param>
    private static string Head(string name, float away)
    {
        return away > 0f ? "  " + name + " " + away.ToString("F0") + " m : " : "";
    }

    /// <summary>화면에 찍힌 밝기입니다.</summary>
    /// <param name="c">화소</param>
    private static float Luma(Color c)
    {
        return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }

    /// <summary>한 장 찍습니다.</summary>
    /// <param name="eye">찍을 카메라</param>
    /// <param name="target">그릴 곳</param>
    /// <param name="into">받을 그림</param>
    private static void Grab(Camera eye, RenderTexture target, Texture2D into)
    {
        eye.targetTexture = target;

        for (int i = 0; i < 3; i++)
        {
            eye.Render();
            while (ShaderUtil.anythingCompiling) System.Threading.Thread.Sleep(50);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        into.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        into.Apply();
        RenderTexture.active = previous;

        eye.targetTexture = null;
    }

    /// <summary>
    /// 찍을 카메라입니다. 운전석에서 앞을 봅니다.
    ///
    /// ⚠ <b>URP 설정을 따로 옮깁니다.</b> <c>Camera.CopyFrom</c> 은 후처리 설정을
    /// 복사하지 않습니다. 그대로 두면 이 카메라만 후처리를 건너뛰어, <b>정작 재려던
    /// 그 계단</b>이 빠진 화면을 재게 됩니다.
    /// </summary>
    /// <param name="source">본 카메라</param>
    private static Camera Shot(Camera source)
    {
        Camera camera = new GameObject("GhostShotCamera").AddComponent<Camera>();
        camera.CopyFrom(source);
        camera.targetTexture = null;
        camera.rect = new Rect(0f, 0f, 1f, 1f);

        UniversalAdditionalCameraData from = source.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData to = camera.GetUniversalAdditionalCameraData();

        if (from != null && to != null)
        {
            to.renderPostProcessing = from.renderPostProcessing;
            to.antialiasing = from.antialiasing;
            to.volumeLayerMask = from.volumeLayerMask;
            to.volumeTrigger = from.volumeTrigger;
            to.renderShadows = from.renderShadows;
            to.renderType = CameraRenderType.Base;
        }

        // ⚠ <b>실루엣 레이어를 보게 해 둡니다.</b> 안 그러면 귀신을 그 레이어로 옮기는
        // 순간 <b>본 화면에서 사라져</b>, "안 보인다" 가 아니라 "없다" 를 재게 됩니다.
        camera.cullingMask |= 1 << MaskLayer;

        // 운전석 자리에서 앞을 봅니다. 눈높이만 맞추고 계기판은 비켜 둡니다.
        camera.transform.position = source.transform.position + Vector3.up * 0.2f;
        camera.transform.rotation = Quaternion.Euler(0f, source.transform.eulerAngles.y, 0f);

        return camera;
    }

    /// <summary>
    /// 귀신만 검은 바탕에 찍는 카메라입니다.
    ///
    /// <b>후처리를 끕니다.</b> 여기서 필요한 것은 "어디가 귀신인가" 뿐이고,
    /// 후처리를 거치면 그 실루엣마저 계단과 빗금에 먹힙니다.
    /// </summary>
    /// <param name="source">같은 자리에서 볼 카메라</param>
    private static Camera Mask(Camera source)
    {
        Camera camera = new GameObject("GhostMaskCamera").AddComponent<Camera>();
        camera.CopyFrom(source);

        camera.targetTexture = null;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.cullingMask = 1 << MaskLayer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;

        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();

        if (data != null)
        {
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.renderType = CameraRenderType.Base;
        }

        camera.transform.SetPositionAndRotation(source.transform.position,
                                                source.transform.rotation);

        return camera;
    }

    /// <summary>원하는 밝기를 그대로 돌려주는 가짜 시계입니다.</summary>
    private sealed class Clock : IGameClock
    {
        public float Daylight { get; set; }
        public float TotalMinutes { get { return 0f; } }
        public bool IsNight { get { return Daylight < 0.5f; } }
        public bool IsRunning { get { return true; } }
        public float GetMinutesPerSecond(float fallback) { return fallback; }
        public void AdvanceMinutes(float minutes) { }
    }
}
