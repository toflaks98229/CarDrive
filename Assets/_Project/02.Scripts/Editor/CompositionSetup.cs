using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Composition;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 씬에 <b>조립 루트</b>를 세우고 설치자들을 자동으로 이어 줍니다.
    ///
    /// <b>왜 도구로 만드는가.</b> DI로 옮기면 "누가 무엇을 아는지"가 코드에 드러나는 대신
    /// <b>씬에 오브젝트 하나를 놓고 칸을 채우는 일</b>이 새로 생깁니다. 그것을 손으로 하면
    /// 이 재편의 이득(배선이 드러남)이 "배선을 손으로 맞추는 수고"로 상쇄됩니다.
    ///
    /// 이 프로젝트는 이미 도구로 월드를 굽고 룩을 맞춰 왔습니다. (메뉴 41개)
    /// 조립도 같은 방식으로 다룹니다. 다시 실행해도 <b>있는 것은 그대로 두고 빠진 것만 채웁니다.</b>
    /// </summary>
    public static class CompositionSetup
    {
        // --- Constants ---

        /// <summary>조립 루트 오브젝트의 이름입니다.</summary>
        private const string RootName = "[Composition Root]";

        // --- Public Methods ---

        /// <summary>
        /// 씬에 조립 루트를 만들고 설치자와 시스템 참조를 이어 줍니다.
        /// </summary>
        [MenuItem("CarDrive/Composition/조립 루트 만들기 (DI)", priority = 1)]
        public static void BuildCompositionRoot()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "조립 루트 만들기");
            }

            CarDriveLifetimeScope scope = GetOrAdd<CarDriveLifetimeScope>(root);
            WorldRuntimeInstaller world = GetOrAdd<WorldRuntimeInstaller>(root);
            SimulationScope simulation = GetOrAdd<SimulationScope>(root);
            PlayerScope player = GetOrAdd<PlayerScope>(root);

            Undo.RecordObject(scope, "설치자 연결");
            scope.worldRuntime = world;
            scope.simulation = simulation;
            scope.player = player;

            int wired = 0;
            wired += WireSimulation(simulation);
            wired += WirePlayer(player);

            EditorUtility.SetDirty(scope);
            EditorUtility.SetDirty(simulation);
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(root.scene);

            Selection.activeGameObject = root;

            Debug.Log("CompositionSetup: 조립 루트를 세우고 참조 " + wired + "개를 이었습니다. " +
                      "빈 칸이 남았다면 그 시스템이 씬에 없다는 뜻입니다.", root);
        }

        /// <summary>
        /// 지금 씬의 조립 상태를 점검해 무엇이 빠졌는지 보고합니다.
        /// </summary>
        [MenuItem("CarDrive/Composition/조립 상태 점검", priority = 2)]
        public static void Diagnose()
        {
            CarDriveLifetimeScope scope = Object.FindAnyObjectByType<CarDriveLifetimeScope>(FindObjectsInactive.Include);
            if (scope == null)
            {
                Debug.LogWarning("CompositionSetup: 씬에 조립 루트가 없습니다. " +
                                 "CarDrive > Composition > 조립 루트 만들기 를 먼저 실행하세요.");
                return;
            }

            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("=== CarDrive 조립 상태 ===");

            report.AppendLine(Line("WorldRuntimeInstaller", scope.worldRuntime));
            report.AppendLine(Line("SimulationScope", scope.simulation));
            report.AppendLine(Line("PlayerScope", scope.player));

            if (scope.simulation != null)
            {
                report.AppendLine("--- 시뮬레이션 ---");
                report.AppendLine(Line("TimeSystem", scope.simulation.timeSystem));
                report.AppendLine(Line("WeatherSystem", scope.simulation.weatherSystem));
                report.AppendLine(Line("WeatherRig", scope.simulation.weatherRig));
                report.AppendLine(Line("NeedsSystem", scope.simulation.needsSystem));
                report.AppendLine(Line("Wallet", scope.simulation.wallet));
                report.AppendLine(Line("SaveSystem", scope.simulation.saveSystem));
            }

            if (scope.player != null)
            {
                report.AppendLine("--- 플레이어 ---");
                report.AppendLine(Line("PlayerModeController", scope.player.modeController));
                report.AppendLine(Line("PlayerHealth", scope.player.playerHealth));
            }

            report.AppendLine("--- 세이브 참여자 ---");
            report.AppendLine(Line("VehicleSaveParticipant",
                Object.FindAnyObjectByType<VehicleSaveParticipant>(FindObjectsInactive.Include)));
            report.AppendLine(Line("PlayerSaveParticipant",
                Object.FindAnyObjectByType<PlayerSaveParticipant>(FindObjectsInactive.Include)));
            report.AppendLine("  (없어도 GameBootstrap 이 실행 중에 만듭니다)");

            Debug.Log(report.ToString(), scope);
        }

        /// <summary>
        /// 명령줄에서 조립 루트를 세우고 씬을 저장합니다.
        ///
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.CompositionSetup.RunHeadless</c>
        ///
        /// 에디터를 열지 않고도 재편 결과를 씬에 반영하고 확인할 수 있어야 하기 때문에 둡니다.
        /// </summary>
        public static void RunHeadless()
        {
            const string scenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("CompositionSetup: 씬을 열지 못했습니다. " + scenePath);
                EditorApplication.Exit(1);
                return;
            }

            BuildCompositionRoot();
            Diagnose();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("COMPOSITION SETUP DONE");

            EditorApplication.Exit(0);
        }

        // --- Private Methods ---

        /// <summary>
        /// 시뮬레이션 설치자의 빈 칸을 씬에서 찾아 채웁니다.
        /// </summary>
        /// <param name="scope">채울 설치자</param>
        /// <returns>이번에 새로 이은 참조의 수</returns>
        private static int WireSimulation(SimulationScope scope)
        {
            Undo.RecordObject(scope, "시뮬레이션 연결");

            int n = 0;
            if (scope.timeSystem == null) { scope.timeSystem = Find<TimeSystem>(); n += scope.timeSystem != null ? 1 : 0; }
            if (scope.weatherSystem == null) { scope.weatherSystem = Find<WeatherSystem>(); n += scope.weatherSystem != null ? 1 : 0; }
            if (scope.weatherRig == null) { scope.weatherRig = Find<WeatherRig>(); n += scope.weatherRig != null ? 1 : 0; }
            if (scope.needsSystem == null) { scope.needsSystem = Find<NeedsSystem>(); n += scope.needsSystem != null ? 1 : 0; }
            if (scope.wallet == null) { scope.wallet = Find<Wallet>(); n += scope.wallet != null ? 1 : 0; }
            if (scope.saveSystem == null) { scope.saveSystem = Find<SaveSystem>(); n += scope.saveSystem != null ? 1 : 0; }

            return n;
        }

        /// <summary>
        /// 플레이어 설치자의 빈 칸을 씬에서 찾아 채웁니다.
        /// </summary>
        /// <param name="scope">채울 설치자</param>
        /// <returns>이번에 새로 이은 참조의 수</returns>
        private static int WirePlayer(PlayerScope scope)
        {
            Undo.RecordObject(scope, "플레이어 연결");

            int n = 0;
            if (scope.modeController == null) { scope.modeController = Find<PlayerModeController>(); n += scope.modeController != null ? 1 : 0; }
            if (scope.playerHealth == null) { scope.playerHealth = Find<PlayerHealth>(); n += scope.playerHealth != null ? 1 : 0; }

            return n;
        }

        /// <summary>
        /// 컴포넌트를 돌려주고, 없으면 붙입니다.
        /// </summary>
        /// <typeparam name="T">확보할 컴포넌트 타입</typeparam>
        /// <param name="host">붙일 대상 오브젝트</param>
        /// <returns>확보된 컴포넌트</returns>
        private static T GetOrAdd<T>(GameObject host) where T : Component
        {
            T found = host.GetComponent<T>();
            if (found != null) return found;

            return Undo.AddComponent<T>(host);
        }

        /// <summary>
        /// 씬에서 이 타입을 하나 찾습니다. 꺼져 있는 오브젝트도 봅니다.
        /// </summary>
        /// <typeparam name="T">찾을 컴포넌트 타입</typeparam>
        /// <returns>찾은 것. 없으면 null입니다.</returns>
        private static T Find<T>() where T : Component
        {
            return Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// 점검 보고서에 넣을 한 줄을 만듭니다.
        /// </summary>
        /// <param name="label">항목 이름</param>
        /// <param name="value">확인할 참조</param>
        /// <returns>연결 여부가 표시된 한 줄</returns>
        private static string Line(string label, Object value)
        {
            return (value != null ? "  [O] " : "  [X] ") + label;
        }
    }
}
