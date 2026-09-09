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
/// <b>실제 씬</b>을 열어 시각대별로 게임 화면을 찍습니다. 룩 변경의 최종 확인용입니다.
///
/// <b>왜 하늘만 찍어서는 부족한가.</b> 하늘만 따로 보면 잘 나와도, 지면·안개와 붙었을 때
/// 지평선에 이음매가 생기거나 색이 어긋날 수 있습니다. 화면에 나가는 그림은 지형 103장과
/// 안개, 그리고 볼륨 그레이딩을 모두 통과한 뒤의 것입니다.
///
/// <b>값을 흉내 내지 않고 진짜 <see cref="SkyController"/>를 돌립니다.</b>
/// 예전에는 이 스크립트가 하늘·주변광·안개 색을 상수로 베껴 두고 흉내 냈는데,
/// 씬 인스펙터가 코드 기본값을 덮어쓰고 있어서 <b>캡처가 실제 게임보다 차갑게 나왔습니다.</b>
/// 지금은 가짜 시계를 꽂고 <c>LateUpdate</c>를 직접 불러, 게임이 쓰는 그 경로로 값을 채웁니다.
/// 그래서 씬에서 색을 바꾸면 캡처도 따라 바뀝니다 — 손으로 맞출 것이 없습니다.
///
/// <b>에셋을 더럽히지 않습니다.</b> 편집 모드의 SkyController 는 재질 에셋에 직접 쓰므로,
/// 찍는 동안만 복제본을 물려 두고 끝나면 원래 참조로 되돌립니다.
///
/// 쓰는 법 (그래픽 장치가 필요하므로 -nographics 를 붙이지 않습니다):
///   Unity.exe -batchmode -projectPath . -executeMethod SceneLookCapture.Run
/// 결과: Logs/SceneLook/*.png
/// </summary>
public static class SceneLookCapture
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string OutputDirectory = "Logs/SceneLook";
    private const int Width = 960;
    private const int Height = 540;

    /// <summary>
    /// ViewRangeScaler 가 런타임에 거는 안개 <b>거리</b>입니다.
    ///
    /// 사다리가 내는 값입니다 — FogStart = FadeStart = 340 x 0.7 x 0.70 = 166.6m,
    /// FogEnd = TerrainActive = max(340, 400) x 0.7 = 280m.
    /// (짙기가 아닙니다. 안개를 지수제곱에서 Linear 로 바꿨습니다.)
    /// </summary>

    /// <summary>원하는 밝기를 그대로 돌려주는 가짜 시계입니다. 편집 중에는 진짜 시계가 없습니다.</summary>
    private sealed class FixedClock : IGameClock
    {
        public float Daylight { get; set; }
        public float TotalMinutes { get { return 0f; } }
        public bool IsNight { get { return Daylight < 0.5f; } }
        public bool IsRunning { get { return true; } }
        public float GetMinutesPerSecond(float fallback) { return fallback; }
        public void AdvanceMinutes(float minutes) { }
    }

    private struct Moment
    {
        public string Name;
        public float Daylight;        // 0 한밤 ~ 1 한낮
        public float SunAngleDegrees; // TimeSystem 과 같은 규칙: (분/1440)*360 - 90
    }

    // TimeSystem 은 Euler(sunAngle, 170, 0) 로 해를 돌립니다. 같은 규칙을 씁니다.
    private static readonly Moment[] Moments =
    {
        new Moment { Name = "night_02h",   Daylight = 0.00f, SunAngleDegrees = 30f - 90f },
        new Moment { Name = "dawn_06h",    Daylight = 0.25f, SunAngleDegrees = 90f - 90f },
        new Moment { Name = "morning_09h", Daylight = 1.00f, SunAngleDegrees = 135f - 90f },
        new Moment { Name = "day_12h",     Daylight = 1.00f, SunAngleDegrees = 180f - 90f },
        new Moment { Name = "golden_17h",  Daylight = 0.75f, SunAngleDegrees = 255f - 90f },
        new Moment { Name = "sunset_19h",  Daylight = 0.30f, SunAngleDegrees = 285f - 90f },
    };

    public static void Run()
    {
        Directory.CreateDirectory(OutputDirectory);

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Camera camera = FindMainCamera();
        if (camera == null)
        {
            Debug.Log("SCENELOOK 메인 카메라를 찾지 못함");
            EditorApplication.Exit(1);
            return;
        }

        SkyController sky = Object.FindAnyObjectByType<SkyController>(FindObjectsInactive.Include);
        if (sky == null)
        {
            Debug.Log("SCENELOOK SkyController 를 찾지 못함");
            EditorApplication.Exit(1);
            return;
        }

        Light sun = sky.sun != null ? sky.sun : FindSun();
        if (sun == null)
        {
            Debug.Log("SCENELOOK 디렉셔널 라이트를 찾지 못함");
            EditorApplication.Exit(1);
            return;
        }

        // 가짜 시계를 꽂습니다. 이게 있어야 밤 화면을 찍을 수 있습니다.
        FixedClock clock = new FixedClock();
        FieldInfo clockField = typeof(SkyController)
            .GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo lateUpdate = typeof(SkyController)
            .GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo activeSkyField = typeof(SkyController)
            .GetField("activeSky", BindingFlags.NonPublic | BindingFlags.Instance);

        if (clockField == null || lateUpdate == null || activeSkyField == null)
        {
            Debug.Log("SCENELOOK SkyController 의 clock/LateUpdate/activeSky 를 찾지 못함 — 이름이 바뀌었는지 확인하십시오");
            EditorApplication.Exit(1);
            return;
        }

        object originalClock = clockField.GetValue(sky);
        clockField.SetValue(sky, clock);

        // 편집 모드의 SkyController 는 에셋에 직접 씁니다. 찍는 동안만 복제본을 물립니다.
        Material dayAsset = sky.skyMaterial;
        Material nightAsset = sky.nightSkyMaterial;
        Material skyboxAsset = RenderSettings.skybox;
        Material dayClone = dayAsset != null ? new Material(dayAsset) : null;
        Material nightClone = nightAsset != null ? new Material(nightAsset) : null;
        sky.skyMaterial = dayClone;
        sky.nightSkyMaterial = nightClone;

        Debug.Log("SCENELOOK 낮 하늘: " + (dayAsset != null ? dayAsset.name : "(없음)")
                  + " / 밤 하늘: " + (nightAsset != null ? nightAsset.name : "(없음 — 별이 안 나옵니다)"));

        // 다른 [ExecuteAlways] 가 값을 덮지 않게 재웁니다. SkyController 는 우리가 직접 부릅니다.
        MonoBehaviour[] sleepers = DisableLookDrivers(sky);

        Quaternion sunRotation = sun.transform.rotation;
        float sunIntensity = sun.intensity;
        Color sunColor = sun.color;

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

        Camera worldCamera = MakeWorldCamera(camera);

        for (int i = 0; i < Moments.Length; i++)
        {
            Moment moment = Moments[i];

            // TimeSystem 이 하는 일: 해를 돌리고 세기를 밝기에 비례시킵니다.
            sun.transform.rotation = Quaternion.Euler(moment.SunAngleDegrees, 170f, 0f);
            sun.intensity = 1.25f * moment.Daylight;

            // SkyController 가 하는 일: 하늘·주변광·안개색·해 색. 진짜 코드를 부릅니다.
            clock.Daylight = moment.Daylight;
            lateUpdate.Invoke(sky, null);

            // ⚠ SkyController 는 <b>재생 중에만</b> RenderSettings.skybox 를 갈아 끼웁니다.
            // 편집 모드에서 그걸 건드리면 씬이 매 프레임 더러워지기 때문입니다.
            // 그래서 여기서는 그 컨트롤러가 고른 하늘을 직접 물려 줍니다.
            // 이걸 빠뜨리면 밤 화면인데 낮 하늘이 찍힙니다 — 실제로 그렇게 찍힌 적이 있습니다.
            Material chosen = activeSkyField.GetValue(sky) as Material;
            if (chosen != null) RenderSettings.skybox = chosen;

            // ViewRangeScaler 가 하는 일: 지수제곱 안개. 이건 SkyController 소관이 아닙니다.

            // 운전석 시점. 실제로 플레이어가 보는 화면이다.
            Texture2D shot = Grab(camera, target);
            File.WriteAllBytes(Path.Combine(OutputDirectory, moment.Name + ".png"), shot.EncodeToPNG());
            Report(moment, "cockpit", shot);
            Object.DestroyImmediate(shot);

            // 바깥 풍경 시점. 운전석은 화면의 절반 이상이 계기판이라
            // <b>세계가 아늑한지</b>는 밖에서 봐야 판단할 수 있습니다.
            if (worldCamera != null)
            {
                Texture2D worldShot = Grab(worldCamera, target);
                File.WriteAllBytes(Path.Combine(OutputDirectory, moment.Name + "_world.png"), worldShot.EncodeToPNG());
                Report(moment, "world", worldShot);
                Object.DestroyImmediate(worldShot);
            }
        }

        // 되돌립니다. 씬은 저장하지 않지만, 메모리 상태도 원래대로 두는 편이 안전합니다.
        sun.transform.rotation = sunRotation;
        sun.intensity = sunIntensity;
        sun.color = sunColor;

        sky.skyMaterial = dayAsset;
        sky.nightSkyMaterial = nightAsset;
        RenderSettings.skybox = skyboxAsset;
        clockField.SetValue(sky, originalClock);

        if (dayClone != null) Object.DestroyImmediate(dayClone);
        if (nightClone != null) Object.DestroyImmediate(nightClone);

        camera.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(target);
        if (worldCamera != null) Object.DestroyImmediate(worldCamera.gameObject);

        RestoreLookDrivers(sleepers);

        Debug.Log("SCENELOOK 끝. 그림은 " + OutputDirectory);

        // 씬을 저장하지 않고 끝냅니다. 찍으려고 만진 값이 저장되면 안 됩니다.
        EditorApplication.Exit(0);
    }

    /// <summary>카메라를 렌더해 그림으로 받아 옵니다.</summary>
    private static Texture2D Grab(Camera camera, RenderTexture target)
    {
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = null;

        Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        shot.Apply();
        RenderTexture.active = previous;

        return shot;
    }

    /// <summary>
    /// 차 뒤 위쪽에서 풍경을 내려다보는 임시 카메라를 만듭니다.
    ///
    /// 운전석 시점은 계기판이 화면의 절반을 넘게 먹어서 하늘과 들판이 조금만 보입니다.
    /// 세계의 인상을 판단하려면 밖에서 본 그림이 따로 필요합니다.
    /// </summary>
    private static Camera MakeWorldCamera(Camera source)
    {
        GameObject holder = new GameObject("SceneLookWorldCamera");
        Camera camera = holder.AddComponent<Camera>();

        camera.CopyFrom(source);
        camera.targetTexture = null;
        camera.rect = new Rect(0f, 0f, 1f, 1f);

        // ⚠ Camera.CopyFrom 은 URP 의 추가 카메라 데이터를 복사하지 않습니다.
        // 그대로 두면 이 카메라만 포스트프로세싱을 건너뛰어 톤매핑이 빠지고,
        // HDR 하늘이 1을 넘긴 채 잘려 <b>하늘이 순백으로 날아갑니다</b>.
        // 실제로 그렇게 찍혀서 밤 화면이 대낮처럼 밝게 나온 적이 있습니다.
        UniversalAdditionalCameraData sourceData = source.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        if (sourceData != null && data != null)
        {
            data.renderPostProcessing = sourceData.renderPostProcessing;
            data.antialiasing = sourceData.antialiasing;
            data.antialiasingQuality = sourceData.antialiasingQuality;
            data.renderShadows = sourceData.renderShadows;
            data.volumeLayerMask = sourceData.volumeLayerMask;
            data.volumeTrigger = sourceData.volumeTrigger;
            data.renderType = CameraRenderType.Base;
        }

        // 차 뒤 위쪽에서 살짝 내려다봅니다. 지평선이 화면 위쪽 1/3 에 오게 둡니다.
        Transform from = source.transform;
        Vector3 back = -from.forward;
        holder.transform.position = from.position + back * 14f + Vector3.up * 6f;
        holder.transform.rotation = Quaternion.Euler(8f, from.eulerAngles.y, 0f);

        return camera;
    }

    /// <summary>찍은 화면의 밝기와 <b>따뜻함</b>을 잽니다.</summary>
    private static void Report(Moment moment, string view, Texture2D shot)
    {
        Color32[] pixels = shot.GetPixels32();

        double sum = 0.0;
        double warmth = 0.0;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            sum += (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
            // 붉은기에서 푸른기를 뺀 값. 양수면 따뜻하고 음수면 차갑습니다.
            warmth += (p.r - p.b) / 255f;
        }

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "SCENELOOK {0,-12} {1,-8} 평균밝기={2:F4} 따뜻함={3:+0.000;-0.000}",
            moment.Name, view, sum / pixels.Length, warmth / pixels.Length));
    }

    private static Camera FindMainCamera()
    {
        if (Camera.main != null) return Camera.main;

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i].cameraType == CameraType.Game && cameras[i].targetTexture == null)
                return cameras[i];

        return cameras.Length > 0 ? cameras[0] : null;
    }

    private static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;

        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
            if (lights[i].type == LightType.Directional)
                return lights[i];

        return null;
    }

    /// <summary>SkyController 말고 값을 덮어쓰는 [ExecuteAlways] 들을 잠시 끕니다.</summary>
    private static MonoBehaviour[] DisableLookDrivers(SkyController keep)
    {
        System.Collections.Generic.List<MonoBehaviour> disabled = new System.Collections.Generic.List<MonoBehaviour>();

        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            MonoBehaviour behaviour = all[i];
            if (behaviour == null || !behaviour.enabled) continue;
            if (ReferenceEquals(behaviour, keep)) continue;

            string typeName = behaviour.GetType().Name;
            if (typeName == "SkyController" || typeName == "WeatherRig")
            {
                behaviour.enabled = false;
                disabled.Add(behaviour);
            }
        }

        return disabled.ToArray();
    }

    private static void RestoreLookDrivers(MonoBehaviour[] sleepers)
    {
        for (int i = 0; i < sleepers.Length; i++)
            if (sleepers[i] != null) sleepers[i].enabled = true;
    }
}
