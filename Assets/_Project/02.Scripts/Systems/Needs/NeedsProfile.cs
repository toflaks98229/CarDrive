using System.Collections.Generic;
using UnityEngine;

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
        [Header("니즈별 설정")]
        [Tooltip("6종 니즈의 설정. 비어 있으면 기본값이 사용됩니다.")]
        public List<NeedSetting> settings = new List<NeedSetting>();

        [Header("니즈 간 연쇄 규칙")]
        [Tooltip("한 니즈가 나빠지면 다른 니즈도 빨리 나빠지게 하는 규칙")]
        public List<NeedCoupling> couplings = new List<NeedCoupling>();

        /// <summary>
        /// 인스펙터의 컨텍스트 메뉴에서 기본값을 채워 넣습니다.
        /// </summary>
        [ContextMenu("기본값으로 채우기")]
        public void FillWithDefaults()
        {
            settings = NeedDefaults.CreateSettings();
            couplings = NeedDefaults.CreateCouplings();
            Debug.Log("NeedsProfile: 기본값으로 채웠습니다.");
        }
    }
}
