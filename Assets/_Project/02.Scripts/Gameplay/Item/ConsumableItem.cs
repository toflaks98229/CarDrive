using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 들어서 먹거나 마실 수 있는 물건의 공통 부분입니다.
    ///
    /// <b>왜 base 클래스인가.</b> 음료와 음식은 <b>같은 방식으로</b> 소비됩니다 —
    /// 조준해 상호작용하면 물건이 사라지고, 니즈가 바뀌고, 연출이 돌고, 다 쓴 껍데기가
    /// 던져집니다. 다른 것은 <b>무엇이 얼마나 바뀌는가</b>뿐입니다.
    /// 그 차이는 데이터이므로, 절차는 여기 한 번만 적습니다.
    ///
    /// <b>효과는 물건이 갖습니다.</b> 예전에는 소비하는 쪽(<see cref="BeverageConsumer"/>)이
    /// "갈증 0.5, 배뇨 0.25"를 들고 있었습니다. 그러면 물이든 맥주든 콜라든 똑같이 반응합니다.
    /// 이제 각 물건이 자기 효과를 갖고, 소비하는 쪽은 그것을 그대로 적용합니다.
    /// </summary>
    public abstract class ConsumableItem : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        [Header("표시")]
        /// <summary>
        /// 조준했을 때 표시할 문구입니다.
        ///
        /// <b>비워 두면 물건 종류가 정합니다.</b> 음료는 "마시기", 음식은 "먹기"입니다.
        /// 이 필드가 없던 시절에 만들어진 프리팹은 값을 직렬화하고 있지 않은데,
        /// 그것들이 갑자기 엉뚱한 문구를 띄우지 않도록 빈 값을 기본으로 둡니다.
        /// </summary>
        [Tooltip("조준했을 때 표시할 문구. 비워 두면 물건 종류에 맞는 기본 문구가 나옵니다.")]
        public string promptLabel = "";

        [Header("효과")]
        /// <summary>
        /// 이 물건이 니즈에 주는 영향입니다.
        ///
        /// <b>비워 두면 소비하는 쪽의 기본값이 쓰입니다.</b> 예전 방식으로 배선된 프리팹이
        /// 그대로 돌아가게 하기 위해서입니다. 새로 만드는 물건은 여기에 적으세요.
        /// </summary>
        [Tooltip("니즈에 주는 영향. 비워 두면 소비하는 쪽의 기본값이 쓰입니다.")]
        public List<NeedEffect> effects = new List<NeedEffect>();

        /// <summary>이 물건이 회복시키는 체력입니다. 음수면 오히려 상합니다.</summary>
        [Tooltip("회복시킬 체력. 음수면 오히려 상합니다.")]
        public float healAmount = 0f;

        [Header("시간")]
        /// <summary>
        /// 소비에 걸리는 시간(초)입니다. 0 이하면 소비하는 쪽의 기본값을 씁니다.
        /// 연출이 붙어 있으면 그 길이가 우선합니다.
        /// </summary>
        [Tooltip("소비에 걸리는 시간(초). 0 이하면 소비하는 쪽의 기본값을 씁니다.")]
        public float consumeSeconds = 0f;

        [Header("다 쓴 뒤")]
        /// <summary>
        /// 다 쓰고 <b>껍데기가 남는지</b> 여부입니다.
        ///
        /// 병과 캔은 다 마셔도 물건이 남아 손에 들려 있으므로 던져야 합니다.
        /// 빵과 초코바는 <b>남는 것이 없습니다</b> — 그런데도 던지면 먹은 빵이
        /// 창밖으로 날아갑니다.
        ///
        /// 켜 두는 것이 기본입니다. 이 항목이 없던 시절에 만들어진 병들이
        /// 그대로 던져지도록 하기 위해서입니다.
        /// </summary>
        [Tooltip("체크하면 다 쓴 껍데기가 남아 던져집니다. 병·캔처럼 실제로 남는 것이 있을 때만 켜세요. " +
                 "빵처럼 남는 것이 없으면 꺼야 먹은 것이 날아가지 않습니다.")]
        public bool leavesEmpty = true;

        // --- Public Properties ---

        /// <summary>이 물건이 자기 효과를 갖고 있는지 여부입니다.</summary>
        public bool HasOwnEffects { get { return effects != null && effects.Count > 0; } }

        /// <summary>
        /// 이것이 <b>마시는</b> 동작인지 여부입니다.
        ///
        /// 마시는 연출(화면에 병이 올라오는 그림)과 마시는 소리는 음료에만 어울립니다.
        /// 빵을 먹는데 병이 올라오면 곤란하므로, 소비하는 쪽이 이 표식을 보고 가릅니다.
        /// UI 사정이 아니라 <b>물건의 성질</b>이라 여기 둡니다.
        /// </summary>
        public virtual bool IsDrink { get { return false; } }

        // --- Private Member Variables ---

        /// <summary>
        /// 이 물건을 소비해 줄 쪽입니다. 물건마다 찾으면 낭비라 한 번만 찾아 나눠 씁니다.
        /// </summary>
        private static BeverageConsumer consumer;

        // --- IInteractable ---

        /// <summary>소비할 수 있는 상태인지 확인합니다.</summary>
        /// <returns>소비하는 쪽이 준비되었고 손이 비어 있으면 true</returns>
        public virtual bool CanInteract()
        {
            BeverageConsumer c = ResolveConsumer();
            if (c == null || c.IsBusy) return false;

            return IsReachable();
        }

        /// <summary>조준했을 때 보여 줄 문구입니다.</summary>
        /// <returns>인스펙터에 적어 둔 문구. 비어 있으면 물건 종류의 기본 문구</returns>
        public virtual string GetInteractionLabel()
        {
            return string.IsNullOrEmpty(promptLabel) ? DefaultPromptLabel : promptLabel;
        }

        /// <summary>
        /// 인스펙터를 비워 두었을 때 쓸 문구입니다. 물건 종류가 정합니다.
        /// </summary>
        protected virtual string DefaultPromptLabel { get { return "쓰기"; } }

        /// <summary>소비합니다.</summary>
        public virtual void Interact()
        {
            BeverageConsumer c = ResolveConsumer();
            if (c == null) return;

            c.Consume(this);
        }

        // --- Public Methods ---

        /// <summary>
        /// 소비가 시작될 때 소비하는 쪽이 부릅니다. 물건이 어딘가에 매여 있다면 여기서 풉니다.
        /// </summary>
        public virtual void OnConsumeStarted()
        {
        }

        // --- Protected Methods ---

        /// <summary>
        /// 지금 손이 닿는 자리에 있는지 확인합니다.
        ///
        /// 차 안에 실린 물건은 <b>그 차에 타고 있을 때만</b> 잡힙니다.
        /// 창밖에서 남의 차 안의 물건을 집는 일을 막습니다.
        /// </summary>
        /// <returns>닿을 수 있으면 true</returns>
        protected virtual bool IsReachable()
        {
            Vehicle vehicle = GetComponentInParent<Vehicle>();
            return vehicle == null || vehicle.IsOccupied;
        }

        // --- Private Methods ---

        /// <summary>소비해 줄 쪽을 찾습니다. 한 번 찾으면 기억합니다.</summary>
        /// <returns>소비 담당. 없으면 null</returns>
        private static BeverageConsumer ResolveConsumer()
        {
            if (consumer != null) return consumer;

            // 정적 메서드라 넘길 대상이 없습니다. 로그에 붙일 문맥이 없을 뿐 동작은 같습니다.
            consumer = GameContext.Resolve<BeverageConsumer>(null);
            return consumer;
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 기억해 둔 것을 비웁니다.
        /// 도메인 리로드를 꺼 두면 지난 실행의 참조가 그대로 남습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            consumer = null;
        }
    }
}
