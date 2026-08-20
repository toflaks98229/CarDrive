using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;
using CarDrive.Systems;
using CarDrive.UI;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 돈·엑토플라즘 경제에 필요한 것들을 한 번에 만들어 배선합니다.
    ///
    ///  1. 엑토플라즘 덩어리 프리팹을 만듭니다. (구체 + 발광 재질 + 라이트)
    ///  2. 적 프리팹 셋에 그 덩어리를 떨어뜨리도록 연결합니다.
    ///  3. 씬에 <see cref="Wallet"/>을 놓습니다.
    ///  4. HUD 캔버스에 숫자 표시 두 줄을 만들고 <see cref="CurrencyUI"/>에 연결합니다.
    ///
    /// 여러 번 실행해도 안전합니다. 이미 있는 것은 다시 만들지 않고 배선만 확인합니다.
    /// </summary>
    public static class EconomySetup
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>만들어질 덩어리 프리팹 경로입니다.</summary>
        private const string DropPrefabPath = "Assets/_Project/05.Prefabs/Items/EctoplasmDrop.prefab";

        /// <summary>덩어리에 쓸 재질 경로입니다.</summary>
        private const string DropMaterialPath = "Assets/_Project/04.Art/00.Materials/EctoplasmDrop.mat";

        /// <summary>재화 한 줄의 높이입니다. 니즈 게이지 줄 간격과 맞춥니다.</summary>
        private const float RowHeight = 34f;

        /// <summary>니즈 패널과 재화 패널 사이의 간격입니다.</summary>
        private const float PanelGap = 8f;

        /// <summary>덩어리를 둘 레이어 이름입니다. 카메라가 보는 레이어여야 합니다.</summary>
        private const string DropLayer = "Default";

        /// <summary>드롭을 연결할 적 프리팹들입니다.</summary>
        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/_Project/05.Prefabs/Monster/Monster_1.prefab",
            "Assets/_Project/05.Prefabs/Monster/Monster_2.prefab",
            "Assets/_Project/05.Prefabs/Monster/Monster_3.prefab",
        };

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Gameplay/경제 배선 설정 (돈 · 엑토플라즘)")]
        public static void Setup()
        {
            List<string> report = new List<string>();

            GameObject drop = CreateDropPrefab(report);
            WireEnemies(drop, report);
            SetupScene(report);

            AssetDatabase.SaveAssets();

            Debug.Log("EconomySetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 배선한 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.EconomySetup.SetupFromCommandLine</c>
        /// </summary>
        public static void SetupFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Setup();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // --- Private Methods : 프리팹 ---

        /// <summary>
        /// 엑토플라즘 덩어리 프리팹을 만듭니다. 이미 있으면 그대로 씁니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>덩어리 프리팹</returns>
        private static GameObject CreateDropPrefab(List<string> report)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DropPrefabPath);
            if (existing != null)
            {
                report.Add("· 덩어리 프리팹이 이미 있습니다: " + DropPrefabPath);
                return existing;
            }

            EnsureFolder("Assets/_Project/05.Prefabs/Items");
            EnsureFolder("Assets/_Project/04.Art/00.Materials");

            // 구체 하나에 발광 재질과 라이트를 붙입니다. 콜라이더는 쓰지 않습니다.
            // (습득 판정은 거리로 합니다. CurrencyPickup 주석 참고)
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "EctoplasmDrop";
            root.transform.localScale = Vector3.one * 0.35f;

            // 레이어를 명시합니다. 카메라 컬링 마스크에서 빠진 레이어에 두면
            // 오브젝트는 멀쩡히 있는데 화면에만 안 보입니다. 찾기 어려운 증상입니다.
            SetLayerRecursively(root, DropLayer);

            Collider collider = root.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            Material material = CreateDropMaterial(report);
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;

            GameObject lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(root.transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.95f, 0.75f);
            light.intensity = 1.6f;
            light.range = 4f;

            CurrencyPickup pickup = root.AddComponent<CurrencyPickup>();
            pickup.currency = CurrencyType.Ectoplasm;
            pickup.amount = 1;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DropPrefabPath);
            Object.DestroyImmediate(root);

            report.Add("· 덩어리 프리팹을 만들었습니다: " + DropPrefabPath);
            return prefab;
        }

        /// <summary>
        /// 덩어리에 쓸 발광 재질을 만듭니다. URP Lit 의 Emission 을 켭니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        /// <returns>재질</returns>
        private static Material CreateDropMaterial(List<string> report)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(DropMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader);
            Color tint = new Color(0.45f, 0.95f, 0.72f);

            material.SetColor("_BaseColor", tint);
            material.SetColor("_Color", tint);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", tint * 2.2f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            AssetDatabase.CreateAsset(material, DropMaterialPath);
            report.Add("· 발광 재질을 만들었습니다: " + DropMaterialPath);

            return material;
        }

        /// <summary>
        /// 적 프리팹들이 덩어리를 떨어뜨리도록 연결합니다.
        /// </summary>
        /// <param name="drop">떨어뜨릴 덩어리 프리팹</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void WireEnemies(GameObject drop, List<string> report)
        {
            if (drop == null) return;

            CurrencyPickup dropPickup = drop.GetComponent<CurrencyPickup>();
            if (dropPickup == null)
            {
                report.Add("! 덩어리 프리팹에 CurrencyPickup 이 없습니다.");
                return;
            }

            for (int i = 0; i < EnemyPrefabPaths.Length; i++)
            {
                string path = EnemyPrefabPaths[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    report.Add("! 적 프리팹을 찾지 못했습니다: " + path);
                    continue;
                }

                EnemyBase enemy = prefab.GetComponent<EnemyBase>();
                if (enemy == null)
                {
                    report.Add("! EnemyBase 가 없습니다: " + path);
                    continue;
                }

                if (enemy.dropPrefab == dropPickup)
                {
                    report.Add("· 이미 연결되어 있습니다: " + prefab.name);
                    continue;
                }

                enemy.dropPrefab = dropPickup;
                EditorUtility.SetDirty(prefab);

                report.Add("· 드롭을 연결했습니다: " + prefab.name +
                           " (" + enemy.dropCountMin + "~" + enemy.dropCountMax + "개)");
            }
        }

        // --- Private Methods : 씬 ---

        /// <summary>
        /// 씬에 지갑과 재화 HUD 를 놓습니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void SetupScene(List<string> report)
        {
            Wallet wallet = Object.FindAnyObjectByType<Wallet>();
            if (wallet == null)
            {
                // 니즈 시스템 옆에 두면 게임 상태를 소유하는 것들이 한자리에 모입니다.
                NeedsSystem needs = Object.FindAnyObjectByType<NeedsSystem>();
                GameObject host = needs != null ? needs.gameObject : new GameObject("Wallet");

                wallet = host.AddComponent<Wallet>();
                report.Add("· 지갑을 놓았습니다: " + host.name);
            }
            else
            {
                report.Add("· 지갑이 이미 있습니다: " + wallet.gameObject.name);
            }

            SetupCurrencyHud(wallet, report);
        }

        /// <summary>
        /// HUD 캔버스에 숫자 두 줄을 만들고 CurrencyUI 에 연결합니다.
        /// </summary>
        /// <param name="wallet">표시할 지갑</param>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void SetupCurrencyHud(Wallet wallet, List<string> report)
        {
            if (Object.FindAnyObjectByType<CurrencyUI>() != null)
            {
                report.Add("· 재화 HUD 가 이미 있습니다.");
                return;
            }

            // 니즈 게이지 바로 아래에 붙입니다. 그래야 둘이 하나의 목록으로 읽힙니다.
            RectTransform needsPanel = FindNeedsPanel();
            Transform parent = needsPanel != null ? needsPanel.parent : null;

            if (parent == null)
            {
                Canvas canvas = FindHudCanvas();
                if (canvas == null)
                {
                    report.Add("! Canvas 를 찾지 못해 재화 HUD 를 만들지 못했습니다. " +
                               "캔버스를 만든 뒤 다시 실행하세요.");
                    return;
                }
                parent = canvas.transform;
            }

            GameObject panel = new GameObject("CurrencyPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();

            if (needsPanel != null)
            {
                // 니즈 패널과 같은 앵커·같은 x·같은 폭을 쓰고, 높이만큼 아래로 내립니다.
                panelRect.anchorMin = needsPanel.anchorMin;
                panelRect.anchorMax = needsPanel.anchorMax;
                panelRect.pivot = needsPanel.pivot;
                panelRect.sizeDelta = new Vector2(needsPanel.sizeDelta.x, RowHeight * 2f + PanelGap);

                Vector2 below = needsPanel.anchoredPosition;
                below.y -= needsPanel.sizeDelta.y + PanelGap;
                panelRect.anchoredPosition = below;

                // 하이어라키에서도 니즈 바로 다음에 오게 합니다.
                panel.transform.SetSiblingIndex(needsPanel.GetSiblingIndex() + 1);
            }
            else
            {
                // 니즈 패널을 못 찾으면 예전처럼 왼쪽 위에 둡니다.
                panelRect.anchorMin = new Vector2(0f, 1f);
                panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
                panelRect.anchoredPosition = new Vector2(24f, -280f);
                panelRect.sizeDelta = new Vector2(360f, RowHeight * 2f + PanelGap);

                report.Add("! NeedsUI 를 찾지 못해 재화 HUD 를 임시 위치에 두었습니다.");
            }

            CurrencyUI ui = panel.AddComponent<CurrencyUI>();
            ui.wallet = wallet;
            ui.entries = new List<CurrencyUI.CurrencyEntry>
            {
                CreateEntry(panel.transform, CurrencyType.Money, 0f),
                CreateEntry(panel.transform, CurrencyType.Ectoplasm, -RowHeight),
            };

            report.Add("· 재화 HUD 를 니즈 게이지 아래에 만들었습니다: " + parent.name + " / CurrencyPanel");
        }

        /// <summary>
        /// 재화 한 줄을 만듭니다. 이름은 왼쪽, 숫자는 오른쪽 정렬입니다.
        /// </summary>
        /// <param name="parent">붙일 부모</param>
        /// <param name="type">표시할 재화</param>
        /// <param name="y">패널 안에서의 세로 위치</param>
        /// <returns>완성된 UI 묶음</returns>
        private static CurrencyUI.CurrencyEntry CreateEntry(Transform parent, CurrencyType type, float y)
        {
            GameObject row = new GameObject(type + "Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, y);
            rowRect.sizeDelta = new Vector2(0f, RowHeight);

            TextMeshProUGUI label = CreateText(row.transform, "Label", TextAlignmentOptions.MidlineLeft);
            TextMeshProUGUI value = CreateText(row.transform, "Value", TextAlignmentOptions.MidlineRight);

            return new CurrencyUI.CurrencyEntry
            {
                type = type,
                labelText = label,
                valueText = value,
            };
        }

        /// <summary>
        /// 한 줄 안의 텍스트를 만듭니다.
        /// </summary>
        /// <param name="parent">붙일 부모</param>
        /// <param name="name">오브젝트 이름</param>
        /// <param name="alignment">정렬</param>
        /// <returns>만들어진 텍스트</returns>
        private static TextMeshProUGUI CreateText(Transform parent, string name, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

            // 폰트를 지정하지 않으면 TMP 기본값에 기대게 되는데,
            // 그 기본값이 비어 있으면 글자가 <b>아무것도 그려지지 않습니다.</b>
            if (text.font == null && TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.fontSize = 26f;
            text.alignment = alignment;
            text.text = name == "Value" ? "0" : "";
            text.textWrappingMode = TextWrappingModes.NoWrap;

            return text;
        }

        /// <summary>
        /// 니즈 게이지 패널을 찾습니다. 재화를 그 아래에 붙이기 위한 기준입니다.
        /// </summary>
        /// <returns>NeedsUI 가 붙은 RectTransform. 없으면 null입니다.</returns>
        private static RectTransform FindNeedsPanel()
        {
            NeedsUI needs = Object.FindAnyObjectByType<NeedsUI>(FindObjectsInactive.Include);
            return needs != null ? needs.GetComponent<RectTransform>() : null;
        }

        /// <summary>
        /// 재화를 붙일 캔버스를 고릅니다. HUD 라는 이름이 들어간 것을 우선합니다.
        /// </summary>
        /// <returns>찾은 캔버스. 씬에 하나도 없으면 null입니다.</returns>
        private static Canvas FindHudCanvas()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            if (canvases.Length == 0) return null;

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name.ToUpperInvariant().Contains("HUD")) return canvases[i];
            }

            return canvases[0];
        }

        /// <summary>
        /// 오브젝트와 모든 자식의 레이어를 바꿉니다.
        /// </summary>
        /// <param name="target">바꿀 오브젝트</param>
        /// <param name="layerName">레이어 이름. 없는 이름이면 아무것도 하지 않습니다.</param>
        private static void SetLayerRecursively(GameObject target, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                Debug.LogWarning("EconomySetup: '" + layerName + "' 레이어가 없습니다. 레이어를 그대로 둡니다.");
                return;
            }

            Transform[] all = target.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = layer;
        }

        /// <summary>
        /// 폴더가 없으면 만듭니다. (중간 폴더까지 차례로 만듭니다)
        /// </summary>
        /// <param name="path">"Assets/A/B" 형태의 폴더 경로</param>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
