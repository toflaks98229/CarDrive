using UnityEditor;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// <b>오른쪽 거울이 왼쪽 그림을 띄우던 것</b>을 고칩니다.
///
/// <b>무엇이 고장이었나.</b> 거울은 셋이고 카메라도 셋인데, 옆거울 <b>둘이 같은
/// 재질</b>(<c>SideMirror</c>)을 쓰고 있었습니다. 그 재질이 문 <c>SideMirror_L</c>
/// 그림이라, 오른쪽 거울에도 <b>왼쪽 풍경</b>이 비쳤습니다.
///
/// 귀신의 시인성을 재다가 드러났습니다 — 뒤에 붙은 귀신이 옆거울 둘에 <b>똑같은
/// 비율</b>로 잡히길래 들여다봤습니다.
///
/// ⚠ <b>차가 둘입니다.</b> 트럭은 세단을 복제해 만들었으므로 같은 고장을 물려받았고,
/// 둘 다 고쳐야 합니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod MirrorFix.Run
/// </code>
/// </summary>
public static class MirrorFix
{
    private const string RightMaterial =
        "Assets/_Project/04.Art/01.Images/RenderTextures/Materials/SideMirror_R.mat";

    private static readonly string[] Cars =
    {
        "Assets/_Project/05.Prefabs/Player/PlayerCar.prefab",
        "Assets/_Project/05.Prefabs/Player/PlayerTruck.prefab",
    };

    public static void Run()
    {
        Material right = AssetDatabase.LoadAssetAtPath<Material>(RightMaterial);

        if (right == null)
        {
            Debug.Log("MIRRORFIX ⚠ 오른쪽 거울 재질이 없습니다 — " + RightMaterial);
            EditorApplication.Exit(1);
            return;
        }

        int fixedPanes = 0;

        foreach (string path in Cars)
        {
            GameObject car = PrefabUtility.LoadPrefabContents(path);

            try
            {
                int here = 0;

                foreach (Renderer pane in car.GetComponentsInChildren<Renderer>(true))
                {
                    // 오른쪽 거울 아래의 판만 봅니다.
                    if (pane.transform.parent == null) continue;
                    if (pane.transform.parent.name != "SideMirror_R") continue;

                    if (pane.sharedMaterial == right) continue;

                    Debug.Log("MIRRORFIX " + System.IO.Path.GetFileName(path) + " · "
                              + pane.name + " · "
                              + (pane.sharedMaterial != null ? pane.sharedMaterial.name : "없음")
                              + " → " + right.name);

                    pane.sharedMaterial = right;
                    here++;
                }

                if (here > 0) PrefabUtility.SaveAsPrefabAsset(car, path);

                fixedPanes += here;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(car);
            }
        }

        AssetDatabase.SaveAssets();

        Debug.Log("MIRRORFIX 끝 — 고친 판 " + fixedPanes + " 개");
        EditorApplication.Exit(0);
    }
}
