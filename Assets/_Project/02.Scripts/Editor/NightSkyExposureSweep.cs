using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 밤 하늘의 <b>노출값을 훑어</b> 별이 가장 잘 보이는 값을 찾습니다.
///
/// <b>왜 필요한가.</b> 밤 HDRI 에는 별이 밝기 값으로 들어 있어서 노출을 올리면 드러나지만,
/// 너무 올리면 하늘 전체가 함께 떠올라 <b>흰 판</b>이 되고 별이 묻힙니다. 이 프로젝트는
/// 컬러 그레이딩이 LDR 이라 1 을 넘는 값이 잘려 나가므로 그 구간이 특히 좁습니다.
/// 적정값은 HDRI 마다 다르므로 눈으로 찾는 수밖에 없습니다.
///
/// 빈 씬에 하늘만 띄웁니다. 지형 103장도 볼륨 그레이딩도 없으므로 <b>하늘 원본</b>이 보입니다.
/// (실제 화면의 분홍끼는 씬 볼륨이 만드는 것이라 여기서는 안 나옵니다. 최종 확인은
/// <see cref="SceneLookCapture"/> 로 하십시오.)
///
/// 쓰는 법 (그래픽 장치가 필요하므로 -nographics 를 붙이지 않습니다):
///   Unity.exe -batchmode -projectPath . -executeMethod NightSkyExposureSweep.Run
/// 결과: Logs/NightSweep/*.png 와 로그의 NIGHTSWEEP 줄
/// </summary>
public static class NightSkyExposureSweep
{
    private const string NightSkyPath = "Assets/_Project/04.Art/01.Images/Skybox/StarNightSky.mat";
    private const string OutputDirectory = "Logs/NightSweep";
    private const int Width = 640;
    private const int Height = 360;

    /// <summary>
    /// 훑어 볼 노출값입니다.
    ///
    /// <b>값이 아주 작습니다.</b> 이 HDRI 에는 달이 들어 있고 달의 밝기 값이 대단히 커서,
    /// 낮 하늘에 쓰는 1.1 같은 값을 주면 달 주변이 통째로 하얗게 날아갑니다.
    /// 별이 살아 있는 구간은 0.1 아래입니다.
    /// </summary>
    private static readonly float[] Exposures = { 0.01f, 0.02f, 0.04f, 0.06f, 0.1f, 0.15f, 0.25f };

    public static void Run()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(NightSkyPath);
        if (source == null)
        {
            Debug.Log("NIGHTSWEEP 재질을 찾지 못함: " + NightSkyPath);
            EditorApplication.Exit(1);
            return;
        }

        // 큐브맵으로 제대로 임포트됐는지부터 확인합니다. 2D 로 들어오면 하늘이 통째로 이상해집니다.
        Texture tex = source.HasProperty("_Tex") ? source.GetTexture("_Tex") : null;
        Debug.Log("NIGHTSWEEP 셰이더=" + (source.shader != null ? source.shader.name : "(없음)")
                  + " 텍스처=" + (tex != null ? tex.name : "(없음)")
                  + " 형=" + (tex != null ? tex.GetType().Name : "-")
                  + " 크기=" + (tex != null ? tex.width.ToString() : "-"));

        if (!(tex is Cubemap))
            Debug.Log("NIGHTSWEEP ⚠ 큐브맵이 아닙니다. 임포트 설정(textureShape: Cube)을 확인하십시오.");

        Directory.CreateDirectory(OutputDirectory);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material probe = new Material(source);
        RenderSettings.skybox = probe;
        RenderSettings.fog = false;

        GameObject cameraObject = new GameObject("NightSweepCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 66f;
        cameraObject.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

        for (int i = 0; i < Exposures.Length; i++)
        {
            float exposure = Exposures[i];
            probe.SetFloat("_Exposure", exposure);
            probe.SetColor("_Tint", Color.white);

            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;

            Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            // 소수 세 자리까지 씁니다. 자리수가 모자라면 0.01 과 0.04 가 같은 파일명이 돼
            // 서로를 덮어써서 어느 값의 그림인지 알 수 없게 됩니다.
            string name = "exposure_" + exposure.ToString("0.000", CultureInfo.InvariantCulture).Replace('.', '_');
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), shot.EncodeToPNG());

            Report(exposure, shot);

            Object.DestroyImmediate(shot);
        }

        camera.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(probe);
        Object.DestroyImmediate(cameraObject);

        Debug.Log("NIGHTSWEEP 끝. 그림은 " + OutputDirectory);
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 별이 보이는 정도를 셉니다.
    ///
    /// 좋은 밤하늘은 <b>바탕은 어둡고 점만 밝은</b> 그림입니다. 그래서 평균 밝기는 낮은데
    /// 밝은 점이 많은 값을 찾습니다. 평균이 높아지면 하늘이 통째로 떠오른 것이라 실패입니다.
    /// </summary>
    private static void Report(float exposure, Texture2D shot)
    {
        Color32[] pixels = shot.GetPixels32();

        double sum = 0.0;
        int stars = 0;
        int washed = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            float luminance = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
            sum += luminance;
            if (luminance > 0.55f) stars++;
            if (luminance > 0.85f) washed++;
        }

        float mean = (float)(sum / pixels.Length);
        float starRatio = (float)stars / pixels.Length;
        float washRatio = (float)washed / pixels.Length;

        // 바탕이 뜨면(평균 0.25 초과) 별이 묻힙니다. 반대로 별점이 거의 없으면 너무 어둡습니다.
        string verdict = mean > 0.25f ? "바탕뜸"
            : starRatio < 0.0005f ? "너무어두움"
            : "좋음";

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "NIGHTSWEEP {0,-8} 노출={1,-4} 평균={2:F4} 별점비율={3:F5} 포화비율={4:F5}",
            verdict, exposure, mean, starRatio, washRatio));
    }
}
