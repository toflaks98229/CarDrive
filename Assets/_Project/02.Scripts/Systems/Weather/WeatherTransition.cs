using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 전환이 한 걸음 나아갔을 때 벌어진 일입니다.
    /// <see cref="WeatherSystem"/>이 이 값을 보고 이벤트를 던집니다.
    /// </summary>
    public enum WeatherStep
    {
        /// <summary>아무 일도 없었습니다.</summary>
        None,

        /// <summary>새 목표를 향해 전환이 시작되었습니다.</summary>
        TransitionStarted,

        /// <summary>중간 기착지에 도착했습니다. 최종 목표까지는 더 가야 합니다.</summary>
        ReachedWaypoint,

        /// <summary>최종 목표에 도착했습니다.</summary>
        ReachedFinal
    }

    /// <summary>
    /// 날씨가 어디에서 어디로, 얼마나 왔는지를 소유합니다.
    ///
    /// <b>왜 떼어 냈는가.</b> 전환에는 값이 여덟 개나 붙어 다닙니다 —
    /// 지금 날씨, 목표, 최종 목표, 진행도, 강도 둘, 전환 시간, 경과 시간, 유지 시간.
    /// 이것들이 <see cref="WeatherSystem"/>의 필드로 흩어져 있으니, 어느 메서드가 무엇을
    /// 건드리는지 추적하기 어려웠습니다. 여덟 개를 한 상자에 담고 <b>바꾸는 방법을 세 개로</b> 줄였습니다.
    ///
    /// <b>맑음에서 폭우로 곧장 가지 않습니다.</b> 심각도 차이가 <c>maxSeverityJump</c>보다 크면
    /// 중간 날씨를 거칩니다. 하늘이 서서히 무거워지는 느낌은 여기서 나옵니다.
    /// </summary>
    public sealed class WeatherTransition
    {
        // --- Private Member Variables ---

        /// <summary>프리셋을 물어볼 곳입니다.</summary>
        private readonly WeatherCatalog _catalog;

        /// <summary>이번 전환에 걸리는 총 시간(게임 분)입니다.</summary>
        private float _transitionMinutes;

        /// <summary>이번 전환에서 지금까지 흐른 시간(게임 분)입니다.</summary>
        private float _elapsedMinutes;

        // --- Public Properties : 상태 ---

        /// <summary>지금 날씨입니다.</summary>
        public WeatherType Current { get; private set; }

        /// <summary>이번 걸음의 목표입니다. 중간 기착지일 수 있습니다.</summary>
        public WeatherType Target { get; private set; }

        /// <summary>최종적으로 가려는 날씨입니다.</summary>
        public WeatherType FinalTarget { get; private set; }

        /// <summary>전환 진행도(0~1)입니다.</summary>
        public float Blend { get; private set; }

        /// <summary>지금 날씨의 강도입니다.</summary>
        public float CurrentIntensity { get; private set; }

        /// <summary>목표 날씨의 강도입니다.</summary>
        public float TargetIntensity { get; private set; }

        /// <summary>현재 날씨를 더 유지할 시간(게임 분)입니다.</summary>
        public float HoldRemaining { get; private set; }

        /// <summary>지금 전환 중인지 여부입니다.</summary>
        public bool IsTransitioning { get { return Current != Target; } }

        // --- Constructor ---

        /// <summary>
        /// 프리셋을 물어볼 카탈로그를 받습니다.
        /// </summary>
        /// <param name="catalog">프리셋 조회에 쓸 카탈로그</param>
        public WeatherTransition(WeatherCatalog catalog)
        {
            _catalog = catalog;
        }

        // --- Public Methods : 설정 ---

        /// <summary>
        /// 전환 없이 이 날씨로 즉시 바꿉니다. 시작할 때와 세이브를 되돌릴 때 씁니다.
        /// </summary>
        /// <param name="type">자리 잡을 날씨</param>
        /// <param name="intensity">쓸 강도. 음수면 무작위로 뽑습니다.</param>
        public void SetImmediate(WeatherType type, float intensity = -1f)
        {
            Current = type;
            Target = type;
            FinalTarget = type;
            Blend = 0f;

            _transitionMinutes = 0f;
            _elapsedMinutes = 0f;

            CurrentIntensity = intensity >= 0f ? Mathf.Clamp01(intensity) : _catalog.RollIntensity(type);
            TargetIntensity = CurrentIntensity;
            HoldRemaining = _catalog.RollDuration(type);
        }

        /// <summary>
        /// 최종 목표를 정하고 그쪽으로 한 걸음 뗍니다.
        /// </summary>
        /// <param name="finalTarget">최종적으로 갈 날씨</param>
        /// <param name="maxSeverityJump">한 번에 건널 수 있는 최대 심각도 차이</param>
        /// <param name="minutesPerSeverity">심각도 1 차이에 걸리는 전환 시간(게임 분)</param>
        /// <param name="minMinutes">전환 시간의 최소값(게임 분)</param>
        /// <returns>전환이 시작되었으면 <see cref="WeatherStep.TransitionStarted"/></returns>
        public WeatherStep BeginStepToward(WeatherType finalTarget, float maxSeverityJump,
                                           float minutesPerSeverity, float minMinutes)
        {
            FinalTarget = finalTarget;
            return StepTowardFinalTarget(maxSeverityJump, minutesPerSeverity, minMinutes);
        }

        /// <summary>
        /// 세이브에서 읽은 상태를 그대로 되돌립니다.
        /// </summary>
        /// <param name="saved">되돌릴 상태</param>
        public void Restore(WeatherSave saved)
        {
            Current = saved.current;
            Target = saved.target;
            FinalTarget = saved.finalTarget;
            Blend = saved.blend;

            CurrentIntensity = saved.currentIntensity;
            TargetIntensity = saved.targetIntensity;

            _transitionMinutes = saved.transitionMinutes;
            _elapsedMinutes = saved.transitionElapsed;
            HoldRemaining = saved.holdRemaining;
        }

        /// <summary>
        /// 지금 상태를 세이브에 담습니다. 진정 시각은 선택기가 따로 담습니다.
        /// </summary>
        /// <param name="into">값을 채워 넣을 저장 항목</param>
        public void CaptureInto(WeatherSave into)
        {
            into.current = Current;
            into.target = Target;
            into.finalTarget = FinalTarget;
            into.blend = Blend;
            into.currentIntensity = CurrentIntensity;
            into.targetIntensity = TargetIntensity;
            into.transitionMinutes = _transitionMinutes;
            into.transitionElapsed = _elapsedMinutes;
            into.holdRemaining = HoldRemaining;
        }

        // --- Public Methods : 진행 ---

        /// <summary>
        /// 전환을 시간만큼 진행시킵니다. 다 오면 목표를 지금 날씨로 확정합니다.
        /// </summary>
        /// <param name="gameMinutes">흐른 게임 시간(분)</param>
        /// <param name="maxSeverityJump">한 번에 건널 수 있는 최대 심각도 차이</param>
        /// <param name="minutesPerSeverity">심각도 1 차이에 걸리는 전환 시간</param>
        /// <param name="minMinutes">전환 시간의 최소값</param>
        /// <param name="routeHoldMinutes">중간 기착지에서 잠시 머무는 시간</param>
        /// <returns>이번 걸음에 벌어진 일</returns>
        public WeatherStep Advance(float gameMinutes, float maxSeverityJump,
                                   float minutesPerSeverity, float minMinutes, float routeHoldMinutes)
        {
            _elapsedMinutes += gameMinutes;
            Blend = _transitionMinutes > 0f ? Mathf.Clamp01(_elapsedMinutes / _transitionMinutes) : 1f;

            if (Blend < 1f) return WeatherStep.None;

            Current = Target;
            CurrentIntensity = TargetIntensity;
            Blend = 0f;
            _elapsedMinutes = 0f;

            if (Current == FinalTarget)
            {
                HoldRemaining = _catalog.RollDuration(Current);
                return WeatherStep.ReachedFinal;
            }

            // 경로 중간이면 잠시 머문 뒤 계속 갑니다.
            HoldRemaining = routeHoldMinutes;
            return WeatherStep.ReachedWaypoint;
        }

        /// <summary>
        /// 유지 시간을 줄입니다. 다 되면 다음 걸음을 뗄 때입니다.
        /// </summary>
        /// <param name="gameMinutes">흐른 게임 시간(분)</param>
        /// <returns>유지 시간이 끝났으면 true</returns>
        public bool AdvanceHold(float gameMinutes)
        {
            HoldRemaining -= gameMinutes;
            return HoldRemaining <= 0f;
        }

        /// <summary>
        /// 최종 목표를 향해 한 걸음 더 뗍니다. 중간 기착지에 머물다 이어 갈 때 씁니다.
        /// </summary>
        /// <param name="maxSeverityJump">한 번에 건널 수 있는 최대 심각도 차이</param>
        /// <param name="minutesPerSeverity">심각도 1 차이에 걸리는 전환 시간</param>
        /// <param name="minMinutes">전환 시간의 최소값</param>
        /// <returns>전환이 시작되었으면 <see cref="WeatherStep.TransitionStarted"/></returns>
        public WeatherStep StepTowardFinalTarget(float maxSeverityJump,
                                                 float minutesPerSeverity, float minMinutes)
        {
            float from = _catalog.GetSeverity(Current);
            float to = _catalog.GetSeverity(FinalTarget);
            float diff = to - from;

            WeatherType next;
            if (Mathf.Abs(diff) <= maxSeverityJump)
            {
                next = FinalTarget;
            }
            else
            {
                // 한 칸만큼 이동한 심각도에 가장 가까운 날씨를 중간 기착지로 삼습니다.
                float wanted = from + Mathf.Sign(diff) * maxSeverityJump;
                next = _catalog.FindClosestSeverity(wanted, Current, FinalTarget);
            }

            // 더 갈 곳이 없으면 그냥 목표로 확정합니다.
            if (next == Current) next = FinalTarget;

            Target = next;
            TargetIntensity = _catalog.RollIntensity(next);

            // 심각도 차이가 클수록 오래 걸립니다.
            float stepDiff = Mathf.Abs(_catalog.GetSeverity(next) - from);
            _transitionMinutes = Mathf.Max(minMinutes, minutesPerSeverity * stepDiff);
            _elapsedMinutes = 0f;
            Blend = 0f;

            return WeatherStep.TransitionStarted;
        }
    }
}
