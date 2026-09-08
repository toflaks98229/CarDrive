using UnityEngine;
using VContainer;
using VContainer.Unity;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Composition
{
    /// <summary>
    /// 시간이 흐르면서 값이 변하는 것들 — <b>시계·날씨·니즈·지갑·세이브</b> — 을 등록합니다.
    ///
    /// 이 다섯이 한 설치자에 묶인 이유가 있습니다. <b>서로 시계를 공유합니다.</b>
    /// 니즈와 날씨는 <see cref="TimeSystem"/>의 배율을 따르고, 세이브는 그 셋의 상태를
    /// 정해진 순서로 되돌립니다. 함께 켜지고 함께 꺼져야 하는 묶음입니다.
    ///
    /// <b>인스펙터가 비어 있어도 동작합니다.</b> 씬을 아직 옮기지 않았을 수 있으므로,
    /// 비어 있으면 <see cref="GameContext"/>로 찾아보고 경고를 남깁니다.
    /// 경고가 보인다면 그 칸을 채우라는 뜻입니다 — 찾는 비용이 아니라 <b>배선이 코드에
    /// 드러나지 않는 것</b>이 문제이기 때문입니다.
    /// </summary>
    public class SimulationScope : MonoBehaviour, IInstaller
    {
        // --- Public Member Variables ---

        /// <summary>하루의 시간 흐름을 소유합니다. 니즈와 날씨가 이 시계를 봅니다.</summary>
        [Header("시뮬레이션 (비워두면 씬에서 찾고 경고합니다)")]
        [Tooltip("하루의 시간 흐름을 소유하는 시계입니다.")]
        public TimeSystem timeSystem;

        /// <summary>날씨의 조율자입니다. 수치만 계산하고 표현은 하지 않습니다.</summary>
        [Tooltip("날씨 수치를 계산하는 조율자입니다.")]
        public WeatherSystem weatherSystem;

        /// <summary>날씨를 눈에 보이게 만드는 표현 담당입니다.</summary>
        [Tooltip("비 파티클과 안개로 날씨를 표현하는 리그입니다.")]
        public WeatherRig weatherRig;

        /// <summary>6종 니즈의 상태를 소유합니다.</summary>
        [Tooltip("허기·갈증·피로·스트레스·배뇨·청결을 관리합니다.")]
        public NeedsSystem needsSystem;

        /// <summary>보유 재화를 소유합니다.</summary>
        [Tooltip("돈과 엑토플라즘을 관리합니다.")]
        public Wallet wallet;

        /// <summary>등록부를 순서대로 훑어 저장·복원합니다.</summary>
        [Tooltip("세이브 참여자들을 순서대로 훑는 저장 시스템입니다.")]
        public SaveSystem saveSystem;

        // --- Public Methods ---

        /// <summary>
        /// 시뮬레이션 시스템들을 컨테이너에 등록합니다.
        /// </summary>
        /// <param name="builder">등록할 컨테이너 빌더</param>
        public void Install(IContainerBuilder builder)
        {
            Resolve();

            // 없으면 게임이 성립하지 않는 것만 오류로 드러냅니다.
            // (weatherRig 는 표현 담당이라 없어도 시뮬레이션은 돕니다)
            DIValidation.RequireRef(this, timeSystem, nameof(timeSystem));
            DIValidation.RequireRef(this, weatherSystem, nameof(weatherSystem));
            DIValidation.RequireRef(this, needsSystem, nameof(needsSystem));
            DIValidation.RequireRef(this, wallet, nameof(wallet));
            DIValidation.RequireRef(this, saveSystem, nameof(saveSystem));

            DIValidation.OptionalRef(this, weatherRig, nameof(weatherRig),
                "비·안개 표현이 없습니다. 날씨 수치는 계속 계산됩니다.");

            // 구체 타입과 <b>계약</b>을 함께 등록합니다.
            //
            // 예전에는 구체 타입만 등록하고, 값을 읽고 싶은 쪽은 WeatherSystem.GetFuelConsumption()
            // 같은 정적 접근자를 불렀습니다. 그래서 소비자의 시그니처에 날씨가 나타나지 않았고,
            // 그 하나 때문에 순수 산술인 Powertrain 을 씬 없이 검증할 수 없었습니다.
            //
            // 이제 <b>무엇이 무엇을 답하는지가 여기 한 곳에 적혀 있습니다.</b> 소비자는 자기가
            // 필요한 만큼만 좁은 계약으로 받고, 그 계약을 누가 구현하는지는 알지 못합니다.
            // 나중에 날씨가 둘로 갈라지거나 시계가 다른 것으로 바뀌어도 바뀌는 곳은 이 줄들뿐입니다.
            if (timeSystem != null)
            {
                builder.RegisterComponent(timeSystem)
                       .As<IGameClock>()
                       .As<ISunSource>();
            }

            if (weatherSystem != null)
            {
                builder.RegisterComponent(weatherSystem)
                       .As<IRoadConditions>()
                       .As<IGhostActivity>()
                       .As<IExposureConditions>()
                       .As<ISkyConditions>();
            }

            if (needsSystem != null)
            {
                builder.RegisterComponent(needsSystem)
                       .As<INeedsSink>();
            }

            if (wallet != null)
            {
                builder.RegisterComponent(wallet)
                       .As<ICurrencySink>()
                       .As<ICurrencyBalance>()
                       .As<ICurrencyStore>();
            }

            if (weatherRig != null) builder.RegisterComponent(weatherRig);
            if (saveSystem != null) builder.RegisterComponent(saveSystem);
        }

        // --- Private Methods ---

        /// <summary>
        /// 인스펙터에서 비워 둔 칸을 씬에서 찾아 채웁니다.
        /// 찾았다면 <see cref="GameContext"/>가 "등록되지 않았다"는 경고를 남깁니다.
        /// </summary>
        private void Resolve()
        {
            if (timeSystem == null) timeSystem = GameContext.Resolve<TimeSystem>(this);
            if (weatherSystem == null) weatherSystem = GameContext.Resolve<WeatherSystem>(this);
            if (weatherRig == null) weatherRig = GameContext.Resolve<WeatherRig>(this);
            if (needsSystem == null) needsSystem = GameContext.Resolve<NeedsSystem>(this);
            if (wallet == null) wallet = GameContext.Resolve<Wallet>(this);
            if (saveSystem == null) saveSystem = GameContext.Resolve<SaveSystem>(this);
        }
    }
}
