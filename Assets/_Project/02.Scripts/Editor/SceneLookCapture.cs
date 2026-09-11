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
    // ⚠ <b>해상도가 곧 그림입니다.</b> 화면 후처리의 디더와 빗금은 <b>화면 화소</b>로
    // 크기를 재므로, 여기서 작게 찍으면 빌드보다 무늬가 굵게 나옵니다. 빌드는
    // 전체화면·네이티브 해상도로 뜨니(ProjectSettings) 판단하려면 같은 크기로
    // 찍어야 합니다. <c>CARDRIVE_SHOT</c> 에 "1920x1080" 처럼 넣으십시오.
    private static int Width = 960;
    private static int Height = 540;

    /// <summary>찍을 크기를 <c>CARDRIVE_SHOT</c> 에서 받습니다("가로x세로").</summary>
    private static void ShotSize()
    {
        string want = System.Environment.GetEnvironmentVariable("CARDRIVE_SHOT");
        if (string.IsNullOrEmpty(want)) return;

        string[] bits = want.Split('x', 'X');
        if (bits.Length != 2) return;

        if (int.TryParse(bits[0], out int w) && int.TryParse(bits[1], out int h)
            && w >= 64 && h >= 64)
        {
            Width = w;
            Height = h;
            Debug.Log("SCENELOOK 찍는 크기 " + Width + "x" + Height);
        }
    }

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
        ShotSize();
        PaletteOverride();

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // ⚠ <b>에디터의 품질 단계는 빌드의 것과 다릅니다.</b> 이 프로젝트는 에디터가
        // Balanced(그림자 35 m · 512), 스탠드얼론 빌드가 High Fidelity(150 m · 2048)
        // 입니다. 빗금은 <b>받은 빛</b>을 읽으므로 그림자가 닿는 거리가 달라지면
        // 그림도 달라집니다 — 에디터에서 찍은 그림으로 빌드를 판단할 수 없습니다.
        string quality = System.Environment.GetEnvironmentVariable("CARDRIVE_QUALITY");
        if (!string.IsNullOrEmpty(quality) && int.TryParse(quality, out int level))
        {
            QualitySettings.SetQualityLevel(level, true);
            Debug.Log("SCENELOOK 품질 단계 " + level + " · " + QualitySettings.names[level]);
        }
        else
        {
            Debug.Log("SCENELOOK 품질 단계 " + QualitySettings.GetQualityLevel()
                      + " · " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        }

        HatchOverride();

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

        // ⚠ <b>나무를 자르는 거리도 사다리 값으로 맞춥니다.</b> 페이드 구간(FadeStart·
        // FadeEnd)은 전역이라 <see cref="ViewRangeScaler"/> 가 편집 중에도 넣어 두는데,
        // <c>Terrain.treeDistance</c> 는 <b>재생 중에만</b> 씁니다. 그래서 그냥 찍으면
        // 씬에 구워진 340 m 까지 나무를 그려 놓고 셰이더는 231 m 에서 지우는,
        // <b>게임에는 없는 상태</b>가 찍힙니다. 2026-09-11 에 나무 LOD 를 판단하다
        // 이 어긋남 때문에 "숲이 사라졌다" 고 오판할 뻔했습니다.
        float treeCut = ViewDistances.Current.TreeCut;
        Terrain[] grounds = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None);
        float[] treeCutWas = new float[grounds.Length];

        for (int i = 0; i < grounds.Length; i++)
        {
            if (grounds[i] == null) continue;

            treeCutWas[i] = grounds[i].treeDistance;
            if (treeCut > 1f) grounds[i].treeDistance = treeCut;
        }

        Debug.Log("SCENELOOK 나무 자르는 거리 " + treeCut.ToString("F0") + " m · 지형 "
                  + grounds.Length + "장 · 페이드 "
                  + ViewDistances.Current.FadeStart.ToString("F0") + "~"
                  + ViewDistances.Current.FadeEnd.ToString("F0") + " m");

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

            // ⚠ 매 컷마다 다시 넣습니다. 다른 부품이 같은 전역을 덮을 수 있습니다.
            HatchOverride();

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
        for (int i = 0; i < grounds.Length; i++)
        {
            if (grounds[i] != null) grounds[i].treeDistance = treeCutWas[i];
        }

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

    /// <summary>
    /// 화면 후처리 팔레트의 값을 환경변수로 덮어씁니다.
    ///
    /// ⚠ <b>배치 에디터는 더러워진 에셋을 나갈 때 디스크에 씁니다.</b> 처음에는
    /// "메모리만 바뀌니 괜찮다" 고 봤는데 아니었습니다 — 시험 삼아 넣은 값이
    /// <c>PostPalette.mat</c> 에 그대로 남아, 다음 실행이 그 값을 물려받았습니다.
    /// 그래서 원래 값을 적어 두고 <see cref="PaletteRestore"/> 에서 되돌립니다.
    /// 사람이 튜닝한 값을 도구가 몰래 바꾸면 안 됩니다.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, float> PaletteWas =
        new System.Collections.Generic.Dictionary<string, float>();

    private static readonly string[,] PaletteKnobs =
    {
        { "CARDRIVE_PAL_LEVELS", "_Levels" },
        { "CARDRIVE_PAL_HATCH",  "_HatchDither" },
        { "CARDRIVE_PAL_SCALE",  "_HatchDitherScale" },
        { "CARDRIVE_PAL_INK",    "_HatchInk" },
        { "CARDRIVE_PAL_SOFT",   "_HatchSoft" },
        { "CARDRIVE_PAL_DEPTH",  "_HatchDepth" },
        { "CARDRIVE_PAL_PIXEL",  "_DitherPixel" },
        { "CARDRIVE_PAL_CHROMA", "_HatchChroma" },
        { "CARDRIVE_PAL_DESAT",  "_Desaturate" },
        { "CARDRIVE_PAL_TAM",    "_HatchTam" },
        { "CARDRIVE_PAL_TAMTOP", "_HatchTamTop" },
        { "CARDRIVE_PAL_SMOOTH", "_HatchToneSmooth" },
        { "CARDRIVE_PAL_BOIL",   "_HatchBoilRate" },
        { "CARDRIVE_PAL_JUMP",   "_HatchBoilJump" },
        { "CARDRIVE_PAL_BLEND",  "_HatchBoilBlend" },
        { "CARDRIVE_PAL_PAPER",  "_PaperGrain" },
        { "CARDRIVE_PAL_PAPERSC","_PaperScale" },
        { "CARDRIVE_PAL_EDGE",   "_PaperEdge" },
    };

    private const string PalettePath = "Assets/_Project/04.Art/00.Materials/PostPalette.mat";

    private static void PaletteOverride()
    {
        Material pal = AssetDatabase.LoadAssetAtPath<Material>(PalettePath);
        if (pal == null) return;

        for (int i = 0; i < PaletteKnobs.GetLength(0); i++)
        {
            string raw = System.Environment.GetEnvironmentVariable(PaletteKnobs[i, 0]);
            if (string.IsNullOrEmpty(raw)) continue;

            if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out float v))
            {
                string prop = PaletteKnobs[i, 1];
                if (!PaletteWas.ContainsKey(prop)) PaletteWas[prop] = pal.GetFloat(prop);
                pal.SetFloat(prop, v);
            }
        }

        // 나가는 길이 하나가 아니라(중간에 Exit 하는 갈래가 여럿) 여기에 걸어 둡니다.
        EditorApplication.quitting -= PaletteRestore;
        EditorApplication.quitting += PaletteRestore;

        Debug.Log("SCENELOOK 팔레트 — 단계 " + pal.GetFloat("_Levels").ToString("F0")
                  + " · 섞기 " + pal.GetFloat("_HatchDither").ToString("F2")
                  + " · 한 판 " + pal.GetFloat("_HatchDitherScale").ToString("F0")
                  + " · 잉크 " + pal.GetFloat("_HatchInk").ToString("F2")
                  + " · 보간 " + pal.GetFloat("_HatchSoft").ToString("F2")
                  + " · 진하기 " + pal.GetFloat("_HatchDepth").ToString("F2"));
    }

    /// <summary>
    /// 빗금 전역을 직접 넣습니다.
    ///
    /// ⚠ <b>컴포넌트의 필드를 바꾸는 것으로는 안 됩니다.</b> <see cref="HatchingRig"/> 는
    /// <c>[ExecuteAlways]</c> 가 아니라 재생 중이 아니면 <c>Update</c> 가 돌지 않습니다.
    /// 게다가 이 도구는 다른 부품을 재우려고 씬의 컴포넌트를 끄는데, 그때
    /// <c>OnDisable</c> 이 돌면서 <b>유효 깃발을 0 으로 내립니다.</b> 그래서 아무것도 안 하면
    /// 획이 빠진 화면이 찍힙니다 — 실제로 그렇게 찍고서 잘못된 결론을 낼 뻔했습니다.
    ///
    /// ⚠ <b>월드 음영의 빗금은 걷어냈습니다.</b> 남은 것은 오줌 자국과 니즈 게이지의
    /// 테두리뿐이고 둘 다 획 크기를 스스로 정하므로, 여기서 넣을 것은 텍스처 두 장과
    /// 유효 깃발(w)뿐입니다.
    /// </summary>
    private static void HatchOverride()
    {
        CarDrive.Systems.HatchingRig rig = Object.FindAnyObjectByType<CarDrive.Systems.HatchingRig>(
            FindObjectsInactive.Include);
        if (rig == null) return;

        Texture2D bright = rig.tamBright;
        Texture2D dark = rig.tamDark;

        // 씬을 건드리지 않고 다른 TAM 으로 찍어 보기 위한 문입니다.
        // 예: CARDRIVE_TAM=_soft → TAM_comic_bright_soft.png 를 씁니다.
        string suffix = System.Environment.GetEnvironmentVariable("CARDRIVE_TAM");
        if (!string.IsNullOrEmpty(suffix))
        {
            const string dir = "Assets/_Project/04.Art/01.Images/Hatching/";
            Texture2D b = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "TAM_comic_bright" + suffix + ".png");
            Texture2D d = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "TAM_comic_dark" + suffix + ".png");

            if (b != null && d != null)
            {
                bright = b;
                dark = d;
                Debug.Log("SCENELOOK TAM 을 바꿔 찍습니다 — " + b.name + " · " + d.name);
            }
            else
            {
                Debug.Log("SCENELOOK ⚠ TAM 사본을 못 찾았습니다 — 접미사 " + suffix);
            }
        }

        bool ready = bright != null && dark != null;
        if (ready)
        {
            Shader.SetGlobalTexture("_CarDriveTamBright", bright);
            Shader.SetGlobalTexture("_CarDriveTamDark", dark);
        }

        Shader.SetGlobalVector("_CarDriveHatchParams", new Vector4(0f, 0f, 0f, ready ? 1f : 0f));

        // 상황이 미는 값입니다. 이 도구는 씬 컴포넌트를 재우므로 LookMood 가 안 돕니다.
        // 예: CARDRIVE_MOOD=0.55,0.45 → 겨눔당하고 지친 화면.
        string mood = System.Environment.GetEnvironmentVariable("CARDRIVE_MOOD");
        Vector4 moodValue = Vector4.zero;

        if (!string.IsNullOrEmpty(mood))
        {
            // ⚠ 칸 수를 고정해 두면 값이 하나 늘 때 <b>조용히 무시됩니다.</b>
            // 실제로 세 번째 칸을 더한 날 그렇게 되어, 아무 변화도 없는 그림을
            // 찍어 놓고 "계단이 안 듣는다" 고 할 뻔했습니다. 있는 만큼만 읽습니다.
            string[] bits = mood.Split(',');

            for (int i = 0; i < bits.Length && i < 3; i++)
            {
                if (float.TryParse(bits[i], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float v))
                {
                    moodValue[i] = v;
                }
            }
        }

        Shader.SetGlobalVector("_CarDriveLookMood", moodValue);

        Debug.Log("SCENELOOK 빗금 전역 — 유효 " + (ready ? 1 : 0)
                  + " · 기분 " + moodValue.x.ToString("F2") + "," + moodValue.y.ToString("F2")
                  + "," + moodValue.z.ToString("F1"));
    }

    /// <summary>덮어썼던 팔레트 값을 원래대로 돌리고 디스크에 씁니다.</summary>
    private static void PaletteRestore()
    {
        if (PaletteWas.Count == 0) return;

        Material pal = AssetDatabase.LoadAssetAtPath<Material>(PalettePath);
        if (pal == null) return;

        foreach (var pair in PaletteWas) pal.SetFloat(pair.Key, pair.Value);

        PaletteWas.Clear();
        EditorUtility.SetDirty(pal);
        AssetDatabase.SaveAssets();
        Debug.Log("SCENELOOK 팔레트 값을 원래대로 되돌렸습니다");
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
