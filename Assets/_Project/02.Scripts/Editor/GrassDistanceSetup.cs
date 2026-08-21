using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 풀 그리는 거리와 밀도만 지형에 적용합니다. <b>풀을 다시 심지 않습니다.</b>
    ///
    /// <b>왜 따로 필요한가.</b> <see cref="CarDriveWorldSettings.detailDistance"/> 는
    /// 게임 중에 읽히는 값이 아닙니다. <see cref="TerrainDressing"/> 이 에디터에서
    /// <c>Terrain.detailObjectDistance</c> 에 <b>구워 넣습니다.</b>
    /// 그래서 에셋의 숫자만 바꾸면 <b>아무 일도 일어나지 않습니다.</b>
    ///
    /// 그런데 그것을 적용하는 유일한 길인 "지면 단장 다시 입히기"는 103장에 풀을
    /// 처음부터 다시 심습니다. 배치가 결정적 해시라 모양이 달라지지는 않지만,
    /// 숫자 하나 바꿀 때마다 치르기에는 비쌉니다.
    ///
    /// 이 도구는 거리와 밀도 <b>둘만</b> 대입합니다. 심은 풀은 그대로 둡니다.
    /// 값을 조금씩 바꿔 가며 눈으로 맞출 때 쓰세요.
    ///
    /// <b>풀만 줄어듭니다.</b> 나무는 <c>Terrain.treeDistance</c> 가 따로 관리하므로
    /// 이 값을 낮춰도 나무는 그대로 섭니다.
    /// </summary>
    public static class GrassDistanceSetup
    {
        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/World/풀 그리는 거리만 적용")]
        public static void Apply()
        {
            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;

            // 꺼져 있는 타일도 담습니다. WorldStreamer 가 멀어진 타일을 꺼 두는데,
            // 그것만 옛 거리를 그대로 안고 있으면 다가갔을 때 혼자 다르게 보입니다.
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            if (terrains.Length == 0)
            {
                Debug.LogWarning("GrassDistanceSetup: 씬에 터레인이 없습니다. " +
                                 "월드 씬을 열고 다시 실행하세요.");
                return;
            }

            float beforeMin = float.MaxValue;
            float beforeMax = float.MinValue;
            int changed = 0;

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null) continue;

                beforeMin = Mathf.Min(beforeMin, terrain.detailObjectDistance);
                beforeMax = Mathf.Max(beforeMax, terrain.detailObjectDistance);

                bool differs = !Mathf.Approximately(terrain.detailObjectDistance, settings.detailDistance)
                               || !Mathf.Approximately(terrain.detailObjectDensity, settings.detailDensity);
                if (!differs) continue;

                Undo.RecordObject(terrain, "풀 그리는 거리");

                terrain.detailObjectDistance = settings.detailDistance;
                terrain.detailObjectDensity = settings.detailDensity;

                EditorUtility.SetDirty(terrain);
                changed++;
            }

            AssetDatabase.SaveAssets();

            List<string> report = new List<string>();
            report.Add("터레인 " + terrains.Length + "장 중 " + changed + "장을 고쳤습니다.");
            report.Add("· 그리는 거리 " + (Mathf.Approximately(beforeMin, beforeMax)
                           ? beforeMin.ToString("F0")
                           : beforeMin.ToString("F0") + "~" + beforeMax.ToString("F0"))
                       + "m  →  " + settings.detailDistance.ToString("F0") + "m");
            report.Add("· 밀도 배율 " + settings.detailDensity.ToString("F2"));
            report.Add("");
            report.Add("풀만 줄어듭니다. 나무는 Terrain.treeDistance 가 따로 관리합니다.");
            report.Add("심은 풀은 건드리지 않았습니다. 다시 심으려면 '지면 단장 다시 입히기' 를 쓰세요.");

            Debug.Log("GrassDistanceSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }
    }
}
