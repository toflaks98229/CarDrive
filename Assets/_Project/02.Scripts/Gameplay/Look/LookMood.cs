using UnityEngine;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 지금 벌어지는 일에 따라 <b>그림의 결</b>을 밀어 줍니다.
    ///
    /// 화면 후처리는 지금까지 시각과 날씨만 따라갔습니다. 그런데 이 게임에서
    /// 사람이 실제로 느끼는 것은 <b>로봇이 나를 보고 있는가</b>, <b>얼마나 지쳤는가</b>
    /// 같은 것들입니다. 그때 그림이 같이 반응하면 화면이 상황을 말하기 시작합니다.
    ///
    /// <b>둘이 다른 목소리를 씁니다.</b> 위협은 <b>잉크</b>로 말하고(선이 더 그어짐)
    /// 피로는 <b>거칠어짐</b>으로 말합니다(팔레트 계단이 줄어듦). 같은 손잡이를 함께
    /// 밀면 무엇 때문에 화면이 무거워졌는지 읽을 수 없습니다. 이 게임의 그림은 잉크로 그려져 있고, 잉크가
    /// 많아지는 것이 이 그림에서 "무거워진다" 는 뜻입니다(조사-손그림_빗금.md 7차).
    ///
    /// ⚠ <b>재질을 만지지 않습니다.</b> <c>PostPalette.mat</c> 은 에셋이라 에디터에서
    /// 값을 쓰면 <b>파일이 바뀌어 남습니다.</b> 이번 조사에서 배치 실행이 그렇게 값을
    /// 남겨 다음 실행이 물려받은 일이 있었습니다. 그래서 여기서는 셰이더 전역으로만
    /// 밀고, 재질에 적힌 값은 사람이 정한 <b>기준선</b>으로 남겨 둡니다.
    ///
    /// 전역은 <b>0 이 중립</b>입니다. 이 컴포넌트가 씬에 없으면 아무 일도 일어나지
    /// 않고 재질에 적힌 그대로 그려집니다.
    ///
    /// ⚠ <b>Systems 가 아니라 Gameplay 에 있습니다.</b> 하는 일은 화면이지만 읽는 것은
    /// 로봇의 기분과 니즈입니다. <c>CarDrive.Systems</c> 는 <c>Gameplay</c> 를 참조하지
    /// 않습니다(그 방향이면 순환입니다). <see cref="HatchingRig"/> 처럼 아무것도 안 읽는
    /// 것은 Systems 에 있어도 되지만 이것은 아닙니다.
    /// </summary>
    [DefaultExecutionOrder(410)]
    [AddComponentMenu("CarDrive/화면의 기분 (LookMood)")]
    public class LookMood : MonoBehaviour
    {
        // --- Constants ---

        private static readonly int MoodId = Shader.PropertyToID("_CarDriveLookMood");

        // --- Public Member Variables ---

        /// <summary>
        /// 로봇이 나를 겨눌 때 획이 얼마나 더 그어지는가.
        ///
        /// 재질의 <c>_HatchInk</c> 에 곱해지는 <b>덤</b>입니다. 0.5 면 1.5 배가 됩니다.
        /// </summary>
        [Header("무엇이 얼마나 미는가")]
        [Tooltip("로봇이 겨눌 때 획이 더 그어지는 정도. _HatchInk 에 곱해지는 덤입니다")]
        [Range(0f, 1.5f)]
        public float threatInk = 0.55f;

        /// <summary>
        /// 지쳤을 때 <b>무채색 자리에</b> 획이 더 그어지는 정도입니다.
        ///
        /// ⚠ <b>채도를 깎는 것이 아닙니다.</b> 처음에 "색이 빠져 획으로 간다" 고 적었는데
        /// 실측이 반박했습니다 — 평균 채도가 0.4342 에서 0.4356 으로 <b>오히려 조금
        /// 올랐습니다.</b> <c>_HatchChroma</c> 는 색이 없는 자리에 획을 더 긋는 손잡이이지
        /// 색을 빼는 손잡이가 아닙니다. 그래서 이것만으로는 피로가 거의 안 읽혔습니다
        /// (화면 변화 2.31/255, 위협은 8.13).
        /// </summary>
        [Tooltip("지쳤을 때 무채색 자리에 획이 더 그어지는 정도. 채도를 깎지는 않습니다")]
        [Range(0f, 1f)]
        public float fatigueChroma = 0.45f;

        /// <summary>
        /// 지쳤을 때 팔레트 계단이 <b>몇 칸 줄어드는가</b>.
        ///
        /// 피로에 따로 목소리를 준 것입니다. 위협은 <b>잉크</b>로 말하고 피로는
        /// <b>거칠어짐</b>으로 말합니다 — 계단이 줄면 색이 뭉뚱그려지고 디더가 드러나
        /// 그림이 성글어집니다. 지쳐서 세상이 단순하게 보이는 것과 같습니다.
        ///
        /// ⚠ 너무 줄이면 그림이 아니라 <b>고장</b>으로 보입니다. 기준선이 32 단계라
        /// 절반 아래로는 내리지 마십시오.
        /// </summary>
        [Tooltip("지쳤을 때 팔레트 계단이 줄어드는 칸 수. 기준선이 32 이므로 크게 주지 마십시오")]
        [Range(0f, 20f)]
        public float fatigueCoarse = 12f;

        /// <summary>비가 올 때 획이 더 그어지는 정도입니다.</summary>
        [Tooltip("비가 올 때 획이 더 그어지는 정도")]
        [Range(0f, 1f)]
        public float rainInk = 0.25f;

        /// <summary>
        /// 값이 따라오는 속도입니다.
        ///
        /// ⚠ <b>바로 꽂으면 안 됩니다.</b> 로봇의 기분은 <c>Idle</c> 에서 <c>Aim</c> 으로
        /// 한 프레임에 건너뜁니다. 그대로 화면에 넣으면 획이 <b>툭 나타납니다.</b>
        /// 무기 반동에 쓴 것과 같은 2차 감쇠로 눌러, 조여드는 것처럼 만듭니다.
        /// </summary>
        [Header("따라오는 속도")]
        [Tooltip("낮을수록 천천히 조여듭니다. 무기 반동에 쓴 것과 같은 2차 감쇠입니다")]
        public SecondOrderSettings response = new SecondOrderSettings(1.1f, 1.0f, 0f);

        /// <summary>
        /// 로봇을 다시 세는 간격(초)입니다.
        ///
        /// 매 프레임 씬을 훑을 이유가 없습니다. 기분은 초 단위로 바뀌고,
        /// 값은 어차피 위에서 눌러 천천히 따라갑니다.
        /// </summary>
        [Tooltip("로봇을 다시 세는 간격(초). 매 프레임 훑을 이유가 없습니다")]
        [Range(0.1f, 2f)]
        public float scanInterval = 0.4f;

        // --- Private Member Variables ---

        private SecondOrderDynamics inkMotion;
        private SecondOrderDynamics chromaMotion;
        private SecondOrderDynamics coarseMotion;

        private NeedsSystem needs;
        private WeatherSystem weather;

        private float scanTimer;
        private float threat;

        // --- Unity Event Functions ---

        void OnEnable()
        {
            inkMotion = new SecondOrderDynamics(response, 0f);
            chromaMotion = new SecondOrderDynamics(response, 0f);
            coarseMotion = new SecondOrderDynamics(response, 0f);
            scanTimer = 0f;
            threat = 0f;
        }

        void Start()
        {
            needs = GameContext.Get<NeedsSystem>();
            weather = GameContext.Get<WeatherSystem>();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            scanTimer -= dt;
            if (scanTimer <= 0f)
            {
                scanTimer = scanInterval;
                threat = Threat();
            }

            Vector3 want = Blend(threat, Fatigue(), Rain(),
                                 threatInk, fatigueChroma, rainInk, fatigueCoarse);

            Shader.SetGlobalVector(MoodId, new Vector4(
                inkMotion.Update(dt, want.x),
                chromaMotion.Update(dt, want.y),
                coarseMotion.Update(dt, want.z),
                0f));
        }

        /// <summary>꺼지면 그림이 기준선으로 돌아가야 합니다.</summary>
        void OnDisable()
        {
            Shader.SetGlobalVector(MoodId, Vector4.zero);
        }

        // --- Public Methods ---

        /// <summary>
        /// 상태 셋을 화면에 밀 값 둘로 바꿉니다.
        ///
        /// <b>왜 따로 떼어 두는가.</b> 여기가 이 컴포넌트에서 유일하게 <b>판단</b>이
        /// 들어 있는 곳입니다. 씬도 시간도 필요 없는 순수한 계산이라, 이것만 떼면
        /// 로봇과 날씨를 세우지 않고도 규칙을 확인할 수 있습니다.
        ///
        /// 위협과 비는 <b>둘 다 획을 그으므로</b> 더하되 1 을 넘지 않게 누릅니다.
        /// 둘이 겹쳤다고 두 배로 어두워지면 그 화면은 못 봅니다.
        /// </summary>
        /// <param name="threat01">가장 사나운 로봇의 기분 (0 쉼 ~ 1 교전)</param>
        /// <param name="fatigue01">피로 (0~1)</param>
        /// <param name="rain01">빗발 (0~1)</param>
        /// <param name="threatWeight">위협이 획에 주는 무게</param>
        /// <param name="fatigueWeight">피로가 채도에 주는 무게</param>
        /// <param name="rainWeight">비가 획에 주는 무게</param>
        /// <param name="coarseWeight">피로가 팔레트 계단에서 빼는 칸 수</param>
        /// <returns>x = 획에 곱할 덤, y = 채도 밀도에 더할 값, z = 줄일 계단 수</returns>
        public static Vector3 Blend(float threat01, float fatigue01, float rain01,
                                    float threatWeight, float fatigueWeight, float rainWeight,
                                    float coarseWeight)
        {
            float ink = threat01 * threatWeight + rain01 * rainWeight;

            // ⚠ 위협과 비가 겹쳐도 한 사람 몫을 넘지 않습니다.
            ink = Mathf.Min(ink, Mathf.Max(threatWeight, rainWeight));

            return new Vector3(ink, fatigue01 * fatigueWeight, fatigue01 * coarseWeight);
        }

        // --- Private Methods ---

        /// <summary>
        /// 씬에서 <b>가장 사나운</b> 로봇의 기분입니다.
        ///
        /// 여럿이 있으면 제일 험한 것이 화면을 정합니다 — 하나가 겨누고 있는데
        /// 나머지가 쉬고 있다고 평균을 내면 그 하나가 묻힙니다.
        /// </summary>
        private float Threat()
        {
            RobotThreat[] robots = FindObjectsByType<RobotThreat>(FindObjectsSortMode.None);
            float worst = 0f;

            for (int i = 0; i < robots.Length; i++)
            {
                if (robots[i] == null || !robots[i].isActiveAndEnabled) continue;

                // Idle 0 · Watch 1 · Aim 2 · Engage 3 을 0~1 로 폅니다.
                float level = (float)(int)robots[i].State / 3f;
                if (level > worst) worst = level;
            }

            return Mathf.Clamp01(worst);
        }

        private float Fatigue()
        {
            return needs == null ? 0f : Mathf.Clamp01(needs.GetValue(NeedType.Fatigue));
        }

        private float Rain()
        {
            return weather == null ? 0f : Mathf.Clamp01(weather.RainIntensity);
        }
    }
}
