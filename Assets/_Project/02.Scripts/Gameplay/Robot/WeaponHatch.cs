using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 덮개를 <b>열고 닫습니다.</b> 미사일 랙의 발사구 판이 이것으로 움직입니다.
    ///
    /// <b>왜 이것이 중요한가.</b> 미사일 랙은 여섯 중 <b>유일하게 되밀리지 않는</b>
    /// 무장입니다. 반동도 회전도 없으니 <b>덮개가 열리는 것이 곧 예고</b>입니다.
    /// 여는 시간을 없애면 아무 신호 없이 여섯 발이 나갑니다 — 플레이어에게는
    /// 그것이 "갑자기 죽었다" 가 됩니다.
    ///
    /// <b>왜 스프링이 아닌가.</b> 덮개는 끝까지 열리고 끝까지 닫혀야 하는 물건이라
    /// 넘나들면 안 됩니다. 반동과 달리 <b>목표에 정확히 서는</b> 것이 맞습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponHatch : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>움직일 덮개입니다. 비어 있으면 자기 트랜스폼입니다.</summary>
        [Header("배선")]
        [Tooltip("움직일 덮개. 비어 있으면 이 오브젝트 자신")]
        public Transform part;

        /// <summary>다 열렸을 때 닫힌 자리에서 얼마나 옮겨 가는지입니다(로컬 m).</summary>
        [Header("움직임")]
        [Tooltip("다 열렸을 때의 이동(로컬 m)")]
        public Vector3 openOffset = new Vector3(0f, 0.9f, 0f);

        /// <summary>여는 데 걸리는 시간입니다(초).</summary>
        [Tooltip("여는 시간(초)")]
        public float openTime = 0.45f;

        /// <summary>닫는 데 걸리는 시간입니다(초).</summary>
        [Tooltip("닫는 시간(초)")]
        public float closeTime = 0.7f;

        // --- Public Properties ---

        /// <summary>0 이면 닫힘, 1 이면 열림입니다.</summary>
        public float Openness { get { return openness; } }

        /// <summary>다 열렸는지입니다. 일제사는 이것을 기다립니다.</summary>
        public bool IsOpen { get { return openness >= 0.999f; } }

        /// <summary>다 닫혔는지입니다.</summary>
        public bool IsClosed { get { return openness <= 0.001f; } }

        // --- Private Member Variables ---

        /// <summary>지금 열린 정도입니다.</summary>
        private float openness;

        /// <summary>열라는 요구가 들어와 있는지입니다.</summary>
        private bool wanted;

        /// <summary>닫힌 자리입니다.</summary>
        private Vector3 home;

        // --- Public Methods ---

        /// <summary>열거나 닫으라고 알립니다. 매 프레임 불러도 됩니다.</summary>
        /// <param name="open">열어야 하면 참</param>
        public void Demand(bool open)
        {
            wanted = open;
        }

        /// <summary>한 프레임분을 진행합니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        public void Tick(float dt)
        {
            if (part == null || dt <= 0f) return;

            float rate = wanted
                ? 1f / Mathf.Max(openTime, 0.01f)
                : -1f / Mathf.Max(closeTime, 0.01f);

            float before = openness;
            openness = Mathf.Clamp01(openness + rate * dt);

            if (Mathf.Approximately(before, openness)) return;

            part.localPosition = home + openOffset * openness;
        }

        // --- Unity Event Functions ---

        private void Awake()
        {
            if (part == null) part = transform;
            home = part.localPosition;
        }

        private void OnValidate()
        {
            openTime = Mathf.Max(0.01f, openTime);
            closeTime = Mathf.Max(0.01f, closeTime);
        }
    }
}
