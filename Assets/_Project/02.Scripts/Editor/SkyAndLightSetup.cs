using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 하늘 머티리얼을 만들고 씬의 조명 설정을 한 번에 잡아 줍니다.
///
/// 손으로 하려면 Lighting 창과 여러 컴포넌트를 오가야 하는데, 값이 서로 맞물려 있어
/// 하나만 어긋나도 밤이 새까매지거나 낮이 하얗게 날아갑니다. 그래서 한 곳에 모읍니다.
///
/// 하는 일:
///  1. CarDrive/Sky 머티리얼을 만들어 씬의 스카이박스로 지정합니다.
///  2. SkyController를 씬에 두고 태양광을 연결합니다.
///  3. 안개를 주행할 수 있는 거리로 맞춥니다.
///  4. WeatherRig에 차량 헤드라이트를 연결합니다. (비어 있으면 날씨가 시야를 못 줄입니다)
///  5. 주변광의 주인을 SkyController 하나로 정리합니다. (WeatherRig 쪽은 끕니다)
/// </summary>
public static class SkyAndLightSetup
{
    // --- Constants ---

    /// <summary>만들어 낸 하늘 머티리얼을 둘 경로입니다.</summary>
    private const string SkyMaterialPath = "Assets/_Project/04.Art/03.Shaders/Sky/CarDriveSky.mat";

    /// <summary>하늘 셰이더의 이름입니다.</summary>
    private const string SkyShaderName = "CarDrive/Sky";

    /// <summary>메인 씬 경로입니다.</summary>
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

    // --- Public Methods ---

    /// <summary>에디터 메뉴에서 실행합니다.</summary>
    [MenuItem("CarDrive/World/하늘과 조명 설정")]
    public static void Setup()
    {
        List<string> report = new List<string>();

        Material sky = CreateSkyMaterial(report);
        ApplyRenderSettings(sky, report);
        EnsureSkyController(sky, report);
        WireWeatherRig(report);

        EditorSceneManager.MarkAllScenesDirty();

        Debug.Log("SkyAndLightSetup:" + System.Environment.NewLine +
                  string.Join(System.Environment.NewLine, report));
    }

    /// <summary>
    /// 명령줄에서 씬을 열고 설정한 뒤 저장합니다.
    /// <c>Unity.exe -batchmode -quit -executeMethod SkyAndLightSetup.SetupFromCommandLine</c>
    /// </summary>
    public static void SetupFromCommandLine()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Setup();

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 설정 결과가 실제로 적용되었는지 확인해 로그로 남깁니다.
    /// 특히 셰이더가 컴파일되는지를 봅니다. 스카이박스 셰이더가 깨지면 하늘이 분홍색이 됩니다.
    /// <c>Unity.exe -batchmode -quit -executeMethod SkyAndLightSetup.VerifyFromCommandLine</c>
    /// </summary>
    public static void VerifyFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        List<string> lines = new List<string>();
        lines.Add("SKY VERIFY ===========================");

        Shader shader = Shader.Find(SkyShaderName);
        if (shader == null)
        {
            lines.Add("셰이더: 찾지 못함 !!");
        }
        else
        {
            bool broken = ShaderUtil.ShaderHasError(shader);
            int msgs = ShaderUtil.GetShaderMessageCount(shader);
            lines.Add("셰이더: " + shader.name + " | 오류 " + (broken ? "있음 !!" : "없음") +
                      " | 메시지 " + msgs + "개");

            if (msgs > 0)
            {
                // 반환 타입이 Unity 버전마다 네임스페이스가 달라 var로 받습니다.
                var m = ShaderUtil.GetShaderMessages(shader);
                for (int i = 0; i < m.Length && i < 6; i++)
                {
                    lines.Add("   " + m[i].severity + ": " + m[i].message.Trim());
                }
            }
        }

        Material sky = RenderSettings.skybox;
        lines.Add("스카이박스: " + (sky != null ? sky.name + " (" + sky.shader.name + ")" : "없음 !!"));

        if (sky != null && sky.HasProperty("_DayFactor"))
        {
            lines.Add("  _DayFactor 프로퍼티: 있음");
        }
        else if (sky != null)
        {
            lines.Add("  _DayFactor 프로퍼티: 없음 !! (SkyController가 제어할 수 없습니다)");
        }

