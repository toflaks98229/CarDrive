using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// 6종 니즈의 증가 속도와 연쇄 규칙을 담는 설정 에셋입니다.
    /// NeedsSystem에 연결하지 않으면 NeedDefaults의 기본값이 그대로 쓰입니다.
    /// (Project 창에서 우클릭 → Create → CarDrive → Needs Profile)
    /// </summary>
    [CreateAssetMenu(fileName = "NeedsProfile", menuName = "CarDrive/Needs Profile")]
    public class NeedsProfile : ScriptableObject
    {
        // --- Public Member Variables ---

        /// <summary>
        /// 니즈별 증가 속도와 문턱값입니다.
        ///
        /// 비어 있으면 <see cref="NeedDefaults"/>의 기본값이 대신 쓰입니다.
        /// 그래서 에셋을 만들어 두기만 하고 채우지 않아도 게임은 그대로 돌아갑니다.
        /// </summary>
        [Header("니즈별 설정")]
        [Tooltip("6종 니즈의 설정. 비어 있으면 기본값이 사용됩니다.")]
        public List<NeedSetting> settings = new List<NeedSetting>();

        /// <summary>
        /// 한 니즈가 나빠지면 다른 니즈도 빨리 나빠지게 하는 규칙입니다.
        ///
        /// 배가 고프면 스트레스가 더 빨리 오르는 식입니다. 이것이 있어야
        /// 니즈 여섯 개가 각자 따로 노는 계기판이 아니라 서로 얽힌 상태가 됩니다.
        /// </summary>
        [Header("니즈 간 연쇄 규칙")]
        [Tooltip("한 니즈가 나빠지면 다른 니즈도 빨리 나빠지게 하는 규칙")]
        public List<NeedCoupling> couplings = new List<NeedCoupling>();

        // --- Public Methods ---

        /// <summary>
        /// 인스펙터의 컨텍스트 메뉴에서 기본값을 채워 넣습니다.
        ///
        /// 지금 들어 있는 값을 <b>덮어씁니다.</b> 손으로 맞춰 둔 수치가 있다면 사라집니다.
        /// 빈 에셋에서 시작할 때, 또는 튜닝을 처음으로 되돌릴 때 쓰세요.
        /// </summary>
        [ContextMenu("기본값으로 채우기")]
        public void FillWithDefaults()
        {
            settings = NeedDefaults.CreateSettings();
            couplings = NeedDefaults.CreateCouplings();
            GameLog.Info(GameLog.Channel.Simulation, "NeedsProfile: 기본값으로 채웠습니다.");
        }
    }
}
