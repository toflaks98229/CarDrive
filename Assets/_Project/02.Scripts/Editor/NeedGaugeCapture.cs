using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 니즈 게이지를 <b>채움 정도별로</b> 찍어 경계 연출을 눈으로 확인합니다.
///
/// <b>왜 SceneLookCapture 로는 안 되는가.</b> 그쪽은 월드 카메라를 렌더하는데 게이지는
/// UI 오버레이라 잡히지 않습니다. 그리고 게이지의 관심사는 시각대가 아니라
/// <b>채움 값</b>입니다 — 반쯤 찼을 때 경계가 어떻게 보이는지가 전부입니다.
///
/// 빈 씬에 판 하나를 세우고 게이지 재질로 직접 그립니다. 씬을 열지 않아 빠르고,
/// 결과가 이 셰이더만의 것이라 판정이 분명합니다.
///
/// <b>빗금 전역을 직접 넣습니다.</b> 게이지는 HatchingRig 가 쏘는 전역을 읽는데,
/// 편집 모드에서 리그를 돌리지 않으면 그 값이 비어 있어 디더 물러섬 경로가 찍힙니다.
/// 그러면 정작 보려던 빗금을 못 봅니다. 그래서 씬의 리그와 같은 값을 여기서 넣고,
/// 리그 없는 경우도 따로 한 장 찍어 물러섬이 실제로 도는지 확인합니다.
///
/// 쓰는 법 (그래픽 장치가 필요하므로 -nographics 를 붙이지 않습니다):
///   Unity.exe -batchmode -projectPath . -executeMethod NeedGaugeCapture.Run
/// 결과: Logs/NeedGauge/*.png
/// </summary>
public static class NeedGaugeCapture
{
    private const string MaterialPath = "Assets/_Project/04.Art/00.Materials/NeedGauge.mat";
    private const string TamBrightPath = "Assets/_Project/04.Art/01.Images/Hatching/TAM_comic_bright.png";
    private const string TamDarkPath = "Assets/_Project/04.Art/01.Images/Hatching/TAM_comic_dark.png";
    private const string OutputDirectory = "Logs/NeedGauge";

    // <b>실제 게이지 치수입니다.</b> 씬의 Need_* 바가 360 x 30 이고 Fill 이 거기 늘어납니다.
    // 획 크기가 화면 픽셀 절대값이라 이 치수가 맞아야 고른 값이 실제와 같습니다.
    // 처음에 640 x 96 으로 찍고 골랐다가, 실제 바가 세 배 납작해 값이 통째로 달라졌습니다.
    // 눈으로 보기 위해 세로만 정수배로 키워 찍습니다(획 크기는 1배 기준 그대로 넘깁니다).
    private const int GaugeWidth = 360;
    private const int GaugeHeight = 30;
    private const int Zoom = 3;

    private const int Width = GaugeWidth * Zoom;
    private const int Height = GaugeHeight * Zoom;

    /// <summary>씬의 HatchingRig 값과 같게 맞춥니다. 어긋나면 캡처가 실제와 달라집니다.</summary>
    private const float HatchScaleMeters = 0.8f;
    private const float HatchStrength = 0.6f;
    private const float HatchStartTone = 0.62f;

    /// <summary>재질에 저작해 둔 값과 같아야 합니다. 어긋나면 캡처가 실제와 달라집니다.</summary>
    private const float HatchPixelSize = 192f;
    private const float HatchAlphaBite = 1f;

    private static readonly float[] Fills = { 0.15f, 0.35f, 0.5f, 0.75f, 1f };

    public static void Run()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (source == null)
        {
            Debug.Log("GAUGE 재질을 찾지 못함: " + MaterialPath);
            EditorApplication.Exit(1);
            return;
        }

        Texture bright = AssetDatabase.LoadAssetAtPath<Texture>(TamBrightPath);
        Texture dark = AssetDatabase.LoadAssetAtPath<Texture>(TamDarkPath);

        Debug.Log("GAUGE 셰이더=" + (source.shader != null ? source.shader.name : "(없음)")
                  + " TAM=" + (bright != null && dark != null ? "있음" : "없음"));

        Directory.CreateDirectory(OutputDirectory);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material probe = new Material(source);

        // 판 하나를 카메라 앞에 세워 게이지 재질로 그립니다.
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.GetComponent<MeshRenderer>().sharedMaterial = probe;
        quad.transform.position = Vector3.zero;
        quad.transform.localScale = new Vector3(GaugeWidth / 100f, GaugeHeight / 100f, 1f);

