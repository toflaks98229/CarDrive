using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 젖은 자국(CarDrive/Splat)을 <b>땅과 벽에서</b> 나이별로 찍습니다.
///
/// 자국은 값 몇 개가 얽혀 모양이 정해집니다 — 번짐·테두리 갉기·가운데 심·흘러내림·나이.
/// 숫자만 봐서는 "젖은 자국"인지 "칠한 원"인지 알 수 없어서 눈으로 골라야 합니다.
///
/// <b>실제 크기로 찍습니다.</b> 획을 월드 좌표(미터)로 긋기 때문에, 판을 크게 만들어 찍으면
/// 획이 실제보다 촘촘해 보여 <b>고른 값이 게임에서 전혀 다르게</b> 나옵니다. 게이지에서
/// 이미 한 번 겪은 일입니다(640px 리그에서 고른 값이 360px 바에서 무너졌습니다).
/// 그래서 판은 게임에서 자국이 자라는 크기 그대로 1m 로 둡니다.
///
/// <b>빗금 전역을 여기서 직접 물립니다.</b> HatchingRig 는 씬에 있는 컴포넌트라
/// 빈 씬에는 없습니다. 그냥 두면 셰이더가 디더로 물러서서, 정작 보려던 손그림 획이
/// 한 번도 안 그려진 그림을 보게 됩니다.
///
/// 쓰는 법 (그래픽 장치가 필요하므로 -nographics 를 붙이지 않습니다):
///   Unity.exe -batchmode -projectPath . -executeMethod SplatCapture.Run
/// 결과: Logs/Splat/*.png
/// </summary>
public static class SplatCapture
{
    private const string ShaderName = "CarDrive/Splat";
    private const string TamBrightPath = "Assets/_Project/04.Art/01.Images/Hatching/TAM_comic_bright.png";
    private const string TamDarkPath = "Assets/_Project/04.Art/01.Images/Hatching/TAM_comic_dark.png";
    private const string OutputDirectory = "Logs/Splat";

    private const int Width = 460;
    private const int Height = 460;

    /// <summary>자국 판의 실제 크기(m)입니다. 게임에서 자라는 크기와 같게 둡니다.</summary>
    private const float QuadMeters = 1.0f;

    /// <summary>씬의 빗금 전역값입니다. HatchingRig 가 넣는 것과 같은 뜻입니다.</summary>
    private const float WorldHatchScale = 0.8f;
    private const float HatchStrength = 0.6f;
    private const float HatchStartTone = 0.62f;

    /// <summary>획 크기를 훑어 볼 후보들(m)입니다.</summary>
    private static readonly float[] HatchScales = { 0.18f, 0.35f, 0.6f };

    /// <summary>
    /// 벽에서 판을 세로로 늘이는 배수와, 그 안에서 몸통을 위로 올리는 정도입니다.
    /// <b>런타임(UrineSplatter)과 같은 값이어야 합니다.</b> 다르면 여기서 고른 값이
    /// 게임에서 다른 그림이 됩니다.
    /// </summary>
    private const float WallAspect = 2.2f;
    private const float WallBodyOffsetY = 0.5f;

