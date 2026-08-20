using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using CarDrive.Common;
using CarDrive.Gameplay;
using CarDrive.Systems;
using CarDrive.UI;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// Feel 레시피를 실제로 만들어 배선합니다.
    ///
    /// 설계서(Feel 도입 설계서)의 "바로 쓸 수 있는 레시피"를 코드로 옮긴 것입니다.
    /// 손으로 만들면 프리팹 세 개 × 피드백 여러 개를 일일이 클릭해야 하고,
    /// 무엇보다 <b>다시 만들 수 없습니다.</b> 도구로 두면 수치를 바꿔 가며 다시 돌릴 수 있습니다.
    ///
    /// 만드는 것:
    ///  1. 적 — 피격 점멸(MMF_Flicker), 사망(카메라 흔들림 + 프리즈 프레임 + 라이트 + 크기)
    ///  2. 엑토플라즘 — 끌려올 때 부풀기와 빛나기
    ///  3. 재화 HUD — 획득 펀치, 잔액 부족 흔들림
    ///  4. 니즈 게이지 — Feel 의 MMProgressBar 로 부드러운 추종 + 지연 바 + 변화 시 팽창
    ///
    /// <b>마시기 UI와 앙크는 만들지 않습니다.</b> 그 둘은 <see cref="UI.DrinkAnimation"/> 과
    /// <see cref="UI.AnkhAnimation"/> 이 이미 <b>배선 없이 동작하는</b> 슬라이드를 갖고 있습니다.
    /// 여기서 피드백을 만들어 붙이면 같은 anchoredPosition 을 두고 서로 밀어냅니다.
    /// Feel 로 바꾸고 싶다면 인스펙터에서 MMF_Player 를 직접 만들어
    /// BeverageConsumer.drinkFeedback / PlayerAttacker.showFeedback 에 연결하세요.
    /// 연결하는 순간 내장 애니메이션은 스스로 물러납니다.
    ///
    /// 적 피격도 같은 원리입니다. 이 도구는 MMF_Flicker 를 붙이면서
    /// EnemyBase.useBuiltInFlicker 를 꺼 둡니다. 둘이 겹치지 않게 하기 위함입니다.
    ///
    /// 여러 번 실행해도 안전합니다. 이미 만들어진 것은 지우고 다시 만듭니다.
    /// </summary>
    public static class FeelSetup
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>적 프리팹 경로입니다.</summary>
        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/_Project/05.Prefabs/Monster/Monster_1.prefab",
            "Assets/_Project/05.Prefabs/Monster/Monster_2.prefab",
            "Assets/_Project/05.Prefabs/Monster/Monster_3.prefab",
        };

        /// <summary>엑토플라즘 덩어리 프리팹 경로입니다.</summary>
        private const string DropPrefabPath = "Assets/_Project/05.Prefabs/Items/EctoplasmDrop.prefab";

        /// <summary>지연 바로 만들 오브젝트의 이름입니다.</summary>
        private const string DelayedBarName = "DelayedBar";

        /// <summary>이 도구가 만드는 피드백 오브젝트의 이름 접두사입니다.</summary>
        private const string FeedbackPrefix = "Feedback_";

        /// <summary>URP Lit 재질에서 색을 담고 있는 속성 이름입니다.</summary>
        private const string UrpColorProperty = "_BaseColor";

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Feel/레시피 만들기 (적 · 엑토플라즘 · 재화 · 니즈 게이지)")]
        public static void Setup()
        {
            List<string> report = new List<string>();

            CleanStrayFeedbacks(report);
            SetupEnemies(report);
            SetupDrop(report);
            SetupCurrencyHud(report);
            SetupNeedsBars(report);

            AssetDatabase.SaveAssets();

            Debug.Log("FeelSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 배선한 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.FeelSetup.SetupFromCommandLine</c>
        /// </summary>
        public static void SetupFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Setup();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // --- Private Methods : 레시피 1 — 적 ---

        /// <summary>
        /// 적 프리팹에 피격·사망 피드백을 만들고 이벤트에 연결합니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void SetupEnemies(List<string> report)
        {
            for (int i = 0; i < EnemyPrefabPaths.Length; i++)
            {
                string path = EnemyPrefabPaths[i];

                // 프리팹 애셋에는 자식을 붙일 수 없습니다. LoadAssetAtPath 로 얻은 것에
                // SetParent 를 하면 새 오브젝트가 <b>열려 있는 씬</b>에 남습니다.
                // 반드시 편집용 사본을 열고, 고치고, 저장해야 합니다.
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                if (prefab == null)
                {
                    report.Add("! 적 프리팹을 열지 못했습니다: " + path);
                    continue;
                }

                EnemyBase enemy = prefab.GetComponent<EnemyBase>();
                if (enemy == null)
                {
                    report.Add("! EnemyBase 가 없습니다: " + prefab.name);
                    PrefabUtility.UnloadPrefabContents(prefab);
                    continue;
                }

                // --- 피격: 재질을 한 번 번쩍이게 합니다 ---
                MMF_Player damage = RebuildPlayer(prefab, "Feedback_Damage");

                MMF_Flicker flicker = (MMF_Flicker)damage.AddFeedback(typeof(MMF_Flicker));
                flicker.Label = "피격 점멸";
                flicker.BoundRenderer = enemy.visualRenderer;
                flicker.PropertyName = UrpColorProperty;
                flicker.FlickerDuration = 0.18f;
                flicker.FlickerPeriod = 0.03f;
                flicker.FlickerColor = new Color(1f, 0.95f, 0.9f);

                // --- 사망: 이 게임에서 가장 중요한 손맛 ---
                MMF_Player death = RebuildPlayer(prefab, "Feedback_Death");

                MMF_FreezeFrame freeze = (MMF_FreezeFrame)death.AddFeedback(typeof(MMF_FreezeFrame));
                freeze.Label = "프리즈 프레임";
                // 0.05초. 이보다 길면 주행 중에 걸리적거립니다.
                freeze.FreezeFrameDuration = 0.05f;

                MMF_CameraShake shake = (MMF_CameraShake)death.AddFeedback(typeof(MMF_CameraShake));
                shake.Label = "카메라 흔들림";
                // 짧고 약하게. 1인칭 주행이라 과하면 바로 멀미가 납니다.
                shake.CameraShakeProperties = new MMCameraShakeProperties(0.15f, 0.12f, 30f);

                if (enemy.flickerLight != null)
                {
                    MMF_Light light = (MMF_Light)death.AddFeedback(typeof(MMF_Light));
                    light.Label = "라이트 점화";
                    light.BoundLight = enemy.flickerLight;
                    light.Duration = 0.25f;
                    light.ModifyIntensity = true;
                    light.ModifyColor = false;
                }

                MMF_Scale scale = (MMF_Scale)death.AddFeedback(typeof(MMF_Scale));
                scale.Label = "부풀었다 수축";
                scale.AnimateScaleTarget = prefab.transform;
                ConfigurePunch(scale, 1.15f, 0.22f);

                damage.ComputeCachedTotalDuration();
                death.ComputeCachedTotalDuration();

                // --- 이벤트 연결 ---
                ClearPersistent(enemy.onDamaged);
                ClearPersistent(enemy.onDied);
                UnityEventTools.AddVoidPersistentListener(enemy.onDamaged, damage.PlayFeedbacks);

                // 내장 점멸과 겹치지 않게 끕니다. 도구를 되돌리려면 이 체크를 다시 켜세요.
                enemy.useBuiltInFlicker = false;
                UnityEventTools.AddVoidPersistentListener(enemy.onDied, death.PlayFeedbacks);

                // 풀로 돌아갈 때 연출을 끊어 줍니다. 없으면 빨갛게 물든 채 다시 나타납니다.
                if (prefab.GetComponent<FeedbackPoolGuard>() == null)
                {
                    prefab.AddComponent<FeedbackPoolGuard>();
                }

                report.Add("· 적 피드백 완료: " + prefab.name + " (피격 1 · 사망 " + death.FeedbacksList.Count + ")");

                PrefabUtility.SaveAsPrefabAsset(prefab, path);
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        // --- Private Methods : 레시피 2 — 엑토플라즘 ---

        /// <summary>
        /// 덩어리가 끌려올 때의 연출을 만듭니다.
        ///
        /// <b>습득 순간에는 덩어리 자신에게 연출을 걸지 않습니다.</b>
        /// <see cref="CurrencyPickup"/> 은 습득 직후 곧바로 풀로 돌아가므로,
        /// 자기 몸에 건 연출은 시작하자마자 잘립니다. 습득의 손맛은 HUD 쪽(레시피 3)이 냅니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void SetupDrop(List<string> report)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DropPrefabPath) == null)
            {
                report.Add("! 덩어리 프리팹이 없습니다. 먼저 CarDrive/Gameplay/경제 배선 설정 을 실행하세요.");
                return;
            }

            // 적과 같은 이유로 편집용 사본을 엽니다.
            GameObject prefab = PrefabUtility.LoadPrefabContents(DropPrefabPath);

            CurrencyPickup pickup = prefab.GetComponent<CurrencyPickup>();
            if (pickup == null)
            {
                report.Add("! CurrencyPickup 이 없습니다: " + prefab.name);
                PrefabUtility.UnloadPrefabContents(prefab);
                return;
            }

            MMF_Player magnet = RebuildPlayer(prefab, "Feedback_Magnet");

            MMF_Scale grow = (MMF_Scale)magnet.AddFeedback(typeof(MMF_Scale));
            grow.Label = "부풀기";
            grow.AnimateScaleTarget = prefab.transform;
            ConfigurePunch(grow, 1.12f, 0.25f);

            Light glow = prefab.GetComponentInChildren<Light>(true);
            if (glow != null)
            {
                MMF_Light light = (MMF_Light)magnet.AddFeedback(typeof(MMF_Light));
                light.Label = "빛나기";
                light.BoundLight = glow;
                light.Duration = 0.3f;
                light.ModifyIntensity = true;
                light.ModifyColor = false;
            }

            magnet.ComputeCachedTotalDuration();

            ClearPersistent(pickup.onMagnetStarted);
            UnityEventTools.AddVoidPersistentListener(pickup.onMagnetStarted, magnet.PlayFeedbacks);

            if (prefab.GetComponent<FeedbackPoolGuard>() == null) prefab.AddComponent<FeedbackPoolGuard>();

            report.Add("· 덩어리 피드백 완료: 끌려올 때 " + magnet.FeedbacksList.Count + "개");

            PrefabUtility.SaveAsPrefabAsset(prefab, DropPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefab);
        }



        // --- Private Methods : 레시피 3 — 재화 HUD ---


        /// <summary>
        /// 재화 획득 펀치와 잔액 부족 흔들림을 만듭니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void SetupCurrencyHud(List<string> report)
        {
            CurrencyUI ui = Object.FindAnyObjectByType<CurrencyUI>();
            Wallet wallet = Object.FindAnyObjectByType<Wallet>();

            if (ui == null || wallet == null)
            {
                report.Add("! 재화 HUD 또는 지갑이 없습니다. 먼저 CarDrive/Gameplay/경제 배선 설정 을 실행하세요.");
                return;
            }

            MMF_Player gain = RebuildPlayer(ui.gameObject, "Feedback_Gain");
            MMF_Player denied = RebuildPlayer(ui.gameObject, "Feedback_Denied");

            for (int i = 0; i < ui.entries.Count; i++)
            {
                if (ui.entries[i].valueText == null) continue;

                MMF_Scale punch = (MMF_Scale)gain.AddFeedback(typeof(MMF_Scale));
                punch.Label = ui.entries[i].type + " 펀치";
                punch.AnimateScaleTarget = ui.entries[i].valueText.transform;

                // 숫자는 화면에 늘 떠 있으므로 특히 절제해야 합니다. 10% 만 커졌다 돌아옵니다.
                ConfigurePunch(punch, 1.1f, 0.2f);
            }

            MMF_Position shake = (MMF_Position)denied.AddFeedback(typeof(MMF_Position));
            shake.Label = "거절 흔들림";
            shake.AnimatePositionTarget = ui.gameObject;
            shake.Space = MMF_Position.Spaces.RectTransform;
            shake.Mode = MMF_Position.Modes.AtoB;
            shake.RelativePosition = true;
            shake.InitialPosition = new Vector3(-12f, 0f, 0f);
            shake.DestinationPosition = Vector3.zero;
            shake.AnimatePositionDuration = 0.25f;

            gain.ComputeCachedTotalDuration();
            denied.ComputeCachedTotalDuration();

            ClearPersistent(wallet.onGained);
            ClearPersistent(wallet.onInsufficientFunds);
            UnityEventTools.AddVoidPersistentListener(wallet.onGained, gain.PlayFeedbacks);
            UnityEventTools.AddVoidPersistentListener(wallet.onInsufficientFunds, denied.PlayFeedbacks);

            EditorUtility.SetDirty(wallet);
            report.Add("· 재화 HUD 피드백 완료: 획득 " + gain.FeedbacksList.Count + " · 거절 1");
        }



        // --- Private Methods : 레시피 4 — 니즈 게이지 ---

        /// <summary>
        /// 니즈 게이지를 Feel 의 <see cref="MMProgressBar"/> 로 바꿉니다.
        ///
        /// Feel 데모의 <c>FeelMMProgressBar</c> 씬이 보여 주는 것이 이것입니다.
        /// 값이 바뀌면 앞의 바가 부드럽게 따라가고, 뒤의 <b>지연 바</b>가 늦게 쫓아오면서
        /// "얼마나 줄었는지"가 눈에 남습니다. 체력·게이지 연출의 표준입니다.
        ///
        /// <b>두 가지를 꺼 둡니다.</b>
        ///  - <c>BumpScaleOnChange</c>: 니즈는 시간에 따라 <b>매 프레임 조금씩</b> 오릅니다.
        ///    켜 두면 게이지가 쉬지 않고 떨립니다. 대신 NeedsUI 가 상호작용으로 값이
        ///    눈에 띄게 바뀐 순간에만 <c>Bump()</c> 를 부릅니다.
        ///  - <c>ChangeColorWhenBumping</c>: 색은 NeedsUI 가 경고·한계에 따라 칠합니다.
        ///    둘 다 켜면 같은 색을 두고 서로 밀어냅니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void SetupNeedsBars(List<string> report)
        {
            NeedsUI ui = Object.FindAnyObjectByType<NeedsUI>(FindObjectsInactive.Include);
            if (ui == null)
            {
                report.Add("! 씬에서 NeedsUI 를 찾지 못해 니즈 게이지를 건너뜁니다.");
                return;
            }

            int done = 0;

            for (int i = 0; i < ui.bars.Count; i++)
            {
                NeedsUI.NeedBar bar = ui.bars[i];
                if (bar.fillImage == null) continue;

                GameObject host = bar.fillImage.gameObject;

                // 지연 바를 먼저 만듭니다. MMProgressBar 를 붙인 뒤에 복제하면
                // 복제본까지 게이지가 되어 서로를 갱신하려 듭니다.
                Image delayed = EnsureDelayedBar(bar.fillImage);

                MMProgressBar progress = host.GetComponent<MMProgressBar>();
                if (progress == null) progress = host.AddComponent<MMProgressBar>();

                progress.ForegroundBar = host.transform;
                progress.FillMode = MMProgressBar.FillModes.FillAmount;
                progress.DelayedBarDecreasing = delayed != null ? delayed.transform : null;

                progress.LerpForegroundBar = true;
                progress.LerpForegroundBarSpeedIncreasing = 15f;
                progress.LerpForegroundBarSpeedDecreasing = 8f;

                progress.BumpScaleOnChange = false;
                progress.BumpOnIncrease = false;
                progress.BumpOnDecrease = false;
                progress.ChangeColorWhenBumping = false;
                progress.BumpDuration = 0.18f;

                bar.progressBar = progress;
                done++;
            }

            EditorUtility.SetDirty(ui);
            report.Add("· 니즈 게이지 " + done + "개를 MMProgressBar 로 바꿨습니다. " +
                       "(상호작용으로 값이 바뀌면 팽창합니다)");
        }

        /// <summary>
        /// 채움 바 뒤에 따라오는 지연 바를 만듭니다. 이미 있으면 그대로 씁니다.
        /// </summary>
        /// <param name="fill">기준이 되는 채움 이미지</param>
        /// <returns>지연 바 이미지. 만들 수 없으면 null입니다.</returns>
        private static Image EnsureDelayedBar(Image fill)
        {
            Transform parent = fill.transform.parent;
            if (parent == null) return null;

            Transform existing = parent.Find(DelayedBarName);
            if (existing != null) return existing.GetComponent<Image>();

            GameObject copy = Object.Instantiate(fill.gameObject, parent);
            copy.name = DelayedBarName;

            // 복제본에 딸려 온 것들을 정리합니다. 게이지도 자식도 필요 없습니다.
            MMProgressBar stray = copy.GetComponent<MMProgressBar>();
            if (stray != null) Object.DestroyImmediate(stray);

            // 채움 바 <b>뒤에</b> 그려져야 하므로 형제 순서를 앞으로 보냅니다.
            copy.transform.SetSiblingIndex(fill.transform.GetSiblingIndex());

            Image image = copy.GetComponent<Image>();
            if (image != null)
            {
                // 줄어든 만큼이 잠깐 남아 보이도록 흐린 색으로 둡니다.
                image.color = new Color(1f, 0.35f, 0.3f, 0.55f);
                image.raycastTarget = false;
            }

            return image;
        }



        /// <summary>
        /// 크기 피드백을 "살짝 튕겼다 제자리로" 돌아오는 펀치로 맞춥니다.
        ///
        /// <b>기본값을 그대로 두면 안 됩니다.</b> MMF_Scale 의 계산은 이렇습니다.
        /// <code>Remap(LerpUnclamped(초기크기, DestinationScale, 커브), 0, 1, RemapCurveZero, RemapCurveOne)</code>
        /// 커브가 0인 <b>평소 상태</b>가 <c>RemapCurveOne</c> 으로 매핑되는데, 그 기본값이 <b>2</b> 입니다.
        /// 즉 아무 설정 없이 재생하면 대상이 <b>두 배로 부풀어 오른 채</b> 시작합니다.
        /// 실제로 돈·엑토플라즘 숫자가 과하게 튀어 오른 원인이 이것이었습니다.
        ///
        /// 그래서 다음처럼 잡습니다.
        ///  - <c>DestinationScale</c> 을 0 으로 두고 커브 정점을 1 로 만들면
        ///    <c>RemapCurveZero</c> 가 <b>정점 크기</b>, <c>RemapCurveOne</c> 이 <b>평소 크기</b>가 됩니다.
        ///  - <c>UniformScaling</c> 을 켜서 X 커브를 세 축에 함께 씁니다.
        ///    끄면 축마다 따로 놀아 한쪽으로만 늘어납니다.
        /// </summary>
        /// <param name="scale">설정할 피드백</param>
        /// <param name="peak">정점에서의 크기 배율. 1.1 이면 10% 커졌다 돌아옵니다.</param>
        /// <param name="duration">한 번 튕기는 데 걸리는 시간(초)</param>
        private static void ConfigurePunch(MMF_Scale scale, float peak, float duration)
        {
            scale.Mode = MMF_Scale.Modes.Absolute;
            scale.AnimateScaleDuration = duration;

            scale.DestinationScale = Vector3.zero;
            scale.RemapCurveZero = peak;
            scale.RemapCurveOne = 1f;

            AnimationCurve punch = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 0f));

            scale.AnimateScaleTweenX = new MMTweenType(punch);
            scale.AnimateScaleTweenY = new MMTweenType(punch);
            scale.AnimateScaleTweenZ = new MMTweenType(punch);

            scale.AnimateX = true;
            scale.AnimateY = true;
            scale.AnimateZ = true;
            scale.UniformScaling = true;
        }

        // --- Private Methods : 정리 ---

        /// <summary>
        /// 씬 루트에 떠도는 피드백 오브젝트를 지웁니다.
        ///
        /// <b>왜 필요한가.</b> 예전 판은 프리팹 애셋에 자식을 붙이려 했습니다.
        /// Unity 는 그것을 허용하지 않고, 만들어진 오브젝트를 <b>열려 있는 씬의 루트</b>에
        /// 남겨 둡니다. 프리팹에는 아무것도 들어가지 않은 채 씬만 지저분해집니다.
        /// 그 흔적을 여기서 걷어냅니다. 정상적으로 만들어진 것(부모가 있는 것)은 건드리지 않습니다.
        /// </summary>
        /// <param name="report">진행 내용을 적을 목록</param>
        private static void CleanStrayFeedbacks(List<string> report)
        {
            MMF_Player[] all = Object.FindObjectsByType<MMF_Player>(FindObjectsInactive.Include);
            int removed = 0;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;

                // 루트에 있고 이름이 우리 규칙을 따르는 것만 지웁니다.
                if (all[i].transform.parent != null) continue;
                if (!all[i].name.StartsWith(FeedbackPrefix)) continue;

                Object.DestroyImmediate(all[i].gameObject);
                removed++;
            }

            if (removed > 0)
            {
                report.Add("· 씬 루트에 남아 있던 피드백 " + removed + "개를 정리했습니다. " +
                           "(프리팹에 들어가지 못하고 떨어져 나온 것들입니다)");
            }
        }

        // --- Private Methods : 공통 ---

        /// <summary>
        /// 이름이 같은 자식을 지우고 새로 만듭니다.
        /// 여러 번 실행해도 피드백이 쌓이지 않게 하기 위함입니다.
        /// </summary>
        /// <param name="host">피드백을 붙일 오브젝트</param>
        /// <param name="name">만들 자식 이름</param>
        /// <returns>비어 있는 새 MMF_Player</returns>
        private static MMF_Player RebuildPlayer(GameObject host, string name)
        {
            Transform existing = host.transform.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject go = new GameObject(name);
            go.transform.SetParent(host.transform, false);

            MMF_Player player = go.AddComponent<MMF_Player>();
            player.InitializationMode = MMFeedbacks.InitializationModes.Awake;

            return player;
        }

        /// <summary>
        /// 인스펙터에 걸려 있던 연결을 모두 지웁니다.
        /// 도구를 다시 돌릴 때 같은 호출이 두 번 걸리지 않게 합니다.
        /// </summary>
        /// <param name="target">비울 이벤트</param>
        private static void ClearPersistent(UnityEngine.Events.UnityEventBase target)
        {
            if (target == null) return;

            for (int i = target.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                UnityEventTools.RemovePersistentListener(target, i);
            }
        }
    }
}
