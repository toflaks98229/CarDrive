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
/// <b>카메라는 아무거나 집으면 안 됩니다.</b> 처음에 <c>Camera.allCameras</c> 의
/// 첫 번째를 썼더니 <b>재질만 바꾸고 두 번 돌린 그림의 노출이 서로 달랐습니다</b> —
/// 한 번은 마젠타, 한 번은 하얗게 날아갔습니다. 재질을 의심하고 값을 올렸다 내렸다
/// 했는데, 원인은 이 씬에 카메라가 다섯 대(본 카메라 둘 + 차의 <b>거울 셋</b>)라는
/// 것이었습니다. <c>allCameras</c> 는 순서를 보장하지 않으므로 실행마다 다른
/// 카메라로 찍고 있었고, 거울 카메라에는 포스트 처리도 색 보정도 걸려 있지 않습니다.
///
/// 그래서 <see cref="SceneLookCapture"/> 와 같은 규칙으로 고릅니다 — <c>Camera.main</c>,
/// 없으면 <b>렌더 타깃이 없는</b> 게임 카메라. 렌더 타깃이 있다는 것이 곧 거울입니다.
///
/// 두 번 돌려 평균밝기가 같은지는 <b>도구가 스스로 찍습니다.</b> 계기가 재현되는지를
/// 눈으로 판단하면 또 같은 함정에 빠집니다.
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

            Camera source = MainCamera();
            if (source == null) throw new Exception("씬에 본 카메라가 없습니다");

            Debug.Log($"MegastructureLookCapture: 카메라 {source.name} · " +
                      $"화각 {source.fieldOfView:F0}° · 원거리 {source.farClipPlane:F0} m");

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

                // <b>같은 자리에서 고개만 돌린 두 장.</b>
                //
                // URP 안개는 카메라 <b>정면 방향의 깊이</b>로 재므로, 같은 건물이
                // 화면 가운데 있을 때와 가장자리에 있을 때 안개를 다르게 먹습니다.
                // 그래서 <b>고개를 돌리는 것만으로</b> 건물이 나타났다 사라집니다.
                //
                // 두 장의 <b>평균밝기가 크게 다르면</b> 그 문제가 남아 있는 것입니다.
                // 카메라는 한 자리에 고정하고 방향만 40° 틀어, 대상이 화면 가운데에
                // 왔다가 가장자리로 갑니다.
                //
                // 대상은 <b>마을 집</b>입니다 - 메가스트럭처는 안개를 덜 먹게 해
                // 두었으므로(_FogScale 0.55) 차이가 가려집니다.
                // <b>이름순으로 고릅니다.</b> FirstOrDefault 로 집었더니 실행마다
                // 다른 집이 걸려 카메라 자리가 달라졌고, 그러면 두 실행을 비교할 수
                // 없습니다. 카메라를 거울로 집던 것과 같은 함정입니다.
                Renderer house = UnityEngine.Object
                    .FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                    .Where(r => r.name.Contains("House"))
                    .OrderBy(r => r.name)
                    .ThenBy(r => r.transform.position.x)
                    .FirstOrDefault();

                if (house != null)
                {
                    Vector3 mark = house.bounds.center;
                    Vector3 away = new Vector3(1f, 0f, 0.35f).normalized;
                    // <b>거리를 고르는 것이 곧 실험 설계입니다.</b> 330 m 로 잡았더니 두 자
                    // 모두 안개에 완전히 먹혀 차이가 0 이었습니다. 방사로는 먹히고
                    // 깊이로는 안 먹히는 구간, 곧 <b>버그가 실제로 무는 자리</b>에 둡니다.
                    Vector3 eye = mark + away * 290f + Vector3.up * 26f;

                    Vector3 straight = (mark - eye).normalized;
                    Vector3 turned = Quaternion.Euler(0f, 40f, 0f) * straight;

                    // <b>숫자로도 남깁니다.</b> 그림은 "달라 보인다" 까지만 말합니다.
                    float radial = Vector3.Distance(eye, mark);
                    float planar = Vector3.Dot(mark - eye, turned);
                    float start = RenderSettings.fogStartDistance;
                    float end = RenderSettings.fogEndDistance;

                    Debug.Log($"  안개 대상 {house.name} · 거리 {radial:F0} m · " +
                              $"40° 틀었을 때 깊이 {planar:F0} m · 안개 {start:F0}~{end:F0} m · " +
                              $"맑기 방사 {Mathf.Clamp01((end - radial) / (end - start)):F3} · " +
                              $"깊이 {Mathf.Clamp01((end - planar) / (end - start)):F3}");

                    Shoot(camera, target, eye, straight, "angle_center");
                    Shoot(camera, target, eye, turned, "angle_edge");
                }

                // <b>하늘을 올려다봅니다.</b> 이 세계에는 하늘 대신 위층의 밑면이
                // 있고, 그것이 보이지 않으면 스카이박스가 안 물린 것입니다.
                //
                // ⚠ <b>데크 위에서 올려다보면 안 됩니다.</b> 처음에 노면과 같은 자리를
                // 썼더니 54 m 위의 <b>윗단 밑면</b>이 화면을 가득 채워, 균일한 갈색
                // 판 한 장이 나왔습니다. 하늘을 본 것이 아니었습니다. 구조물에서
                // 떨어진 들판에서 올려다봐야 합니다.
                Vector3 skyEye = middle + Vector3.up * (spine.position.y + 26f) + side * 250f;
                Vector3 skyLook = (-side + Vector3.up * 1.5f).normalized;

                Shoot(camera, target, skyEye, skyLook, "sky_up");

                // 하늘이 무엇으로 그려지고 있는지 한 줄 남깁니다. 파노라마를
                // 다시 구워도 화면이 안 바뀌면, 먼저 의심할 곳이 여기입니다.
                Material sky = RenderSettings.skybox;

                Debug.Log($"  하늘 {sky?.name} · 노출 " +
                          $"{(sky != null && sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : -1f):F3}");

                // <b>기둥 밑동.</b> 다가갈 수 있는 유일한 거대 구조라, 가까이서
                // 형태가 살아 있는지가 이 물건의 값어치를 정합니다. 멀리서는
                // 안개에 지워지는 것이 의도이지만 <b>가까이서도 지워지면</b>
                // 그냥 흰 판을 하나 세운 것입니다.
                Transform column = GameObject.Find("SkyColumns")?.transform.childCount > 0
                    ? GameObject.Find("SkyColumns").transform.GetChild(0)
                    : null;

                if (column != null)
                {
                    Vector3 foot = column.position;
                    Vector3 eye = foot + new Vector3(88f, 14f, 62f);

                    Shoot(camera, target, eye,
                          (foot + Vector3.up * 150f - eye).normalized, "column");
                }

                // <b>지반의 가장자리.</b> 대지가 자연 지형이 아니라 건축물의 한
                // 조각이라면, 끝까지 가면 땅이 아니라 잘린 단면과 <b>바닥이 안 보이는
                // 구름</b>이 나와야 합니다.
                //
                // ⚠ <c>Terrain.activeTerrain</c> 으로 자리를 잡으면 안 됩니다. 지형이
                // 타일 103 장이라 아무 타일이나 걸리고, 그 타일 모서리는 세계의
                // 끝이 아니라 <b>이웃 타일과의 이음매</b>일 수 있습니다. 실제로 처음에
                // 그렇게 잡아 들판 한가운데를 찍었습니다. 테두리 조각을 직접 찾습니다.
                Transform rim = GameObject.Find("WorldRim")?.transform;

                if (rim != null && rim.childCount > 0)
                {
                    // <b>이름순으로 고릅니다.</b> 계층 순서는 실행마다 다를 수 있고,
                    // 그러면 두 실행의 그림을 견줄 수 없습니다.
                    Transform piece = Enumerable.Range(0, rim.childCount)
                        .Select(i => rim.GetChild(i))
                        .OrderBy(t => t.name, StringComparer.Ordinal)
                        .First();

                    Vector3 out2 = piece.forward;

                    // <b>연석 너머로 내려다봐야</b> 합니다. 눈높이에서 찍었더니
                    // 화면의 대부분이 풀밭이고 연석은 낮은 담으로 보였습니다 -
                    // 이 장면의 요점은 담이 아니라 <b>그 아래에 아무것도 없다</b>는
                    // 것이라, 바닥이 보이는 각으로 서야 합니다.
                    Vector3 stand = piece.position - out2 * 7f + Vector3.up * 11f;

                    Shoot(camera, target, stand,
                          (out2 + Vector3.down * 0.85f).normalized, "edge_out");

                    // 조금 물러나 지평선까지. 잘린 단면과 구름과 천장이 <b>한 화면에</b>
                    // 들어오는지가 이 세계 설정이 서는지 마는지를 정합니다.
                    Shoot(camera, target,
                          piece.position - out2 * 46f + Vector3.up * 20f,
                          (out2 + Vector3.down * 0.22f).normalized, "edge_far");

                    // 테두리를 <b>따라</b> 봅니다. 조각 사이의 단차가 계단으로
                    // 읽히는지, 아니면 그냥 어긋난 것으로 보이는지가 여기서 갈립니다.
                    Shoot(camera, target,
                          piece.position + piece.right * 42f - out2 * 10f + Vector3.up * 9f,
                          (-piece.right + out2 * 0.30f + Vector3.down * 0.20f).normalized,
                          "edge_along");
                }

                // <b>안개가 완전히 닫힌 뒤</b>. 안개는 257 m 에서 100% 인데 파클립은
                // 482 m 라, 이 자리는 <b>그리는데 안 보이는</b> 구간입니다. 438 m 짜리
                // 지표가 여기서 읽히지 않으면 그것은 지표가 아닙니다.
                Shoot(camera, target,
                      middle + Vector3.up * (spine.position.y + 34f) + side * 420f,
                      (-side + along * 0.35f).normalized, "land");
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

        Camera main = MainCamera();

        Quaternion wasRotation = sun.transform.rotation;
        float wasIntensity = sun.intensity;
        Color wasColor = sun.color;

        // <b>바꾼 것은 전부 되돌립니다.</b> 해와 하늘과 시계는 되돌리면서 파클립과
        // 안개는 빠뜨리고 있었습니다. 배치모드는 씬을 저장하지 않아 티가 안 났지만,
        // 에디터에서 부르면 본 카메라의 파클립이 482 로 남습니다.
        float wasFar = main != null ? main.farClipPlane : 0f;
        bool wasFog = RenderSettings.fog;
        FogMode wasFogMode = RenderSettings.fogMode;
        float wasFogStart = RenderSettings.fogStartDistance;
        float wasFogEnd = RenderSettings.fogEndDistance;

        sun.transform.rotation = Quaternion.Euler(SunAngleDegrees, 170f, 0f);
        sun.intensity = 1.25f * Daylight;

        lateUpdate.Invoke(sky, null);

        // ⚠ SkyController 는 <b>재생 중에만</b> RenderSettings.skybox 를 갈아 끼웁니다.
        // 여기서는 그것이 고른 하늘을 직접 물려 줍니다.
        if (activeSkyField.GetValue(sky) is Material chosen) RenderSettings.skybox = chosen;

        // <b>안개 거리를 여기 적어 두면 안 됩니다.</b> 처음에는 166.6 / 280 을
        // 상수로 베껴 왔는데, 그 값은 사다리가 <b>어느 시점에 내던 값</b>을 옮겨 적은
        // 것이라 설정이 바뀌면 조용히 거짓말이 됩니다. 런타임이 읽는 곳에서 읽습니다.
        if (main != null) ViewDistances.SetViewBase(main.farClipPlane);

        ViewDistances.Ladder ladder = ViewDistances.Current;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = ladder.FogStart;
        RenderSettings.fogEndDistance = ladder.FogEnd;

        if (main != null) main.farClipPlane = ladder.FarClip;

        Debug.Log($"MegastructureLookCapture: 사다리 — 안개 {ladder.FogStart:F0} ~ " +
                  $"{ladder.FogEnd:F0} m · 파클립 {ladder.FarClip:F0} m · " +
                  $"나무 페이드 {ladder.FadeStart:F0} ~ {ladder.FadeEnd:F0} m · " +
                  $"터레인 {ladder.TerrainActive:F0} m");

        release = () =>
        {
            sun.transform.rotation = wasRotation;
            sun.intensity = wasIntensity;
            sun.color = wasColor;

            sky.skyMaterial = dayAsset;
            sky.nightSkyMaterial = nightAsset;
            RenderSettings.skybox = skyboxAsset;
            clockField.SetValue(sky, wasClock);

            if (main != null) main.farClipPlane = wasFar;

            RenderSettings.fog = wasFog;
            RenderSettings.fogMode = wasFogMode;
            RenderSettings.fogStartDistance = wasFogStart;
            RenderSettings.fogEndDistance = wasFogEnd;

            if (dayClone != null) UnityEngine.Object.DestroyImmediate(dayClone);
            if (nightClone != null) UnityEngine.Object.DestroyImmediate(nightClone);

            foreach (MonoBehaviour b in sleepers) if (b != null) b.enabled = true;
        };
    }

    /// <summary>
    /// 찍을 카메라입니다. <b>렌더 타깃이 달린 것은 거울</b>이므로 거릅니다.
    ///
    /// 거울에는 포스트 처리와 색 보정이 없어, 그것으로 찍으면 게임과 전혀 다른
    /// 그림이 나옵니다. 그리고 <c>allCameras</c> 의 순서는 보장되지 않으므로,
    /// 거르지 않으면 <b>실행마다 다른 그림</b>이 나옵니다.
    /// </summary>
    private static Camera MainCamera()
    {
        if (Camera.main != null) return Camera.main;

        return UnityEngine.Object
            .FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.cameraType == CameraType.Game && c.targetTexture == null);
    }

    private static void Shoot(Camera camera, RenderTexture target,
                              Vector3 at, Vector3 look, string name)
    {
        camera.transform.SetPositionAndRotation(
            at, Quaternion.LookRotation(look.normalized, Vector3.up));

        camera.targetTexture = target;

        // <b>한 번 버리고 두 번째를 씁니다.</b> 카메라를 옮긴 직후의 첫 프레임은
        // 아직 안 익습니다 - 볼륨과 노출이 새 자리를 반영하기 전에 그려집니다.
        // 두 번 돌린 그림에서 <b>첫 촬영만</b> 평균밝기가 0.33 과 0.06 으로 갈렸고,
        // 나머지 셋은 소수 넷째 자리까지 같았습니다. 그 하나가 이 프레임입니다.
        camera.Render();
        camera.Render();

        camera.targetTexture = null;

        Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        shot.Apply();
        RenderTexture.active = previous;

        File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());

        // <b>평균밝기를 같이 찍습니다.</b> 두 번 돌려 이 값이 같아야 계기입니다.
        // 그림을 눈으로 비교하면 "비슷해 보인다" 로 넘어가게 됩니다.
        Color32[] pixels = shot.GetPixels32();
        double sum = 0.0;

        for (int i = 0; i < pixels.Length; i++)
        {
            sum += (0.299f * pixels[i].r + 0.587f * pixels[i].g + 0.114f * pixels[i].b) / 255f;
        }

        UnityEngine.Object.DestroyImmediate(shot);

        Debug.Log($"  {name,-12} ({at.x,6:F0}, {at.y,5:F0}, {at.z,6:F0}) · " +
                  $"평균밝기 {sum / pixels.Length:F4}");
    }

    private static Bounds Bounds(Transform spine)
    {
        Renderer[] renderers = spine.GetComponentsInChildren<Renderer>(true);
        Bounds box = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);
        return box;
    }
}
