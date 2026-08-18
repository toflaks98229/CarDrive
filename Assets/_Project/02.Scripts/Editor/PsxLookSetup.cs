using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 화면 룩을 PSX 방식으로 바꿉니다.
///
/// 하는 일:
///  1. 픽셀라이즈를 비롯한 <b>렌더러 피처를 모두 끕니다.</b> (지우지 않습니다)
///  2. 조명·안개·주변광 설정을 Unity 기본값으로 되돌립니다.
///     (SkyController 가 매 프레임 몰아 주던 것도 함께 끕니다)
///  3. 지면을 CarDrive/PSX Terrain 머티리얼로 갈아 끼웁니다.
///
/// 픽셀라이즈와 PSX는 겉보기가 비슷해 보이지만 방식이 다릅니다.
/// 픽셀라이즈는 <b>다 그린 화면을 뭉개는</b> 후처리라 지오메트리는 매끈한 채로 남습니다.
/// PSX의 인상은 반대로 <b>그리는 단계</b>에서 나옵니다. 정점이 격자에 붙어 흔들리고,
/// 원근 보정이 없어 텍스처가 휩니다. 그래서 인상을 셰이더 쪽으로 옮깁니다.
///
/// 후처리는 <b>지우지 않고 꺼 둡니다.</b> 나중에 픽셀라이즈를 겹쳐 보고 싶을 때
/// 인스펙터에서 체크 한 번이면 되살아납니다.
/// </summary>
public static class PsxLookSetup
{
    // --- Constants ---

    /// <summary>PSX 터레인 셰이더의 이름입니다.</summary>
    private const string TerrainShaderName = "CarDrive/PSX Terrain";

    /// <summary>만들어 낼 터레인 머티리얼 경로입니다.</summary>
    private const string TerrainMaterialPath = "Assets/_Project/04.Art/03.Shaders/PSX/PSXTerrain.mat";

    /// <summary>메인 씬 경로입니다.</summary>
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

    /// <summary>렌더러 데이터가 있는 폴더입니다.</summary>
    private const string SettingsFolder = "Assets/_Project/07.Settings";

    // --- Public Methods ---

    /// <summary>에디터 메뉴에서 실행합니다.</summary>
    [MenuItem("CarDrive/World/PSX 룩으로 전환")]
    public static void Apply()
    {
        List<string> report = new List<string>();

        DisableRendererFeatures(report);
        ResetLighting(report);
        ApplyPsxTerrain(report);

        Debug.Log("PsxLookSetup:" + System.Environment.NewLine +
                  string.Join(System.Environment.NewLine, report));
    }

    /// <summary>
    /// 명령줄에서 씬을 열고 적용한 뒤 저장합니다.
    /// <c>Unity.exe -batchmode -quit -executeMethod PsxLookSetup.ApplyFromCommandLine</c>
    /// </summary>
    public static void ApplyFromCommandLine()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Apply();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    // --- Private Methods ---

    /// <summary>
    /// 모든 렌더러 피처를 <b>끕니다. 지우지는 않습니다.</b>
    ///
    /// 지워 버리면 나중에 다시 쓰고 싶을 때 에셋을 새로 만들고 설정을 다시 잡아야 합니다.
    /// 피처에는 원래 켜고 끄는 스위치(m_Active)가 있으니 그것만 내려 둡니다.
    /// 인스펙터에서 체크 한 번으로 되살릴 수 있습니다.
    /// </summary>
    /// <param name="report">결과를 적을 목록</param>
    private static void DisableRendererFeatures(List<string> report)
    {
        string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { SettingsFolder });
        int turnedOff = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UniversalRendererData data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;

            for (int f = 0; f < data.rendererFeatures.Count; f++)
            {
                ScriptableRendererFeature feature = data.rendererFeatures[f];
                if (feature == null || !feature.isActive) continue;

                Undo.RecordObject(feature, "PSX 룩으로 전환");
                feature.SetActive(false);
                EditorUtility.SetDirty(feature);

                report.Add("  피처 끔: " + feature.name + "  (" + Path.GetFileName(path) + ")");
                turnedOff++;
            }