        lines.Add("안개: " + (RenderSettings.fog ? "켬" : "끔") + " | " + RenderSettings.fogMode +
                  " | 밀도 " + RenderSettings.fogDensity.ToString("F4") +
                  " (가시거리 약 " + (Mathf.Sqrt(Mathf.Log(20f)) / Mathf.Max(0.0001f, RenderSettings.fogDensity)).ToString("F0") + "m)");

        SkyController ctrl = Object.FindAnyObjectByType<SkyController>();
        lines.Add("SkyController: " + (ctrl != null ? "있음 | 머티리얼 " + (ctrl.skyMaterial != null ? "연결" : "없음 !!") +
                  " | 태양광 " + (ctrl.sun != null ? ctrl.sun.name : "없음 !!") : "없음 !!"));

        WeatherRig rig = Object.FindAnyObjectByType<WeatherRig>();
        lines.Add("WeatherRig: " + (rig != null
            ? "헤드라이트 " + (rig.headlights != null ? rig.headlights.Count : 0) + "개 | controlAmbient " + (rig.controlAmbient ? "켬 !!" : "끔")
            : "없음 !!"));

        lines.Add("주변광 모드: " + RenderSettings.ambientMode);
        lines.Add("======================================");

        Debug.Log(string.Join(System.Environment.NewLine, lines));
    }

    /// <summary>
    /// 시간대별로 하늘을 실제로 렌더링해 PNG로 저장합니다.
    /// 수치만으로는 하늘이 제대로 보이는지 알 수 없어 눈으로 확인할 그림을 남깁니다.
    /// <c>Unity.exe -batchmode -quit -executeMethod SkyAndLightSetup.CaptureFromCommandLine</c>
    /// </summary>
    public static void CaptureFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Material sky = RenderSettings.skybox;
        if (sky == null)
        {
            Debug.LogError("CAPTURE: 스카이박스가 없습니다.");
            EditorApplication.Exit(1);
            return;
        }

        // Temp 는 Unity 가 종료할 때 지웁니다. Logs 는 남고 .gitignore 에도 들어 있습니다.
        string outDir = "Logs/SkyCapture";
        Directory.CreateDirectory(outDir);

        // 이 도구는 머티리얼 프로퍼티를 직접 건드립니다. 그대로 두면 에셋이 수정된 채로 남아
        // 커밋할 때마다 의미 없는 변경이 따라붙습니다. 끝나면 되돌립니다.
        float savedDay = sky.GetFloat("_DayFactor");
        Vector4 savedSun = sky.GetVector("_SunDirection");

        GameObject camGo = new GameObject("CaptureCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 60f;
        cam.transform.position = new Vector3(0f, 20f, 0f);

        // 해가 뜨는 쪽(동쪽)을 바라보되 살짝 위를 봅니다.
        cam.transform.rotation = Quaternion.Euler(-8f, 90f, 0f);

        // 시각, 낮 정도, 해의 고도(도)
        float[][] moments =
        {
            new[] { 5.5f,  0.05f, -4f  },   // 여명 직전
            new[] { 7f,    0.35f,  12f },   // 아침
            new[] { 13f,   1.00f,  70f },   // 한낮
            new[] { 19.5f, 0.25f,   6f },   // 해 질 녘
            new[] { 23f,   0.00f, -35f }    // 한밤
        };

        RenderTexture rt = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32);
        Texture2D shot = new Texture2D(480, 270, TextureFormat.RGB24, false);

        for (int i = 0; i < moments.Length; i++)
        {
            float hour = moments[i][0];
            float day = moments[i][1];
            float elevation = moments[i][2];

            sky.SetFloat("_DayFactor", day);

            // 동쪽 낮은 하늘에 해를 둡니다. (셰이더는 '해가 있는 방향'을 받습니다)
            Vector3 toSun = Quaternion.Euler(-elevation, 90f, 0f) * Vector3.forward;
            sky.SetVector("_SunDirection", toSun);

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            shot.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
            shot.Apply();
            RenderTexture.active = prev;

            string path = outDir + "/sky_" + hour.ToString("00.0").Replace('.', '_') + ".png";
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Debug.Log("CAPTURE: " + path + "  (낮 " + day.ToString("F2") + ", 고도 " + elevation + "도)");
        }

        cam.targetTexture = null;
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(shot);
        rt.Release();
        Object.DestroyImmediate(rt);

        sky.SetFloat("_DayFactor", savedDay);
        sky.SetVector("_SunDirection", savedSun);
        AssetDatabase.SaveAssets();

        Debug.Log("CAPTURE: 완료 -> " + outDir);
    }

    /// <summary>
    /// 한낮에 맵이 왜 어두운지 알아보기 위해, 조명 조합을 바꿔 가며 렌더링하고
    /// 지면의 실제 밝기를 재서 로그로 남깁니다.
    /// <c>Unity.exe -batchmode -quit -executeMethod SkyAndLightSetup.DiagnoseFromCommandLine</c>
    /// </summary>
    public static void DiagnoseFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Material sky = RenderSettings.skybox;
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            SkyController c = Object.FindAnyObjectByType<SkyController>();
            if (c != null) sun = c.sun;
        }
        if (sun == null) { Debug.LogError("DIAG: 태양광을 찾지 못했습니다."); EditorApplication.Exit(1); return; }

        string outDir = "Logs/LightDiag";
        Directory.CreateDirectory(outDir);

        // 머티리얼을 건드린 뒤 되돌립니다. (Capture 와 같은 이유)
        float savedDay = sky != null ? sky.GetFloat("_DayFactor") : 0f;
        Vector4 savedSun = sky != null ? sky.GetVector("_SunDirection") : Vector4.zero;

        // 한낮으로 고정합니다.
        if (sky != null)
        {
            sky.SetFloat("_DayFactor", 1f);
            sky.SetVector("_SunDirection", new Vector4(0.2f, 0.95f, 0.2f, 0f).normalized);
        }
        sun.transform.rotation = Quaternion.Euler(60f, 30f, 0f);
        sun.color = new Color(1f, 0.96f, 0.87f);

        GameObject camGo = new GameObject("DiagCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 60f;
        // 북쪽 길을 내려다봅니다. 지면이 화면 대부분을 차지해야 밝기를 잴 수 있습니다.
        cam.transform.position = new Vector3(6f, 26f, 40f);
        cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

        // 이름, 주변광 배율, 태양 세기
        // 낮과 밤을 함께 재야 합니다. 낮만 보고 밝기를 올리면 밤이 같이 밝아져
        // 헤드라이트가 의미를 잃습니다.
        object[][] configs =
        {
            new object[] { "낮_한낮",   1.0f, 1.6f, true  },
            new object[] { "밤_한밤",   1.0f, 1.6f, false }
        };

        Color dayAmbient = new Color(0.42f, 0.47f, 0.55f);
        Color nightAmbient = new Color(0.055f, 0.065f, 0.095f);
        float moonIntensity = 0.12f;
        RenderTexture rt = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32);
        Texture2D shot = new Texture2D(480, 270, TextureFormat.RGB24, false);

        for (int i = 0; i < configs.Length; i++)
        {
            string name = (string)configs[i][0];
            float ambScale = (float)configs[i][1];
            float sunMax = (float)configs[i][2];
            bool isDay = (bool)configs[i][3];

            Color amb = (isDay ? dayAmbient : nightAmbient) * ambScale;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = amb;
            RenderSettings.ambientEquatorColor = amb * 0.7f;
            RenderSettings.ambientGroundColor = amb * 0.35f;

            sun.intensity = isDay ? sunMax : moonIntensity;
            sun.color = isDay ? new Color(1f, 0.96f, 0.87f) : new Color(0.55f, 0.66f, 0.95f);
            sun.transform.rotation = isDay ? Quaternion.Euler(60f, 30f, 0f) : Quaternion.Euler(-40f, 30f, 0f);

            if (sky != null) sky.SetFloat("_DayFactor", isDay ? 1f : 0f);

            RenderSettings.fogColor = isDay
                ? new Color(0.62f, 0.66f, 0.72f)
                : new Color(0.045f, 0.055f, 0.085f);

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            shot.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
            shot.Apply();
            RenderTexture.active = prev;

            // 화면 아래쪽 절반만 재면 대부분 지면입니다.
            Color[] px = shot.GetPixels(0, 0, 480, 135);
            float lum = 0f;
            for (int p = 0; p < px.Length; p++)
            {
                lum += 0.2126f * px[p].r + 0.7152f * px[p].g + 0.0722f * px[p].b;
            }
            lum /= px.Length;

            File.WriteAllBytes(outDir + "/" + name + ".png", shot.EncodeToPNG());
            Debug.Log("DIAG " + name + ": 지면 평균 밝기 " + (lum * 255f).ToString("F1") + " / 255  (" + (lum * 100f).ToString("F1") + "%)");
        }

        cam.targetTexture = null;
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(shot);
        rt.Release();
        Object.DestroyImmediate(rt);

        if (sky != null)
        {
            sky.SetFloat("_DayFactor", savedDay);
            sky.SetVector("_SunDirection", savedSun);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// 빌드에 끌려 들어가던 구 타일 프리팹 참조를 끊습니다.
    ///
    /// 월드는 이제 미리 구운 터레인(bakedRoot)을 쓰므로 WorldStreamer 의 tilePrefabs 는
    /// 실행 중에 쓰이지 않습니다. 그런데 참조가 남아 있으면 그 프리팹과 프리팹이 물고 있는
    /// TerrainData 가 빌드에 포함됩니다. 그 TerrainData 가 손상되어 빌드가 실패했습니다.
    /// <c>Unity.exe -batchmode -quit -executeMethod SkyAndLightSetup.DropLegacyTilesFromCommandLine</c>
    /// </summary>
    public static void DropLegacyTilesFromCommandLine()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        WorldStreamer streamer = Object.FindAnyObjectByType<WorldStreamer>();
        if (streamer == null)
        {
            Debug.LogError("DROP: WorldStreamer 를 찾지 못했습니다.");
            EditorApplication.Exit(1);
            return;
        }

        if (streamer.bakedRoot == null)
        {
            Debug.LogError("DROP: bakedRoot 가 비어 있습니다. 구운 월드가 없으면 tilePrefabs 를 지우면 안 됩니다.");
            EditorApplication.Exit(1);
            return;
        }

        int before = streamer.tilePrefabs != null ? streamer.tilePrefabs.Count : 0;

        Undo.RecordObject(streamer, "구 타일 참조 제거");
        if (streamer.tilePrefabs != null) streamer.tilePrefabs.Clear();
        EditorUtility.SetDirty(streamer);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("DROP: tilePrefabs 에서 " + before + "개를 비웠습니다. (bakedRoot 사용 중)");
    }

    // --- Private Methods ---

    /// <summary>
    /// 하늘 머티리얼을 만들거나 이미 있으면 가져옵니다.
    /// </summary>
    /// <param name="report">결과를 적을 목록</param>
    /// <returns>하늘 머티리얼. 셰이더를 찾지 못하면 null입니다.</returns>
    private static Material CreateSkyMaterial(List<string> report)
    {
        Shader shader = Shader.Find(SkyShaderName);
        if (shader == null)
        {
            report.Add("  [실패] " + SkyShaderName + " 셰이더를 찾지 못했습니다.");
            return null;
        }

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (existing != null)
        {
            report.Add("  하늘 머티리얼: 기존 것을 씁니다.");
            return existing;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(SkyMaterialPath));

        Material mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, SkyMaterialPath);

        report.Add("  하늘 머티리얼: 새로 만들었습니다. " + SkyMaterialPath);
        return mat;
    }

    /// <summary>
    /// 스카이박스와 안개를 지정합니다.
    /// </summary>
    /// <param name="sky">쓸 하늘 머티리얼</param>
    /// <param name="report">결과를 적을 목록</param>
    private static void ApplyRenderSettings(Material sky, List<string> report)
    {
        if (sky != null)
        {
            RenderSettings.skybox = sky;
            report.Add("  스카이박스: CarDrive/Sky 로 교체했습니다. (이전: Unity 기본 프로시저럴)");
        }

        // 안개는 지수 제곱이라 밀도가 조금만 올라가도 시야가 급격히 줄어듭니다.
        //
        //   보이는 거리 ~= sqrt(ln(20)) / density
        //   density 0.05 -> 약 35m,  0.012 -> 약 145m
        //
        // 35m는 시속 60km에서 2초 앞밖에 보이지 않아 주행이 성립하지 않습니다.
        // 여기서 잡는 값은 <b>가장 궂은 날씨일 때의 상한</b>이고, 평소에는
        // WeatherRig가 이보다 훨씬 옅게 씁니다.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.012f;

        report.Add("  안개: 밀도 0.05 -> 0.012 (가시거리 약 35m -> 145m)");

        // SkyController가 실행 중에 Trilight로 바꾸지만, 씬에도 같은 값을 저장해 둡니다.
        // 그러지 않으면 씬을 연 직후 첫 프레임까지 주변광이 하늘색(별빛 포함)으로 잡혀
        // 밤이 잠깐 밝게 보입니다.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        report.Add("  주변광 모드: Skybox -> Trilight (SkyController가 색을 몰아 줍니다)");

        // 한낮 태양 세기입니다. Linear 컬러 스페이스에서 1.0 은 정오치고 약합니다.
        // 자동 노출이 없는 파이프라인이라 이 값이 그대로 화면 밝기가 됩니다.
        TimeSystem time = Object.FindAnyObjectByType<TimeSystem>();
        if (time != null && time.sunMaxIntensity < 1.5f)
        {
            Undo.RecordObject(time, "하늘과 조명 설정");
            report.Add("  태양 최대 세기: " + time.sunMaxIntensity.ToString("F2") + " -> 1.60");
            time.sunMaxIntensity = 1.6f;
            EditorUtility.SetDirty(time);
        }
    }

    /// <summary>
    /// 씬에 SkyController를 두고 태양광을 연결합니다.
    /// </summary>
    /// <param name="sky">쓸 하늘 머티리얼</param>
    /// <param name="report">결과를 적을 목록</param>
    private static void EnsureSkyController(Material sky, List<string> report)
    {
        SkyController controller = Object.FindAnyObjectByType<SkyController>();

        if (controller == null)
        {
            TimeSystem time = Object.FindAnyObjectByType<TimeSystem>();

            // 시간 시스템 옆에 두는 편이 찾기 쉽습니다.
            GameObject host = time != null ? time.gameObject : new GameObject("SkyController");
            controller = Undo.AddComponent<SkyController>(host);

            report.Add("  SkyController: " + host.name + " 에 붙였습니다.");
        }
        else
        {
            report.Add("  SkyController: 이미 있습니다.");
        }

        Undo.RecordObject(controller, "하늘과 조명 설정");
        controller.skyMaterial = sky;

        if (controller.sun == null)
        {
            TimeSystem time = Object.FindAnyObjectByType<TimeSystem>();
            if (time != null && time.sunLight != null) controller.sun = time.sunLight;
        }

        if (controller.sun == null) report.Add("  [주의] 태양광을 찾지 못했습니다. TimeSystem.sunLight 를 확인하세요.");
        EditorUtility.SetDirty(controller);
    }

    /// <summary>
    /// WeatherRig에 차량 헤드라이트를 연결하고, 주변광 제어를 SkyController에 넘깁니다.
    /// </summary>
    /// <param name="report">결과를 적을 목록</param>
    private static void WireWeatherRig(List<string> report)
    {
        WeatherRig rig = Object.FindAnyObjectByType<WeatherRig>();
        if (rig == null)
        {
            report.Add("  [주의] WeatherRig 를 찾지 못했습니다.");
            return;
        }

        Undo.RecordObject(rig, "하늘과 조명 설정");

        // 주변광은 SkyController가 소유합니다. 둘 다 켜면 매 프레임 서로를 덮어씁니다.
        if (rig.controlAmbient)
        {
            rig.controlAmbient = false;
            report.Add("  WeatherRig.controlAmbient: 껐습니다. (주변광은 SkyController가 담당)");
        }

        // 헤드라이트가 비어 있으면 날씨가 시야를 줄여도 불빛은 그대로라 티가 나지 않습니다.
        if (rig.headlights == null) rig.headlights = new List<Light>();

        if (rig.headlights.Count == 0)
        {
            List<Light> found = FindVehicleHeadlights();
            if (found.Count > 0)
            {
                rig.headlights.AddRange(found);
                report.Add("  WeatherRig.headlights: 차량 조명 " + found.Count + "개를 연결했습니다.");
            }
            else
            {
                report.Add("  [주의] 차량에서 헤드라이트를 찾지 못했습니다. 직접 연결하세요.");
            }
        }
        else
        {
            report.Add("  WeatherRig.headlights: 이미 " + rig.headlights.Count + "개 연결되어 있습니다.");
        }

        EditorUtility.SetDirty(rig);
    }

    /// <summary>
    /// 씬의 차량에서 전방을 비추는 조명을 찾습니다.
    /// 스포트라이트이거나 이름에 head/front가 들어간 것을 헤드라이트로 봅니다.
    /// </summary>
    /// <returns>찾은 조명 목록</returns>
    private static List<Light> FindVehicleHeadlights()
    {
        List<Light> result = new List<Light>();

        Vehicle[] vehicles = Object.FindObjectsByType<Vehicle>(FindObjectsInactive.Include);
        for (int v = 0; v < vehicles.Length; v++)
        {
            Light[] lights = vehicles[v].GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;

                string n = lights[i].name.ToLowerInvariant();
                bool looksLikeHeadlight = lights[i].type == LightType.Spot ||
                                          n.Contains("head") || n.Contains("front");

                if (looksLikeHeadlight && !result.Contains(lights[i])) result.Add(lights[i]);
            }
        }

        return result;
    }
}
