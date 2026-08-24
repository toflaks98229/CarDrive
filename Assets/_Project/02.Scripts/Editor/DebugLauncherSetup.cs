using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 플레이어의 조준 기준에 <see cref="DebugProjectileLauncher"/> 를 붙입니다.
    ///
    /// <b>왜 도구로 만드는가.</b> 붙일 자리가 하나로 정해져 있기 때문입니다.
    /// 이 프로젝트의 조준 계열 컴포넌트(<see cref="PlayerInteractor"/> 등)는
    /// <b>카메라 오브젝트</b>에 붙어 있고, 발사기도 같은 자리에 있어야 조준 방향이 맞습니다.
    /// 손으로 붙이면 계층 구조를 뒤져야 하고, 엉뚱한 오브젝트에 붙여도 <b>경고 없이 빗나갑니다.</b>
    ///
    /// 여러 번 눌러도 안전합니다. 이미 붙어 있으면 그대로 둡니다.
    /// </summary>
    public static class DebugLauncherSetup
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Gameplay/디버그 발사기 배선")]
        public static void Setup()
        {
            List<string> report = new List<string>();

            GameObject host = FindAimHost(report);

            if (host == null)
            {
                Debug.LogWarning("DebugLauncherSetup:" + System.Environment.NewLine +
                                 string.Join(System.Environment.NewLine, report));
                return;
            }

            DebugProjectileLauncher launcher = host.GetComponent<DebugProjectileLauncher>();

            if (launcher == null)
            {
                launcher = Undo.AddComponent<DebugProjectileLauncher>(host);
                report.Add("· 발사기를 붙였습니다: " + Path(host.transform));
            }
            else
            {
                report.Add("· 발사기가 이미 붙어 있습니다: " + Path(host.transform));
            }

            report.Add("· " + launcher.fireKey + " 키로 쏩니다. 질량 " + launcher.mass + "kg · 속도 " +
                       launcher.speed + "m/s → 운동량 " + (launcher.mass * launcher.speed).ToString("F0") + " N·s");
            report.Add("· 예상 Δv — 4족(320kg) " + DeltaV(launcher, 320f).ToString("F1") +
                       " · 드레드노트(900kg) " + DeltaV(launcher, 900f).ToString("F1") +
                       " · 스트라이더(2000kg) " + DeltaV(launcher, 2000f).ToString("F1") + " m/s");
            report.Add("· 넘어짐 문턱은 차례로 3.5 · 5.0 · 7.0 m/s 입니다. 모자라면 질량이나 속도를 올리세요.");

            EditorSceneManager.MarkSceneDirty(host.scene);
            Selection.activeGameObject = host;

            Debug.Log("DebugLauncherSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 배선한 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.DebugLauncherSetup.SetupFromCommandLine</c>
        /// </summary>
        public static void SetupFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Setup();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // --- Private Methods ---

        /// <summary>
        /// 이 발사체가 그 질량의 대상에게 줄 속도 변화를 어림잡습니다.
        ///
        /// 분모에 발사체 질량을 더하는 이유는 <b>공이 벽처럼 멈춰 서지 않기 때문</b>입니다.
        /// 부딪힌 뒤 둘이 운동량을 나눠 가지므로 맞은 쪽이 전부 받지는 못합니다.
        /// </summary>
        /// <param name="launcher">발사기</param>
        /// <param name="targetMass">맞는 것의 질량(kg)</param>
        /// <returns>속도 변화(m/s)</returns>
        private static float DeltaV(DebugProjectileLauncher launcher, float targetMass)
        {
            return launcher.mass * launcher.speed / (targetMass + launcher.mass);
        }

        /// <summary>
        /// 조준 기준이 될 오브젝트를 찾습니다.
        ///
        /// <b>왜 상호작용 컴포넌트를 먼저 찾는가.</b> 이 프로젝트에서 "조준하는 자리"의 <b>사실상의 정의</b>가
        /// 그것이 붙어 있는 오브젝트입니다. 카메라가 여럿일 수 있으므로(차량 카메라 등)
        /// 태그나 <c>Camera.main</c> 보다 이쪽이 정확합니다. 못 찾으면 그때 카메라로 물러납니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>찾은 오브젝트. 없으면 null</returns>
        private static GameObject FindAimHost(List<string> report)
        {
            PlayerInteractor interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            if (interactor != null) return interactor.gameObject;

            report.Add("· " + nameof(PlayerInteractor) + " 를 찾지 못해 메인 카메라를 찾습니다.");

            Camera camera = Camera.main;
            if (camera != null) return camera.gameObject;

            report.Add("· 조준 기준이 될 오브젝트를 찾지 못했습니다. 씬을 열고 다시 눌러 주세요.");
            return null;
        }

        /// <summary>계층 구조에서의 경로를 만듭니다. 어디에 붙었는지 로그로 보여 주려는 것입니다.</summary>
        /// <param name="target">대상</param>
        /// <returns>"부모/자식" 형태의 경로</returns>
        private static string Path(Transform target)
        {
            string path = target.name;

            for (Transform parent = target.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }
    }
}
