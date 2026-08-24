using ConsoleDisplay.Templates;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleDisplay.Showcase
{
    /// <summary>
    /// 데모 방을 진행합니다. 가까운 버튼을 찾아 안내를 띄우고, 눌렀을 때 템플릿을 실행합니다.
    ///
    /// <b>안내는 게임 화면에만 띄웁니다.</b> 두 번째 화면은 템플릿이 통째로 씁니다.
    /// 거기에 조작 설명을 겹쳐 쓰면 "이 에셋이 보여 주려는 것"을 가리게 됩니다.
    /// </summary>
    public sealed class ShowcaseDirector : MonoBehaviour
    {
        // --- Public Member Variables ---

        [Tooltip("가까운 버튼의 이름과 설명이 나올 곳입니다.")]
        [SerializeField] private Text prompt;

        [Tooltip("조작 설명이 나올 곳입니다.")]
        [SerializeField] private Text help;

        // --- Private Member Variables ---

        private System.Collections.Generic.IReadOnlyList<ShowcaseKiosk> kiosks;
        private ShowcaseKiosk near;

        // --- Unity Event Functions ---

        private void Start()
        {
            kiosks = ShowcaseKiosk.All;

            if (help != null)
            {
                help.text = "WASD 이동   마우스 시점   E 실행   좌클릭 마우스 다시 고정   Esc 잠금 해제";
            }

            if (!SecondDisplay.IsSupported)
            {
                // 지원되지 않는 환경에서 버튼만 눌러 대면 아무 일도 안 일어나는 것처럼 보입니다.
                if (prompt != null)
                {
                    prompt.text = "이 플랫폼에서는 두 번째 화면을 띄울 수 없습니다 (윈도우 전용)";
                }

                enabled = false;
            }
        }

        private void Update()
        {
            near = FindNearest();
            UpdatePrompt();

            if (near != null && ShowcaseInput.InteractPressed)
            {
                Activate(near);
            }
        }

        // --- Private Methods ---

        /// <summary>손이 닿는 거리에 있는 버튼 중 가장 가까운 것을 찾습니다.</summary>
        private ShowcaseKiosk FindNearest()
        {
            if (kiosks == null)
            {
                return null;
            }

            ShowcaseKiosk best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < kiosks.Count; i++)
            {
                ShowcaseKiosk kiosk = kiosks[i];
                if (kiosk == null || !kiosk.IsPlayerInReach())
                {
                    continue;
                }

                float distance = Vector3.Distance(kiosk.transform.position, transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = kiosk;
                }
            }

            return best;
        }

        /// <summary>지금 무엇을 할 수 있는지 알려 줍니다.</summary>
        private void UpdatePrompt()
        {
            if (prompt == null)
            {
                return;
            }

            if (near == null)
            {
                ConsoleTemplate active = ConsoleTemplateRunner.Instance.Active;
                prompt.text = active != null
                    ? "실행 중 : " + active.DisplayName
                    : "버튼 앞으로 가면 실행할 수 있습니다";
                return;
            }

            prompt.text = "[E] " + near.Title + "\n" + near.Summary;
        }

        /// <summary>버튼을 눌러 템플릿을 갈아 끼웁니다.</summary>
        private void Activate(ShowcaseKiosk kiosk)
        {
            for (int i = 0; i < kiosks.Count; i++)
            {
                if (kiosks[i] != null)
                {
                    kiosks[i].MarkStopped();
                }
            }

            kiosk.Press();

            // 로그 스트림 버튼을 눌렀는데 아무 로그도 안 나오면 고장난 것처럼 보입니다.
            // 눌렀다는 사실 자체를 로그로 남겨 화면에 뭔가 나오게 합니다.
            Debug.Log("[Showcase] " + kiosk.Title + " 실행");
        }
    }
}