            EditorUtility.SetDirty(data);
        }

        if (turnedOff == 0) report.Add("  렌더러 피처: 이미 모두 꺼져 있습니다.");
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 조명·안개·주변광을 Unity 기본값으로 되돌립니다.
    /// </summary>
    /// <param name="report">결과를 적을 목록</param>
    private static void ResetLighting(List<string> report)
    {
        // 매 프레임 조명을 몰아 주던 컴포넌트를 먼저 멈춥니다.
        // 이것을 끄지 않으면 아래에서 되돌린 값이 첫 프레임에 다시 덮어써집니다.
        SkyController sky = Object.FindAnyObjectByType<SkyController>();
        if (sky != null)
        {
            Undo.RecordObject(sky, "PSX 룩으로 전환");
            sky.driveAmbient = false;
            sky.driveFogColor = false;
            sky.enabled = false;
            EditorUtility.SetDirty(sky);

            report.Add("  SkyController: 껐습니다. (주변광·안개색·태양 자동 조절 중단)");
        }

        WeatherRig rig = Object.FindAnyObjectByType<WeatherRig>();
        if (rig != null)
        {
            Undo.RecordObject(rig, "PSX 룩으로 전환");
            rig.controlAmbient = false;
            rig.controlRenderFog = false;
            rig.controlVisibility = false;
            EditorUtility.SetDirty(rig);

            report.Add("  WeatherRig: 조명·안개·시야 제어를 껐습니다. (비 파티클은 그대로)");
        }

        // 태양광을 기본값으로.
        Light sun = RenderSettings.sun;
        if (sun == null && sky != null) sun = sky.sun;
        if (sun != null)
        {
            Undo.RecordObject(sun, "PSX 룩으로 전환");
            sun.intensity = 1f;
            sun.color = Color.white;
            EditorUtility.SetDirty(sun);

            report.Add("  태양광: 세기 1, 흰색으로 되돌렸습니다.");
        }

        TimeSystem time = Object.FindAnyObjectByType<TimeSystem>();
        if (time != null)
        {
            Undo.RecordObject(time, "PSX 룩으로 전환");
            time.sunMaxIntensity = 1f;
            EditorUtility.SetDirty(time);

            report.Add("  TimeSystem.sunMaxIntensity: 1 로 되돌렸습니다.");
        }

        // 렌더 설정을 Unity 기본값으로.
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 1f;

        // 안개는 PSX 룩의 핵심이라 끄지 않고 기본값으로 둡니다.
        // (그 시절 게임은 짧은 시야를 안개로 가렸습니다)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.35f, 0.38f, 0.45f);
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 260f;

        report.Add("  주변광: Skybox 모드, 세기 1 (Unity 기본값)");
        report.Add("  안개: 선형 60~260m 로 초기화. PSX 룩의 일부라 끄지 않았습니다.");
    }

    /// <summary>
    /// 구운 터레인에 PSX 머티리얼을 씌웁니다.
    /// </summary>
    /// <param name="report">결과를 적을 목록</param>
    private static void ApplyPsxTerrain(List<string> report)
    {
        Shader shader = Shader.Find(TerrainShaderName);
        if (shader == null)
        {
            report.Add("  [실패] " + TerrainShaderName + " 셰이더를 찾지 못했습니다.");
            return;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
        if (mat == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TerrainMaterialPath));
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, TerrainMaterialPath);

            report.Add("  PSX 터레인 머티리얼을 만들었습니다. " + TerrainMaterialPath);
        }
        else
        {
            mat.shader = shader;
            report.Add("  PSX 터레인 머티리얼: 기존 것을 씁니다.");
        }

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
        int applied = 0;

        for (int i = 0; i < terrains.Length; i++)
        {
            Undo.RecordObject(terrains[i], "PSX 룩으로 전환");

            terrains[i].materialTemplate = mat;

            // 먼 지면을 통짜 텍스처(basemap)로 대체하면 PSX 셰이더가 아니라
            // 흐릿한 저해상도 이미지가 보입니다. 그 거리를 밀어 두어 항상 스플랫으로 그립니다.
            terrains[i].basemapDistance = 20000f;

            // 그 시절에는 지형이 그림자를 받지 않았습니다. 정점 조명만으로 충분합니다.
            terrains[i].heightmapPixelError = 12f;

            EditorUtility.SetDirty(terrains[i]);
            applied++;
        }

        report.Add("  터레인 " + applied + "개에 PSX 머티리얼을 적용했습니다.");
    }
}
