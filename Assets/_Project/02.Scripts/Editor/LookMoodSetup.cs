using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;
using CarDrive.Systems;

/// <summary>
/// <see cref="LookMood"/> 를 씬에 붙입니다.
///
/// <see cref="HatchingRig"/> 과 <b>같은 오브젝트에</b> 둡니다. 둘 다 화면의 결을
/// 셰이더 전역으로 미는 것이라, 한자리에 모여 있어야 나중에 찾습니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod LookMoodSetup.Run
/// </code>
/// </summary>
public static class LookMoodSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        HatchingRig rig = Object.FindAnyObjectByType<HatchingRig>(FindObjectsInactive.Include);

        if (rig == null)
        {
            Debug.Log("MOOD ⚠ HatchingRig 을 씬에서 찾지 못했습니다");
            EditorApplication.Exit(1);
            return;
        }

        LookMood mood = rig.GetComponent<LookMood>();

        if (mood == null)
        {
            mood = rig.gameObject.AddComponent<LookMood>();
            Debug.Log("MOOD 붙였습니다 — " + rig.gameObject.name);
        }
        else
        {
            Debug.Log("MOOD 이미 붙어 있습니다 — " + rig.gameObject.name);
        }

        EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("MOOD 위협 " + mood.threatInk.ToString("F2")
                  + " · 피로 " + mood.fatigueChroma.ToString("F2")
                  + " · 비 " + mood.rainInk.ToString("F2"));
        EditorApplication.Exit(0);
    }
}
