using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 시야 밖의 귀신을 <b>화면 가장자리의 얼룩</b>으로 알립니다.
    ///
    /// <b>메우는 구멍.</b> 귀신이 붙는 자리를 재 보니 셋 다 <b>앞을 볼 때 화면에 한
    /// 화소도 안 들어옵니다</b> — 뒤에 붙는 것은 운전석 1.35 m 뒤, 옆의 둘은 좌우
    /// 8 m 입니다. 단서는 거울과 소리뿐이고, 거울은 화면의 5~10% 라 <b>보고 있어야</b>
    /// 보입니다. 그래서 "있다" 는 사실만 눈가에 얹습니다.
    ///
    /// ⚠ <b>어느 놈인지, 얼마나 아픈지는 말하지 않습니다.</b> 화살표나 체력 막대를
    /// 달면 이 게임의 밤이 <b>관리할 수 있는 것</b>이 됩니다. 가장자리가 어두워지는
    /// 것까지가 이 부품의 말 전부이고, 나머지는 고개를 돌려 확인해야 합니다.
    ///
    /// ⚠ <b>화면에 들어와 있으면 얼룩이 사라집니다.</b> 보이는 것을 또 알리면
    /// 눈가가 늘 더러워 있고, 그러면 <b>정말 안 보일 때</b>의 얼룩이 안 읽힙니다.
    ///
    /// <b>어떻게 그리는가.</b> 후처리(<c>CarDrivePalette</c>)가 전역 하나를 읽습니다.
    /// 새 패스도, 캔버스도 늘리지 않습니다.
    /// </summary>
    [AddComponentMenu("CarDrive/귀신 눈가 얼룩 (GhostEdgeCue)")]
    public class GhostEdgeCue : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>기준이 될 눈입니다. 비워두면 메인 카메라를 씁니다.</summary>
        [Header("배선")]
        [Tooltip("기준이 될 눈. 비워두면 메인 카메라를 씁니다")]
        public Camera eye;

        /// <summary>이 거리 안이면 가장 진합니다(m).</summary>
        [Header("진하기")]
        [Tooltip("이 거리 안이면 가장 진합니다(m)")]
        [Range(0.5f, 20f)]
        public float near = 3f;

        /// <summary>이 거리 밖이면 아무것도 안 보입니다(m).</summary>
        [Tooltip("이 거리 밖이면 아무것도 안 보입니다(m)")]
        [Range(5f, 80f)]
        public float far = 22f;

        /// <summary>가장 진할 때의 세기입니다.</summary>
        [Tooltip("가장 진할 때의 세기. 1 이면 그 모서리가 거의 검어집니다")]
        [Range(0f, 1f)]
        public float strength = 0.55f;

        /// <summary>
        /// 얼룩이 <b>숨 쉬는</b> 빠르기입니다(초당 회).
        ///
        /// 가만히 있는 얼룩은 화면의 때로 읽힙니다. 느리게 들썩여야 <b>살아 있는
        /// 것</b>이 붙어 있다고 읽힙니다.
        /// </summary>
        [Tooltip("얼룩이 숨 쉬는 빠르기(초당 회). 0 이면 가만히 있습니다")]
        [Range(0f, 3f)]
        public float breath = 0.7f;

        /// <summary>나타나고 사라지는 데 걸리는 시간(초)입니다.</summary>
        [Tooltip("나타나고 사라지는 데 걸리는 시간(초)")]
        [Range(0f, 3f)]
        public float fade = 0.6f;

        // --- Public Properties ---

        /// <summary>지금 화면에 얹힌 세기입니다. 0 이면 아무것도 안 보입니다.</summary>
        public float Showing { get; private set; }

        /// <summary>지금 얼룩이 앉은 쪽입니다.</summary>
        public Vector2 Side { get; private set; }

        // --- Private Member Variables ---

        private static readonly int EdgeId = Shader.PropertyToID("_CarDriveGhostEdge");

        // --- Unity Event Functions ---

        void OnEnable()
        {
            Showing = 0f;
            Push();
        }

        void OnDisable()
        {
            // ⚠ <b>끄면서 지웁니다.</b> 전역은 컴포넌트가 사라져도 남습니다 —
            // 안 지우면 귀신이 없는데 눈가가 계속 어둡습니다.
            Showing = 0f;
            Push();
        }

        void LateUpdate()
        {
            Camera lens = eye != null ? eye : Camera.main;

            if (lens == null)
            {
                Showing = 0f;
                Push();
                return;
            }

            Vector2 want = Vector2.zero;
            float wanted = 0f;

            foreach (AttachedGhostController ghost in Object.FindObjectsByType<AttachedGhostController>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (ghost == null) continue;

                Vector3 view = lens.WorldToViewportPoint(ghost.transform.position);
                if (!OffScreen(view)) continue;

                float away = Vector3.Distance(ghost.transform.position, lens.transform.position);
                float mine = Weight(away, near, far);

                if (mine <= wanted) continue;

                wanted = mine;
                want = Toward(view);
            }

            // 나타나고 사라지는 것을 잇습니다. 톡 켜지면 그것은 알림이지 기척이 아닙니다.
            float step = fade > 0.001f ? Time.deltaTime / fade : 1f;
            Showing = Mathf.MoveTowards(Showing, wanted, step);

            if (wanted > 0f) Side = want;

            Push();
        }

        // --- Public Methods ---

        /// <summary>
        /// 그 자리가 화면 밖인가.
        ///
        /// <b>뒤에 있는 것도 화면 밖입니다.</b> 뷰포트 z 가 음수면 카메라 뒤라,
        /// x·y 가 0~1 안에 들어와도 보이지 않습니다 — 뒤에 붙는 귀신이 정확히
        /// 그 경우입니다.
        /// </summary>
        /// <param name="view">뷰포트 좌표</param>
        /// <returns>화면 밖이면 true</returns>
        public static bool OffScreen(Vector3 view)
        {
            if (view.z <= 0f) return true;

            return view.x < 0f || view.x > 1f || view.y < 0f || view.y > 1f;
        }

        /// <summary>
        /// 얼룩이 앉을 쪽입니다. 화면 한가운데에서 그 귀신 쪽으로 향하는 방향입니다.
        ///
        /// ⚠ <b>뒤에 있는 것은 좌우를 뒤집어야 합니다.</b> 카메라 뒤의 점은 뷰포트에서
        /// 앞뒤가 뒤집혀 찍히므로, 그대로 쓰면 <b>반대쪽</b> 눈가가 어두워집니다.
        /// </summary>
        /// <param name="view">뷰포트 좌표</param>
        /// <returns>길이 1 인 방향. 잴 수 없으면 아래쪽</returns>
        public static Vector2 Toward(Vector3 view)
        {
            Vector2 fromMid = new Vector2(view.x - 0.5f, view.y - 0.5f);

            if (view.z <= 0f) fromMid = -fromMid;

            // 정확히 한가운데면 방향이 없습니다. 뒤에 있는 것으로 보고 아래로 둡니다.
            if (fromMid.sqrMagnitude < 1e-8f) return new Vector2(0f, -1f);

            return fromMid.normalized;
        }

        /// <summary>
        /// 그 거리에서의 진하기입니다.
        ///
        /// <b>왜 갈라 두는가.</b> 이 곡선이 "얼마나 가까워야 알려 주는가" 전부입니다.
        /// 귀신도 카메라도 없이 확인할 수 있어야 합니다.
        /// </summary>
        /// <param name="metres">귀신까지의 거리</param>
        /// <param name="near">이 안이면 1</param>
        /// <param name="far">이 밖이면 0</param>
        /// <returns>0~1</returns>
        public static float Weight(float metres, float near, float far)
        {
            if (far <= near) return metres <= near ? 1f : 0f;

            return 1f - Mathf.Clamp01((metres - near) / (far - near));
        }

        // --- Private Methods ---

        /// <summary>후처리에 넘깁니다.</summary>
        private void Push()
        {
            float pulse = 1f;

            if (breath > 0.001f && Showing > 0f)
            {
                // 0.75~1 사이로 들썩입니다. 더 깊게 흔들면 깜빡임이 됩니다.
                pulse = 0.875f + 0.125f * Mathf.Sin(Time.time * breath * Mathf.PI * 2f);
            }

            Shader.SetGlobalVector(EdgeId,
                new Vector4(Side.x, Side.y, Showing * strength * pulse, 0f));
        }
    }
}
