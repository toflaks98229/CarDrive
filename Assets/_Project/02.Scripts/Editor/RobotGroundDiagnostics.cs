using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 로봇이 <b>왜 지형을 못 찾는지</b>를 눈으로 보여 줍니다.
    ///
    /// <b>왜 필요한가.</b> 다리가 허공을 딛는 증상은 원인이 넷쯤 됩니다.
    /// 레이어 마스크에서 지면이 빠졌거나, 로봇이 지형 범위 밖이거나,
    /// 탐침 거리가 짧거나, <b>로봇이 지면보다 아래에 있거나</b>.
    /// 화면만 봐서는 넷을 구분할 수 없습니다. 넷 다 "발이 뜬다"로 똑같이 보입니다.
    ///
    /// 특히 마지막 것이 함정입니다. 이 월드의 평지는 <b>y≈19.6m</b> 에 있습니다.
    /// (터레인 높이 범위 70m × 기준 높이 0.28) 프리팹을 씬에 끌어다 놓으면 보통 원점 근처에
    /// 떨어지는데, 그 자리는 지면보다 20m 아래입니다. 아래로 쏘는 레이는 사거리를 아무리 늘려도
    /// 위에 있는 표면에 닿지 않고, 터레인 콜라이더는 아래에서 올려다볼 때 잡히지 않습니다.
    /// <b>레이캐스트로는 원리적으로 못 찾는 자리</b>입니다.
    ///
    /// 그래서 이 도구는 레이캐스트와 <b>터레인 높이맵을 따로</b> 물어보고 둘을 나란히 찍습니다.
    /// 레이캐스트만 실패하고 높이맵은 성공한다면, 그 로봇은 지하에 있습니다.
    /// </summary>
    public static class RobotGroundDiagnostics
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Gameplay/보행 로봇 지면 점검")]
        public static void Run()
        {
            List<string> report = new List<string>();

            GroundProbe.Invalidate();

            ReportTerrains(report);

            WalkerRobot[] robots = Object.FindObjectsByType<WalkerRobot>(FindObjectsSortMode.None);

            if (robots.Length == 0)
            {
                report.Add("");
                report.Add("씬에 로봇이 없습니다. 씬뷰가 보고 있는 자리만 확인합니다.");

                SceneView view = SceneView.lastActiveSceneView;
                ReportPoint(report, "  씬뷰 중심", view != null ? view.pivot : Vector3.zero, ~0);
            }

            for (int i = 0; i < robots.Length; i++) ReportRobot(report, robots[i]);

            Debug.Log("RobotGroundDiagnostics:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 점검합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.RobotGroundDiagnostics.RunFromCommandLine</c>
        /// </summary>
        public static void RunFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Run();
        }

        // --- Private Methods ---

        /// <summary>씬에 깔린 터레인이 몇 장이고 어디를 덮는지 적습니다.</summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void ReportTerrains(List<string> report)
        {
            Terrain[] terrains = Terrain.activeTerrains;

            report.Add("터레인 " + terrains.Length + "장");

            if (terrains.Length == 0)
            {
                report.Add("  · 활성 터레인이 없습니다. 지형 타일이 전부 꺼져 있는지 확인하세요.");
                return;
            }

            Bounds bounds = new Bounds(terrains[0].transform.position, Vector3.zero);
            HashSet<int> layers = new HashSet<int>();

            for (int i = 0; i < terrains.Length; i++)
            {
                TerrainData data = terrains[i].terrainData;
                if (data == null) continue;

                Vector3 origin = terrains[i].transform.position;

                bounds.Encapsulate(origin);
                bounds.Encapsulate(origin + data.size);
                layers.Add(terrains[i].gameObject.layer);
            }

            report.Add("  · 덮는 범위 X [" + bounds.min.x.ToString("F0") + " ~ " + bounds.max.x.ToString("F0") + "]" +
                       " Z [" + bounds.min.z.ToString("F0") + " ~ " + bounds.max.z.ToString("F0") + "]");

            foreach (int layer in layers)
            {
                report.Add("  · 레이어 " + layer + " (" + LayerMask.LayerToName(layer) + ")");
            }
        }

        /// <summary>로봇 하나의 루트와 네 발이 땅을 찾는지 적습니다.</summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <param name="robot">확인할 로봇</param>
        private static void ReportRobot(List<string> report, WalkerRobot robot)
        {
            report.Add("");
            RobotPhysicsMotor motor = robot.GetComponent<RobotPhysicsMotor>();

            report.Add("[" + robot.name + "] 다리 " + robot.LegCount + "개 · 루트 " +
                       robot.transform.position.ToString("F2") +
                       (motor != null ? " · 물리 " + (motor.IsSupported ? "지지 중" : "떠 있음") : " · 물리 없음"));
            report.Add("  작업 반경 " + robot.StrideRadius.ToString("F2") + "m · 계획 보폭 " +
                       robot.PlannedStride.ToString("F2") + "m · 최고 속도 " +
                       robot.MaxTravelSpeed.ToString("F2") + "m/s · 보행 " + WalkerGait.DisplayName(robot.ActiveGait));

            ReportMask(report, robot.groundMask);
            ReportPoint(report, "  루트 아래", robot.transform.position, robot.groundMask);

            if (robot.legs == null) return;

            for (int i = 0; i < robot.legs.Length; i++)
            {
                if (robot.legs[i] == null) continue;

                Vector3 home = robot.transform.TransformPoint(robot.legs[i].homeOffset);
                ReportPoint(report, "  " + robot.legs[i].name, home, robot.groundMask);
            }
        }

        /// <summary>레이어 마스크가 지형 레이어를 담고 있는지 적습니다.</summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <param name="mask">확인할 마스크</param>
        private static void ReportMask(List<string> report, LayerMask mask)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains.Length == 0) return;

            int layer = terrains[0].gameObject.layer;
            bool included = (mask.value & (1 << layer)) != 0;

            report.Add("  마스크가 지형 레이어(" + LayerMask.LayerToName(layer) + ")를 " +
                       (included ? "포함합니다." : "빠뜨렸습니다. ← 이것이 원인입니다."));
        }

        /// <summary>
        /// 한 자리에서 레이캐스트와 터레인 높이맵을 <b>따로</b> 물어보고 나란히 적습니다.
        /// 둘의 차이가 곧 진단입니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <param name="label">줄 앞에 붙일 이름</param>
        /// <param name="around">확인할 자리</param>
        /// <param name="mask">땅으로 볼 레이어</param>
        private static void ReportPoint(List<string> report, string label, Vector3 around, LayerMask mask)
        {
            bool rayHit = Physics.Raycast(around + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 8f,
                mask, QueryTriggerInteraction.Ignore);

            bool terrainHit = GroundProbe.SampleTerrain(around, out Vector3 terrainPoint, out Vector3 _);

            string ray = rayHit ? "레이 y=" + hit.point.y.ToString("F2") + " (" + hit.collider.name + ")" : "레이 실패";
            string terrainText = terrainHit ? "지형 y=" + terrainPoint.y.ToString("F2") : "지형 없음";

            string verdict = "";
            if (!rayHit && terrainHit)
            {
                float delta = terrainPoint.y - around.y;
                verdict = delta > 0f
                    ? "  ← 지형보다 " + delta.ToString("F1") + "m 아래에 있습니다. 레이로는 못 찾는 자리입니다."
                    : "  ← 지형보다 " + (-delta).ToString("F1") + "m 위에 있습니다. 탐침 거리가 짧습니다.";
            }

            report.Add(label + ": " + ray + " | " + terrainText + verdict);
        }
    }
}
