using UnityEngine;
using VContainer;
using VContainer.Unity;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.Composition
{
    /// <summary>
    /// 플레이어와 그 주변 — <b>상태 전환·체력·세이브 참여자</b> — 를 등록합니다.
    ///
    /// <b>여기가 타입 안전성을 지키는 자리입니다.</b> <see cref="NeedsSystem"/>의 피해 대상은
    /// 예전에 <c>public PlayerHealth healthBar</c>였고, 그 타입 덕분에 인스펙터에서
    /// 차량 내구도를 끌어다 놓는 실수가 컴파일 단계에서 막혔습니다. 이 프로젝트에서
    /// 가장 좋은 설계였는데, 그 한 줄이 Systems → Gameplay 역참조를 만들고 있었습니다.
    ///
    /// 이제 그 안전성이 <b>이 파일로 옮겨 왔습니다.</b> 아래 <see cref="playerHealth"/>는
    /// 여전히 <see cref="PlayerHealth"/> 타입이고, <see cref="GameBootstrap"/>이 그것을
    /// 니즈에 넘깁니다. 오배선은 여전히 컴파일 에러입니다.
    /// </summary>
    public class PlayerScope : MonoBehaviour, IInstaller
    {
        // --- Public Member Variables ---

        /// <summary>탑승·하차 상태 전환을 실행합니다.</summary>
        [Header("플레이어 (비워두면 씬에서 찾고 경고합니다)")]
        [Tooltip("탑승·하차 상태 전환을 실행하는 컨트롤러입니다.")]
        public PlayerModeController modeController;

        /// <summary>
        /// 플레이어 체력입니다. <b>타입이 <see cref="PlayerHealth"/>인 것이 요점입니다.</b>
        /// 이 칸에 차량 내구도를 끌어다 놓는 일은 타입 때문에 불가능합니다.
        /// </summary>
        [Tooltip("플레이어 체력입니다. 니즈 한계 초과 시 이것이 깎입니다.")]
        public PlayerHealth playerHealth;

        // --- Public Methods ---

        /// <summary>
        /// 플레이어 관련 컴포넌트와 세이브 참여자를 등록합니다.
        /// </summary>
        /// <param name="builder">등록할 컨테이너 빌더</param>
        public void Install(IContainerBuilder builder)
        {
            if (modeController == null) modeController = GameContext.Resolve<PlayerModeController>(this);
            if (playerHealth == null) playerHealth = GameContext.Resolve<PlayerHealth>(this);

            DIValidation.RequireRef(this, modeController, nameof(modeController));
            DIValidation.OptionalRef(this, playerHealth, nameof(playerHealth),
                "니즈가 한계를 넘어도 체력이 줄지 않고 이벤트만 발생합니다.");

            if (modeController != null) builder.RegisterComponent(modeController);
            if (playerHealth != null) builder.RegisterComponent(playerHealth);

            // 속도를 묻는 쪽(TerrainDetailLod)은 인터페이스만 압니다. 답하는 것은 여기서 정합니다.
            builder.Register<VehicleSpeedSource>(Lifetime.Singleton).As<ISpeedSource>();
        }
    }
}
