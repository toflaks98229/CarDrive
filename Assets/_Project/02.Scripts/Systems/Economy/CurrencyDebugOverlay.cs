using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 디버그로 재화를 올리고 내릴 때의 한 걸음입니다.
    /// </summary>
    [System.Serializable]
    public class CurrencyDebugStep
    {
        /// <summary>어떤 재화에 대한 걸음인지입니다.</summary>
        [Tooltip("어떤 재화에 대한 걸음인지")]
        public CurrencyType type;

        /// <summary>키 한 번에 오르내릴 양입니다.</summary>
        [Tooltip("키 한 번에 오르내릴 양")]
        public int amount = 1000;
    }

    /// <summary>
    /// 지갑 상태를 바로 확인하고 손으로 올리고 내릴 수 있는 디버그 오버레이입니다.
    ///
    /// <see cref="NeedsDebugOverlay"/> 와 같은 자리에 있는 도구입니다 — 정식 화면
    /// (<c>CurrencyUI</c>)이 있더라도, <b>값을 원하는 만큼 만들어 놓고 시험하는 길</b>은
    /// 따로 필요합니다. 물건값을 정하거나 계산 거절을 확인하려면 지갑을 비웠다 채웠다
    /// 해야 하는데, 그때마다 귀신을 잡으러 갈 수는 없습니다.
    ///
    /// <see cref="Wallet"/> 과 같은 GameObject 에 붙여 두면 됩니다.
    /// </summary>
    [RequireComponent(typeof(Wallet))]
    public class CurrencyDebugOverlay : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>오버레이를 화면에 그릴지 여부입니다. <see cref="toggleKey"/>로도 전환합니다.</summary>
        [Header("표시")]
        [Tooltip("오버레이를 켜고 끕니다.")]
        public bool showOverlay = false;

        /// <summary>오버레이 표시를 전환하는 키입니다.</summary>
        [Tooltip("오버레이 표시를 토글하는 키. F1 니즈 · F2 시간 · F3 성능 이 이미 쓰이고 있습니다.")]
        public KeyCode toggleKey = KeyCode.F4;

        /// <summary>화면 좌상단으로부터의 여백입니다.</summary>
        [Tooltip("화면 좌상단으로부터의 여백")]
        public Vector2 margin = new Vector2(360f, 16f);

        /// <summary>줄 하나의 가로·세로 크기입니다.</summary>
        [Tooltip("줄 하나의 크기")]
        public Vector2 rowSize = new Vector2(260f, 20f);

        /// <summary>숫자키로 재화를 강제로 조절하는 테스트 조작을 허용할지 여부입니다.</summary>
        [Header("테스트 조작")]
        [Tooltip("체크하면 오버레이가 켜져 있는 동안 숫자키로 재화를 올리고 내릴 수 있습니다.")]
        public bool enableTestKeys = true;

        /// <summary>
        /// 첫 번째 재화에 배정할 키입니다. 두 번째 재화는 그다음 키를 씁니다.
        ///
        /// <b>7부터 시작하는 이유가 있습니다.</b> <see cref="NeedsDebugOverlay"/> 가
        /// 0~6 을 이미 쓰고 있고, 그쪽 테스트 키는 오버레이를 꺼 두어도 계속 살아 있습니다.
        /// 같은 키를 쓰면 돈을 올리면서 배가 고파집니다.
        /// </summary>
        [Tooltip("첫 번째 재화에 배정할 키. 니즈 오버레이가 0~6 을 쓰므로 7부터 시작합니다.")]
        public KeyCode firstTestKey = KeyCode.Alpha7;

        /// <summary>모든 재화를 시작값으로 되돌리는 키입니다.</summary>
        [Tooltip("모든 재화를 시작값으로 되돌리는 키")]
        public KeyCode resetKey = KeyCode.Minus;

        /// <summary>
        /// 재화별 한 걸음입니다. 목록에 없는 재화는 <see cref="defaultStep"/> 을 씁니다.
        ///
        /// 돈과 엑토플라즘은 자릿수가 다릅니다 — 돈 1,000원과 엑토플라즘 1,000개는
        /// 전혀 다른 크기라, 한 걸음을 하나로 두면 한쪽이 쓸모없어집니다.
        /// </summary>
        [Tooltip("재화별 한 걸음. 목록에 없으면 아래 기본 걸음을 씁니다.")]
        public List<CurrencyDebugStep> steps = new List<CurrencyDebugStep>
        {
            new CurrencyDebugStep { type = CurrencyType.Money, amount = 1000 },
            new CurrencyDebugStep { type = CurrencyType.Ectoplasm, amount = 10 }
        };

        /// <summary>목록에 없는 재화에 쓸 한 걸음입니다.</summary>
        [Tooltip("목록에 없는 재화에 쓸 한 걸음")]
        public int defaultStep = 100;

        // --- Private Member Variables ---

        /// <summary>값을 읽고 쓸 지갑입니다. 같은 GameObject 에서 가져옵니다.</summary>
        private Wallet wallet;

        /// <summary>줄 배경을 그릴 1x1 흰색 텍스처입니다. 색은 <c>GUI.color</c> 로 입힙니다.</summary>
        private Texture2D rowTexture;

        /// <summary>글자 크기를 줄인 라벨 스타일입니다. <c>OnGUI</c> 에서 처음 필요할 때 만듭니다.</summary>
        private GUIStyle labelStyle;

        // --- Unity Event Functions ---

        /// <summary>지갑 참조를 가져오고 줄 배경용 텍스처를 만듭니다.</summary>
        void Awake()
        {
            wallet = GetComponent<Wallet>();

            rowTexture = new Texture2D(1, 1);
            rowTexture.SetPixel(0, 0, Color.white);
            rowTexture.Apply();
        }

        /// <summary>코드로 만든 텍스처를 해제합니다. 두지 않으면 씬을 오갈 때마다 쌓입니다.</summary>
        void OnDestroy()
        {
            if (rowTexture != null) Destroy(rowTexture);
        }

        /// <summary>표시 전환 키와 테스트 조작 키 입력을 받습니다.</summary>
        void Update()
        {
            if (GameInput.GetKeyDownRaw(toggleKey))
            {
                showOverlay = !showOverlay;
            }

            // <b>오버레이가 켜져 있을 때만</b> 조작을 받습니다.
            // 니즈 쪽과 달리 이렇게 두는 이유는 위 firstTestKey 주석에 적었습니다 —
            // 숫자키가 겹치는 도구가 이미 있으므로, 어느 쪽이 듣는지 화면으로 보여야 합니다.
            if (enableTestKeys && showOverlay) HandleTestKeys();
        }

        /// <summary>재화 목록과 테스트 조작 안내를 그립니다.</summary>
        void OnGUI()
        {
            if (!showOverlay || wallet == null) return;

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 12;
            }

            IReadOnlyList<CurrencySetting> all = wallet.GetAllSettings();
            if (all == null) return;

            float y = margin.y;

            GUI.color = Color.white;
            GUI.Label(new Rect(margin.x, y, 320f, 18f), "재화  (" + toggleKey + " 로 표시 전환)", labelStyle);
            y += 20f;

            for (int i = 0; i < all.Count; i++)
            {
                DrawCurrencyRow(all[i], i, new Rect(margin.x, y, rowSize.x, rowSize.y));
                y += rowSize.y + 4f;
            }

            if (enableTestKeys)
            {
                y += 6f;
                GUI.color = new Color(1f, 1f, 1f, 0.6f);

                for (int i = 0; i < all.Count; i++)
                {
                    KeyCode key = KeyForIndex(i);
                    if (key == KeyCode.None) continue;

                    GUI.Label(new Rect(margin.x, y, 400f, 18f),
                        KeyLabel(key) + ": " + all[i].displayName + " +" + StepFor(all[i].type).ToString("N0") +
                        "   (Shift 를 누르면 빼기)", labelStyle);
                    y += 18f;
                }

                GUI.Label(new Rect(margin.x, y, 400f, 18f), KeyLabel(resetKey) + ": 모두 시작값으로", labelStyle);
            }

            GUI.color = Color.white;
        }

        // --- Private Methods ---

        /// <summary>재화 한 줄을 그립니다.</summary>
        /// <param name="setting">그릴 재화의 설정 (이름·색상)</param>
        /// <param name="index">몇 번째 재화인지. 배정된 키를 적는 데 씁니다.</param>
        /// <param name="rect">그릴 화면 영역</param>
        private void DrawCurrencyRow(CurrencySetting setting, int index, Rect rect)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, rowTexture);

            // 왼쪽에 재화 색으로 얇은 띠를 둡니다. 어느 줄이 무엇인지 한눈에 갈리도록.
            GUI.color = setting.displayColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), rowTexture);

            GUI.color = Color.white;

            string amount = wallet.Format(setting.type);
            string key = enableTestKeys ? "  [" + KeyLabel(KeyForIndex(index)) + "]" : "";

            GUI.Label(new Rect(rect.x + 8f, rect.y + 1f, rect.width, rect.height),
                setting.displayName + "   " + amount + key, labelStyle);
        }

        /// <summary>
        /// 숫자키로 재화를 조절합니다. Shift 를 함께 누르면 뺍니다.
        /// </summary>
        private void HandleTestKeys()
        {
            IReadOnlyList<CurrencySetting> all = wallet.GetAllSettings();
            if (all == null) return;

            bool subtract = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            for (int i = 0; i < all.Count; i++)
            {
                KeyCode key = KeyForIndex(i);
                if (key == KeyCode.None) continue;
                if (!GameInput.GetKeyDownRaw(key)) continue;

                CurrencyType type = all[i].type;
                int step = StepFor(type);

                if (subtract) wallet.TrySpend(type, Mathf.Min(step, wallet.Get(type)));
                else wallet.Add(type, step);
            }

            if (GameInput.GetKeyDownRaw(resetKey)) wallet.ResetAll();
        }

        /// <summary>
        /// 몇 번째 재화에 어떤 키가 배정되는지 계산합니다.
        /// </summary>
        /// <param name="index">재화의 순서</param>
        /// <returns>배정된 키. 배정할 자리가 없으면 <c>KeyCode.None</c></returns>
        private KeyCode KeyForIndex(int index)
        {
            if (firstTestKey == KeyCode.None) return KeyCode.None;

            KeyCode key = firstTestKey + index;

            // 숫자열을 벗어나면 배정하지 않습니다. 엉뚱한 키가 걸리는 편보다 없는 편이 낫습니다.
            if (key > KeyCode.Alpha9) return KeyCode.None;

            return key;
        }

        /// <summary>이 재화의 한 걸음을 돌려줍니다.</summary>
        /// <param name="type">찾을 재화</param>
        /// <returns>목록에 적힌 걸음. 없으면 <see cref="defaultStep"/></returns>
        private int StepFor(CurrencyType type)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && steps[i].type == type) return steps[i].amount;
            }
            return defaultStep;
        }

        /// <summary>안내 문구에 넣을 키 이름입니다.</summary>
        /// <param name="key">이름을 물어볼 키</param>
        /// <returns>사람이 읽을 키 이름</returns>
        private static string KeyLabel(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return ((int)(key - KeyCode.Alpha0)).ToString();
            }

            if (key == KeyCode.Minus) return "-";

            return key.ToString();
        }
    }
}
