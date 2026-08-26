using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CarDrive.UI;

/// <summary>
/// 니즈 게이지를 <b>디더 방식으로 갈아 끼웁니다.</b> 한 번 쓰고 마는 배선 도구입니다.
///
/// 하는 일 셋 —
///   1. <c>CarDrive/UI Need Gauge</c> 셰이더를 쓰는 재질을 만듭니다.
///   2. 씬의 <see cref="NeedsUI"/> 에 그 재질을 꽂습니다.
///   3. 게이지 뒤에 있던 <b>지연 바(빨간 바)</b>를 끕니다.
///
/// <b>지우지 않고 끕니다.</b> 지연 바는 손으로 만든 UI 오브젝트인데 씬에 아직 커밋되지 않은
/// 다른 변경이 섞여 있어, 되돌릴 기준이 없는 상태에서 지우는 것은 위험합니다.
/// 화면에서 사라지는 결과는 같고, 확인한 뒤 손으로 지우면 됩니다.
///
/// 쓰는 법:
///   Unity.exe -batchmode -nographics -projectPath . -executeMethod NeedGaugeSetup.Run
/// </summary>
public static class NeedGaugeSetup
{
    /// <summary>배선할 씬입니다.</summary>
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

    /// <summary>만들 재질의 자리입니다.</summary>
    private const string MaterialPath = "Assets/_Project/04.Art/00.Materials/NeedGauge.mat";

    /// <summary>게이지가 쓸 셰이더의 이름입니다.</summary>
    private const string ShaderName = "CarDrive/UI Need Gauge";

    /// <summary>재질을 만들고 씬에 꽂은 뒤 지연 바를 끕니다.</summary>
    public static void Run()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.Log("GAUGESETUP 셰이더를 찾지 못했습니다: " + ShaderName);
            EditorApplication.Exit(1);
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            Debug.Log("GAUGESETUP 재질을 만들었습니다: " + MaterialPath);
        }
        else
        {
            material.shader = shader;
            Debug.Log("GAUGESETUP 재질이 이미 있어 셰이더만 맞췄습니다.");
        }

        EditorUtility.SetDirty(material);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        NeedsUI ui = Object.FindAnyObjectByType<NeedsUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            Debug.Log("GAUGESETUP 씬에서 NeedsUI 를 찾지 못했습니다.");
            EditorApplication.Exit(1);
            return;
        }

        ui.gaugeMaterial = material;

        int hidden = 0;

        for (int i = 0; i < ui.bars.Count; i++)
        {
            NeedsUI.NeedBar bar = ui.bars[i];
            if (bar == null || bar.progressBar == null) continue;

            Transform delayed = bar.progressBar.DelayedBarDecreasing;
            if (delayed != null && delayed.gameObject.activeSelf)
            {
                delayed.gameObject.SetActive(false);
                hidden++;
            }

            Transform delayedUp = bar.progressBar.DelayedBarIncreasing;
            if (delayedUp != null && delayedUp.gameObject.activeSelf)
            {
                delayedUp.gameObject.SetActive(false);
                hidden++;
            }
        }

        Debug.Log("GAUGESETUP 게이지 " + ui.bars.Count + "개에 재질을 꽂았고, 지연 바 " + hidden + "개를 껐습니다.");

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("GAUGESETUP 끝");

        EditorApplication.Exit(0);
    }
}
