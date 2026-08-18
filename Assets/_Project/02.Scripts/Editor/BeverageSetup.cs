using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 음료 마시기·던지기에 필요한 배선을 잡아 줍니다.
///
///  1. PlayerInteractor 옆에 BeverageConsumer 를 붙입니다. (마시기 절차의 주인)
///  2. 병 프리팹을 상호작용 레이어로 옮깁니다.
///     레이캐스트가 Interactable 레이어만 보기 때문에, 병이 Default 레이어에 있으면
///     상자에서 굴러 나온 병을 조준해도 잡히지 않습니다.
/// </summary>
public static class BeverageSetup
{
    /// <summary>상호작용 레이어의 이름입니다.</summary>
    private const string InteractableLayer = "Interactable";

    /// <summary>메인 씬 경로입니다.</summary>
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

    /// <summary>병 프리팹 경로입니다.</summary>
    private const string BottlePrefabPath = "Assets/_Project/05.Prefabs/Items/Beer.prefab";

    // --- Public Methods ---

    /// <summary>에디터 메뉴에서 실행합니다.</summary>
    [MenuItem("CarDrive/Gameplay/음료 배선 설정")]
    public static void Setup()
    {
        List<string> report = new List<string>();

        SetupBottlePrefab(report);
        SetupConsumer(report);

        Debug.Log("BeverageSetup:" + System.Environment.NewLine +
                  string.Join(System.Environment.NewLine, report));
    }

    /// <summary>
    /// 명령줄에서 씬을 열고 배선한 뒤 저장합니다.
    /// <c>Unity.exe -batchmode -quit -executeMethod BeverageSetup.SetupFromCommandLine</c>
    /// </summary>
    public static void SetupFromCommandLine()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Setup();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    // --- Private Methods ---

    /// <summary>
    /// 병 프리팹을 상호작용 레이어로 옮깁니다.
    /// </summary>
    /// <param name="report">결과를 적을 목록</param>
    private static void SetupBottlePrefab(List<string> report)
    {
        int layer = LayerMask.NameToLayer(InteractableLayer);
        if (layer < 0)
        {
            report.Add("  [실패] " + InteractableLayer + " 레이어가 없습니다.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BottlePrefabPath);
        if (prefab == null)
        {
            report.Add("  [실패] 병 프리팹을 찾지 못했습니다. " + BottlePrefabPath);
            return;
        }

        if (prefab.layer == layer)
        {
            report.Add("  병 프리팹: 이미 " + InteractableLayer + " 레이어입니다.");
            return;
        }

        // 병에는 자식이 거의 없지만, 콜라이더가 자식에 있을 수 있으므로 전부 옮깁니다.
        GameObject root = PrefabUtility.LoadPrefabContents(BottlePrefabPath);
        int moved = 0;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].gameObject.layer == layer) continue;

            all[i].gameObject.layer = layer;
            moved++;
        }

        PrefabUtility.SaveAsPrefabAsset(root, BottlePrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        report.Add("  병 프리팹: 오브젝트 " + moved + "개를 " + InteractableLayer + " 레이어로 옮겼습니다. " +
                   "(이래야 굴러 나온 병을 조준할 수 있습니다)");
    }

    /// <summary>
    /// PlayerInteractor 가 있는 오브젝트에 BeverageConsumer 를 붙입니다.
    /// </summary>
    /// <param name="report">결과를 적을 목록</param>
    private static void SetupConsumer(List<string> report)
    {
        PlayerInteractor interactor = Object.FindAnyObjectByType<PlayerInteractor>();
        if (interactor == null)
        {
            report.Add("  [실패] PlayerInteractor 를 찾지 못했습니다.");
            return;
        }

        BeverageConsumer consumer = Object.FindAnyObjectByType<BeverageConsumer>();
        if (consumer == null)
        {
            consumer = Undo.AddComponent<BeverageConsumer>(interactor.gameObject);
            report.Add("  BeverageConsumer: " + interactor.gameObject.name + " 에 붙였습니다.");
        }
        else
        {
            report.Add("  BeverageConsumer: 이미 있습니다.");
        }

        Undo.RecordObject(interactor, "음료 배선 설정");
        interactor.beverageConsumer = consumer;
        EditorUtility.SetDirty(interactor);

        // 나머지 참조는 BeverageConsumer 가 실행 중에 스스로 찾습니다.
        EditorUtility.SetDirty(consumer);
    }
}
