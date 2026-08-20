using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>어느 바퀴에 구동력을 보낼지 정합니다.</summary>
    public enum WheelDriveType
    {
        /// <summary>앞바퀴만 굴립니다.</summary>
        FrontWheelDrive,

        /// <summary>뒷바퀴만 굴립니다.</summary>
        RearWheelDrive,

        /// <summary>네 바퀴를 모두 굴립니다.</summary>
        AllWheelDrive
    }

    /// <summary>어느 바퀴를 꺾을지 정합니다.</summary>
    public enum WheelSteerType
    {
        /// <summary>앞바퀴만 꺾습니다.</summary>
        FrontWheelSteer,

        /// <summary>뒷바퀴도 반대로 꺾어 회전 반경을 줄입니다.</summary>
        AllWheelSteer
    }

    /// <summary>
    /// 바퀴 무리에 구동력·조향·제동을 적용하는 구동계입니다.
    ///
    /// <b>왜 떼어 냈는가.</b> 예전에는 <see cref="CarController"/>가 휠 콜라이더 넷을
    /// 개별 필드로 들고, <c>ApplyMotorTorque</c>·<c>ApplySteering</c>·<c>HandleBraking</c>·
    /// <c>GetAverageWheelRPM</c> <b>네 메서드가 각자 같은 switch를 다시 썼습니다.</b>
    /// 구동 방식을 하나 늘리면 네 곳을 함께 고쳐야 했고, 바퀴가 여섯인 차량은 아예 불가능했습니다.
    ///
    /// 이제 <b>"어느 바퀴가 굴러가고 어느 바퀴가 꺾이는가"를 <see cref="Configure"/>에서 한 번만
    /// 계산해 표로 만듭니다.</b> 이후의 적용은 전부 그 표를 훑는 단순한 반복문입니다.
    /// 바퀴가 여섯이 되어도 배열이 길어질 뿐 코드는 그대로입니다.
    ///
    /// <b>MonoBehaviour가 아닙니다.</b> 설정값은 <see cref="CarController"/>가 직렬화해 들고 있고,
    /// 이 클래스는 그 값을 받아 계산만 합니다. 컴포넌트로 만들면 프리팹에 손으로 붙여야 하는데,
    /// 그렇게 얻을 것이 없습니다. (자세한 이유는 CarController 주석에 적어 두었습니다)
    /// </summary>
    public sealed class WheelDriveline
    {
        // --- Constants ---

        /// <summary>뒷바퀴 조향 시 앞바퀴 대비 꺾이는 비율입니다. 반대 방향으로 절반만 꺾습니다.</summary>
        private const float RearSteerRatio = -0.5f;

        // --- Private Member Variables ---

        /// <summary>이 차량의 바퀴들입니다. 앞 둘, 뒤 둘 순서로 담깁니다.</summary>
        private WheelCollider[] _wheels;

        /// <summary>바퀴별로 구동력을 받는지 여부입니다. <see cref="Configure"/>에서 한 번 정합니다.</summary>
        private bool[] _isDriven;

        /// <summary>바퀴별 조향 배율입니다. 1이면 그대로, 0이면 고정, 음수면 반대로 꺾입니다.</summary>
        private float[] _steerFactors;

        /// <summary>구동륜 하나가 받을 토크 비율입니다. 구동륜이 넷이면 0.5입니다.</summary>
        private float _torqueShare = 1f;

        /// <summary>구동륜 개수입니다. 평균 회전수를 낼 때 나누는 값입니다.</summary>
        private int _drivenCount;

        // --- Public Properties ---

        /// <summary>바퀴가 하나라도 준비되었는지 여부입니다.</summary>
        public bool IsReady { get { return _wheels != null && _wheels.Length > 0; } }

        /// <summary>이 구동계가 다루는 바퀴들입니다. 접지력 조정 등 바깥에서 훑을 때 씁니다.</summary>
        public IReadOnlyList<WheelCollider> Wheels { get { return _wheels; } }

        // --- Public Methods ---

        /// <summary>
        /// 바퀴와 구동·조향 방식을 받아 적용 표를 만듭니다.
        ///
        /// <b>분기는 여기서 끝납니다.</b> 아래의 적용 메서드들은 이 표만 보고 돌기 때문에
        /// 구동 방식을 몰라도 됩니다.
        /// </summary>
        /// <param name="frontWheels">앞바퀴들. null 항목은 걸러집니다.</param>
        /// <param name="rearWheels">뒷바퀴들. null 항목은 걸러집니다.</param>
        /// <param name="driveType">구동 방식</param>
        /// <param name="steerType">조향 방식</param>
        public void Configure(IReadOnlyList<WheelCollider> frontWheels,
                              IReadOnlyList<WheelCollider> rearWheels,
                              WheelDriveType driveType,
                              WheelSteerType steerType)
        {
            List<WheelCollider> collected = new List<WheelCollider>();
            List<bool> isFront = new List<bool>();

            AppendWheels(frontWheels, true, collected, isFront);
            AppendWheels(rearWheels, false, collected, isFront);

            _wheels = collected.ToArray();
            _isDriven = new bool[_wheels.Length];
            _steerFactors = new float[_wheels.Length];
            _drivenCount = 0;

            for (int i = 0; i < _wheels.Length; i++)
            {
                _isDriven[i] = IsDrivenBy(driveType, isFront[i]);
                if (_isDriven[i]) _drivenCount++;

                _steerFactors[i] = GetSteerFactor(steerType, isFront[i]);
            }

            // 구동륜이 넷이면 각 바퀴가 절반씩 받습니다.
            // 예전 코드가 AWD 에서만 torque/2 를 하던 것과 같은 결과입니다.
            _torqueShare = _drivenCount > 2 ? 0.5f : 1f;
        }

        /// <summary>
        /// 구동륜의 평균 회전수를 돌려줍니다. 동력계가 엔진 RPM을 낼 때 씁니다.
        /// </summary>
        /// <returns>구동륜 평균 RPM. 구동륜이 없으면 0입니다.</returns>
        public float GetAverageDrivenRpm()
        {
            if (!IsReady || _drivenCount == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_isDriven[i]) sum += _wheels[i].rpm;
            }

            return sum / _drivenCount;
        }

        /// <summary>
        /// 구동륜에 토크를 나눠 겁니다. 구동륜이 아닌 바퀴는 0으로 둡니다.
        /// </summary>
        /// <param name="torque">구동계 전체에 걸 토크</param>
        public void ApplyMotorTorque(float torque)
        {
            if (!IsReady) return;

            float perWheel = torque * _torqueShare;
            for (int i = 0; i < _wheels.Length; i++)
            {
                _wheels[i].motorTorque = _isDriven[i] ? perWheel : 0f;
            }
        }

        /// <summary>
        /// 조향각을 적용합니다. 바퀴별 배율은 <see cref="Configure"/>에서 이미 정해져 있습니다.
        /// </summary>
        /// <param name="steerAngle">앞바퀴 기준 조향각(도)</param>
        public void ApplySteerAngle(float steerAngle)
        {
            if (!IsReady) return;

            for (int i = 0; i < _wheels.Length; i++)
            {
                _wheels[i].steerAngle = steerAngle * _steerFactors[i];
            }
        }

        /// <summary>
        /// 모든 바퀴에 같은 제동 토크를 겁니다.
        /// </summary>
        /// <param name="brakeTorque">걸 제동 토크</param>
        public void ApplyBrakeTorque(float brakeTorque)
        {
            if (!IsReady) return;

            for (int i = 0; i < _wheels.Length; i++)
            {
                _wheels[i].brakeTorque = brakeTorque;
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// 바퀴 목록을 모으면서 앞뒤 구분을 함께 기록합니다. null 항목은 건너뜁니다.
        /// </summary>
        /// <param name="source">모을 바퀴들</param>
        /// <param name="front">이 무리가 앞바퀴인지 여부</param>
        /// <param name="into">모은 바퀴를 담을 목록</param>
        /// <param name="frontFlags">앞뒤 구분을 담을 목록</param>
        private static void AppendWheels(IReadOnlyList<WheelCollider> source, bool front,
                                         List<WheelCollider> into, List<bool> frontFlags)
        {
            if (source == null) return;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] == null) continue;

                into.Add(source[i]);
                frontFlags.Add(front);
            }
        }

        /// <summary>
        /// 이 바퀴가 구동력을 받는지 판단합니다.
        /// </summary>
        /// <param name="driveType">구동 방식</param>
        /// <param name="isFront">앞바퀴인지 여부</param>
        /// <returns>구동륜이면 true</returns>
        private static bool IsDrivenBy(WheelDriveType driveType, bool isFront)
        {
            switch (driveType)
            {
                case WheelDriveType.FrontWheelDrive: return isFront;
                case WheelDriveType.RearWheelDrive: return !isFront;
                default: return true;
            }
        }

        /// <summary>
        /// 이 바퀴의 조향 배율을 구합니다.
        /// </summary>
        /// <param name="steerType">조향 방식</param>
        /// <param name="isFront">앞바퀴인지 여부</param>
        /// <returns>조향각에 곱할 배율</returns>
        private static float GetSteerFactor(WheelSteerType steerType, bool isFront)
        {
            if (isFront) return 1f;
            return steerType == WheelSteerType.AllWheelSteer ? RearSteerRatio : 0f;
        }
    }
}
