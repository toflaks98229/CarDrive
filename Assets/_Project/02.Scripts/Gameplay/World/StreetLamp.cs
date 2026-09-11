using System.Collections.Generic;
using UnityEngine;
using VContainer;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 길가의 등입니다. <b>어두워지면 켜지고</b> 밝아지면 꺼집니다.
    ///
    /// <b>왜 필요한가.</b> 이 세계의 밤은 헤드라이트 바깥이 통째로 검습니다. 그것이
    /// 이 게임의 밤이지만, <b>아무 데도 불이 없으면</b> 길이 어디로 이어지는지도
    /// 읽히지 않고 마을이 멀리서 보이지도 않습니다. 등은 <b>돌아갈 곳</b>을 멀리서
    /// 알려 주는 유일한 표시입니다.
    ///
    /// <b>꺼진 등이 있습니다.</b> 사람이 떠난 세계라 등도 죽습니다. 죽은 등은
    /// <see cref="LampLighter"/> 가 다시 켭니다 — 그것이 로봇과 귀신을 잇는
    /// 유일한 고리입니다(<c>로봇_기획.md</c> 의 6번).
    ///
    /// ⚠ <b>시간을 직접 읽지 않습니다.</b> <see cref="IGameClock"/> 이 밝기를 압니다.
    /// 해의 각도나 시각을 여기서 또 계산하면 하늘과 등이 서로 다른 시각을 믿게 됩니다.
    /// </summary>
    [AddComponentMenu("CarDrive/길가의 등 (StreetLamp)")]
    public class StreetLamp : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>켜고 끌 빛입니다. 비워두면 자기 계층에서 찾습니다.</summary>
        [Header("부품")]
        [Tooltip("켜고 끌 빛. 비워두면 자기 계층에서 찾습니다")]
        public Light bulb;

        /// <summary>켜졌을 때 스스로 빛나 보일 부분입니다.</summary>
        [Tooltip("켜졌을 때 스스로 빛나 보일 부분(등갓 안쪽 등)")]
        public Renderer glass;

        /// <summary>등이 켜졌을 때 등갓이 내는 색입니다.</summary>
        [Tooltip("켜졌을 때 등갓이 내는 색")]
        [ColorUsage(false, true)]
        public Color glow = new Color(1.6f, 1.25f, 0.7f);

        /// <summary>
        /// 이 밝기 <b>아래로</b> 내려가면 켭니다(0이면 한밤, 1이면 한낮).
        ///
        /// 해가 완전히 진 뒤에 켜면 늦습니다. 사람이 등을 켜는 때는 아직 볼 수 있을
        /// 때입니다.
        /// </summary>
        [Header("언제 켜는가")]
        [Tooltip("이 밝기 아래로 내려가면 켭니다")]
        [Range(0f, 1f)]
        public float onBelow = 0.35f;

        /// <summary>
        /// 이 밝기 <b>위로</b> 올라가야 끕니다.
        ///
        /// ⚠ <b>켜는 값과 같게 두면 안 됩니다.</b> 밝기가 문턱에서 떨리면 등이
        /// 깜빡입니다. 끄는 문턱을 위로 벌려 두어야 한 번 켜진 등이 붙어 있습니다.
        /// </summary>
        [Tooltip("이 밝기 위로 올라가야 끕니다. 켜는 값보다 커야 깜빡이지 않습니다")]
        [Range(0f, 1f)]
        public float offAbove = 0.5f;

        /// <summary>이 등이 밝히는 반경입니다(m). 귀신이 꺼리는 범위이기도 합니다.</summary>
        [Header("밝히는 범위")]
        [Tooltip("이 등이 밝히는 반경(m). 귀신이 꺼리는 범위이기도 합니다")]
        [Range(2f, 60f)]
        public float reach = 18f;

        /// <summary>
        /// 죽은 등인가.
        ///
        /// 죽은 등은 어두워져도 켜지지 않습니다. <see cref="LampLighter"/> 가
        /// <see cref="Relight"/> 로 되살립니다.
        /// </summary>
        [Header("상태")]
        [Tooltip("죽은 등. 어두워져도 켜지지 않습니다")]
        public bool broken;

        /// <summary>켜지고 꺼질 때 낼 소리입니다.</summary>
        [Header("소리")]
        [Tooltip("켜지고 꺼질 때 낼 소리")]
        public AudioClip clack;

        /// <summary>소리 크기입니다.</summary>
        [Tooltip("소리 크기")]
        [Range(0f, 1f)]
        public float clackVolume = 0.35f;

        // --- Public Properties ---

        /// <summary>지금 타고 있는가.</summary>
        public bool Burning { get; private set; }

        /// <summary>세계에 있는 등 전부입니다. 읽기만 됩니다.</summary>
        public static IReadOnlyList<StreetLamp> All { get { return all; } }

        // --- Private Member Variables ---

        /// <summary>
        /// 켜져 있는 것까지 포함한 등 전부입니다.
        ///
        /// <b>왜 등록부인가.</b> 귀신이 "여기가 밝은가" 를 물을 때마다 씬을 훑으면
        /// 스폰마다 세계 전체를 뒤지게 됩니다. 등은 <c>OnEnable</c>·<c>OnDisable</c>
        /// 에서 스스로 넣고 뺍니다 — <see cref="Vehicle"/> 이 하는 그대로입니다.
        /// </summary>
        private static readonly List<StreetLamp> all = new List<StreetLamp>();

        private IGameClock clock = NullGameClock.Instance;
        private MaterialPropertyBlock paint;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        // --- Unity Event Functions ---

        void Awake()
        {
            if (bulb == null) bulb = GetComponentInChildren<Light>(true);

            Show();
        }

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDisable()
        {
            all.Remove(this);
        }

        void Update()
        {
            bool want = !broken && ShouldBurn(clock.Daylight, Burning, onBelow, offAbove);
            if (want == Burning) return;

            Burning = want;
            Show();

            if (clack != null) OneShotAudioPool.Play(clack, transform.position, clackVolume);
        }

        // --- Injection ---

        /// <summary>게임 시계를 받습니다. 해가 얼마나 남았는지는 시계가 압니다.</summary>
        /// <param name="gameClock">게임 시계</param>
        [Inject]
        public void Construct(IGameClock gameClock)
        {
            if (gameClock != null) clock = gameClock;
        }

        // --- Public Methods ---

        /// <summary>
        /// 플레이 모드에 들어갈 때 등록부를 비웁니다.
        ///
        /// 도메인 리로드를 꺼 두면 지난 실행의 등이 유령으로 남습니다.
        /// <see cref="Vehicle"/> 이 같은 일을 합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        /// <summary>
        /// 지금 켜져 있어야 하는가.
        ///
        /// <b>왜 지금 상태까지 보는가.</b> 문턱이 하나면 밝기가 그 언저리에서 떨릴 때
        /// 등이 깜빡입니다. 켜는 문턱과 끄는 문턱을 벌려 두고, <b>지금 켜져 있으면</b>
        /// 끄는 문턱을 넘을 때까지 버팁니다.
        /// </summary>
        /// <param name="daylight">0이면 한밤, 1이면 한낮</param>
        /// <param name="burning">지금 타고 있는가</param>
        /// <param name="onBelow">이 아래로 내려가면 켭니다</param>
        /// <param name="offAbove">이 위로 올라가야 끕니다</param>
        /// <returns>켜져 있어야 하면 true</returns>
        public static bool ShouldBurn(float daylight, bool burning, float onBelow, float offAbove)
        {
            // 끄는 문턱이 켜는 문턱보다 낮게 잡히면 벌어진 구간이 없어져 깜빡입니다.
            float off = Mathf.Max(offAbove, onBelow);

            if (burning) return daylight < off;

            return daylight < onBelow;
        }

        /// <summary>
        /// 그 자리가 <b>얼마나 밝은가</b>. 0이면 캄캄하고 1이면 등 바로 아래입니다.
        ///
        /// <b>가장 밝은 등 하나만 셉니다.</b> 여러 등을 더하면 등이 촘촘한 마을이
        /// 1을 훌쩍 넘어, 귀신이 마을에 아예 안 나오게 됩니다. 마을이 안전한 것과
        /// 마을에 아무 일도 안 일어나는 것은 다릅니다.
        /// </summary>
        /// <param name="at">볼 자리</param>
        /// <returns>0~1</returns>
        public static float LightAt(Vector3 at)
        {
            float best = 0f;

            for (int i = 0; i < all.Count; i++)
            {
                StreetLamp lamp = all[i];
                if (lamp == null || !lamp.Burning || lamp.reach <= 0f) continue;

                float away = Vector3.Distance(lamp.transform.position, at);
                if (away >= lamp.reach) continue;

                float lit = 1f - away / lamp.reach;
                if (lit > best) best = lit;
            }

            return best;
        }

        /// <summary>죽은 등을 되살립니다. 어두우면 그 자리에서 켜집니다.</summary>
        public void Relight()
        {
            broken = false;
        }

        /// <summary>등을 죽입니다. 세이브와 도구가 씁니다.</summary>
        public void Break()
        {
            broken = true;

            if (!Burning) return;

            Burning = false;
            Show();
        }

        // --- Private Methods ---

        /// <summary>켜짐을 눈에 보이게 만듭니다.</summary>
        private void Show()
        {
            if (bulb != null) bulb.enabled = Burning;

            if (glass == null) return;

            // ⚠ <b>재질을 복제하지 않습니다.</b> 등은 여럿이고 재질은 하나입니다.
            // <c>glass.material</c> 을 건드리면 등 수만큼 재질이 복제되어 남습니다.
            if (paint == null) paint = new MaterialPropertyBlock();

            glass.GetPropertyBlock(paint);
            paint.SetColor(EmissionId, Burning ? glow : Color.black);
            glass.SetPropertyBlock(paint);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = broken ? new Color(1f, 0.4f, 0.3f, 0.5f)
                                  : new Color(1f, 0.9f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
