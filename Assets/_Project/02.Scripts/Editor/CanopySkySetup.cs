using System;
using System.IO;
using CarDrive.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 블렌더가 구운 <b>천장 파노라마</b>를 하늘 자리에 물립니다.
///
/// 이 세계의 대지는 자연 지형이 아니라 거대 건축물의 <b>인공 지반 한 조각</b>이라,
/// 하늘에 있어야 할 것은 구름이 아니라 <b>위층의 밑면</b>입니다.
/// <c>Art/Blender/build_canopy.py</c> 가 그 밑면을 등장방형 한 장으로 굽고,
/// 여기서 큐브맵으로 세워 <see cref="SkyController"/> 에 꽂습니다.
///
/// <b>왜 진짜 기하가 아닌가.</b> 천장이 2.4 km 위라 원거리 클립 482 m 에 통째로
/// 잘립니다. 살리려면 레이어별 컬링 거리와 파클립 예외가 필요하고, 그러면 원경
/// 지표 문제를 고치며 맞춰 둔 안개·클립 사다리가 다시 어긋납니다. 스카이박스는
/// 언제나 무한대에 그려지므로 그 값들을 하나도 건드리지 않습니다.
///
/// <b>밤에도 같은 천장입니다.</b> 지붕이 덮인 세계에서 별이 보이면 이 설정이
/// 통째로 무너지므로, 밤 하늘도 같은 큐브맵을 씁니다. 노출만 떨어뜨리면 천장은
/// 어둠에 잠기고 <b>빛우물만 남습니다</b> — 별 대신 뜨는 것이 그것입니다.
/// 원래 쓰던 사진 하늘 둘은 지우지 않았습니다. 되돌리려면 참조만 바꾸면 됩니다.
///
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod CanopySkySetup.Run -quit
/// </code>
/// </summary>
public static class CanopySkySetup
{
    private const string SkyDir = "Assets/_Project/04.Art/01.Images/Skybox";
    private const string Baked = SkyDir + "/CanopySky_Baked.hdr";
    private const string DayMat = SkyDir + "/CanopySky.mat";
    private const string NightMat = SkyDir + "/CanopyNightSky.mat";
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

    /// <summary>
    /// 낮의 노출입니다. <b>0.5 인 데는 이유가 있습니다.</b>
    ///
    /// <c>Skybox/Cubemap</c> 은 <c>unity_ColorSpaceDouble</c> 로 두 배를 곱합니다.
    /// 노출 0.5 를 걸면 그 두 배가 지워져, <b>블렌더가 구운 선형값이 그대로 화면의
    /// 선형값</b>이 됩니다. 그러면 하늘을 씬의 다른 값에 맞추는 일이 산수가 됩니다 -
    /// 안개색이 선형 0.745 이니 파노라마의 지평선 연무도 0.745 로 구우면 끝입니다.
    ///
    /// 그 관계를 모르고 1.0 으로 두었을 때는 하늘이 네 배쯤 밝아, 어두운 지붕이어야
    /// 할 천장이 <b>크림색 판 한 장</b>으로 나왔습니다.
    /// </summary>
    private const float DayExposure = 0.5f;

    /// <summary>
    /// 밤의 노출입니다. 별 사진에 쓰던 0.04 는 <b>여기서는 너무 어둡습니다</b> —
    /// 은하수는 넓게 퍼져 있어 낮은 노출에서도 살지만, 빛우물은 점이라 같이
    /// 낮추면 천장이 통째로 검은 판이 됩니다.
    /// </summary>
    private const float NightExposure = 0.08f;

    public static void Run()
    {
        int errors = 0;

        try
        {
            if (!File.Exists(Baked))
            {
                Debug.LogError("CanopySkySetup: 구운 파노라마가 없습니다 — " + Baked +
                               " (Art/Blender/build_canopy.py 를 먼저 돌립니다)");
                errors++;
            }
            else
            {
                Cubemap();

                Texture sky = AssetDatabase.LoadAssetAtPath<Texture>(Baked);

                Material day = Sky(DayMat, sky, DayExposure);
                Material night = Sky(NightMat, sky, NightExposure);

                errors += Wire(day, night);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("CanopySkySetup: " + e);
            errors++;
        }

        if (Application.isBatchMode) EditorApplication.Exit(errors > 0 ? 2 : 0);
    }

    /// <summary>
    /// 등장방형 한 장을 큐브맵으로 세웁니다.
    ///
    /// 기존 하늘 두 장이 쓰는 것과 같은 길입니다 — <c>textureShape</c> 를 큐브로
    /// 두면 유니티가 2:1 파노라마를 알아보고 여섯 면으로 구워 줍니다. 이렇게
    /// 해야 <c>Skybox/Cubemap</c> 셰이더에 그대로 꽂힙니다.
    /// </summary>
    private static void Cubemap()
    {
        TextureImporter importer = AssetImporter.GetAtPath(Baked) as TextureImporter;

        if (importer == null)
        {
            AssetDatabase.ImportAsset(Baked, ImportAssetOptions.ForceSynchronousImport);
            importer = AssetImporter.GetAtPath(Baked) as TextureImporter;
        }

        if (importer == null)
        {
            Debug.LogError("CanopySkySetup: 임포터를 못 얻었습니다 — " + Baked);
            return;
        }

        importer.textureShape = TextureImporterShape.TextureCube;
        importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
        importer.maxTextureSize = 2048;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;

        importer.SaveAndReimport();

        Debug.Log("CanopySkySetup: 큐브맵으로 임포트 — " + Baked);
    }

    /// <summary>하늘 머티리얼 하나를 만들거나 고쳐 씁니다.</summary>
    private static Material Sky(string path, Texture sky, float exposure)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            mat = new Material(Shader.Find("Skybox/Cubemap"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetTexture("_Tex", sky);
        mat.SetFloat("_Exposure", exposure);
        mat.SetFloat("_Rotation", 0f);
        mat.SetColor("_Tint", Color.white);

        EditorUtility.SetDirty(mat);

        return mat;
    }

    /// <summary>
    /// 씬의 <see cref="SkyController"/> 에 꽂고 저장합니다.
    ///
    /// <c>_Exposure</c> 는 재생 중 <see cref="SkyController"/> 가 매 프레임 덮어
    /// 쓰므로, 머티리얼에 적은 값은 편집 중에만 보입니다. <b>실제 손잡이는
    /// 컴포넌트의 <c>dayExposure</c> 와 <c>nightSkyExposure</c> 입니다.</b>
    /// </summary>
    private static int Wire(Material day, Material night)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        SkyController controller =
            UnityEngine.Object.FindFirstObjectByType<SkyController>(FindObjectsInactive.Include);

        if (controller == null)
        {
            Debug.LogError("CanopySkySetup: 씬에 SkyController 가 없습니다");
            return 1;
        }

        controller.skyMaterial = day;
        controller.nightSkyMaterial = night;
        controller.dayExposure = DayExposure;
        controller.nightSkyExposure = NightExposure;

        EditorUtility.SetDirty(controller);

        RenderSettings.skybox = day;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("CanopySkySetup: " + controller.name + " 에 물림 — 낮 " +
                  day.name + " · 밤 " + night.name);

        return 0;
    }
}
