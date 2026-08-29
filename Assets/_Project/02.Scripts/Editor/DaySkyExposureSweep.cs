using System.Globalization;
using System.IO;
using System.Reflection;
using CarDrive.Common;
using CarDrive.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 낮 하늘 노출(<c>SkyController.dayExposure</c>)을 여러 값으로 찍어 구름이 언제 살아나는지 봅니다.
///
/// <b>왜 훑는가.</b> 지금 낮 하늘은 밝기가 195 로 잘리지도 않았는데 구름이 안 보입니다.
/// 대비를 재 보면 편차가 <b>9</b> 밖에 안 됩니다 — 같은 사진인데 일몰(편차 27)에는 구름이
/// 또렷합니다. 밝은 쪽이 다 뭉친 것이라, 값 하나를 눈대중으로 고르면 이번에도 뭉칩니다.
/// 밤 하늘 노출을 정할 때도 훑어서 0.04 를 찾았습니다.
///
/// <b>대비를 함께 잽니다.</b> 그림만 보면 "좀 나아졌나" 로 넘어가기 쉬운데, 편차는 다투지
/// 않습니다. 일몰의 27 을 목표로 삼고, 낮이 그 근처까지 올라오는 값을 고릅니다.
///
/// <b>진짜 SkyController 를 돌립니다.</b> 코드 기본값으로 찍으면 씬에서 맞춰 둔 값과 다른
/// 그림이 나옵니다 — 예전에 그렇게 해서 전부 다시 찍었습니다.
///
/// 쓰는 법 (그래픽 장치가 필요하므로 -nographics 를 붙이지 않습니다):
///   Unity.exe -batchmode -projectPath . -executeMethod DaySkyExposureSweep.Run
/// 결과: Logs/DaySky/*.png
/// </summary>
public static class DaySkyExposureSweep
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string OutputDirectory = "Logs/DaySky";

    private const int Width = 960;
    private const int Height = 540;

    /// <summary>
    /// 런타임에 ViewRangeScaler 가 쓰는 안개 거리입니다.
    ///
    /// <b>짙기가 아니라 거리입니다.</b> 예전에는 여기에 짙기를 적어 두었는데, 그 값이
    /// 씬 YAML 의 m_FogDensity 였고 <b>런타임에 한 번도 쓰이지 않는 값</b>이었습니다.
    /// 사다리가 시야 거리에서 다시 계산하기 때문입니다. 그래서 캡처가 게임 화면이 아닌 것을
    /// 보여 주고 있었습니다.
    ///
    /// 지금 사다리 값: FadeStart = 340 x 0.7 x 0.70 = 166.6m,
    /// TerrainActive = max(340, 400) x 0.7 = 280m.
    /// </summary>
    private const float RuntimeFogStart = 166.6f;
    private const float RuntimeFogEnd = 280f;

    /// <summary>훑어 볼 노출 값들입니다. 지금 값은 1.1 입니다.</summary>
    private static readonly float[] Exposures = { 1.1f, 0.85f, 0.7f, 0.55f, 0.45f, 0.35f };

    /// <summary>정오. 가장 밝아서 뭉치는 시각입니다.</summary>
    private const float NoonSunAngle = 180f - 90f;

    /// <summary>가짜 시계입니다. SkyController 가 이것만 봅니다.</summary>
    private sealed class FixedClock : IGameClock
    {
        public float Daylight { get; set; }
        public float TotalMinutes { get { return 0f; } }
        public bool IsNight { get { return Daylight < 0.5f; } }
        public bool IsRunning { get { return true; } }
        public float GetMinutesPerSecond(float fallback) { return fallback; }
        public void AdvanceMinutes(float minutes) { }
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(OutputDirectory);

        SkyController sky = Object.FindAnyObjectByType<SkyController>(FindObjectsInactive.Include);
        if (sky == null) { Fail("SkyController 를 찾지 못함"); return; }

        Light sun = sky.sun != null ? sky.sun : FindSun();
        if (sun == null) { Fail("디렉셔널 라이트를 찾지 못함"); return; }

        Camera source = Camera.main;
        if (source == null) { Fail("메인 카메라를 찾지 못함"); return; }

        FieldInfo clockField = typeof(SkyController)
            .GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo lateUpdate = typeof(SkyController)
            .GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo activeSkyField = typeof(SkyController)
            .GetField("activeSky", BindingFlags.NonPublic | BindingFlags.Instance);

        if (clockField == null || lateUpdate == null || activeSkyField == null)
        {
            Fail("SkyController 의 clock/LateUpdate/activeSky 를 찾지 못함 — 이름이 바뀌었는지 확인하십시오");
            return;
        }

        // 에디트 모드의 SkyController 는 <b>에셋에 직접 씁니다.</b> 찍는 동안만 복제본을 물려
        // 프로젝트 파일이 더러워지지 않게 합니다.
        Material dayAsset = sky.skyMaterial;
        Material nightAsset = sky.nightSkyMaterial;
        Material skyboxAsset = RenderSettings.skybox;
        Material dayClone = dayAsset != null ? new Material(dayAsset) : null;
        Material nightClone = nightAsset != null ? new Material(nightAsset) : null;
        sky.skyMaterial = dayClone;
        sky.nightSkyMaterial = nightClone;

        object originalClock = clockField.GetValue(sky);
        float originalExposure = sky.dayExposure;
        Quaternion sunRotation = sun.transform.rotation;
        float sunIntensity = sun.intensity;
        Color sunColor = sun.color;

        FixedClock clock = new FixedClock { Daylight = 1f };
        clockField.SetValue(sky, clock);

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        Camera camera = MakeCamera(source);

        Debug.Log("DAYSKY 낮 하늘: " + (dayAsset != null ? dayAsset.name : "(없음)"));
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "DAYSKY {0,-10} {1,-10} {2,-10} {3}", "노출", "하늘평균", "하늘편차", "판정"));

        for (int i = 0; i < Exposures.Length; i++)
        {
            sky.dayExposure = Exposures[i];

            sun.transform.rotation = Quaternion.Euler(NoonSunAngle, 170f, 0f);
            sun.intensity = 1.25f;

            lateUpdate.Invoke(sky, null);

            // SkyController 는 재생 중에만 RenderSettings.skybox 를 갈아 끼웁니다.
            // 여기서는 그것이 고른 하늘을 직접 물려 줍니다.
            Material chosen = activeSkyField.GetValue(sky) as Material;
            if (chosen != null) RenderSettings.skybox = chosen;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = RuntimeFogStart;
            RenderSettings.fogEndDistance = RuntimeFogEnd;

            Texture2D shot = Grab(camera, target);
            string name = "exp" + Exposures[i].ToString("0.00", CultureInfo.InvariantCulture);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());

            Report(Exposures[i], shot);
            Object.DestroyImmediate(shot);
        }

        // ── 이 캡처에 볼륨(후처리)이 실제로 걸렸는지 확인합니다 ──
        //
        // <b>따지지 말고 잽니다.</b> Camera.CopyFrom 은 URP 의 추가 카메라 데이터를 복사하지
        // 않아서, 그것을 손으로 옮기지 않으면 톤매핑·컬러 그레이딩이 통째로 빠집니다.
        // 그러면 위에서 잰 대비 수치가 <b>게임 화면이 아닌 것을 잰 값</b>이 됩니다.
        // 후처리를 껐다 켜서 그림이 달라지는지 보면 다툴 것이 없습니다.
        VerifyPostProcessing(camera, target, sky, lateUpdate, activeSkyField, originalExposure);

        // 되돌립니다. 씬은 저장하지 않지만 메모리 상태도 원래대로 두는 편이 안전합니다.
        sky.dayExposure = originalExposure;
        sky.skyMaterial = dayAsset;
        sky.nightSkyMaterial = nightAsset;
        RenderSettings.skybox = skyboxAsset;
        clockField.SetValue(sky, originalClock);
        sun.transform.rotation = sunRotation;
        sun.intensity = sunIntensity;
        sun.color = sunColor;

        if (dayClone != null) Object.DestroyImmediate(dayClone);
        if (nightClone != null) Object.DestroyImmediate(nightClone);

        camera.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(camera.gameObject);

        Debug.Log("DAYSKY 끝. 그림은 " + OutputDirectory);
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 후처리를 켠 그림과 끈 그림이 다른지 재서, 볼륨이 실제로 걸렸는지 확인합니다.
    ///
    /// 같으면 볼륨이 안 걸린 것이고, 그러면 이 도구가 잰 대비는 게임 화면의 값이 아닙니다.
    /// </summary>
    private static void VerifyPostProcessing(Camera camera, RenderTexture target, SkyController sky,
                                             MethodInfo lateUpdate, FieldInfo activeSkyField,
                                             float exposure)
    {
        sky.dayExposure = exposure;
        lateUpdate.Invoke(sky, null);
        Material chosen = activeSkyField.GetValue(sky) as Material;
        if (chosen != null) RenderSettings.skybox = chosen;

        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        bool had = data != null && data.renderPostProcessing;

        Texture2D on = Grab(camera, target);
        if (data != null) data.renderPostProcessing = false;
        Texture2D off = Grab(camera, target);
        if (data != null) data.renderPostProcessing = had;

        Color32[] a = on.GetPixels32();
        Color32[] b = off.GetPixels32();

        long diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff += Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);

        float avg = (float)diff / (a.Length * 3);

        File.WriteAllBytes(Path.Combine(OutputDirectory, "postON.png"), on.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(OutputDirectory, "postOFF.png"), off.EncodeToPNG());

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "DAYSKY 후처리 확인: 켬/끔 채널당 평균차 {0:0.00} -> {1}",
            avg, avg >= 1.0f ? "볼륨이 걸려 있음" : "<<<< 볼륨이 안 걸림. 위 수치는 못 믿음"));

        Object.DestroyImmediate(on);
        Object.DestroyImmediate(off);
    }

    /// <summary>
    /// 하늘 부분의 평균과 <b>편차</b>를 잽니다.
    ///
    /// 평균은 "얼마나 밝은가" 이고 편차는 "구름이 보이는가" 입니다. 밝기가 255 로 잘리지
    /// 않아도 밝은 쪽이 뭉치면 편차가 죽습니다 — 지금 낮이 정확히 그 상태(편차 9)입니다.
    /// </summary>
    private static void Report(float exposure, Texture2D shot)
    {
        int rows = Mathf.Max(1, Mathf.RoundToInt(shot.height * 0.22f));
        Color[] px = shot.GetPixels(0, shot.height - rows, shot.width, rows);

        float sum = 0f;
        for (int i = 0; i < px.Length; i++)
            sum += px[i].r * 0.299f + px[i].g * 0.587f + px[i].b * 0.114f;
        float mean = sum / px.Length;

        float varSum = 0f;
        for (int i = 0; i < px.Length; i++)
        {
            float l = px[i].r * 0.299f + px[i].g * 0.587f + px[i].b * 0.114f;
            varSum += (l - mean) * (l - mean);
        }
        float sd = Mathf.Sqrt(varSum / px.Length);

        // 일몰의 편차가 27/255 = 0.106 입니다. 그 근처면 구름이 읽힙니다.
        string verdict = sd * 255f >= 22f ? "구름 보임"
                       : sd * 255f >= 14f ? "희미함"
                       : "뭉갬";

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "DAYSKY {0,-10:0.00} {1,-10:0.0} {2,-10:0.0} {3}",
            exposure, mean * 255f, sd * 255f, verdict));
    }

    /// <summary>
    /// 바깥 풍경을 보는 카메라입니다. 운전석은 절반이 계기판이라 하늘이 조금밖에 안 보입니다.
    ///
    /// ⚠ <c>Camera.CopyFrom</c> 은 URP 의 추가 카메라 데이터를 복사하지 않습니다.
    /// 빠뜨리면 톤매핑이 안 걸려 하늘이 <b>순백</b>으로 찍힙니다 — 실제로 그렇게 나온 적이 있습니다.
    /// </summary>
    private static Camera MakeCamera(Camera source)
    {
        GameObject go = new GameObject("DaySkyCam");
        Camera camera = go.AddComponent<Camera>();
        camera.CopyFrom(source);

        UniversalAdditionalCameraData sourceData = source.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        if (sourceData != null && data != null)
        {
            data.renderPostProcessing = sourceData.renderPostProcessing;
            data.antialiasing = sourceData.antialiasing;
            data.renderShadows = sourceData.renderShadows;
            data.volumeLayerMask = sourceData.volumeLayerMask;
            data.volumeTrigger = sourceData.volumeTrigger;
        }

        // 지평선이 화면 가운데쯤 오게 두어 하늘과 땅을 함께 봅니다.
        go.transform.position = source.transform.position + Vector3.up * 6f - source.transform.forward * 12f;
        go.transform.rotation = Quaternion.Euler(4f, source.transform.eulerAngles.y, 0f);
        return camera;
    }

    private static Texture2D Grab(Camera camera, RenderTexture target)
    {
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = null;

        Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        shot.Apply();
        RenderTexture.active = prev;
        return shot;
    }

    private static Light FindSun()
    {
        foreach (Light light in Object.FindObjectsByType<Light>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (light.type == LightType.Directional) return light;
        return null;
    }

    private static void Fail(string message)
    {
        Debug.Log("DAYSKY 실패: " + message);
        EditorApplication.Exit(1);
    }
}
