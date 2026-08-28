using System.IO;
using CarDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 젖은 자국 재질을 만들고 <see cref="UrineSplatter"/>를 씬에 붙입니다. 한 번만 돌리면 됩니다.
///
/// <b>왜 손이 아니라 도구인가.</b> 배치모드에서 씬을 열고 붙여야 하는 일이고, 값이
/// 여럿(레이어 마스크·재질·부모)이라 손으로 하면 무엇을 어떻게 걸었는지 기록이 남지 않습니다.
/// 다시 돌려도 이미 있으면 건드리지 않으므로 몇 번을 돌려도 같은 결과입니다.
///
/// 쓰는 법:
///   Unity.exe -batchmode -projectPath . -executeMethod UrineSplatSetup.Run
/// </summary>
public static class UrineSplatSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string ShaderName = "CarDrive/Splat";
    private const string MaterialPath = "Assets/_Project/04.Art/00.Materials/UrineSplat.mat";
    private const string ChildName = "UrineSplats";

    /// <summary>
    /// 자국이 붙을 수 있는 레이어입니다 — Default(0)·Interactable(6)·Car(8)·Prop(9)·Ground(11).
    ///
    /// <b>Enemy 와 Water 는 뺐습니다.</b> 걸어 다니는 것에 자국을 붙이면 판이 제자리에
    /// 남아 몸에서 떨어져 나가고, 물 위에 젖은 자국은 뜻이 없습니다.
    /// Ignore Raycast 도 당연히 빠집니다.
    /// </summary>
    public const int SurfaceMask = (1 << 0) | (1 << 6) | (1 << 8) | (1 << 9) | (1 << 11);

    public static void Run()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) { Fail("셰이더를 못 찾음: " + ShaderName); return; }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            Debug.Log("SPLATSETUP 재질을 만듦: " + MaterialPath);
        }

        // <b>있든 없든 항상 다시 씁니다.</b> 예전에는 재질이 있으면 건너뛰었는데,
        // 셰이더 프로퍼티의 <b>뜻이 바뀌었을 때</b> 그러면 옛 뜻의 숫자가 새 셰이더로
        // 흘러들어갑니다. _NoiseScale 과 _CoreSize 가 정확히 그런 경우입니다 —
        // 판 단위였던 것이 몸통 반지름 단위가 됐습니다.
        mat.SetColor("_Color", new Color(0.78f, 0.68f, 0.32f, 0.85f));
        mat.SetFloat("_HatchScale", 0.6f);
        mat.SetFloat("_NoiseScale", 3.5f);   // 정의역이 uv(0~1) -> nb(-1~1) 로 바뀌어 주기 두 배
        mat.SetFloat("_CoreSize", 0.35f);    // 판 단위 -> 몸통 반지름 대비 비율
        mat.SetFloat("_EdgeBite", 0.55f);
        mat.SetFloat("_DripPitch", 0.035f);
        mat.SetFloat("_DripTaper", 0.6f);
        EditorUtility.SetDirty(mat);
        Debug.Log("SPLATSETUP 재질 값을 새 뜻으로 다시 씀");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        UrineRelief relief = Object.FindAnyObjectByType<UrineRelief>(FindObjectsInactive.Include);
        if (relief == null) { Fail("씬에서 UrineRelief 를 못 찾음"); return; }

        UrineSplatter splatter = relief.GetComponentInChildren<UrineSplatter>(true);
        if (splatter == null)
        {
            GameObject go = new GameObject(ChildName);
            go.transform.SetParent(relief.transform, false);
            splatter = go.AddComponent<UrineSplatter>();
            Debug.Log("SPLATSETUP UrineSplatter 를 붙임: " + relief.name + "/" + ChildName);
        }
        else
        {
            Debug.Log("SPLATSETUP UrineSplatter 가 이미 있음");
        }

        splatter.splatMaterial = mat;

        // 필드 이름이 바뀌어(판 지름 -> 몸통 지름) 씬 값이 기본값으로 떨어졌습니다.
        // 옛 값 0.3 은 <b>판</b> 지름이라 그대로 이어받으면 자국이 두 배 넘게 커집니다.
        // 그래서 [FormerlySerializedAs] 를 일부러 안 붙이고 여기서 새로 써 넣습니다.
        SerializedObject so = new SerializedObject(splatter);
        so.FindProperty("_surfaceMask").intValue = SurfaceMask;
        so.FindProperty("startBodyDiameter").floatValue = 0.14f;
        so.FindProperty("maxBodyDiameter").floatValue = 1.4f;
        so.FindProperty("bodyGrowPerSecond").floatValue = 0.58f;
        so.FindProperty("startDripReach").floatValue = 0.05f;
        so.FindProperty("maxDripReach").floatValue = 1.2f;
        so.FindProperty("dripReachPerSecond").floatValue = 0.4f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // UrineRelief 는 자식에서 스스로 찾지만, 씬에 명시로 걸어 두면 나중에 자식 구조가
        // 바뀌어도 조용히 끊기지 않습니다.
        SerializedObject rso = new SerializedObject(relief);
        SerializedProperty prop = rso.FindProperty("_splatter");
        if (prop == null) { Fail("UrineRelief 에 _splatter 필드가 없음"); return; }
        prop.objectReferenceValue = splatter;
        rso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(relief.gameObject.scene);
        EditorSceneManager.SaveScene(relief.gameObject.scene);
        AssetDatabase.SaveAssets();

        Debug.Log("SPLATSETUP 끝.");
        EditorApplication.Exit(0);
    }

    private static void Fail(string message)
    {
        Debug.Log("SPLATSETUP 실패: " + message);
        EditorApplication.Exit(1);
    }
}