        GameObject cameraObject = new GameObject("GaugeCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        // 어두운 남색 바탕. 실제 HUD 가 어두운 화면 위에 얹히므로 그쪽에 맞춥니다.
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = GaugeHeight / 200f;
        cameraObject.transform.position = new Vector3(0f, 0f, -5f);
        cameraObject.transform.rotation = Quaternion.identity;

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

        // 빗금이 도는 경우와, 리그가 없어 디더로 물러서는 경우를 모두 찍습니다.
        Capture(camera, target, probe, bright, dark, hatch: true);
        Capture(camera, target, probe, bright, dark, hatch: false);

        // 획 크기를 훑습니다. 이 값만 이 셰이더가 정하므로 눈으로 골라야 합니다.
        SweepStrokeSize(camera, target, probe, bright, dark);

        camera.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(probe);
        Object.DestroyImmediate(quad);
        Object.DestroyImmediate(cameraObject);

        Debug.Log("GAUGE 끝. 그림은 " + OutputDirectory);
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 획 크기를 여러 값으로 찍습니다. 채움은 절반으로 고정합니다 — 경계만 보면 되기 때문입니다.
    ///
    /// 이 값이 작으면 획이 잘아 그냥 흐릿한 결로 보이고, 크면 획 하나가 게이지를 가로질러
    /// 무늬가 아니라 얼룩이 됩니다. 사이 어딘가를 눈으로 골라야 합니다.
    /// </summary>
    private static void SweepStrokeSize(Camera camera, RenderTexture target, Material probe,
                                        Texture bright, Texture dark)
    {
        if (bright == null || dark == null) return;

        Shader.SetGlobalTexture("_CarDriveTamBright", bright);
        Shader.SetGlobalTexture("_CarDriveTamDark", dark);
        Shader.SetGlobalVector("_CarDriveHatchParams",
            new Vector4(HatchScaleMeters, HatchStrength, HatchStartTone, 1f));

        probe.SetFloat("_Fill", 0.5f);

        // 30px 짜리 바에 맞는 구간입니다. 실제 픽셀 기준값에 확대배를 곱해 넘깁니다.
        float[] sizes = { 96f, 144f, 192f, 256f, 340f };
        float[] bites = { 0.85f, 1f };

        for (int s = 0; s < sizes.Length; s++)
        {
            for (int b = 0; b < bites.Length; b++)
            {
                probe.SetFloat("_HatchPixelSize", sizes[s] * Zoom);
                probe.SetFloat("_HatchAlphaBite", bites[b]);

                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;

                Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;

                string name = string.Format(CultureInfo.InvariantCulture,
                    "grid_stroke{0:000}_bite{1:00}", sizes[s], bites[b] * 100f);
                File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());

                Report(string.Format(CultureInfo.InvariantCulture, "획{0:000}먹기{1:00}",
                    sizes[s], bites[b] * 100f), 0.5f, shot);
                Object.DestroyImmediate(shot);
            }
        }
    }

    private static void Capture(Camera camera, RenderTexture target, Material probe,
                                Texture bright, Texture dark, bool hatch)
    {
        if (hatch && bright != null && dark != null)
        {
            Shader.SetGlobalTexture("_CarDriveTamBright", bright);
            Shader.SetGlobalTexture("_CarDriveTamDark", dark);
            Shader.SetGlobalVector("_CarDriveHatchParams",
                new Vector4(HatchScaleMeters, HatchStrength, HatchStartTone, 1f));
        }
        else
        {
            // w = 0 이면 셰이더가 빗금을 건너뛰고 디더로 물러섭니다.
            Shader.SetGlobalVector("_CarDriveHatchParams", Vector4.zero);
        }

        // 확대해 찍으므로 획 크기도 같은 배율로 키워야 실제 화면과 같은 그림이 됩니다.
        probe.SetFloat("_HatchPixelSize", HatchPixelSize * Zoom);
        probe.SetFloat("_HatchAlphaBite", HatchAlphaBite);

        string tag = hatch ? "hatch" : "dither";

        for (int i = 0; i < Fills.Length; i++)
        {
            float fill = Fills[i];
            probe.SetFloat("_Fill", fill);

            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;

            Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            string name = string.Format(CultureInfo.InvariantCulture, "{0}_fill{1:000}", tag, fill * 100f);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());

            Report(tag, fill, shot);
            Object.DestroyImmediate(shot);
        }
    }

    /// <summary>
    /// 경계가 실제로 <b>부드럽게 끝나는지</b> 봅니다.
    ///
    /// 가로 한 줄을 훑어 밝은 픽셀이 어디서 끝나는지, 그리고 그 끝 언저리에
    /// 중간 밝기가 몇 열이나 있는지를 셉니다. 딱 잘리면 중간 열이 0 에 가깝고,
    /// 획이나 점으로 흩어지면 여러 열에 걸칩니다.
    /// </summary>
    private static void Report(string tag, float fill, Texture2D shot)
    {
        Color32[] pixels = shot.GetPixels32();
        int row = shot.height / 2;

        int lastLit = -1;
        int softColumns = 0;

        for (int x = 0; x < shot.width; x++)
        {
            // 세로로 평균 내어 한 줄의 대표 밝기를 냅니다. 디더는 줄마다 다르기 때문입니다.
            double sum = 0.0;
            for (int y = row - 8; y <= row + 8; y++)
            {
                Color32 p = pixels[y * shot.width + x];
                sum += (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
            }
            float lum = (float)(sum / 17.0);

            if (lum > 0.20f) lastLit = x;
            if (lum > 0.12f && lum < 0.55f) softColumns++;
        }

        float edge = shot.width > 0 ? (float)(lastLit + 1) / shot.width : 0f;

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "GAUGE {0,-6} 채움={1:0.00} 실제끝={2:0.00} 중간밝기열={3}",
            tag, fill, edge, softColumns));
    }
}
