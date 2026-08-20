using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 출력(0~1)에 따라 물줄기가 어떻게 보일지 정하는 범위입니다.
    ///
    /// <b>직렬화되지 않습니다.</b> 값은 <see cref="UrineRelief"/>가 자기 인스펙터 필드에서
    /// 만들어 넘깁니다. 여기에 <c>[SerializeField]</c>를 붙여 값을 옮기면 직렬화 경로가 바뀌어
    /// <b>씬에 맞춰 둔 수치가 전부 초기화됩니다.</b>
    /// </summary>
    public struct StreamEmissionRange
    {
        /// <summary>출력이 최소일 때 초당 방출 입자 수입니다.</summary>
        public float MinRate;

        /// <summary>출력이 최대일 때 초당 방출 입자 수입니다.</summary>
        public float MaxRate;

        /// <summary>출력이 최소일 때의 입자 속도입니다.</summary>
        public float MinSpeed;

        /// <summary>출력이 최대일 때의 입자 속도입니다.</summary>
        public float MaxSpeed;

        /// <summary>출력이 최소일 때의 입자 수명입니다.</summary>
        public float MinLifetime;

        /// <summary>출력이 최대일 때의 입자 수명입니다.</summary>
        public float MaxLifetime;
    }

    /// <summary>
    /// 물줄기를 <b>보여 주는</b> 일만 맡습니다. 파티클 모듈 설정, 노즐 각도, 방출량이 전부입니다.
    ///
    /// <b>왜 떼어 냈는가.</b> <see cref="UrineRelief"/>는 압력·잔뇨·역류를 계산하고 니즈에 반영하는
    /// 시뮬레이션인데, 그 안에 <c>ParticleSystem.MainModule</c>·<c>EmissionModule</c>·
    /// <c>ShapeModule</c>을 만지는 코드가 섞여 있었습니다. 그래서
    ///  - 물의 <b>양</b>을 고치려는 사람이 파티클 API를 함께 읽어야 했고,
    ///  - 연출을 바꾸려는 사람이 니즈 계산을 건드릴 위험이 있었으며,
    ///  - 계산이 옳은지 확인하려면 파티클이 있는 씬이 필요했습니다.
    ///
    /// 이제 경계는 숫자 둘입니다 — <b>출력(0~1)과 노즐 각도(도).</b>
    /// 이 클래스는 그 둘을 받아 그림으로 바꿀 뿐, 배뇨가 무엇인지 모릅니다.
    /// 나중에 파티클 대신 셰이더나 메시로 바꿔도 <see cref="UrineRelief"/>는 그대로입니다.
    /// </summary>
    public sealed class UrineStreamView
    {
        // --- Constants ---

        /// <summary>이보다 작은 누적 방출량은 다음 프레임으로 넘깁니다.</summary>
        private const int MinEmitCount = 1;

        // --- Private Member Variables ---

        /// <summary>물줄기를 그리는 파티클입니다.</summary>
        private ParticleSystem _stream;

        /// <summary>출력에 따라 보간할 방출 범위입니다.</summary>
        private StreamEmissionRange _range;

        /// <summary>
        /// 한 입자 미만의 방출량을 다음 프레임으로 넘기기 위한 누적값입니다.
        /// 이것이 없으면 초당 16발 같은 낮은 출력에서 방출이 뚝뚝 끊깁니다.
        /// </summary>
        private float _emitAccumulator;

        /// <summary>씬에 배치된 각도입니다. "정면을 볼 때 노즐이 숙이는 정도"로 삼습니다.</summary>
        private float _basePitch;

        // --- Public Properties ---

        /// <summary>그릴 파티클이 준비되었는지 여부입니다.</summary>
        public bool IsReady { get { return _stream != null; } }

        /// <summary>
        /// 씬 배치에서 읽은 기본 숙임 각도(도)입니다.
        /// 조준 각도를 계산하는 쪽이 이 값을 기준으로 삼습니다.
        /// </summary>
        public float BasePitch { get { return _basePitch; } }

        // --- Public Methods ---

        /// <summary>
        /// 파티클을 받아 물줄기 모양을 확정합니다. <c>Start</c>에서 한 번 부르세요.
        /// </summary>
        /// <param name="stream">물줄기를 그릴 파티클. null이면 준비되지 않은 상태로 남습니다.</param>
        /// <param name="coneAngle">퍼지는 원뿔 각도(도). 작을수록 일직선에 가깝습니다.</param>
        /// <param name="range">출력에 따라 보간할 방출 범위</param>
        /// <returns>준비되었으면 true</returns>
        public bool Configure(ParticleSystem stream, float coneAngle, StreamEmissionRange range)
        {
            _stream = stream;
            _range = range;
            _emitAccumulator = 0f;

            if (_stream == null) return false;

            // 방출은 이쪽이 프레임마다 직접 하므로 Emission 모듈은 꺼야 합니다.
            // 켜 두면 파티클이 자기 주기로도 뿜어 이중으로 나옵니다.
            ParticleSystem.EmissionModule emission = _stream.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = _stream.shape;
            shape.angle = coneAngle;

            // <b>시뮬레이션은 반드시 월드 공간이어야 합니다.</b>
            // 로컬 공간이면 (1) 조준하려고 노즐을 돌리는 순간 이미 날아간 입자까지 함께 끌려가고,
            // (2) 중력 방향도 노즐을 따라 회전해서 위로 쏴도 땅으로 떨어지지 않습니다.
            ParticleSystem.MainModule main = _stream.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            _basePitch = NormalizeAngle(_stream.transform.localEulerAngles.x);
            return true;
        }

        /// <summary>
        /// 노즐을 지정한 각도로 돌립니다.
        /// </summary>
        /// <param name="pitch">노즐의 X축 로컬 각도(도). 양수면 아래를 향합니다.</param>
        public void SetPitch(float pitch)
        {
            if (_stream == null) return;
            _stream.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>
        /// 이번 프레임의 물줄기를 뿜습니다. 출력이 셀수록 빠르고 굵고 멀리 갑니다.
        /// </summary>
        /// <param name="flow">출력 비율(0~1)</param>
        /// <param name="deltaTime">이번 프레임의 시간</param>
        public void Emit(float flow, float deltaTime)
        {
            if (_stream == null) return;

            ParticleSystem.MainModule main = _stream.main;
            main.startSpeed = Mathf.Lerp(_range.MinSpeed, _range.MaxSpeed, flow);
            main.startLifetime = Mathf.Lerp(_range.MinLifetime, _range.MaxLifetime, flow);

            // 소수점 이하 방출량은 누적해 두었다가 1이 넘을 때 내보냅니다.
            float rate = Mathf.Lerp(_range.MinRate, _range.MaxRate, flow);
            _emitAccumulator += rate * deltaTime;

            int count = Mathf.FloorToInt(_emitAccumulator);
            if (count < MinEmitCount) return;

            _emitAccumulator -= count;
            _stream.Emit(count);
        }

        /// <summary>
        /// 방출 누적을 비웁니다. 줄기가 멈출 때 부르세요.
        /// 남겨 두면 다시 시작하는 순간 밀렸던 입자가 한꺼번에 튀어나옵니다.
        /// </summary>
        public void ResetEmission()
        {
            _emitAccumulator = 0f;
        }

        // --- Private Methods ---

        /// <summary>
        /// 0~360으로 들어오는 각도를 -180~180으로 맞춥니다.
        /// </summary>
        /// <param name="degrees">맞출 각도</param>
        /// <returns>-180에서 180 사이의 각도</returns>
        private static float NormalizeAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            if (degrees < -180f) degrees += 360f;
            return degrees;
        }
    }
}
