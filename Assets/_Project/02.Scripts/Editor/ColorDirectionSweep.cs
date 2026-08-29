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
/// 안개 짙기를 훑으며 화면을 <b>구역별 색상·채도·명도</b>로 재서, 색감이 어디서 무너지는지 봅니다.
///
/// <b>왜 구역별 HSV 인가.</b> 평균 밝기 하나로는 "색감이 좋아졌다" 를 판정할 수 없습니다.
/// 실제로 지금 화면을 재 보니 전경 들판의 <b>채도는 75% 로 레퍼런스와 이미 같았습니다.</b>
/// 무너진 것은 다른 둘이었습니다 —
///   (1) 화면 전체의 색상폭이 18도 안쪽이라 초록도 파랑도 없다(나무 구역이 37도 = 땅과 같은 주황).
///   (2) 들판 명도가 49% 로 레퍼런스(85%)보다 한참 어둡다.
/// 채도만 올리려 들었으면 엉뚱한 것을 고쳤을 것입니다.
///
/// <b>안개를 의심하는 이유.</b> 안개색이 (0.878, 0.812, 0.765) 로 색상 30도쯤인 주황빛입니다.
/// 중경의 초록 나무가 그 색에 묻히면 <b>주황이 됩니다.</b>
///
/// ⚠ 처음에는 씬 YAML 의 density 0.012 를 근거로 "100m 에서 76%" 라고 적었는데
/// <b>그 값은 런타임에 한 번도 쓰이지 않습니다</b> — 사다리가 FogReachFactor/View 로
/// 다시 계산해 0.0063 을 씁니다(100m 에서 33%). 지금은 Linear 로 바꿨으므로
/// 짙기가 아니라 <b>끝 거리</b>를 훑습니다.
///
/// 쓰는 법 (그래픽 장치가 필요하므로 -nographics 를 붙이지 않습니다):
///   Unity.exe -batchmode -projectPath . -executeMethod ColorDirectionSweep.Run
/// 결과: Logs/ColorDir/*.png
/// </summary>
public static class ColorDirectionSweep
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string OutputDirectory = "Logs/ColorDir";

    private const int Width = 960;
    private const int Height = 540;

    /// <summary>
    /// 훑어 볼 안개 <b>끝 거리</b>(m)입니다. 시작 거리는 늘 끝의 절반으로 둡니다.
    ///
    /// 사다리가 내는 값은 시작 166.6m / 끝 280m 입니다. 그보다 가깝게·멀게 두면
    /// 중경의 초록이 얼마나 살아나는지 봅니다.
    /// </summary>
    private static readonly float[] FogEnds = { 120f, 180f, 280f, 400f, 800f };

    private const float NoonSunAngle = 180f - 90f;

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
        Camera source = Camera.main;
        if (sun == null || source == null) { Fail("해 또는 메인 카메라를 찾지 못함"); return; }

        FieldInfo clockField = typeof(SkyController)
            .GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo lateUpdate = typeof(SkyController)
            .GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo activeSkyField = typeof(SkyController)
            .GetField("activeSky", BindingFlags.NonPublic | BindingFlags.Instance);
        if (clockField == null || lateUpdate == null || activeSkyField == null)
        {
            Fail("SkyController 의 clock/LateUpdate/activeSky 를 찾지 못함");
            return;
        }

        // 에디트 모드의 SkyController 는 에셋에 직접 씁니다. 찍는 동안만 복제본을 물립니다.
        Material dayAsset = sky.skyMaterial;
        Material nightAsset = sky.nightSkyMaterial;
        Material skyboxAsset = RenderSettings.skybox;
        sky.skyMaterial = dayAsset != null ? new Material(dayAsset) : null;
        sky.nightSkyMaterial = nightAsset != null ? new Material(nightAsset) : null;
        Material dayClone = sky.skyMaterial;
        Material nightClone = sky.nightSkyMaterial;

        object originalClock = clockField.GetValue(sky);
        clockField.SetValue(sky, new FixedClock { Daylight = 1f });

        Quaternion sunRotation = sun.transform.rotation;
        float sunIntensity = sun.intensity;

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        Camera camera = MakeCamera(source);

        Debug.Log("COLORDIR  안개끝   100m선명도   구역별 [색상도/채도%/명도%]");

        for (int i = 0; i < FogEnds.Length; i++)
        {
            sun.transform.rotation = Quaternion.Euler(NoonSunAngle, 170f, 0f);
            sun.intensity = 1.25f;

            lateUpdate.Invoke(sky, null);
            Material chosen = activeSkyField.GetValue(sky) as Material;
            if (chosen != null) RenderSettings.skybox = chosen;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogEndDistance = FogEnds[i];
            RenderSettings.fogStartDistance = FogEnds[i] * 0.5f;

            Texture2D shot = Grab(camera, target);
            string name = "end" + FogEnds[i].ToString("0", CultureInfo.InvariantCulture);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());

            Report(FogEnds[i], shot);
            Object.DestroyImmediate(shot);
        }

        sky.skyMaterial = dayAsset;
        sky.nightSkyMaterial = nightAsset;
        RenderSettings.skybox = skyboxAsset;
        clockField.SetValue(sky, originalClock);
        sun.transform.rotation = sunRotation;
        sun.intensity = sunIntensity;

        if (dayClone != null) Object.DestroyImmediate(dayClone);
        if (nightClone != null) Object.DestroyImmediate(nightClone);

        camera.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(camera.gameObject);

        Debug.Log("COLORDIR 끝. 그림은 " + OutputDirectory);
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 화면을 네 띠로 나눠 각각의 <b>색상 중앙값·평균 채도·평균 명도</b>를 냅니다.
    ///
    /// 색상은 <b>중앙값</b>을 씁니다. 평균을 쓰면 0도와 350도가 175도(청록)로 섞여
    /// 있지도 않은 색이 나옵니다.
    /// </summary>
    private static void Report(float fogEnd, Texture2D shot)
    {
        // Linear 안개가 100m 에서 남기는 선명도입니다. 시작 전이면 100% 입니다.
        float start = fogEnd * 0.5f;
        float clarity = 1f - Mathf.Clamp01((100f - start) / Mathf.Max(fogEnd - start, 0.01f));

        string sky = Zone(shot, 0.00f, 0.22f);
        string far = Zone(shot, 0.30f, 0.45f);
        string mid = Zone(shot, 0.50f, 0.70f);
        string near = Zone(shot, 0.75f, 0.98f);

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "COLORDIR  끝{0,4:0}m   {1,5:0}%      하늘 {2}  원경 {3}  중경 {4}  전경 {5}",
            fogEnd, clarity * 100f, sky, far, mid, near));
    }

    private static string Zone(Texture2D shot, float y0, float y1)
    {
        int lo = Mathf.RoundToInt(shot.height * (1f - y1));
        int hi = Mathf.RoundToInt(shot.height * (1f - y0));
        Color[] px = shot.GetPixels(0, lo, shot.width, Mathf.Max(1, hi - lo));

        var hues = new System.Collections.Generic.List<float>(px.Length);
        float satSum = 0f, valSum = 0f;

        for (int i = 0; i < px.Length; i++)
        {
            float h, s, v;
            Color.RGBToHSV(px[i], out h, out s, out v);
            hues.Add(h * 360f);
            satSum += s; valSum += v;
        }

        hues.Sort();
        float median = hues[hues.Count / 2];

        return string.Format(CultureInfo.InvariantCulture, "[{0,3:0}/{1,2:0}/{2,2:0}]",
            median, satSum / px.Length * 100f, valSum / px.Length * 100f);
    }

    /// <summary>
    /// ⚠ <c>Camera.CopyFrom</c> 은 URP 의 추가 카메라 데이터를 복사하지 않습니다.
    /// 빠뜨리면 톤매핑·컬러 그레이딩이 통째로 빠져 <b>게임 화면이 아닌 것을 재게</b> 됩니다.
    /// </summary>
    private static Camera MakeCamera(Camera source)
    {
        GameObject go = new GameObject("ColorDirCam");
        Camera camera = go.AddComponent<Camera>();
        camera.CopyFrom(source);

        UniversalAdditionalCameraData sd = source.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData d = camera.GetUniversalAdditionalCameraData();
        if (sd != null && d != null)
        {
            d.renderPostProcessing = sd.renderPostProcessing;
            d.antialiasing = sd.antialiasing;
            d.renderShadows = sd.renderShadows;
            d.volumeLayerMask = sd.volumeLayerMask;
            d.volumeTrigger = sd.volumeTrigger;
        }

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
        foreach (Light l in Object.FindObjectsByType<Light>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional) return l;
        return null;
    }

    private static void Fail(string message)
    {
        Debug.Log("COLORDIR 실패: " + message);
        EditorApplication.Exit(1);
    }
}
