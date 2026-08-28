using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 땅 얼룩과 벽 자국을 <b>나란히</b> 찍어 같은 그림인지 봅니다.
///
/// <b>왜 나란히인가.</b> 둘은 아예 다른 길로 그려집니다 — 벽은 판 하나에 셰이더가 전부
/// 그리고, 땅은 지도에서 읽은 값을 지형 셰이더가 해석합니다. 각각 따로 보면 "그럭저럭
/// 비슷하다" 로 넘어가기 쉬운데, 붙여 놓으면 <b>테두리 결이 다르다·획 크기가 다르다·
/// 색이 다르다</b> 가 바로 보입니다.
///
/// 땅 쪽은 지형을 띄우는 대신 <b>같은 함수를 쓰는 판</b>에 그립니다. 실제 지형을 세우려면
/// 타일과 지도 매니저까지 돌려야 하는데, 여기서 보려는 것은 <b>얼룩의 생김새</b>뿐입니다.
///
/// 쓰는 법 (그래픽 장치가 필요하므로 -nographics 를 붙이지 않습니다):
///   Unity.exe -batchmode -projectPath . -executeMethod SplatStyleCapture.Run
/// 결과: Logs/SplatStyle/*.png
/// </summary>
public static class SplatStyleCapture
{
    private const string OutputDirectory = "Logs/SplatStyle";
    private const string TamBrightPath = "Assets/_Project/04.Art/01.Images/Hatching/TAM_comic_bright.png";
    private const string TamDarkPath = "Assets/_Project/04.Art/01.Images/Hatching/TAM_comic_dark.png";

    private const int Width = 460;
    private const int Height = 460;

    /// <summary>구워 넣을 얼룩의 반지름(m). 게임에서 다 자란 웅덩이가 0.7m 입니다.</summary>
    private const float BlobRadius = 0.35f;

    /// <summary>지도 조회가 되는지 가리는 진단용. 왼쪽은 마름, 오른쪽은 흠뻑으로 굽습니다.</summary>
    private const bool DebugHalfMap = false;

    /// <summary>보는 범위(m). 두 장이 같은 배율이어야 견줄 수 있습니다.</summary>
    private const float ViewMeters = 2.4f;

