using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// "이 종류는 이렇게 동작한다"를 적어 둔 설정 한 줄입니다.
    /// <see cref="DefinitionTable{TKey,TSetting,TState}"/>이 이 둘만 보고 표를 짭니다.
    /// </summary>
    /// <typeparam name="TKey">설정을 구분하는 열거형 (NeedType, CurrencyType 등)</typeparam>
    public interface IDefinition<TKey>
    {
        /// <summary>이 설정이 어떤 종류에 대한 것인지입니다.</summary>
        TKey Key { get; }

        /// <summary>빠진 항목을 메웠다고 알릴 때 쓸 이름입니다.</summary>
        string DisplayName { get; }
    }

    /// <summary>
    /// 실행 중에 값이 변하는 상태 한 줄입니다.
    ///
    /// <c>TSelf</c>로 자기 자신을 가리키게 해서, 복제와 복사가 <b>같은 타입끼리만</b>
    /// 일어나도록 컴파일러가 지켜 줍니다.
    /// </summary>
    /// <typeparam name="TKey">상태를 구분하는 열거형</typeparam>
    /// <typeparam name="TSelf">자기 자신의 타입</typeparam>
    public interface IDefinitionState<TKey, TSelf>
    {
        /// <summary>이 상태가 어떤 종류에 대한 것인지입니다.</summary>
        TKey Key { get; }

        /// <summary>세이브에 담을 사본을 만듭니다.</summary>
        /// <returns>값이 같은 새 인스턴스</returns>
        TSelf Clone();

        /// <summary>
        /// 다른 상태의 값을 받아 옵니다. <b>키는 옮기지 않습니다.</b>
        /// 받는 쪽은 이미 제자리에 있는 것이고, 옮길 것은 값뿐이기 때문입니다.
        /// </summary>
        /// <param name="other">값을 가져올 상태</param>
        void CopyFrom(TSelf other);
    }

    /// <summary>
    /// 설정과 상태를 짝지어 관리하는 표입니다.
    ///
    /// <b>왜 만들었는가.</b> <see cref="Systems.NeedsSystem"/>과 <see cref="Systems.Wallet"/>이
    /// 똑같은 골격을 각자 손으로 쓰고 있었습니다.
    ///  1. 작성된 설정이 있으면 <b>복사본</b>을 만든다 (원본이 에셋이라 건드리면 안 됩니다)
    ///  2. 빠진 항목을 기본값으로 메우고 경고를 남긴다
    ///  3. 설정마다 상태를 하나씩 만든다 (인스펙터에 이미 적어 둔 것은 존중)
    ///  4. 조회용 딕셔너리를 짓는다
    ///  5. 세이브를 위해 상태를 담고 되돌린다
    ///
    /// 두 번은 우연일 수 있지만 <b>세 번째 복사본이 나올 자리</b>였습니다. 그때는 형태를
    /// 코드로 굳혀야 합니다. 이제 새 시스템은 이 표를 하나 들고 <see cref="Build"/>만 부르면 됩니다.
    ///
    /// <b>상태 목록은 넘겨받은 것을 그대로 씁니다.</b> 복사하지 않습니다.
    /// 그래야 <c>[SerializeField]</c>로 인스펙터에 보이는 그 리스트가 실행 중에도 살아 있어서,
    /// 플레이 중에 값을 눈으로 보고 손으로 고칠 수 있습니다. 원래 그러라고 공개해 둔 것입니다.
    /// </summary>
    /// <typeparam name="TKey">설정과 상태를 구분하는 열거형</typeparam>
    /// <typeparam name="TSetting">설정 한 줄의 타입</typeparam>
    /// <typeparam name="TState">상태 한 줄의 타입</typeparam>
    public class DefinitionTable<TKey, TSetting, TState>
        where TSetting : class, IDefinition<TKey>
        where TState : class, IDefinitionState<TKey, TState>
    {
        // --- Private Member Variables ---

        /// <summary>종류로 설정을 찾는 표입니다.</summary>
        private readonly Dictionary<TKey, TSetting> settingLookup = new Dictionary<TKey, TSetting>();

        /// <summary>종류로 상태를 찾는 표입니다.</summary>
        private readonly Dictionary<TKey, TState> stateLookup = new Dictionary<TKey, TState>();

        /// <summary>이번 실행에 쓸 설정 목록입니다.</summary>
        private List<TSetting> settings;

        /// <summary>실행 중 상태 목록입니다. 소유자의 것을 그대로 가리킵니다.</summary>
        private List<TState> states;

        // --- Public Properties ---

        /// <summary>이번 실행에 쓸 설정 목록입니다. 빠진 항목이 메워진 뒤의 것입니다.</summary>
        public List<TSetting> Settings { get { return settings; } }

        /// <summary>실행 중 상태 목록입니다.</summary>
        public List<TState> States { get { return states; } }

        // --- Public Methods ---

        /// <summary>
        /// 표를 짭니다. 소유자의 <c>Awake</c>에서 한 번 부르세요.
        /// </summary>
        /// <param name="authored">인스펙터나 에셋에 적어 둔 설정. 비어 있으면 기본값만 씁니다.</param>
        /// <param name="fallback">기본값 설정. 빠진 항목을 메우는 데도 씁니다.</param>
        /// <param name="stateList">상태를 담을 목록. <b>소유자의 직렬화 리스트를 그대로 넘기세요.</b></param>
        /// <param name="createState">설정 하나로 새 상태를 만드는 방법. 시작값이 시스템마다 달라 넘겨받습니다.</param>
        /// <param name="ownerName">경고에 남길 소유자 이름</param>
        /// <param name="context">경고를 클릭했을 때 선택될 대상</param>
        public void Build(List<TSetting> authored,
                          List<TSetting> fallback,
                          List<TState> stateList,
                          System.Func<TSetting, TState> createState,
                          string ownerName,
                          Object context)
        {
            // 1. 설정을 정합니다.
            //
            // <b>반드시 복사본을 만듭니다.</b> 작성된 목록을 그대로 들고 있으면, 아래에서 빠진 항목을
            // 메울 때 그 원본이 함께 늘어납니다. 원본이 ScriptableObject 에셋이면
            // 에디터에서는 그 변경이 디스크까지 저장됩니다.
            bool hasAuthored = authored != null && authored.Count > 0;
            settings = hasAuthored ? new List<TSetting>(authored) : fallback;

            settingLookup.Clear();
            for (int i = 0; i < settings.Count; i++)
            {
                settingLookup[settings[i].Key] = settings[i];
            }

            // 2. 빠진 항목을 기본값으로 메웁니다.
            //    기본값을 그대로 쓴 경우에는 이미 전부 들어 있으므로 건너뜁니다.
            if (hasAuthored && fallback != null)
            {
                for (int i = 0; i < fallback.Count; i++)
                {
                    if (settingLookup.ContainsKey(fallback[i].Key)) continue;

                    Debug.LogWarning(ownerName + ": 설정에 " + fallback[i].DisplayName +
                                     "이(가) 없어 기본값을 사용합니다.", context);
                    settings.Add(fallback[i]);
                    settingLookup[fallback[i].Key] = fallback[i];
                }
            }

            // 3. 상태를 맞춥니다. 인스펙터에 미리 적어 둔 값은 그대로 둡니다.
            states = stateList != null ? stateList : new List<TState>();

            stateLookup.Clear();
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] == null) continue;
                stateLookup[states[i].Key] = states[i];
            }

            for (int i = 0; i < settings.Count; i++)
            {
                if (stateLookup.ContainsKey(settings[i].Key)) continue;

                TState created = createState(settings[i]);
                if (created == null) continue;

                states.Add(created);
                stateLookup[created.Key] = created;
            }
        }

        /// <summary>
        /// 종류에 해당하는 설정을 돌려줍니다.
        /// </summary>
        /// <param name="key">찾을 종류</param>
        /// <returns>설정. 없으면 null입니다.</returns>
        public TSetting GetSetting(TKey key)
        {
            TSetting setting;
            return settingLookup.TryGetValue(key, out setting) ? setting : null;
        }

        /// <summary>
        /// 종류에 해당하는 상태를 돌려줍니다.
        /// </summary>
        /// <param name="key">찾을 종류</param>
        /// <returns>상태. 없으면 null입니다.</returns>
        public TState GetState(TKey key)
        {
            TState state;
            return stateLookup.TryGetValue(key, out state) ? state : null;
        }

        /// <summary>
        /// 지금 상태를 사본으로 담습니다. 세이브에 씁니다.
        /// </summary>
        /// <returns>상태 사본 목록. 원본과 이어져 있지 않습니다.</returns>
        public List<TState> Capture()
        {
            List<TState> copy = new List<TState>(states.Count);
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] == null) continue;
                copy.Add(states[i].Clone());
            }
            return copy;
        }

        /// <summary>
        /// 담아 둔 상태를 되돌립니다.
        ///
        /// <b>목록에 없는 종류는 건너뜁니다.</b> 예전 세이브에 지금은 사라진 종류가 들어 있어도
        /// 불러오기가 실패하지 않아야 하기 때문입니다.
        /// </summary>
        /// <param name="saved">되돌릴 상태 목록. null이면 아무것도 하지 않습니다.</param>
        public void Restore(List<TState> saved)
        {
            if (saved == null) return;

            for (int i = 0; i < saved.Count; i++)
            {
                if (saved[i] == null) continue;

                TState target = GetState(saved[i].Key);
                if (target == null) continue;

                target.CopyFrom(saved[i]);
            }
        }
    }
}
