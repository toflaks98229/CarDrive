using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 빗금이 <b>움직이는 물체 위에서 미끄러지는지</b>를 눈과 숫자로 확인합니다.
///
/// <b>왜 이 검사가 따로 필요한가.</b> 빗금은 월드 좌표로 긋습니다(HatchingRig.scale 주석 참고).
/// 붙박이 건물에는 그것이 맞지만 <b>차는 달립니다.</b> 획이 세계에 박혀 있으면 차가 지나갈 때
/// 획이 차체 위를 흘러가, 손으로 그린 잉크가 아니라 <b>비춘 무늬</b>로 보입니다.
/// 그런데 <b>정지 화면 한 장으로는 이것을 절대 알 수 없습니다.</b> 두 자리에서 찍어 비교해야 합니다.
///
/// <b>어떻게 재는가.</b> 차와 카메라를 <b>같은 만큼</b> 옮깁니다. 그러면 차는 화면의 같은 픽셀에
/// 같은 각도로 놓이고 해도 그대로라, 두 그림은 <b>픽셀 단위로 같아야 합니다.</b>
/// 남는 차이는 곧 <b>획이 미끄러진 양</b>입니다.
///
/// 배경(나무·상자·지형)도 시차만큼 달라지므로 <b>차가 그려진 픽셀에서만</b> 잽니다.
/// 마스크는 차를 껐다 켠 두 그림의 차이로 만듭니다. 구름 그림자는 월드 XZ 로 샘플해서
/// 차를 옮기는 것만으로 차 위의 그늘이 바뀌므로 잠시 끕니다.
///
/// <c>_HATCH_LOCAL</c> 을 끈 것과 켠 것을 <b>둘 다</b> 찍어 나란히 둡니다.
/// 끈 쪽에서 차이가 크고 켠 쪽에서 0 에 가까우면 고쳐진 것입니다.
///
/// ⚠ 렌더링이 필요하므로 <c>-nographics</c> 를 붙이면 안 됩니다.
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod HatchMotionCheck.Run -logFile &lt;로그&gt;
/// </code>
/// 결과: <c>Logs/HatchMotion/*.png</c>
/// </summary>
public static class HatchMotionCheck
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string CarMaterialPath = "Assets/_Project/04.Art/00.Materials/CarBody.mat";
    private const string OutputDirectory = "Logs/HatchMotion";
    /// <summary>씬이 실제로 쓰는 빗금 값입니다. 마지막에 이 값으로 한 번 더 잽니다.</summary>
    private static Vector4 _sceneParams;

    private const int Width = 640;
    private const int Height = 640;

    /// <summary>
    /// 두 번째 자리로 옮길 거리입니다.
    ///
    /// <b>획 한 판(0.8m)의 배수를 피했습니다.</b> 배수만큼 옮기면 월드 빗금이라도 같은 무늬가
    /// 다시 맞아떨어져, 고장 나 있는데 통과한 것처럼 보입니다.
    /// </summary>
    private static readonly Vector3 Step = new Vector3(0.37f, 0f, 0.41f);

    public static void Run()
    {
        Directory.CreateDirectory(OutputDirectory);

        // 비동기 컴파일이 켜져 있으면 아직 컴파일되지 않은 셰이더가 단색으로 찍힙니다.
        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Material carMaterial = AssetDatabase.LoadAssetAtPath<Material>(CarMaterialPath);
        if (carMaterial == null) { Fail("차체 머티리얼을 못 찾음: " + CarMaterialPath); return; }

        Renderer[] carRenderers = FindRenderersUsing(carMaterial);
        if (carRenderers.Length == 0) { Fail("그 머티리얼을 쓰는 렌더러가 씬에 없음"); return; }

        Transform car = FindCarRoot(carRenderers);
        if (car == null) { Fail("차 루트를 못 찾음"); return; }

        ApplyHatchGlobals();

        // 구름 그림자는 월드 XZ 로 샘플하므로 차를 옮기면 그것만으로 그림이 달라집니다.
        MonoBehaviour[] sleepers = DisableByTypeName("CloudShadows");
        Shader.SetGlobalVector("_CloudShadowParams", Vector4.zero);

        Camera source = FindMainCamera();
        if (source == null) { Fail("카메라를 못 찾음"); return; }

        Camera camera = MakeCarCamera(source);
        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

        Vector3 home = car.position;
        Vector3 poseA = home;

        // 원본을 그대로 두고 사본으로 바꿔 끼웁니다. 에셋이 더러워지면 안 됩니다.
        Material probe = new Material(carMaterial);
        Material[][] originals = Swap(carRenderers, carMaterial, probe);

        bool passed = true;

        // ── 차가 화면에 있기는 한가 ──
        //
        // 이것이 첫 관문입니다. 차가 프레임 밖이면 아래의 모든 숫자가 <b>배경만 비교한 값</b>이라
        // 무엇을 재도 통과처럼 보입니다.
        car.position = poseA;

        // 차를 끄면 경계 상자를 잴 수 없으므로 <b>끄기 전에</b> 재 둡니다.
        Bounds framing = WorldBounds(carRenderers);

        Texture2D withCar = Grab(camera, framing, target);

        bool[] wasOn = new bool[carRenderers.Length];
        for (int i = 0; i < carRenderers.Length; i++)
        {
            wasOn[i] = carRenderers[i].enabled;
            carRenderers[i].enabled = false;
        }
        Texture2D withoutCar = Grab(camera, framing, target);
        for (int i = 0; i < carRenderers.Length; i++) carRenderers[i].enabled = wasOn[i];

        double coverage;
        bool[] carMask = CarMask(withCar, withoutCar, out coverage);

        File.WriteAllBytes(Path.Combine(OutputDirectory, "framing.png"), withCar.EncodeToPNG());
        Object.DestroyImmediate(withCar);
        Object.DestroyImmediate(withoutCar);

        bool framingOk = coverage > 0.15;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "HATCHMOTION 차가 덮은 화면 비율={0:P1} {1}",
            coverage, framingOk ? "OK" : "⚠ 차가 프레임에 너무 작습니다"));

        // ── 먼저 획이 실제로 보이는지부터 ──
        //
        // 이 확인이 없으면 <b>고장 난 검사가 통과합니다.</b> 획이 아예 그려지지 않으면
        // 두 자리의 그림이 당연히 같아지고, 그것을 "안 미끄러진다" 로 읽게 됩니다.
        // 한 번 그렇게 헛짚었습니다.
        car.position = poseA;

        probe.SetFloat("_UseHatching", 0f);
        probe.DisableKeyword("_HATCHING");
        Texture2D bare = Grab(camera, carRenderers, target);

        probe.SetFloat("_UseHatching", 1f);
        probe.EnableKeyword("_HATCHING");

        Dictionary<string, Texture2D> shots = new Dictionary<string, Texture2D>();

        for (int mode = 0; mode < 2; mode++)
        {
            bool local = mode == 1;
            string tag = local ? "local" : "world";

            probe.SetFloat("_HatchLocal", local ? 1f : 0f);
            if (local) probe.EnableKeyword("_HATCH_LOCAL");
            else probe.DisableKeyword("_HATCH_LOCAL");

            car.position = poseA;
            shots[tag + "_a"] = Grab(camera, carRenderers, target);

            car.position = poseA + Step;
            shots[tag + "_b"] = Grab(camera, carRenderers, target);
        }

        // 전부 <b>차 픽셀 위에서만</b> 잽니다.
        double ink = MeanAbsoluteDifference(bare, shots["world_a"], carMask);
        double driftWorld = MeanAbsoluteDifference(shots["world_a"], shots["world_b"], carMask);
        double driftLocal = MeanAbsoluteDifference(shots["local_a"], shots["local_b"], carMask);
        double modeGap = MeanAbsoluteDifference(shots["world_a"], shots["local_a"], carMask);

        // 획이 눈에 띄게 그려지고 있어야 나머지 숫자가 의미를 가집니다.
        bool inkOk = ink > 0.005;

        // 같은 자리에서도 두 방식은 서로 다른 무늬여야 합니다. 같다면 토글이 안 먹은 것입니다.
        //
        // <b>고정 문턱을 쓰지 않습니다.</b> 이 값은 화면에서 차가 차지하는 넓이와 그날의
        // 빛에 함께 움직입니다. 대신 <b>월드가 흘러간 양</b>에 견줍니다 — 방식이 바뀌면
        // 무늬가 그만큼은 달라져야 합니다.
        bool toggleOk = modeGap > driftWorld * 0.5;

        // 월드는 흘러야 하고, 물체는 붙어 있어야 합니다.
        bool fixedOk = driftLocal < driftWorld * 0.35;

        passed = framingOk && inkOk && toggleOk && fixedOk;

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "HATCHMOTION 획이 보이는가={0:F5} {1}", ink, inkOk ? "OK" : "⚠ 획이 거의 없음"));
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "HATCHMOTION 두 방식이 다른가={0:F5} {1}", modeGap, toggleOk ? "OK" : "⚠ 토글이 안 먹음"));
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "HATCHMOTION 옮겼을 때 월드={0:F5} 물체={1:F5} {2}",
            driftWorld, driftLocal, fixedOk ? "OK (물체 쪽이 붙어 있음)" : "⚠ 물체 쪽도 미끄러짐"));

        foreach (KeyValuePair<string, Texture2D> pair in shots)
            File.WriteAllBytes(Path.Combine(OutputDirectory, pair.Key + ".png"), pair.Value.EncodeToPNG());

        File.WriteAllBytes(Path.Combine(OutputDirectory, "hatch_off.png"), bare.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(OutputDirectory, "world_diff.png"),
                           DifferenceImage(shots["world_a"], shots["world_b"]).EncodeToPNG());
        File.WriteAllBytes(Path.Combine(OutputDirectory, "local_diff.png"),
                           DifferenceImage(shots["local_a"], shots["local_b"]).EncodeToPNG());

        Object.DestroyImmediate(bare);
        foreach (KeyValuePair<string, Texture2D> pair in shots)
            Object.DestroyImmediate(pair.Value);

        // ── 마지막으로, 게임이 실제로 쓰는 값에서도 획이 보이는가 ──
        //
        // 위의 숫자들은 세기·시작밝기를 1 로 올려 잰 것입니다. <b>붙어 있다</b>는 것과
        // <b>보인다</b>는 것은 다른 이야기라, 씬 값으로 한 번 더 잽니다.
        // 여기가 0 에 가까우면 토글만 켜 두고 화면에는 아무 일도 일어나지 않는 것입니다.
        Shader.SetGlobalVector("_CarDriveHatchParams", _sceneParams);
        car.position = poseA;

        probe.SetFloat("_UseHatching", 0f);
        probe.DisableKeyword("_HATCHING");
        Texture2D plain = Grab(camera, carRenderers, target);

        probe.SetFloat("_UseHatching", 1f);
        probe.EnableKeyword("_HATCHING");
        Texture2D inked = Grab(camera, carRenderers, target);

        double sceneInk = MeanAbsoluteDifference(plain, inked, carMask);
        File.WriteAllBytes(Path.Combine(OutputDirectory, "scene_plain.png"), plain.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(OutputDirectory, "scene_inked.png"), inked.EncodeToPNG());
        File.WriteAllBytes(Path.Combine(OutputDirectory, "scene_ink_diff.png"),
                           DifferenceImage(plain, inked).EncodeToPNG());
        Object.DestroyImmediate(plain);
        Object.DestroyImmediate(inked);

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "HATCHMOTION 씬 값(세기 {0} / 시작밝기 {1})에서 획이 보이는가={2:F5} — 차체 _Ambient 를 낮추면 커집니다",
            _sceneParams.y, _sceneParams.z, sceneInk));

        Restore(carRenderers, originals);
        Object.DestroyImmediate(probe);

        car.position = home;
        camera.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(camera.gameObject);
        for (int i = 0; i < sleepers.Length; i++)
            if (sleepers[i] != null) sleepers[i].enabled = true;

        Debug.Log("HATCHMOTION 끝. 그림은 " + OutputDirectory);

        // 씬은 저장하지 않습니다. 차를 옮겨 둔 채로 저장되면 큰일입니다.
        EditorApplication.Exit(passed ? 0 : 1);
    }

    // --- Private Methods ---

    private static void Fail(string message)
    {
        Debug.Log("HATCHMOTION " + message);
        EditorApplication.Exit(2);
    }

    /// <summary>
    /// HatchingRig 의 값을 셰이더 전역에 넣습니다.
    ///
    /// 그 컴포넌트는 <c>[ExecuteAlways]</c> 가 아니라 편집 모드에서 <c>Update</c> 가 돌지 않습니다.
    /// 넣지 않으면 <c>_CarDriveHatchParams.w</c> 가 0 이라 셰이더가 빗금을 통째로 건너뛰고,
    /// <b>고장 났는데도 두 그림이 똑같이 나옵니다.</b>
    /// </summary>
    private static void ApplyHatchGlobals()
    {
        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].GetType().Name != "HatchingRig") continue;

            System.Type type = all[i].GetType();
            Texture2D bright = type.GetField("tamBright").GetValue(all[i]) as Texture2D;
            Texture2D dark = type.GetField("tamDark").GetValue(all[i]) as Texture2D;
            float scale = (float)type.GetField("scale").GetValue(all[i]);
            float strength = (float)type.GetField("strength").GetValue(all[i]);
            float startTone = (float)type.GetField("startTone").GetValue(all[i]);

            bool ready = bright != null && dark != null;

            _sceneParams = new Vector4(scale, strength, startTone, ready ? 1f : 0f);
            if (ready)
            {
                Shader.SetGlobalTexture("_CarDriveTamBright", bright);
                Shader.SetGlobalTexture("_CarDriveTamDark", dark);
            }
            // <b>일부러 끝까지 올립니다.</b> 씬 값(세기 0.6 / 시작밝기 0.62)은 햇빛을 받는 면에
            // 획을 거의 남기지 않습니다. 그 상태로 재면 획이 미끄러졌는지가 <b>배경 잡음에 묻힙니다.</b>
            // 여기서 보려는 것은 획의 진하기가 아니라 <b>어디에 붙어 있는가</b>이므로,
            // 밝은 면까지 획이 올라오게 해서 신호를 키웁니다.
            Shader.SetGlobalVector("_CarDriveHatchParams",
                new Vector4(scale, 1f, 1f, ready ? 1f : 0f));

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "HATCHMOTION 빗금 전역: 판={0}m 준비={1} (세기·시작밝기는 검사용으로 1 로 올림. 씬 값은 {2}/{3})",
                scale, ready, strength, startTone));
            return;
        }

        Debug.Log("HATCHMOTION ⚠ HatchingRig 이 씬에 없습니다. 빗금 없이 찍힙니다.");
    }

    private static Renderer[] FindRenderersUsing(Material material)
    {
        List<Renderer> found = new List<Renderer>();
        Renderer[] all = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            Material[] shared = all[i].sharedMaterials;
            for (int m = 0; m < shared.Length; m++)
                if (shared[m] == material) { found.Add(all[i]); break; }
        }

        return found.ToArray();
    }

    /// <summary>그 렌더러들을 모두 담는 가장 위쪽 트랜스폼입니다.</summary>
    private static Transform FindCarRoot(Renderer[] renderers)
    {
        Transform top = renderers[0].transform;
        while (top.parent != null) top = top.parent;
        return top;
    }

    private static Material[][] Swap(Renderer[] renderers, Material from, Material to)
    {
        Material[][] originals = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] shared = renderers[i].sharedMaterials;
            originals[i] = (Material[])shared.Clone();

            for (int m = 0; m < shared.Length; m++)
                if (shared[m] == from) shared[m] = to;

            renderers[i].sharedMaterials = shared;
        }

        return originals;
    }

    private static void Restore(Renderer[] renderers, Material[][] originals)
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sharedMaterials = originals[i];
    }

    private static MonoBehaviour[] DisableByTypeName(string typeName)
    {
        List<MonoBehaviour> disabled = new List<MonoBehaviour>();
        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].enabled && all[i].GetType().Name == typeName)
            {
                all[i].enabled = false;
                disabled.Add(all[i]);
            }

        return disabled.ToArray();
    }

    /// <summary>차를 비스듬히 위에서 내려다보는 임시 카메라입니다. 지붕과 보닛이 가장 넓게 보입니다.</summary>
    private static Camera MakeCarCamera(Camera source)
    {
        GameObject holder = new GameObject("HatchMotionCamera");
        Camera camera = holder.AddComponent<Camera>();

        camera.CopyFrom(source);
        camera.targetTexture = null;
        camera.rect = new Rect(0f, 0f, 1f, 1f);

        // ⚠ CopyFrom 은 URP 의 추가 카메라 데이터를 복사하지 않습니다.
        // 그대로 두면 이 카메라만 포스트프로세싱이 빠져 톤이 달라집니다.
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

        return camera;
    }

    /// <summary>
    /// 차를 늘 같은 화면 자리에 두고 찍습니다. 카메라가 차를 따라갑니다.
    ///
    /// <b>차의 트랜스폼이 아니라 렌더러의 경계 상자로 겨냥합니다.</b> 프리팹 인스턴스가
    /// 어떤 리그 밑에 매달려 있으면 위쪽 트랜스폼의 원점은 차가 있는 자리가 아닙니다.
    /// 처음에 그것으로 겨냥했다가 <b>차가 화면에 없는 그림</b>을 찍어 놓고
    /// "획이 안 미끄러진다" 고 읽을 뻔했습니다.
    /// </summary>
    private static Texture2D Grab(Camera camera, Renderer[] car, RenderTexture target)
    {
        return Grab(camera, WorldBounds(car), target);
    }

    /// <summary>겨냥할 상자를 직접 주는 판입니다. <b>차를 꺼 놓고 찍을 때</b> 이것이 필요합니다.</summary>
    private static Texture2D Grab(Camera camera, Bounds bounds, RenderTexture target)
    {
        float distance = Mathf.Max(1f, bounds.extents.magnitude) * 1.35f;

        camera.transform.position = bounds.center + new Vector3(0.62f, 0.45f, 0.65f) * distance;
        camera.transform.LookAt(bounds.center);

        camera.targetTexture = target;

        // 아직 컴파일 중인 셰이더가 단색으로 찍히지 않도록 몇 번 그립니다.
        for (int i = 0; i < 3; i++)
        {
            camera.Render();
            while (ShaderUtil.anythingCompiling) System.Threading.Thread.Sleep(50);
        }

        camera.targetTexture = null;

        Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        shot.Apply();
        RenderTexture.active = previous;

        return shot;
    }

    /// <summary>두 그림의 채널 평균 절대 차이입니다. 0 이면 완전히 같습니다.</summary>
    private static double MeanAbsoluteDifference(Texture2D a, Texture2D b)
    {
        return MeanAbsoluteDifference(a, b, null);
    }

    /// <summary>
    /// <c>mask</c> 가 참인 픽셀만 셉니다.
    ///
    /// <b>화면 전체로 재면 안 됩니다.</b> 차는 프레임의 일부일 뿐이고 나머지는 하늘·지형이라,
    /// 차 위에서 획이 통째로 흘러도 전체 평균으로는 소수점 세 자리에서 움직입니다.
    /// 그 숫자로는 고쳐졌는지 아닌지를 가를 수 없습니다.
    /// </summary>
    private static double MeanAbsoluteDifference(Texture2D a, Texture2D b, bool[] mask)
    {
        Color32[] pa = a.GetPixels32();
        Color32[] pb = b.GetPixels32();

        double sum = 0.0;
        int counted = 0;

        for (int i = 0; i < pa.Length; i++)
        {
            if (mask != null && !mask[i]) continue;

            sum += (Mathf.Abs(pa[i].r - pb[i].r)
                  + Mathf.Abs(pa[i].g - pb[i].g)
                  + Mathf.Abs(pa[i].b - pb[i].b)) / (3.0 * 255.0);
            counted++;
        }

        return counted == 0 ? 0.0 : sum / counted;
    }

    /// <summary>차가 그려진 픽셀입니다. 차를 끈 그림과 달라진 자리를 모읍니다.</summary>
    private static bool[] CarMask(Texture2D withCar, Texture2D withoutCar, out double coverage)
    {
        Color32[] pa = withCar.GetPixels32();
        Color32[] pb = withoutCar.GetPixels32();

        bool[] mask = new bool[pa.Length];
        int hits = 0;

        for (int i = 0; i < pa.Length; i++)
        {
            int d = Mathf.Abs(pa[i].r - pb[i].r)
                  + Mathf.Abs(pa[i].g - pb[i].g)
                  + Mathf.Abs(pa[i].b - pb[i].b);

            mask[i] = d > 12;
            if (mask[i]) hits++;
        }

        coverage = (double)hits / pa.Length;
        return mask;
    }

    /// <summary>차이를 눈으로 보게 8배로 부풀린 그림입니다.</summary>
    private static Texture2D DifferenceImage(Texture2D a, Texture2D b)
    {
        Color32[] pa = a.GetPixels32();
        Color32[] pb = b.GetPixels32();
        Color32[] outPixels = new Color32[pa.Length];

        for (int i = 0; i < pa.Length; i++)
        {
            byte r = (byte)Mathf.Min(255, Mathf.Abs(pa[i].r - pb[i].r) * 8);
            byte g = (byte)Mathf.Min(255, Mathf.Abs(pa[i].g - pb[i].g) * 8);
            byte bl = (byte)Mathf.Min(255, Mathf.Abs(pa[i].b - pb[i].b) * 8);
            outPixels[i] = new Color32(r, g, bl, 255);
        }

        Texture2D diff = new Texture2D(a.width, a.height, TextureFormat.RGB24, false);
        diff.SetPixels32(outPixels);
        diff.Apply();
        return diff;
    }

    /// <summary>렌더러 전체를 감싸는 월드 경계 상자입니다.</summary>
    private static Bounds WorldBounds(Renderer[] renderers)
    {
        Bounds bounds = new Bounds();
        bool started = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].enabled || !renderers[i].gameObject.activeInHierarchy) continue;

            if (!started) { bounds = renderers[i].bounds; started = true; }
            else bounds.Encapsulate(renderers[i].bounds);
        }

        return started ? bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private static Camera FindMainCamera()
    {
        if (Camera.main != null) return Camera.main;

        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i].cameraType == CameraType.Game && cameras[i].targetTexture == null)
                return cameras[i];

        return null;
    }
}