    public static void Run()
    {
        Directory.CreateDirectory(OutputDirectory);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Texture bright = AssetDatabase.LoadAssetAtPath<Texture>(TamBrightPath);
        Texture dark = AssetDatabase.LoadAssetAtPath<Texture>(TamDarkPath);

        Shader.SetGlobalTexture("_CarDriveTamBright", bright);
        Shader.SetGlobalTexture("_CarDriveTamDark", dark);
        Shader.SetGlobalVector("_CarDriveHatchParams", new Vector4(0.8f, 0.6f, 0.62f, 1f));

        // ── 땅 쪽: 손으로 만든 지도에 동그란 얼룩 하나를 구워 넣습니다 ──
        //
        // 컴퓨트를 돌리지 않습니다. 여기서 보려는 것은 <b>지형 셰이더가 그 값을 어떻게
        // 해석하는가</b> 이지 컴퓨트가 잘 칠하는가가 아닙니다(그건 SplatManagerTests 가 봅니다).
        const int MapSize = 256;
        Texture2D map = new Texture2D(MapSize, MapSize, TextureFormat.RFloat, false);
        Color[] px = new Color[MapSize * MapSize];
        for (int y = 0; y < MapSize; y++)
        {
            for (int x = 0; x < MapSize; x++)
            {
                // 지도는 2m 를 덮고, 한가운데에 반지름 0.5m 짜리 얼룩을 둡니다.
                float u = (x + 0.5f) / MapSize;
                float v = (y + 0.5f) / MapSize;
                // d 는 <b>가운데에서의 미터</b>입니다(지도가 2m 를 덮으므로 x2).
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;

                // 컴퓨트의 브러시와 같은 식입니다: 1 - smoothstep(r*0.45, r, dist).
                //
                // <b>Mathf.SmoothStep 을 쓰면 안 됩니다.</b> 이름은 같아도 HLSL 의
                // smoothstep(edge0, edge1, x) 이 아니라 <b>from 과 to 사이를 t 로 보간</b>하는
                // 함수입니다. 그걸 썼다가 w 가 늘 0.65~0.84 로 나와 지도 전체가 젖었고,
                // 멀쩡한 셰이더를 한참 의심했습니다. HLSL 과 같은 식을 직접 씁니다.
                float t = Mathf.Clamp01((d - BlobRadius * 0.45f) / (BlobRadius * 0.55f));
                float w = 1f - t * t * (3f - 2f * t);

                if (DebugHalfMap) w = (u < 0.5f) ? 0f : 1f;   // 왼쪽 마름 / 오른쪽 흠뻑
                px[y * MapSize + x] = new Color(w, 0, 0, 1);
            }
        }
        map.SetPixels(px);
        map.Apply();

        Shader.SetGlobalTexture("_GlobalSplatMap", map);
        Shader.SetGlobalVector("_GlobalSplatMapRect", new Vector4(-1f, -1f, 1f / 2f, 1f / 2f));
        Shader.SetGlobalFloat("_GlobalSplatMapOn", 1f);

        // <b>빛과 팔레트를 세웁니다.</b> 처음에는 빈 씬에 판만 놓고 찍었는데, 빛이 없어
        // 전부 환경광으로만 깔리고 지형 팔레트도 기본값이라 <b>바탕이 무엇인지 알 수 없는</b>
        // 그림이 나왔습니다. 얼룩이 보이는지 보려면 마른 땅이 먼저 땅으로 보여야 합니다.
        GameObject lightObj = new GameObject("Sun");
        Light sun = lightObj.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.color = new Color(1f, 0.96f, 0.88f);
        lightObj.transform.rotation = Quaternion.Euler(52f, -30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.52f);

        GameObject camObj = new GameObject("StyleCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.18f, 0.16f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = ViewMeters * 0.5f;

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

        // 땅: 지형 셰이더를 쓰는 판을 위에서 내려다봅니다.
        Material ground = new Material(Shader.Find("CarDrive/Toon Terrain"));

        // 흙빛으로 맞춥니다. 벽 캡처의 바탕(0.45, 0.35, 0.23)과 같은 색이라야 견줄 수 있습니다.
        Color dirt = new Color(0.45f, 0.35f, 0.23f, 1f);
        ground.SetColor("_GrassColorA", dirt);
        ground.SetColor("_GrassColorB", dirt);
        ground.SetColor("_DirtColorA", dirt);
        ground.SetColor("_DirtColorB", dirt);
        ground.SetColor("_RoadColorA", dirt);
        ground.SetColor("_RoadColorB", dirt);
        GameObject groundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        groundQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 눕힙니다
        groundQuad.transform.localScale = new Vector3(2f, 2f, 1f);
        groundQuad.GetComponent<Renderer>().sharedMaterial = ground;

        camObj.transform.position = new Vector3(0f, 3f, 0f);
        camObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Shot(cam, target, "ground_stain");

        Object.DestroyImmediate(groundQuad);

        // 벽: 자국 셰이더를 쓰는 판을 정면에서 봅니다. 같은 크기의 얼룩으로 맞춥니다.
        Material wall = new Material(Shader.Find("CarDrive/Splat"));
        wall.SetFloat("_BodyRadius", BlobRadius);
        wall.SetFloat("_DripAmount", 0f);
        wall.SetFloat("_Age", 0f);
        wall.SetFloat("_Seed", 2f);
        wall.SetFloat("_HatchScale", 0.6f);

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.transform.position = new Vector3(0f, 0f, 0.05f);
        bg.transform.localScale = new Vector3(6f, 6f, 1f);
        Material bgm = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        bgm.color = new Color(0.45f, 0.35f, 0.23f, 1f);
        bg.GetComponent<Renderer>().sharedMaterial = bgm;

        GameObject wallQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wallQuad.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        wallQuad.GetComponent<Renderer>().sharedMaterial = wall;

        camObj.transform.position = new Vector3(0f, 0f, -3f);
        camObj.transform.rotation = Quaternion.identity;
        Shot(cam, target, "wall_stain");

        cam.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(camObj);
        Object.DestroyImmediate(wallQuad);
        Object.DestroyImmediate(bg);
        Object.DestroyImmediate(wall);
        Object.DestroyImmediate(bgm);
        Object.DestroyImmediate(ground);
        Object.DestroyImmediate(map);
        Object.DestroyImmediate(lightObj);

        Debug.Log("SPLATSTYLE 끝. 그림은 " + OutputDirectory);
        EditorApplication.Exit(0);
    }

    private static void Shot(Camera cam, RenderTexture target, string name)
    {
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
        Debug.Log(string.Format(CultureInfo.InvariantCulture, "SPLATSTYLE {0} 찍음", name));

        Object.DestroyImmediate(shot);
    }
}
