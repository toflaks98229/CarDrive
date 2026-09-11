using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// 운전석 카메라를 <b>차에서 떼어 플레이어의 리그로</b> 옮깁니다.
///
/// <b>무엇이 고장이었나.</b> 메인 카메라가
/// <c>PlayerCar/CamTargetPoint/DriverPivot</c> 아래, 즉 <b>세단에 볼트로 박혀</b>
/// 있었습니다. 그래서 트럭 문으로 타면 <see cref="Vehicle.Current"/> 도
/// <c>CurrentVehicle</c> 도 트럭이 되는데 <b>눈만 세단에 남아</b>, 옆 차에 탄 것처럼
/// 보였습니다. 실측으로 트럭 운전석에서 6.0 m 떨어져 있었습니다.
///
/// <see cref="CarCameraFollow"/> 의 설명이 원래 답을 적어 두고 있습니다 —
/// "이 컴포넌트는 리그에 붙고, MainCamera 는 그 리그의 자식으로 둡니다."
/// 씬이 그 말과 어긋나 있었습니다. 리그는 차마다 다른 운전석을 따라가므로,
/// 카메라가 리그 아래 있으면 <b>어느 차에 타든</b> 그 차의 운전석에 앉습니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod CameraRigSetup.Run
/// </code>
/// </summary>
public static class CameraRigSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        PlayerModeController player = Object.FindAnyObjectByType<PlayerModeController>(
            FindObjectsInactive.Include);

        if (player == null || player.carCameraFollow == null)
        {
            Debug.Log("CAMRIG ⚠ 플레이어나 추종 카메라를 못 찾았습니다");
            EditorApplication.Exit(1);
            return;
        }

        Transform rig = player.carCameraFollow.transform;
        Transform pivot = player.driverPivot;

        if (pivot == null)
        {
            Debug.Log("CAMRIG ⚠ 운전석 피벗이 비어 있습니다");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("CAMRIG 전 — 피벗 " + Path(pivot) + " · 리그 " + Path(rig));

        if (pivot.parent == rig)
        {
            Debug.Log("CAMRIG 이미 리그 아래에 있습니다");
            EditorApplication.Exit(0);
            return;
        }

        // ⚠ <b>리그를 먼저 운전석 자리에 놓습니다.</b> 피벗을 옮긴 뒤에 리그를 옮기면
        // 첫 프레임에 카메라가 엉뚱한 데서 시작합니다.
        Transform seat = player.carCameraFollow.target;

        if (seat != null) rig.SetPositionAndRotation(seat.position, seat.rotation);

        // 자리를 유지한 채 옮기고, 로컬을 0 으로 맞춥니다. 리그가 곧 운전석이므로
        // 피벗은 그 위에 얹힌 <b>머리 회전</b>일 뿐입니다.
        pivot.SetParent(rig, false);
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("CAMRIG 후 — 피벗 " + Path(pivot) + " · 리그 자리 "
                  + rig.position.ToString("F1"));

        foreach (Camera camera in pivot.GetComponentsInChildren<Camera>(true))
        {
            Debug.Log("CAMRIG 딸려 온 카메라 — " + Path(camera.transform));
        }

        EditorApplication.Exit(0);
    }

    private static string Path(Transform t)
    {
        if (t == null) return "없음";

        string s = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
        return s;
    }
}