    public static void Run()
    {
        Shader sh = Shader.Find(ShaderName);
        if (sh == null) { Debug.Log("SPLAT 셰이더를 못 찾음: " + ShaderName); EditorApplication.Exit(1); return; }

        Texture bright = AssetDatabase.LoadAssetAtPath<Texture>(TamBrightPath);
        Texture dark = AssetDatabase.LoadAssetAtPath<Texture>(TamDarkPath);
        if (bright == null || dark == null)
        {
            Debug.Log("SPLAT TAM 을 못 찾음. 획 없이 디더로만 찍힙니다.");
        }

        Directory.CreateDirectory(OutputDirectory);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material mat = new Material(sh);

        // 자국이 얹힐 바탕. 흙빛으로 두어 실제 지면과 비슷하게 봅니다.
        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.transform.position = new Vector3(0, 0, 0.02f);
        bg.transform.localScale = new Vector3(QuadMeters * 3f, QuadMeters * 3f, 1f);
        Material bgm = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        bgm.color = new Color(0.45f, 0.35f, 0.23f, 1f);
        bg.GetComponent<Renderer>().sharedMaterial = bgm;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.position = Vector3.zero;
        quad.transform.localScale = new Vector3(QuadMeters, QuadMeters, 1f);
        quad.GetComponent<Renderer>().sharedMaterial = mat;

        GameObject camObj = new GameObject("SplatCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.18f, 0.16f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = QuadMeters * 0.55f;
        camObj.transform.position = new Vector3(0, 0, -3f);

        BindHatch(bright, dark, true);

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

        // 먼저 획 크기를 훑습니다. 자국은 30cm~1.4m 짜리라 지면에 쓰는 80cm 획은 너무 굵습니다.
        for (int i = 0; i < HatchScales.Length; i++)
        {
            mat.SetFloat("_HatchScale", HatchScales[i]);
            Shot(cam, quad, bg, target, mat, "scale_" + HatchScales[i].ToString("0.00", CultureInfo.InvariantCulture),
                 grow: 1.0f, age: 0.0f, drip: 0f, seed: 2f, aspect: 1f);
        }

        // 고른 값으로 실제 상태들을 찍습니다.
        mat.SetFloat("_HatchScale", 0.6f);

        // 땅: 흘러내림 없음, 판은 정사각.
        Shot(cam, quad, bg, target, mat, "ground_new",   grow: 0.55f, age: 0.0f,  drip: 0f, seed: 1f, aspect: 1f);
        Shot(cam, quad, bg, target, mat, "ground_grown", grow: 1.0f,  age: 0.0f,  drip: 0f, seed: 2f, aspect: 1f);
        Shot(cam, quad, bg, target, mat, "ground_dry",   grow: 1.0f,  age: 0.65f, drip: 0f, seed: 2f, aspect: 1f);

        // 벽: 판을 세로로 늘여 줄기가 자랄 자리를 줍니다.
        Shot(cam, quad, bg, target, mat, "wall_new",     grow: 0.6f,  age: 0.0f,  drip: 0.35f, seed: 3f, aspect: WallAspect);
        Shot(cam, quad, bg, target, mat, "wall_run",     grow: 0.85f, age: 0.15f, drip: 1f,    seed: 3f, aspect: WallAspect);
        Shot(cam, quad, bg, target, mat, "wall_dry",     grow: 0.85f, age: 0.7f,  drip: 1f,    seed: 3f, aspect: WallAspect);

        // 리그가 없을 때 물러서는 길도 한 장 남겨 둡니다.
        BindHatch(bright, dark, false);
        Shot(cam, quad, bg, target, mat, "fallback_dither", grow: 1.0f, age: 0.0f, drip: 0f, seed: 2f, aspect: 1f);

        cam.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(camObj);
        Object.DestroyImmediate(quad);
        Object.DestroyImmediate(bg);
        Object.DestroyImmediate(mat);
        Object.DestroyImmediate(bgm);

        Debug.Log("SPLAT 끝. 그림은 " + OutputDirectory);
        EditorApplication.Exit(0);
    }

    /// <summary>HatchingRig 가 하는 일을 대신합니다. w = 0 이면 셰이더가 디더로 물러섭니다.</summary>
    private static void BindHatch(Texture bright, Texture dark, bool on)
    {
        if (on && bright != null && dark != null)
        {
            Shader.SetGlobalTexture("_CarDriveTamBright", bright);
            Shader.SetGlobalTexture("_CarDriveTamDark", dark);
            Shader.SetGlobalVector("_CarDriveHatchParams",
                new Vector4(WorldHatchScale, HatchStrength, HatchStartTone, 1f));
        }
        else
        {
            Shader.SetGlobalVector("_CarDriveHatchParams", Vector4.zero);
        }
    }

    private static void Shot(Camera cam, GameObject quad, GameObject bg, RenderTexture target,
                             Material mat, string name,
                             float grow, float age, float drip, float seed, float aspect)
    {
        // 판을 늘이면 셰이더에도 같은 비를 알려야 몸통이 계란이 되지 않습니다.
        float offsetY = aspect > 1.01f ? WallBodyOffsetY : 0f;
        quad.transform.localScale = new Vector3(QuadMeters, QuadMeters * aspect, 1f);
        bg.transform.localScale = new Vector3(QuadMeters * 3f, QuadMeters * 3f * aspect, 1f);
        cam.orthographicSize = QuadMeters * aspect * 0.55f;
        mat.SetFloat("_Aspect", aspect);
        mat.SetFloat("_BodyOffsetY", offsetY);

        mat.SetFloat("_Grow", grow);
        mat.SetFloat("_Age", age);
        mat.SetFloat("_DripAmount", drip);
        mat.SetFloat("_Seed", seed);

        cam.targetTexture = target;
        cam.Render();
        cam.targetTexture = null;

        Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        shot.Apply();
        RenderTexture.active = prev;

        File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());

        // 덮인 넓이를 재 둡니다. 나이가 들수록 줄어야 정상입니다.
        Color32[] px = shot.GetPixels32();
        int wet = 0;
        for (int i = 0; i < px.Length; i++)
            if (px[i].r > 120 && px[i].g > 95) wet++;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "SPLAT {0,-17} 번짐={1:0.00} 나이={2:0.00} 흐름={3:0.00} 덮인비율={4:P1}",
            name, grow, age, drip, (float)wet / px.Length));

        Object.DestroyImmediate(shot);
    }
}
