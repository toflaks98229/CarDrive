using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 풀을 밟고 지나갈 것들에 <see cref="GrassPusher"/>를 붙입니다.
    ///
    /// 붙일 곳을 경로로 적어 두지 않고 <b>컴포넌트로 찾습니다.</b>
    /// 프리팹이 옮겨지거나 이름이 바뀌어도 계속 동작하고, 나중에 차나 적이 늘어나도
    /// 이 도구를 다시 돌리면 새로 생긴 것까지 함께 배선됩니다.
    ///
    /// 차에는 바퀴 넷과 <b>차체 한가운데</b>에도 붙입니다.
    /// 바퀴에만 붙이면 네 모서리만 눌리고 가운데가 남아, 그 남은 풀이 차 바닥을 뚫고
    /// 실내로 올라옵니다. 운전 중에는 카메라가 실내에 있어 그게 그대로 보입니다.
    /// </summary>
    public static class GrassTrampleSetup
    {
        // --- Constants ---

        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>프리팹을 찾을 폴더입니다.</summary>
        private const string PrefabFolder = "Assets/_Project/05.Prefabs";

        /// <summary>차체 한가운데에 만들 오브젝트의 이름입니다.</summary>
        private const string BodyPusherName = "GrassPusher_Body";

        /// <summary>만질 만한 값들은 설정 에셋에 있습니다. (CarDrive 메뉴의 월드 창)</summary>
        private static CarDriveWorldSettings Settings { get { return CarDriveWorldSettings.Instance; } }

        private static float WheelRadius { get { return Settings.wheelRadius; } }
        private static float BodyRadius { get { return Settings.bodyRadius; } }
        private static float PlayerRadius { get { return Settings.playerRadius; } }
        private static float GhostRadius { get { return Settings.ghostRadius; } }
        private static float PlayerMass { get { return Settings.playerMass; } }

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/World/풀 눕히기 배선")]
        public static void Apply()
        {
            List<string> report = new List<string>();

            WirePrefabs(report);
            WireScene(report);

            AssetDatabase.SaveAssets();

            Debug.Log("GrassTrampleSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 실행하고 씬을 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod GrassTrampleSetup.ApplyFromCommandLine</c>
        /// </summary>
        public static void ApplyFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Apply();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 배선이 실제로 붙었는지 확인합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod GrassTrampleSetup.VerifyFromCommandLine</c>
        /// </summary>
        public static void VerifyFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GrassPusher[] found = Object.FindObjectsByType<GrassPusher>(FindObjectsInactive.Include);
            Debug.Log("TRAMPLE 씬에서 찾은 누르개: " + found.Length + "개");

            for (int i = 0; i < found.Length; i++)
            {
                Debug.Log("TRAMPLE   " + Path(found[i].transform) + "  반경 " + found[i].radius + "m");
            }

            // 셰이더가 실제로 그 값을 읽을 수 있는 상태인지도 봅니다.
            Material grass = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/04.Art/03.Shaders/LowPoly/LowPolyGrass.mat");

            if (grass == null)
            {
                Debug.Log("TRAMPLE [경고] 풀 머티리얼을 찾지 못했습니다.");
                return;
            }

            Debug.Log("TRAMPLE 풀 머티리얼: 눕는 정도 " + grass.GetFloat("_PushLay") +
                      " / 밀리는 거리 " + grass.GetFloat("_PushSpread") + "m" +
                      " / 위아래 " + grass.GetFloat("_PushHeightReach") + "m");
        }

        // --- Private Methods ---





        /// <summary>프리팹 폴더를 훑어 배선합니다.</summary>
        /// <param name="report">결과를 적을 목록</param>
        private static void WirePrefabs(List<string> report)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !NeedsWiring(asset)) continue;

                // 프리팹은 열어서 고치고 다시 저장해야 합니다.
                // 에셋을 직접 건드리면 중첩 프리팹에서 조용히 어긋납니다.
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                WireRoot(root, report, System.IO.Path.GetFileName(path));

                // <b>새로 붙였을 때만 저장하면 안 됩니다.</b>
                // 이미 붙어 있는 것의 값만 바꾸는 경우가 훨씬 많은데,
                // 그때 저장하지 않으면 바꾼 값이 조용히 사라집니다. 실제로 그렇게 당했습니다.
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>씬에 직접 놓인 것들을 배선합니다.</summary>
        /// <param name="report">결과를 적을 목록</param>
        private static void WireScene(List<string> report)
        {
            // 프리팹 인스턴스는 위에서 프리팹 쪽을 고쳤으므로 저절로 따라옵니다.
            // 여기서는 씬에만 있는 것들을 챙깁니다.
            PlayerFootMotor[] players = Object.FindObjectsByType<PlayerFootMotor>(FindObjectsInactive.Include);
            for (int i = 0; i < players.Length; i++)
            {
                Attach(players[i].gameObject, PlayerRadius, true, PlayerMass, report, "씬");
            }

            EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include);
            for (int i = 0; i < enemies.Length; i++)
            {
                Attach(enemies[i].gameObject, GhostRadius, false, 0f, report, "씬");
            }

            CarVisuals[] cars = Object.FindObjectsByType<CarVisuals>(FindObjectsInactive.Include);
            for (int i = 0; i < cars.Length; i++)
            {
                WireCar(cars[i], report, "씬");
            }
        }

        /// <summary>이 프리팹에 붙일 것이 있는지 봅니다.</summary>
        /// <param name="root">볼 프리팹</param>
        /// <returns>붙일 것이 있으면 true입니다.</returns>
        private static bool NeedsWiring(GameObject root)
        {
            return root.GetComponentInChildren<CarVisuals>(true) != null
                || root.GetComponentInChildren<PlayerFootMotor>(true) != null
                || root.GetComponentInChildren<EnemyController>(true) != null;
        }

        /// <summary>한 뿌리 아래를 훑어 붙일 곳마다 붙입니다.</summary>
        /// <param name="root">뿌리 오브젝트</param>
        /// <param name="report">결과를 적을 목록</param>
        /// <param name="where">어디인지 적을 이름</param>
        /// <returns>새로 붙인 개수</returns>
        private static int WireRoot(GameObject root, List<string> report, string where)
        {
            int added = 0;

            CarVisuals[] cars = root.GetComponentsInChildren<CarVisuals>(true);
            for (int i = 0; i < cars.Length; i++) added += WireCar(cars[i], report, where);

            PlayerFootMotor[] players = root.GetComponentsInChildren<PlayerFootMotor>(true);
            for (int i = 0; i < players.Length; i++) added += Attach(players[i].gameObject, PlayerRadius, true, PlayerMass, report, where);

            EnemyController[] enemies = root.GetComponentsInChildren<EnemyController>(true);
            // 유령은 자국을 남기지 않습니다. 지나가는 동안만 풀이 눕습니다.
            for (int i = 0; i < enemies.Length; i++) added += Attach(enemies[i].gameObject, GhostRadius, false, 0f, report, where);

            return added;
        }

        /// <summary>
        /// 차 한 대를 배선합니다. 바퀴 넷과 차체 가운데에 붙입니다.
        /// </summary>
        /// <param name="visuals">차의 바퀴를 들고 있는 컴포넌트</param>
        /// <param name="report">결과를 적을 목록</param>
        /// <param name="where">어디인지 적을 이름</param>
        /// <returns>새로 붙인 개수</returns>
        private static int WireCar(CarVisuals visuals, List<string> report, string where)
        {
            Transform[] wheels =
            {
                visuals.frontLeftWheelTransform,
                visuals.frontRightWheelTransform,
                visuals.rearLeftWheelTransform,
                visuals.rearRightWheelTransform
            };

            int added = 0;
            Vector3 sum = Vector3.zero;
            int counted = 0;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null) continue;

                // 바퀴는 자국을 남깁니다. 무게는 0으로 두어 차의 Rigidbody 에서 읽게 합니다.
                added += Attach(wheels[i].gameObject, WheelRadius, true, 0f, report, where);

                sum += visuals.transform.InverseTransformPoint(wheels[i].position);
                counted++;
            }

            if (counted == 0)
            {
                report.Add("  [경고] " + where + ": 차의 바퀴를 찾지 못했습니다.");
                return added;
            }

            // 바퀴 넷 사이의 빈 가운데를 덮을 하나를 더 둡니다.
            // 이것이 없으면 차 한가운데 풀이 남아 실내로 뚫고 올라옵니다.
            Transform body = visuals.transform.Find(BodyPusherName);
            if (body == null)
            {
                GameObject go = new GameObject(BodyPusherName);
                go.transform.SetParent(visuals.transform, false);
                body = go.transform;

                report.Add("  " + where + ": 차체 가운데에 누르개를 만들었습니다.");
            }

            // 바퀴들의 한가운데, 바닥 높이에 둡니다.
            Vector3 center = sum / counted;
            body.localPosition = new Vector3(center.x, center.y, center.z);

            // <b>차체는 자국을 남기지 않습니다.</b>
            // 차 밑을 비우기 위한 것이지 바닥에 눌린 자국을 내려는 게 아닙니다.
            // 남기면 차 폭만큼 넓은 띠가 생겨 바퀴 자국이 아니라 불도저 자국이 됩니다.
            added += Attach(body.gameObject, BodyRadius, false, 0f, report, where);
            return added;
        }

        /// <summary>
        /// 오브젝트 하나에 누르개를 붙입니다. 이미 있으면 반경만 맞춥니다.
        /// </summary>
        /// <param name="target">붙일 오브젝트</param>
        /// <param name="radius">누를 반경</param>
        /// <param name="report">결과를 적을 목록</param>
        /// <param name="where">어디인지 적을 이름</param>
        /// <returns>새로 붙였으면 1, 이미 있었으면 0입니다.</returns>
        private static int Attach(GameObject target, float radius, bool leavesMark, float mass,
                                  List<string> report, string where)
        {
            GrassPusher pusher = target.GetComponent<GrassPusher>();
            bool isNew = pusher == null;

            if (isNew) pusher = target.AddComponent<GrassPusher>();

            pusher.radius = radius;
            pusher.leavesMark = leavesMark;
            pusher.mass = mass;

            EditorUtility.SetDirty(target);

            report.Add("  " + (isNew ? "" : "(갱신) ") + where + ": " + Path(target.transform) +
                       " 반경 " + radius + "m / 자국 " + (leavesMark ? "남김" : "안 남김") +
                       (leavesMark ? " / " + pusher.MarkSeconds.ToString("F0") + "초" : ""));

            return isNew ? 1 : 0;
        }

        /// <summary>오브젝트의 자리를 부모까지 이어 적습니다.</summary>
        /// <param name="target">적을 오브젝트</param>
        /// <returns>"차/바퀴앞왼쪽" 같은 문자열</returns>
        private static string Path(Transform target)
        {
            string path = target.name;

            Transform cursor = target.parent;
            while (cursor != null)
            {
                path = cursor.name + "/" + path;
                cursor = cursor.parent;
            }

            return path;
        }
    }
}
