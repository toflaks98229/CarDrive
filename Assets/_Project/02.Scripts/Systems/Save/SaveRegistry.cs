using System.Collections.Generic;

namespace CarDrive.Systems
{
    /// <summary>
    /// 자기 상태를 세이브에 담고 되돌릴 줄 아는 것의 규약입니다.
    ///
    /// <b>왜 만들었는가.</b> 예전에는 <see cref="SaveSystem"/>이 시스템 여섯 개를 구체 타입으로
    /// 알고 있었습니다. 그래서 시스템을 하나 늘릴 때마다 네 곳을 고쳐야 했습니다.
    /// <c>SaveData</c>에 필드, <c>Capture</c>에 한 줄, <c>Restore</c>에 한 줄,
    /// <c>ResolveReferences</c>에 한 줄. 게다가 <b>복원 순서가 메서드의 줄 순서로만</b>
    /// 표현되어 있어서, 새 시스템이 어디에 끼어야 하는지 읽어 낼 방법이 없었습니다.
    ///
    /// 이제 <b>순서를 각자가 <see cref="SaveOrder"/>로 선언</b>합니다.
    /// <see cref="SaveSystem"/>은 정렬해서 훑기만 하므로 어떤 시스템이 있는지 몰라도 됩니다.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// 담고 되돌리는 순서입니다. <b>작을수록 먼저</b>입니다.
        /// 값은 <see cref="SaveOrders"/>에 모아 두었습니다. 거기서 골라 쓰세요.
        ///
        /// 순서가 중요한 이유가 있습니다. 시계를 먼저 맞춰야 니즈와 날씨가 옳은 시각을 보고 움직입니다.
        /// </summary>
        int SaveOrder { get; }

        /// <summary>
        /// 자기 상태를 세이브 자료에 적습니다.
        /// </summary>
        /// <param name="data">적어 넣을 세이브 자료</param>
        void CaptureInto(SaveData data);

        /// <summary>
        /// 세이브 자료에서 자기 상태를 되돌립니다.
        /// </summary>
        /// <param name="data">읽어 올 세이브 자료</param>
        void RestoreFrom(SaveData data);
    }

    /// <summary>
    /// 세이브 순서를 한곳에 모아 둡니다.
    ///
    /// 숫자를 열 단위로 띄워 둔 것은 <b>사이에 끼워 넣을 자리를 남기기 위해서</b>입니다.
    /// 나중에 날씨와 니즈 사이에 무언가 들어가야 하면 25를 쓰면 됩니다.
    /// </summary>
    public static class SaveOrders
    {
        /// <summary>시계가 가장 먼저입니다. 니즈와 날씨가 이 시각을 보고 움직입니다.</summary>
        public const int Time = 10;

        /// <summary>날씨는 시계 다음입니다. 전환 진행도가 시각에 묶여 있습니다.</summary>
        public const int Weather = 20;

        /// <summary>니즈입니다.</summary>
        public const int Needs = 30;

        /// <summary>지갑입니다. 다른 것에 기대지 않아 마지막이어도 됩니다.</summary>
        public const int Wallet = 40;
    }

    /// <summary>
    /// 세이브에 참여하는 것들의 등록부입니다.
    ///
    /// <see cref="Gameplay.Vehicle"/>의 등록부와 같은 방식입니다. 스스로 넣고 스스로 빠집니다.
    /// 등록은 <c>Awake</c>, 해제는 <c>OnDestroy</c>에서 하세요.
    /// </summary>
    public static class SaveRegistry
    {
        // --- Private Member Variables ---

        /// <summary>등록된 것들입니다. 순서는 보장하지 않습니다. 꺼낼 때 정렬합니다.</summary>
        private static readonly List<ISaveable> saveables = new List<ISaveable>();

        /// <summary><see cref="GetOrdered"/>가 돌려줄 정렬된 목록입니다. 매번 새로 만들지 않습니다.</summary>
        private static readonly List<ISaveable> ordered = new List<ISaveable>();

        /// <summary>정렬 기준입니다. 델리게이트를 매번 새로 만들지 않도록 들고 있습니다.</summary>
        private static readonly System.Comparison<ISaveable> ByOrder = CompareByOrder;

        // --- Public Methods ---

        /// <summary>
        /// 세이브 참여자를 등록합니다. <c>Awake</c>에서 부르세요.
        /// </summary>
        /// <param name="saveable">등록할 대상. 이미 등록되어 있으면 아무 일도 하지 않습니다.</param>
        public static void Register(ISaveable saveable)
        {
            if (saveable == null || saveables.Contains(saveable)) return;
            saveables.Add(saveable);
        }

        /// <summary>
        /// 등록을 해제합니다. <c>OnDestroy</c>에서 부르세요.
        /// </summary>
        /// <param name="saveable">해제할 대상</param>
        public static void Unregister(ISaveable saveable)
        {
            if (saveable == null) return;
            saveables.Remove(saveable);
        }

        /// <summary>
        /// 등록된 것들을 <see cref="ISaveable.SaveOrder"/> 오름차순으로 돌려줍니다.
        ///
        /// <b>돌려주는 목록을 들고 있지 마세요.</b> 다음 호출 때 같은 목록을 다시 씁니다.
        /// 그 자리에서 훑고 버리라고 만든 것입니다.
        /// </summary>
        /// <returns>순서대로 정렬된 참여자 목록</returns>
        public static List<ISaveable> GetOrdered()
        {
            ordered.Clear();

            for (int i = 0; i < saveables.Count; i++)
            {
                ISaveable saveable = saveables[i];
                if (saveable == null) continue;

                // <b>인터페이스 참조로는 파괴 여부를 알 수 없습니다.</b>
                // Unity 의 "파괴됨"은 Object 의 == 연산자가 판단하는데, ISaveable 로 들고 있으면
                // 그 연산자를 타지 않습니다. 그래서 Object 로 되돌려 확인합니다.
                // (ReferenceEquals 가 false 여야 진짜 Unity 객체이고, 그때만 == null 이 의미를 가집니다)
                UnityEngine.Object unityObject = saveable as UnityEngine.Object;
                if (!ReferenceEquals(unityObject, null) && unityObject == null) continue;

                ordered.Add(saveable);
            }

            ordered.Sort(ByOrder);
            return ordered;
        }

        /// <summary>등록을 모두 비웁니다. 씬을 다시 불러들일 때처럼 상태가 꼬였을 때 씁니다.</summary>
        public static void Clear()
        {
            saveables.Clear();
            ordered.Clear();
        }

        // --- Private Methods ---

        /// <summary>
        /// 순서 값으로 비교합니다.
        /// </summary>
        /// <param name="a">앞쪽</param>
        /// <param name="b">뒤쪽</param>
        /// <returns>a가 먼저면 음수</returns>
        private static int CompareByOrder(ISaveable a, ISaveable b)
        {
            return a.SaveOrder.CompareTo(b.SaveOrder);
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 정적 상태를 비웁니다.
        /// 에디터에서 도메인 리로드를 꺼 두면 static 값이 지난 실행에서 그대로 남기 때문입니다.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            saveables.Clear();
            ordered.Clear();
        }
    }
}
