using System.Collections.Generic;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 날씨에 따라 타이어 접지력을 조절합니다.
    ///
    /// <b>왜 떼어 냈는가.</b> 이 일은 주행 조율과 아무 관계가 없습니다.
    /// 프리팹에 잡아 둔 원래 마찰 강성을 기억해 두고, 날씨의 미끄러움을 읽어 배율을 곱하는 것이 전부입니다.
    /// 그런데도 <see cref="CarController"/> 안에 배열 셋과 메서드 둘로 흩어져 있어서,
    /// 주행 로직을 읽으려면 접지력 코드를 함께 넘겨야 했습니다.
    ///
    /// <b>변화가 없으면 건드리지 않습니다.</b> <c>WheelFrictionCurve</c>는 구조체라
    /// 대입할 때마다 물리 재계산이 걸립니다. 매 물리 프레임 같은 값을 다시 넣으면
    /// 그 비용만 계속 나갑니다. 그래서 <see cref="GripChangeEpsilon"/>보다 작은 변화는 건너뜁니다.
    /// </summary>
    public sealed class WheelGripTuner
    {
        // --- Constants ---

        /// <summary>이보다 작은 배율 변화는 무시합니다. 물리 재계산을 아끼기 위한 문턱입니다.</summary>
        private const float GripChangeEpsilon = 0.005f;

        /// <summary>미끄러움이 0이나 음수로 들어와도 나눗셈이 깨지지 않도록 두는 하한입니다.</summary>
        private const float MinSlipperiness = 0.01f;

        // --- Private Member Variables ---

        /// <summary>조절할 바퀴들입니다.</summary>
        private IReadOnlyList<WheelCollider> _wheels;

        /// <summary>바퀴별 원래 전방 마찰 강성입니다. 여기에 배율을 곱해 적용합니다.</summary>
        private float[] _baseForwardStiffness;

        /// <summary>바퀴별 원래 측면 마찰 강성입니다.</summary>
        private float[] _baseSidewaysStiffness;

        /// <summary>마지막으로 적용한 배율입니다. -1은 아직 한 번도 적용하지 않았다는 뜻입니다.</summary>
        private float _appliedGrip = -1f;

        // --- Public Methods ---

        /// <summary>
        /// 프리팹에 잡아 둔 마찰 강성을 기준값으로 기억합니다. <c>Start</c>에서 한 번 부르세요.
        ///
        /// <b>기준을 기억해 두는 이유가 있습니다.</b> 배율을 현재 값에 거듭 곱하면
        /// 비가 오래 올수록 접지력이 0으로 수렴합니다. 언제나 원래 값에서 다시 계산해야 합니다.
        /// </summary>
        /// <param name="wheels">조절할 바퀴들</param>
        public void CacheBaseStiffness(IReadOnlyList<WheelCollider> wheels)
        {
            _wheels = wheels;
            _appliedGrip = -1f;

            if (_wheels == null) return;

            _baseForwardStiffness = new float[_wheels.Count];
            _baseSidewaysStiffness = new float[_wheels.Count];

            for (int i = 0; i < _wheels.Count; i++)
            {
                if (_wheels[i] == null) continue;

                _baseForwardStiffness[i] = _wheels[i].forwardFriction.stiffness;
                _baseSidewaysStiffness[i] = _wheels[i].sidewaysFriction.stiffness;
            }
        }

        /// <summary>
        /// 지금 날씨에 맞는 접지력 배율을 구합니다.
        /// </summary>
        /// <param name="influence">날씨를 얼마나 반영할지. 0이면 무시, 1이면 그대로 적용합니다.</param>
        /// <param name="minFactor">배율의 하한. 너무 낮추면 운전이 불가능해집니다.</param>
        /// <returns><paramref name="minFactor"/>와 1 사이의 배율</returns>
        public static float CalculateGrip(float influence, float minFactor)
        {
            // 미끄러움은 1(평소)에서 1.9(폭우)까지 올라갑니다.
            // WeatherSystem이 씬에 없으면 1이 돌아오므로 아무 영향이 없습니다.
            float slipperiness = Mathf.Max(MinSlipperiness, WeatherSystem.GetRoadSlipperiness());
            float effective = Mathf.Lerp(1f, slipperiness, influence);

            return Mathf.Clamp(1f / effective, minFactor, 1f);
        }

        /// <summary>
        /// 접지력 배율을 바퀴에 적용합니다. 지난번과 같은 값이면 아무 일도 하지 않습니다.
        /// </summary>
        /// <param name="grip">적용할 배율. 1이면 원래 접지력입니다.</param>
        public void Apply(float grip)
        {
            if (_wheels == null || _baseForwardStiffness == null) return;
            if (Mathf.Abs(grip - _appliedGrip) < GripChangeEpsilon) return;

            _appliedGrip = grip;

            for (int i = 0; i < _wheels.Count; i++)
            {
                if (_wheels[i] == null) continue;

                WheelFrictionCurve forward = _wheels[i].forwardFriction;
                forward.stiffness = _baseForwardStiffness[i] * grip;
                _wheels[i].forwardFriction = forward;

                WheelFrictionCurve sideways = _wheels[i].sidewaysFriction;
                sideways.stiffness = _baseSidewaysStiffness[i] * grip;
                _wheels[i].sidewaysFriction = sideways;
            }
        }
    }
}
