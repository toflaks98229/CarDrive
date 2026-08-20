using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using CarDrive.Gameplay;
using CarDrive.Systems;
using CarDrive.UI;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// "엑토플라즘이 안 보인다", "재화 숫자가 안 뜬다" 같은 증상의 원인을 짚어 줍니다.
    ///
    /// 이런 문제는 원인이 여러 겹입니다. 프리팹이 없어서일 수도, 배선이 빠져서일 수도,
    /// <b>레이어가 카메라 컬링 마스크에서 빠져서</b>일 수도 있습니다. 마지막 경우가 특히 고약합니다.
    /// 오브젝트는 하이어라키에 멀쩡히 있고 로그도 정상인데 화면에만 없기 때문입니다.
    ///
    /// 그래서 눈으로 찾지 말고 <b>순서대로 확인</b>합니다. 각 항목은 무엇이 잘못됐는지와
    /// 무엇을 하면 되는지를 함께 알려 줍니다.
    /// </summary>
    public static class EconomyDiagnostics
    {
        /// <summary>덩어리 프리팹 경로입니다.</summary>
        private const string DropPrefabPath = "Assets/_Project/05.Prefabs/Items/EctoplasmDrop.prefab";

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Gameplay/경제 배선 점검")]
        public static void Run()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("경제 배선 점검");
            report.AppendLine("──────────────────────────────");

            GameObject drop = CheckDropPrefab(report);
            CheckEnemyWiring(drop, report);
            CheckWallet(report);
            CheckCurrencyUI(report);
            CheckCameraLayers(drop, report);

            Debug.Log(report.ToString());
        }

        // --- Private Methods ---

        /// <summary>덩어리 프리팹이 있는지, 쓸 만한 상태인지 확인합니다.</summary>
        /// <param name="report">결과를 적을 곳</param>
        /// <returns>찾은 프리팹. 없으면 null입니다.</returns>
        private static GameObject CheckDropPrefab(StringBuilder report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DropPrefabPath);

            if (prefab == null)
            {
                report.AppendLine("✗ 엑토플라즘 프리팹이 없습니다.");
                report.AppendLine("   → CarDrive > Gameplay > 경제 배선 설정 을 먼저 실행하세요.");
                return null;
            }

            report.AppendLine("✓ 엑토플라즘 프리팹: " + DropPrefabPath);
            report.AppendLine("   레이어: " + LayerMask.LayerToName(prefab.layer) + " (" + prefab.layer + ")");

            if (prefab.GetComponent<CurrencyPickup>() == null)
            {
                report.AppendLine("   ✗ CurrencyPickup 이 없어 습득되지 않습니다.");
            }

            Renderer renderer = prefab.GetComponentInChildren<Renderer>(true);
            if (renderer == null) report.AppendLine("   ✗ Renderer 가 없어 화면에 그려지지 않습니다.");
            else if (renderer.sharedMaterial == null) report.AppendLine("   ✗ 재질이 비어 있어 분홍색으로 보입니다.");

            return prefab;
        }

        /// <summary>적 프리팹이 덩어리를 떨어뜨리도록 연결되어 있는지 확인합니다.</summary>
        /// <param name="drop">확인할 덩어리 프리팹</param>
        /// <param name="report">결과를 적을 곳</param>
        private static void CheckEnemyWiring(GameObject drop, StringBuilder report)
        {
            string[] paths =
            {
                "Assets/_Project/05.Prefabs/Monster/Monster_1.prefab",
                "Assets/_Project/05.Prefabs/Monster/Monster_2.prefab",
                "Assets/_Project/05.Prefabs/Monster/Monster_3.prefab",
            };

            List<string> unwired = new List<string>();

            for (int i = 0; i < paths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefab == null) continue;

                EnemyBase enemy = prefab.GetComponent<EnemyBase>();
                if (enemy == null || enemy.dropPrefab == null) unwired.Add(prefab.name);
            }

            if (unwired.Count == 0)
            {
                report.AppendLine("✓ 적 " + paths.Length + "종이 모두 덩어리를 떨어뜨립니다.");
                return;
            }

            report.AppendLine("✗ 드롭이 연결되지 않은 적: " + string.Join(", ", unwired));
            report.AppendLine("   → 쓰러뜨려도 아무것도 떨어지지 않습니다. 경제 배선 설정 을 실행하세요.");
        }

        /// <summary>씬에 지갑이 있는지 확인합니다.</summary>
        /// <param name="report">결과를 적을 곳</param>
        private static void CheckWallet(StringBuilder report)
        {
            Wallet wallet = Object.FindAnyObjectByType<Wallet>();

            if (wallet == null)
            {
                report.AppendLine("✗ 씬에 Wallet 이 없습니다.");
                report.AppendLine("   → 주워도 숫자가 오르지 않습니다. 경제 배선 설정 을 실행하세요.");
                return;
            }

            report.AppendLine("✓ Wallet: " + wallet.gameObject.name);
        }

        /// <summary>재화 HUD 가 실제로 보일 상태인지 확인합니다.</summary>
        /// <param name="report">결과를 적을 곳</param>
        private static void CheckCurrencyUI(StringBuilder report)
        {
            CurrencyUI ui = Object.FindAnyObjectByType<CurrencyUI>(FindObjectsInactive.Include);

            if (ui == null)
            {
                report.AppendLine("✗ 씬에 CurrencyUI 가 없습니다.");
                report.AppendLine("   → 재화 숫자가 화면에 뜨지 않습니다. 경제 배선 설정 을 실행하세요.");
                return;
            }

            report.AppendLine("✓ CurrencyUI: " + ui.gameObject.name + " (표시 항목 " + ui.entries.Count + "개)");

            if (!ui.gameObject.activeInHierarchy)
            {
                report.AppendLine("   ✗ 오브젝트가 꺼져 있습니다.");
            }

            Canvas canvas = ui.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                report.AppendLine("   ✗ Canvas 밖에 있어 그려지지 않습니다.");
            }
            else if (!canvas.isActiveAndEnabled)
            {
                report.AppendLine("   ✗ 부모 Canvas(" + canvas.name + ")가 꺼져 있습니다.");
            }

            // 폰트가 없으면 글자가 통째로 안 보입니다. 가장 헷갈리는 증상입니다.
            for (int i = 0; i < ui.entries.Count; i++)
            {
                TextMeshProUGUI value = ui.entries[i].valueText;

                if (value == null)
                {
                    report.AppendLine("   ✗ " + ui.entries[i].type + " 의 숫자 텍스트가 비어 있습니다.");
                    continue;
                }
                if (value.font == null)
                {
                    report.AppendLine("   ✗ " + ui.entries[i].type + " 의 텍스트에 폰트가 없어 글자가 그려지지 않습니다.");
                }
            }
        }

        /// <summary>
        /// 덩어리의 레이어가 카메라에 실제로 보이는지 확인합니다.
        ///
        /// <b>이 항목이 이 도구의 핵심입니다.</b> 나머지는 하이어라키를 보면 알 수 있지만,
        /// 컬링 마스크는 눈으로 확인하기 어렵고 증상이 "그냥 안 보임"이라 오해하기 쉽습니다.
        /// </summary>
        /// <param name="drop">확인할 덩어리 프리팹</param>
        /// <param name="report">결과를 적을 곳</param>
        private static void CheckCameraLayers(GameObject drop, StringBuilder report)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);

            if (cameras.Length == 0)
            {
                report.AppendLine("✗ 씬에 카메라가 없습니다.");
                return;
            }

            report.AppendLine("카메라 컬링 마스크");

            int dropLayer = drop != null ? drop.layer : 0;
            bool anyCanSee = false;

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                bool sees = (camera.cullingMask & (1 << dropLayer)) != 0;
                if (sees && camera.targetTexture == null) anyCanSee = true;

                report.AppendLine("   " + (sees ? "보임" : "가림") + "  " + camera.name +
                                  "  (mask=" + camera.cullingMask +
                                  (camera.targetTexture != null ? ", 렌더 텍스처" : "") + ")");
            }

            report.AppendLine("   → 덩어리 레이어: " + LayerMask.LayerToName(dropLayer) + " (" + dropLayer + ")");

            if (anyCanSee)
            {
                report.AppendLine("   ✓ 화면에 나오는 카메라 중 이 레이어를 보는 카메라가 있습니다.");
            }
            else
            {
                report.AppendLine("   ✗ 어떤 카메라도 이 레이어를 보지 않습니다. 덩어리가 있어도 화면에 안 보입니다.");
                report.AppendLine("      → 카메라의 Culling Mask 에 이 레이어를 추가하거나, 프리팹 레이어를 바꾸세요.");
            }
        }
    }
}
