using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CarDrive.Common;
using CarDrive.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 메가스트럭처를 <b>진짜 씬에서</b> 찍습니다.
///
/// <see cref="MegastructureSetup.Preview"/> 는 빈 씬에 제가 만든 빛을 켜고 찍습니다.
/// 만든 것이 서로 맞물리는지는 그것으로 충분하지만, <b>어떤 값이 맞는지</b>는 그것으로
/// 판단하면 안 됩니다 - 게임에는 볼륨의 노출, 툰 램프, 팔레트, 안개가 걸려 있고 그
/// 넷이 색과 명도를 전부 다시 씁니다. 실제로 알베도를 0.58 에서 0.66 으로 올린 뒤
/// 미리보기에서는 하얗게 날아갔는데, 그것은 미리보기의 빛이 1.4 여서였습니다.
///
/// <see cref="SceneLookCapture"/> 는 <b>차가 선 자리</b>에서 찍으므로 여기에 쓸 수
/// 없습니다. 메가스트럭처는 출발 지점에서 416 m 떨어져 있고 안개는 280 m 에서 끝나
/// 화면에 아예 안 들어옵니다.
///
/// <b>조명 상태를 못 박고 찍습니다.</b> 처음에는 그냥 씬을 열고 카메라만 옮겨
/// 찍었는데, 같은 재질로 두 번 찍은 그림이 <b>완전히 달랐습니다</b> — 한 번은
/// 노면이 마젠타로 나오고 한 번은 화면 전체가 하얗게 날아갔습니다. 하늘·주변광·
/// 안개·노출은 재생 중에 도는 컴포넌트가 만드는 것이라, 편집 모드에서는 <b>마지막에
/// 씬을 만진 스크립트가 남긴 값</b>이 그대로 찍힙니다. 그 그림으로 알베도를 판단하면
/// 재질을 고치는 것이 아니라 <b>잡음을 쫓게 됩니다.</b>
///
/// 그래서 <see cref="SceneLookCapture"/> 와 같은 방법으로 못 박습니다 — 가짜 시계를
/// 꽂고, 다른 [ExecuteAlways] 를 재우고, 해와 하늘과 안개를 직접 물립니다.
///
/// <b>⚠ 아직 재현되지 않습니다. 이 그림으로 재질 값을 판단하지 마십시오.</b>
///
/// 못 박는 것을 여기까지 옮겼는데도 <b>재질만 바꾸고 두 번 돌린 그림의 노출이
/// 서로 달랐습니다.</b> 한 번은 밝고 한 번은 어두웠고, 어두운 면이 마젠타로 밀리는
/// 정도도 달랐습니다. 무엇이 남았는지는 아직 못 찾았습니다 — 볼륨의 노출과 색
/// 보정이 재생 중에만 도는 무언가에 달려 있는 것으로 보입니다.
///
/// 그러므로 지금 이것이 답할 수 있는 것은 <b>무엇이 어디에 있는가</b>뿐입니다:
/// 차선이 소실점까지 이어지는가, 데크 밑 등이 보이는가, 경사로가 길로 보이는가.
/// <b>얼마나 밝은가·무슨 색인가는 답하지 못합니다.</b>
///
/// 고치는 방향은 이 도구를 더 손보는 것이 아니라, 이미 맞는 그림을 내고 있는
/// <see cref="SceneLookCapture"/> 에 시점을 하나 더 다는 것입니다. 반쪽짜리 계기를
/// 둘 갖는 것보다 맞는 계기 하나를 늘리는 편이 낫습니다.
///
/// ⚠ 렌더가 필요하므로 <c>-nographics</c> 를 붙이면 안 됩니다.
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod MegastructureLookCapture.Run
/// </code>
/// </summary>
public static class MegastructureLookCapture
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string ManifestPath =
        "Assets/_Project/04.Art/02.Models/Megastructure/presets.json";
    private const string HolderName = "Megastructure";
    private const string OutputDirectory = "Logs/MegaLook";

    private const int Width = 1280;
    private const int Height = 720;

    /// <summary>ViewRangeScaler 가 런타임에 거는 안개 거리입니다. 사다리가 내는 값입니다.</summary>
    private const float RuntimeFogStart = 166.6f;
    private const float RuntimeFogEnd = 280f;

    /// <summary>찍는 시각. 한낮은 그림자가 죽어 형태를 못 봅니다.</summary>
    private const float Daylight = 1.0f;
    private const float SunAngleDegrees = 135f - 90f;

    /// <summary>원하는 밝기를 그대로 돌려주는 가짜 시계입니다.</summary>
    private sealed class FixedClock : IGameClock
    {
        public float Daylight { get; set; }
        public float TotalMinutes { get { return 0f; } }
        public bool IsNight { get { return Daylight < 0.5f; } }
        public bool IsRunning { get { return true; } }
        public float GetMinutesPerSecond(float fallback) { return fallback; }
        public void AdvanceMinutes(float minutes) { }
    }

    [Serializable]
    private class Manifest
    {
        public float burial;
        public float deckTop;
        public float width;
    }

    public static void Run()
    {
        int errors = 0;

        try
        {
            Directory.CreateDirectory(OutputDirectory);

            EditorSettings.asyncShaderCompilation = false;
            ShaderUtil.allowAsyncCompilation = false;

            Manifest spec = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform spine = scene.GetRootGameObjects()
                .FirstOrDefault(g => g.name == HolderName)?.transform;

            if (spine == null) throw new Exception("씬에 메가스트럭처가 없습니다");

            Camera source = Camera.allCameras.FirstOrDefault()
                            ?? UnityEngine.Object.FindAnyObjectByType<Camera>(
                                FindObjectsInactive.Include);

            if (source == null) throw new Exception("씬에 카메라가 없습니다");

            Pin(out Action release);

            // <b>씬의 카메라를 그대로 씁니다. 복제하면 안 됩니다.</b> 복제본으로
            // 찍었더니 화면이 새까맣게 나왔습니다 - 포스트 처리와 볼륨 설정이
            // 카메라에 딸린 컴포넌트와 자식에 흩어져 있어, 복제하고 자식을 지우는
            // 순간 게임과 다른 카메라가 됩니다. 자리만 잠시 빌리고 돌려줍니다.
            Camera camera = source;
            Vector3 wasAt = camera.transform.position;
            Quaternion wasLook = camera.transform.rotation;

            float deck = spine.position.y + spec.deckTop;
            Vector3 along = spine.forward;
            Vector3 side = spine.right;

            Bounds all = Bounds(spine);
            Vector3 middle = new Vector3(all.center.x, 0f, all.center.z);

            // ⚠ <c>ReadWrite</c> 를 sRGB 로 <b>강제하면 안 됩니다.</b> 프로젝트가
            // 선형 공간이라 감마가 한 번 더 먹혀 그림이 통째로 어두워집니다.
            // 검증된 도구(SceneLookCapture)가 쓰는 것과 같은 형태여야 합니다.
            RenderTexture target = new RenderTexture(Width, Height, 24,
                RenderTextureFormat.ARGB32);

            try
            {
                // 노면. 이 게임에서 <b>가장 오래 보게 될</b> 화면입니다.
                Shoot(camera, target, middle + Vector3.up * (deck + 1.35f) - side * 11f,
                      along + Vector3.down * 0.03f, "road");

                // 데크 밑. 차로 지나가며 보는 각이고, 지금 가장 어두운 곳입니다.
                Shoot(camera, target,
                      middle + Vector3.up * (spine.position.y + 2.0f) - along * 120f,
                      along, "under");

                // 경사로. 지상에서 데크로 올라가는 입구가 <b>길로 보이는지</b>.
                Renderer ramp = spine.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(r => r.name.Contains("Ramp"));

                if (ramp != null)
                {
                    Vector3 at = ramp.bounds.center;
                    Vector3 eye = new Vector3(at.x, spine.position.y + 6f, at.z)
                                  - side * 92f - along * 30f;
                    Shoot(camera, target, eye, (at - eye).normalized, "ramp");
                }

                // 멀리서. 지형 위에 선 것이 <b>지표로 읽히는지</b>.
                Shoot(camera, target,
                      middle + Vector3.up * (spine.position.y + 26f) + side * 250f,
                      (-side + along * 0.5f).normalized, "far");
            }
            finally
            {
                release();
                camera.targetTexture = null;
                camera.transform.SetPositionAndRotation(wasAt, wasLook);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }

            Debug.Log("MegastructureLookCapture: 저장 — " + OutputDirectory);
        }
        catch (Exception e)
        {
            Debug.LogError("MegastructureLookCapture: " + e);
            errors++;
        }

        // 씬은 저장하지 않습니다. 찍으려고 만든 카메라가 남으면 안 됩니다.
        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    // --- Private Methods ---

    /// <summary>
    /// 하늘·해·안개를 <b>정해진 값으로</b> 물리고, 되돌리는 방법을 돌려줍니다.
    ///
    /// SkyController 의 진짜 코드를 부릅니다 - 여기서 값을 흉내 내면 그림과 게임이
    /// 갈라지고, 그러면 재는 의미가 없어집니다.
    /// </summary>
    private static void Pin(out Action release)
    {
        SkyController sky = UnityEngine.Object.FindAnyObjectByType<SkyController>(
            FindObjectsInactive.Include);

        Light sun = RenderSettings.sun ?? UnityEngine.Object
            .FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(l => l.type == LightType.Directional);

        if (sky == null || sun == null)
        {
            throw new Exception("씬에서 SkyController 나 해를 찾지 못했습니다");
        }

        FieldInfo clockField = typeof(SkyController)
            .GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo lateUpdate = typeof(SkyController)
            .GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo activeSkyField = typeof(SkyController)
            .GetField("activeSky", BindingFlags.NonPublic | BindingFlags.Instance);

        if (clockField == null || lateUpdate == null || activeSkyField == null)
        {
            throw new Exception("SkyController 의 clock/LateUpdate/activeSky 를 찾지 못했습니다");
        }

        object wasClock = clockField.GetValue(sky);
        FixedClock clock = new FixedClock { Daylight = Daylight };
        clockField.SetValue(sky, clock);

        // 편집 모드의 SkyController 는 <b>에셋에 직접 씁니다.</b> 찍는 동안만 복제본을.
        Material dayAsset = sky.skyMaterial;
        Material nightAsset = sky.nightSkyMaterial;
        Material skyboxAsset = RenderSettings.skybox;
        Material dayClone = dayAsset != null ? new Material(dayAsset) : null;
        Material nightClone = nightAsset != null ? new Material(nightAsset) : null;
        sky.skyMaterial = dayClone;
        sky.nightSkyMaterial = nightClone;

        // 값을 덮어쓰는 다른 [ExecuteAlways] 를 재웁니다.
        List<MonoBehaviour> sleepers = new List<MonoBehaviour>();

        foreach (MonoBehaviour b in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (b == null || !b.enabled || ReferenceEquals(b, sky)) continue;

            string name = b.GetType().Name;
            if (name != "SkyController" && name != "WeatherRig") continue;

            b.enabled = false;
            sleepers.Add(b);
        }

        Quaternion wasRotation = sun.transform.rotation;
        float wasIntensity = sun.intensity;
        Color wasColor = sun.color;

        sun.transform.rotation = Quaternion.Euler(SunAngleDegrees, 170f, 0f);
        sun.intensity = 1.25f * Daylight;

        lateUpdate.Invoke(sky, null);

        // ⚠ SkyController 는 <b>재생 중에만</b> RenderSettings.skybox 를 갈아 끼웁니다.
        // 여기서는 그것이 고른 하늘을 직접 물려 줍니다.
        if (activeSkyField.GetValue(sky) is Material chosen) RenderSettings.skybox = chosen;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = RuntimeFogStart;
        RenderSettings.fogEndDistance = RuntimeFogEnd;

        release = () =>
        {
            sun.transform.rotation = wasRotation;
            sun.intensity = wasIntensity;
            sun.color = wasColor;

            sky.skyMaterial = dayAsset;
            sky.nightSkyMaterial = nightAsset;
            RenderSettings.skybox = skyboxAsset;
            clockField.SetValue(sky, wasClock);

            if (dayClone != null) UnityEngine.Object.DestroyImmediate(dayClone);
            if (nightClone != null) UnityEngine.Object.DestroyImmediate(nightClone);

            foreach (MonoBehaviour b in sleepers) if (b != null) b.enabled = true;
        };
    }

    private static void Shoot(Camera camera, RenderTexture target,
                              Vector3 at, Vector3 look, string name)
    {
        camera.transform.SetPositionAndRotation(
            at, Quaternion.LookRotation(look.normalized, Vector3.up));

        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = null;

        Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        shot.Apply();
        RenderTexture.active = previous;

        File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(shot);

        Debug.Log($"  {name,-6} ({at.x:F0}, {at.y:F0}, {at.z:F0})");
    }

    private static Bounds Bounds(Transform spine)
    {
        Renderer[] renderers = spine.GetComponentsInChildren<Renderer>(true);
        Bounds box = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);
        return box;
    }
}
