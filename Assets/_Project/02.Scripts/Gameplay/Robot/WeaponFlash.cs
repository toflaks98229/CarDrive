using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 총구가 <b>번쩍입니다.</b> 쏘는 것이 화면에 보이게 하는 유일한 부품입니다.
    ///
    /// <b>왜 빛인가.</b> 이 게임의 후처리는 밝기를 <b>여섯 단계</b>로 계단화하고 채도를
    /// 30% 깎습니다 — 색은 화면까지 못 가고 <b>휘도만 살아남습니다.</b> 그래서 총구
    /// 섬광은 색이 화려한 파티클이 아니라 <b>밝은 빛</b>이라야 읽힙니다. 게다가
    /// 밤에는 빛이 포신과 발밑을 같이 비추어, 멀리서도 <b>어디서 쐈는지</b>가 보입니다.
    ///
    /// <b>왜 파티클도 같이 쓰는가.</b> 낮에는 빛이 거의 안 보입니다. 그래서 조각
    /// 하나를 같이 띄웁니다 — 낮에는 그것이, 밤에는 빛이 일을 합니다.
    ///
    /// <b>짧아야 합니다.</b> 0.06 초면 60fps 에서 네 프레임입니다. 길게 잡으면
    /// 섬광이 아니라 <b>등</b>이 됩니다.
    ///
    /// <b>스스로 돌지 않습니다.</b> <see cref="RobotWeapon"/> 이 쏘는 그 프레임에
    /// 불러 줍니다 — 한 프레임만 밀려도 포신이 밀린 뒤에 번쩍입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponFlash : MonoBehaviour
    {
        // --- Public Member Variables : 배선 ---

        /// <summary>번쩍일 빛입니다. 비어 있으면 자기 자식에서 찾습니다.</summary>
        [Header("배선")]
        [Tooltip("번쩍일 빛. 비어 있으면 자식에서 찾습니다")]
        public Light glow;

        /// <summary>같이 띄울 파티클입니다. 없어도 됩니다.</summary>
        [Tooltip("같이 띄울 파티클. 없어도 됩니다")]
        public ParticleSystem puff;

        // --- Public Member Variables : 움직임 ---

        /// <summary>가장 밝을 때의 세기입니다.</summary>
        [Header("움직임")]
        [Tooltip("가장 밝을 때의 세기")]
        public float peak = 40f;

        /// <summary>빛이 닿는 거리입니다(m).</summary>
        [Tooltip("빛이 닿는 거리(m)")]
        public float reach = 14f;

        /// <summary>꺼지는 데 걸리는 시간입니다(초).</summary>
        [Tooltip("꺼지는 데 걸리는 시간(초)")]
        public float duration = 0.06f;

        // --- Public Properties ---

        /// <summary>지금 번쩍이고 있는지입니다.</summary>
        public bool Lit { get { return left > 0f; } }

        /// <summary>이번 판에서 가장 밝았던 세기입니다. 실측이 봅니다.</summary>
        public float PeakSeen { get; private set; }

        // --- Private Member Variables ---

        /// <summary>남은 시간입니다.</summary>
        private float left;

        // --- Public Methods ---

        /// <summary>한 번 번쩍입니다.</summary>
        public void Pop()
        {
            left = duration;

            if (glow != null)
            {
                glow.enabled = true;
                glow.intensity = peak;
                glow.range = reach;
            }

            if (peak > PeakSeen) PeakSeen = peak;

            if (puff != null) puff.Play(true);
        }

        /// <summary>한 프레임분을 진행합니다. <see cref="RobotWeapon"/> 이 불러 줍니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        public void Tick(float dt)
        {
            if (left <= 0f || dt <= 0f) return;

            left -= dt;

            if (glow == null) return;

            if (left <= 0f)
            {
                glow.enabled = false;
                glow.intensity = 0f;
                return;
            }

            // <b>선형으로 꺼집니다.</b> 네 프레임짜리라 곡선을 넣어도 안 보이고,
            // 값 하나가 늘면 조율할 것만 늘어납니다.
            glow.intensity = peak * (left / Mathf.Max(duration, 0.001f));
        }

        // --- Unity Event Functions ---

        private void Awake()
        {
            if (glow == null) glow = GetComponentInChildren<Light>(true);
            if (puff == null) puff = GetComponentInChildren<ParticleSystem>(true);

            if (glow != null)
            {
                glow.enabled = false;
                glow.intensity = 0f;
            }
        }

        private void OnValidate()
        {
            peak = Mathf.Max(0f, peak);
            reach = Mathf.Max(0.5f, reach);
            duration = Mathf.Clamp(duration, 0.01f, 0.5f);
        }
    }
}
