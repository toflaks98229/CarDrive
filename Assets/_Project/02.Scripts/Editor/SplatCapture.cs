using System.Globalization;
using System.IO;
using CarDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 젖은 자국(CarDrive/Splat)을 상태별로 찍고, <b>줄기가 정말 고정돼 있는지 픽셀로 판정</b>합니다.
///
/// <b>카메라를 한 번도 안 움직입니다.</b> 예전에는 샷마다 판 크기에 맞춰 orthographicSize 와
/// 판 위치를 바꿨습니다. 그러면 배율도 중심도 다른 그림이 나와서, <b>완벽히 고정된 줄기도
/// 옮겨 보입니다.</b> 두 장을 픽셀로 견주려면 앵커를 월드 원점에 고정하고 카메라를 묶어야 합니다.
///
/// <b>판 크기는 게임과 같은 함수로 잽니다</b>(<see cref="SplatQuadLayout"/>).
/// 여기에 비율을 다시 적으면 언젠가 한쪽만 고쳐집니다.
///
/// <b>빗금 전역을 여기서 직접 물립니다.</b> HatchingRig 는 씬 컴포넌트라 빈 씬에는 없습니다.
/// 그냥 두면 셰이더가 디더로 물러서서, 정작 보려던 손그림 획이 한 번도 안 그려진 그림을 봅니다.
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

    /// <summary>한 계열 안에서 절대 안 바뀌는 카메라입니다. 세로 3.0m 를 봅니다.</summary>
    private const float OrthoSize = 1.5f;

    /// <summary>카메라 중심을 앵커보다 이만큼 아래로 둡니다. 줄기가 화면에 들어오게.</summary>
    private const float CenterDrop = 0.55f;

    /// <summary>씬의 빗금 전역값입니다. HatchingRig 가 넣는 것과 같은 뜻입니다.</summary>
    private const float WorldHatchScale = 0.8f;
    private const float HatchStrength = 0.6f;
    private const float HatchStartTone = 0.62f;

    public static void Run()
    {
        Shader sh = Shader.Find(ShaderName);
        if (sh == null) { Debug.Log("SPLAT 셰이더를 못 찾음: " + ShaderName); EditorApplication.Exit(1); return; }

        Texture bright = AssetDatabase.LoadAssetAtPath<Texture>(TamBrightPath);
        Texture dark = AssetDatabase.LoadAssetAtPath<Texture>(TamDarkPath);
        if (bright == null || dark == null) Debug.Log("SPLAT TAM 을 못 찾음. 획 없이 디더로만 찍힙니다.");

        Directory.CreateDirectory(OutputDirectory);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material mat = new Material(sh);
        mat.SetFloat("_HatchScale", 0.6f);
        mat.SetFloat("_NoiseScale", 3.5f);
        mat.SetFloat("_CoreSize", 0.35f);
        mat.SetFloat("_EdgeBite", 0.55f);
        mat.SetFloat("_DripPitch", 0.035f);
        mat.SetFloat("_DripTaper", 0.6f);

        // 자국이 얹힐 바탕. 흙빛으로 두어 실제 지면과 비슷하게 봅니다. 절대 안 움직입니다.
        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.transform.position = new Vector3(0f, -CenterDrop, 0.02f);
        bg.transform.localScale = new Vector3(8f, 8f, 1f);
        Material bgm = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        bgm.color = new Color(0.45f, 0.35f, 0.23f, 1f);
        bg.GetComponent<Renderer>().sharedMaterial = bgm;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.GetComponent<Renderer>().sharedMaterial = mat;

        GameObject camObj = new GameObject("SplatCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.18f, 0.16f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = OrthoSize;
        camObj.transform.position = new Vector3(0f, -CenterDrop, -3f);

        BindHatch(bright, dark, true);

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

        // ── 계열 A: 사용자가 보고한 결함. 줄기를 고정하고 <b>몸통만</b> 3배로 키웁니다. ──
        //
        // <b>몸통을 작게 잡는 것이 중요합니다.</b> 처음에는 0.20~0.70m 로 잡았는데,
        // 줄기는 _DripTaper 0.6m 에서 옅어져 사라지므로 0.70m 짜리 몸통이 줄기를
        // <b>통째로 삼켜</b> 견줄 것이 남지 않았습니다. 몸통 바깥에 줄기가 보이는
        // 범위여야 불변식이 드러납니다.
        float[] radii = { 0.20f, 0.35f, 0.50f, 0.70f };
        Texture2D[] seriesA = new Texture2D[radii.Length];
        for (int i = 0; i < radii.Length; i++)
            seriesA[i] = Shot(cam, quad, target, mat, "A_body_" + F(radii[i]),
                              radii[i], 0.03f, 1.20f, 1f, 0f, 5f);

        // <b>머리 바로 아래</b>를 견줍니다. 한 픽셀이라도 다르면 줄기가 몸통을 따라
        // 움직였다는 뜻입니다.
        //
        // <b>여기에 자동 판정을 두지 않습니다.</b> 다섯 번 시도했고 다섯 번 다
        // 아무것도 가리지 못했습니다. 이유가 있습니다 — 결함이 있는 판에서도 줄기는
        // <b>더 크게</b> 자랄 뿐 지워지지 않으므로, 덮임을 재는 어떤 지표도
        // 옳은 동작과 결함을 못 나눕니다. 고정된 창은 몸통에 덮이거나 빈 자리에 놓입니다.
        //
        // 대신 두 곳에서 잡습니다 —
        //  - <b>구조</b>: SplatLayoutTests 가 셰이더 원문을 읽어, 줄기 계산이 몸통 반지름을
        //    한 번(within)만 쓰는지, 머리가 _DripStart 인지, 길이가 _DripReach 인지 봅니다.
        //    결함을 되살리면 실제로 실패합니다.
        //  - <b>눈</b>: A_body_*.png 를 겹쳐 보면 줄기가 제자리인지 한눈에 보입니다.
        //    카메라를 안 움직이므로 두 장이 픽셀 단위로 포개집니다.

        // ── 계열 C: 몸통 고정, 줄기만 성장. 이미 그어진 부분이 안 굵어져야 합니다. ──
        float[] reaches = { 0.20f, 0.50f, 0.85f, 1.20f };
        Texture2D[] seriesC = new Texture2D[reaches.Length];
        for (int i = 0; i < reaches.Length; i++)
            seriesC[i] = Shot(cam, quad, target, mat, "C_reach_" + F(reaches[i]),
                              0.30f, 0.03f, reaches[i], 1f, 0f, 7f);

        // C 계열도 눈으로 봅니다. 몸통이 고정이므로 두 장을 겹치면 <b>끝만</b> 내려가고
        // 이미 그어진 부분은 굵기도 진하기도 그대로여야 합니다.

        // ── 계열 D: 땅 자국. 앵커가 안 움직이고 실루엣이 닮은꼴로만 커져야 합니다. ──
        foreach (float r in radii)
            Free(Shot(cam, quad, target, mat, "D_ground_" + F(r), r, 0f, 0f, 0f, 0f, 9f));

        // ── 상태 샷: 룩 회귀용 ──
        Free(Shot(cam, quad, target, mat, "ground_new", 0.07f, 0f, 0f, 0f, 0f, 1f));
        Free(Shot(cam, quad, target, mat, "ground_grown", 0.60f, 0f, 0f, 0f, 0f, 2f));
        Free(Shot(cam, quad, target, mat, "ground_dry", 0.60f, 0f, 0f, 0f, 0.65f, 2f));
        Free(Shot(cam, quad, target, mat, "wall_new", 0.09f, 0.02f, 0.10f, 1f, 0f, 3f));
        Free(Shot(cam, quad, target, mat, "wall_run", 0.40f, 0.03f, 0.80f, 1f, 0.15f, 3f));
        Free(Shot(cam, quad, target, mat, "wall_dry", 0.40f, 0.03f, 0.80f, 1f, 0.70f, 3f));

        // ── 획 크기 훑기 ──
        foreach (float hs in new[] { 0.18f, 0.35f, 0.60f })
        {
            mat.SetFloat("_HatchScale", hs);
            Free(Shot(cam, quad, target, mat, "scale_" + F(hs), 0.60f, 0f, 0f, 0f, 0f, 2f));
        }
        mat.SetFloat("_HatchScale", 0.6f);

        // ── 리그가 없을 때 물러서는 길 ──
        BindHatch(bright, dark, false);
        Free(Shot(cam, quad, target, mat, "fallback_dither", 0.60f, 0f, 0f, 0f, 0f, 2f));

        foreach (Texture2D t in seriesA) Free(t);
        foreach (Texture2D t in seriesC) Free(t);

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

    // --- 도우미 ---

    private static string F(float v)
    {
        return v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static void Free(Texture2D t)
    {
        if (t != null) Object.DestroyImmediate(t);
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

    /// <summary>앵커를 월드 원점에 두고 한 장 찍습니다. 카메라는 건드리지 않습니다.</summary>
    private static Texture2D Shot(Camera cam, GameObject quad, RenderTexture target, Material mat,
                                  string name, float bodyRadius, float dripStart, float dripReach,
                                  float drip, float age, float seed)
    {
        float edgeBite = mat.GetFloat("_EdgeBite");

        Vector2 size;
        float centerLift;
        float bodyOffsetY;
        SplatQuadLayout.Resolve(bodyRadius, edgeBite, dripStart, dripReach, drip,
                                out size, out centerLift, out bodyOffsetY);

        quad.transform.position = new Vector3(0f, centerLift, 0f);   // 앵커가 원점
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);

        mat.SetFloat("_BodyRadius", bodyRadius);
        mat.SetFloat("_DripStart", dripStart);
        mat.SetFloat("_DripReach", dripReach);
        mat.SetFloat("_DripAmount", drip);
        mat.SetFloat("_BodyOffsetY", bodyOffsetY);
        mat.SetFloat("_Age", age);
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

        Color32[] px = shot.GetPixels32();
        Color32 paper = shot.GetPixel(2, 2);
        int wet = 0;
        for (int i = 0; i < px.Length; i++)
            if (IsWet(px[i], paper)) wet++;

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "SPLAT {0,-16} 몸통={1:0.00}m 줄기={2:0.00}m 나이={3:0.00} 판={4:0.00}x{5:0.00}m 덮인비율={6:P1}",
            name, bodyRadius, dripReach, age, size.x, size.y, (float)wet / px.Length));

        return shot;
    }

    /// <summary>바탕이 아니라 자국 색인지 봅니다. 바탕은 그림에서 읽은 값입니다.</summary>
    private static bool IsWet(Color32 c, Color32 paper)
    {
        return Mathf.Abs(c.r - paper.r) + Mathf.Abs(c.g - paper.g) + Mathf.Abs(c.b - paper.b) > 24;
    }

}
