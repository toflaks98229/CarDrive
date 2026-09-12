using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 미사일 칸이 <b>하나씩 비어 갑니다.</b>
    ///
    /// <b>왜 필요한가.</b> 미사일 랙은 여섯 중 <b>유일하게 되밀리지 않는</b> 무장입니다.
    /// 반동도 회전도 없으니 쏘았다는 증거가 섬광과 소리뿐이고, 여섯 발이 0.22 초
    /// 간격으로 나가는 동안 <b>몇 발이 남았는지</b>를 말하는 것이 아무것도 없었습니다.
    /// 칸이 비어 가는 것은 <b>남은 것을 세게 하는</b> 유일한 표시입니다.
    ///
    /// <b>왜 덮개가 아니라 미사일인가.</b> 덮개(<see cref="WeaponHatch"/>)는 이미 있고,
    /// 그것이 열리는 것은 <b>예고</b>입니다. 열린 뒤에 보이는 것이 빈 콘크리트 면이면
    /// 쏘기 전과 쏜 뒤가 같습니다 — 안에 든 것이 보여야 없어지는 것도 보입니다.
    ///
    /// ⚠ <b>스스로 돌지 않습니다.</b> <see cref="RobotWeapon"/> 이 쏘는 그 프레임에
    /// <see cref="Spend"/> 를, 한 묶음이 끝나면 <see cref="Reload"/> 를 불러 줍니다.
    /// 그래야 화면의 칸과 실제 발수가 어긋나지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponCells : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>
        /// 칸에 든 것들입니다. <b>앞에서부터</b> 하나씩 사라집니다.
        ///
        /// 비어 있으면 이 부품은 아무 일도 하지 않습니다.
        /// </summary>
        [Header("배선")]
        [Tooltip("칸에 든 것들. 앞에서부터 하나씩 사라집니다")]
        public Transform[] loaded;

        /// <summary>다시 채우는 데 걸리는 시간(초)입니다.</summary>
        [Header("다시 채우기")]
        [Tooltip("한 묶음을 다 쏜 뒤 다시 채우기까지의 시간(초)")]
        [Range(0f, 30f)]
        public float refillSeconds = 8f;

        // --- Public Properties ---

        /// <summary>비어 있는 칸의 수입니다.</summary>
        public int Spent { get; private set; }

        /// <summary>남은 칸의 수입니다.</summary>
        public int Left { get { return Mathf.Max(Count - Spent, 0); } }

        /// <summary>칸의 수입니다.</summary>
        public int Count { get { return loaded != null ? loaded.Length : 0; } }

        // --- Private Member Variables ---

        private float waiting;

        // --- Unity Event Functions ---

        void OnEnable()
        {
            Fill();
        }

        // --- Public Methods ---

        /// <summary>
        /// 한 칸을 비웁니다. 쏘는 그 프레임에 부릅니다.
        ///
        /// 다 비었으면 아무 일도 하지 않습니다 — 발수가 칸보다 많은 무장이 생겨도
        /// 여기서 터지지 않아야 합니다.
        /// </summary>
        public void Spend()
        {
            if (Spent >= Count) return;

            Transform gone = loaded[Spent];
            if (gone != null) gone.gameObject.SetActive(false);

            Spent++;
        }

        /// <summary>
        /// 다시 채우기를 <b>예약</b>합니다. 한 묶음이 끝날 때 부릅니다.
        ///
        /// ⚠ <b>그 자리에서 채우지 않습니다.</b> 마지막 발의 섬광이 가시기도 전에
        /// 여섯 칸이 도로 차 있으면, 비어 가는 것을 본 적이 없는 것과 같습니다.
        /// </summary>
        public void Reload()
        {
            waiting = refillSeconds;

            // 기다릴 시간이 없으면 지금 채웁니다.
            if (waiting <= 0f) Fill();
        }

        /// <summary>
        /// 한 프레임분 셉니다.
        /// </summary>
        /// <param name="dt">지난 시간(초)</param>
        public void Tick(float dt)
        {
            if (waiting <= 0f) return;

            waiting -= dt;
            if (waiting > 0f) return;

            waiting = 0f;
            Fill();
        }

        /// <summary>모든 칸을 채웁니다.</summary>
        public void Fill()
        {
            Spent = 0;
            waiting = 0f;

            if (loaded == null) return;

            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null) loaded[i].gameObject.SetActive(true);
            }
        }
    }
}
