using UnityEngine;
using VContainer;
using VContainer.Unity;
using CarDrive.Common;

namespace CarDrive.Composition
{
    /// <summary>
    /// 이 게임의 <b>조립 루트</b>입니다. 무엇이 존재하고 무엇이 무엇을 아는지가 여기서 정해집니다.
    ///
    /// <b>왜 하나뿐인가.</b> SCPPJ 는 루트 스코프와 씬 스코프를 나눕니다. 스테이지가 여럿이고
    /// 씬을 전환하기 때문입니다. 이 게임은 <b>씬을 다시 불러들이지 않습니다</b> —
    /// 월드가 파괴되지 않고 시드로 고정되는 것이 설계의 전제입니다.
    /// (<c>WorldStreamer</c> 주석과 <c>SaveData</c> 에 지형이 없는 이유를 보세요)
    /// 그래서 스코프를 둘로 나누면 <b>지금은 쓰이지 않는 경계</b>가 하나 늘 뿐입니다.
    /// 스테이지 전환이 실제로 생기면 그때 나누세요 — 설치자는 이미 갈라져 있어 옮기기만 하면 됩니다.
    ///
    /// <b>실행 순서를 -1000 으로 둡니다.</b> 컨테이너 조립과 <see cref="GameBootstrap"/> 의
    /// 연결이 다른 어떤 <c>Awake</c> 보다도 먼저 끝나야, 그 뒤의 <c>Start</c> 들이
    /// 이미 이어진 상태를 보게 됩니다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class CarDriveLifetimeScope : LifetimeScope
    {
        // --- Private Member Variables : 조립 감시 ---

        /// <summary>이번 플레이 세션에서 이 스코프가 조립된 횟수입니다.</summary>
        private static int assembleCount;

        // --- Public Member Variables ---

        /// <summary>지형·시야·풀 구동체를 확보하는 설치자입니다.</summary>
        [Header("설치자 (Installers)")]
        [Tooltip("지형·시야·풀 구동체를 하나씩만 확보합니다.")]
        public WorldRuntimeInstaller worldRuntime;

        /// <summary>시계·날씨·니즈·지갑·세이브를 등록하는 설치자입니다.</summary>
        [Tooltip("시계·날씨·니즈·지갑·세이브를 등록합니다.")]
        public SimulationScope simulation;

        /// <summary>플레이어와 그 주변을 등록하는 설치자입니다.</summary>
        [Tooltip("플레이어 상태 전환·체력·속도원을 등록합니다.")]
        public PlayerScope player;

        // --- Protected Methods ---

        /// <summary>
        /// 설치자들을 차례로 실행하고 진입점을 등록합니다.
        /// </summary>
        /// <param name="builder">의존성을 등록할 컨테이너 빌더</param>
        protected override void Configure(IContainerBuilder builder)
        {
            // 이 스코프는 플레이 세션당 한 번만 조립되어야 합니다.
            // 두 번째 조립은 루트가 둘이라는 뜻이고, 그러면 첫 번째가 만든 구동체와 연결이
            // 통째로 버려집니다. 조용히 지나가면 원인을 찾기가 매우 어렵습니다.
            assembleCount++;
            if (assembleCount > 1)
            {
                GameLog.Error(GameLog.Channel.Core,
                    "[CarDriveLifetimeScope] 루트 스코프가 이번 세션에서 " + assembleCount +
                    "번째로 조립되었습니다. 씬에 이 컴포넌트가 둘 이상 있는지 확인하세요.", this);
            }

            // 설치자는 자기가 없어도 게임이 뜨도록 각자 확인합니다.
            // 여기서는 순서만 정합니다 — 월드 구동체가 먼저 서고, 그 위에 시뮬레이션과 플레이어가 붙습니다.
            if (worldRuntime != null) worldRuntime.Install(builder);
            else MissingInstaller(nameof(worldRuntime));

            if (simulation != null) simulation.Install(builder);
            else MissingInstaller(nameof(simulation));

            if (player != null) player.Install(builder);
            else MissingInstaller(nameof(player));

            // 연결은 전부 여기 하나가 합니다.
            builder.RegisterEntryPoint<GameBootstrap>();
        }

        /// <summary>파괴될 때 조립 횟수를 되돌려, 도메인 리로드를 꺼 둔 에디터에서 오탐이 쌓이지 않게 합니다.</summary>
        protected override void OnDestroy()
        {
            if (assembleCount > 0) assembleCount--;
            base.OnDestroy();
        }

        // --- Private Methods ---

        /// <summary>
        /// 설치자가 연결되지 않았음을 알립니다.
        /// </summary>
        /// <param name="fieldName">비어 있는 설치자 필드의 이름</param>
        private void MissingInstaller(string fieldName)
        {
            GameLog.Error(GameLog.Channel.Core,
                "[CarDriveLifetimeScope] 설치자 '" + fieldName + "'가 연결되지 않아 그 묶음이 등록되지 않았습니다. " +
                "CarDrive > Composition > 조립 루트 만들기 를 실행하면 자동으로 이어집니다.", this);
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 조립 횟수를 비웁니다.
        /// 도메인 리로드를 꺼 두면 지난 실행의 값이 그대로 남습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            assembleCount = 0;
        }
    }
}
