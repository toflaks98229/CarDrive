using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 2차 시스템 하나를 인스펙터에서 조율하는 <b>세 숫자</b>입니다.
    ///
    /// 셋 다 "감쇠 계수 0.87" 같은 물리 상수가 아니라 <b>눈으로 고르는 값</b>입니다.
    /// 그래서 인스펙터에 그대로 내놓고, 실제 계수 k1·k2·k3 계산은
    /// <see cref="SecondOrderCoefficients"/> 안에 숨깁니다.
    /// </summary>
    [System.Serializable]
    public struct SecondOrderSettings
    {
        /// <summary>고유 진동수 f (Hz). 시스템이 반응하는 <b>빠르기</b>입니다.</summary>
        [Tooltip("고유 진동수 f (Hz). 클수록 빠르게 따라붙습니다. 흔들리는 주기이기도 합니다.")]
        [Min(0.01f)]
        public float frequency;

        /// <summary>감쇠비 ζ. 0이면 영원히 흔들리고, 1이면 흔들림 없이 멈추며, 1보다 크면 느리게 다가갑니다.</summary>
        [Tooltip("감쇠비 ζ. 0=영원히 흔들림, 0~1=흔들리다 잦아듦, 1=흔들림 없음, 1 초과=느리게 다가감")]
        [Min(0f)]
        public float damping;

        /// <summary>초기 반응 r. 0이면 천천히 출발하고, 1보다 크면 튀어나가며, 음수면 <b>반대로 먼저 물러납니다</b>.</summary>
        [Tooltip("초기 반응 r. 0=천천히 출발, 1=자연스러운 출발, 1 초과=튀어나감(추진), 음수=반대로 먼저 움츠림(예비 동작)")]
        public float response;

        /// <summary>세 숫자를 직접 지정해 만듭니다.</summary>
        /// <param name="frequency">고유 진동수 f (Hz)</param>
        /// <param name="damping">감쇠비 ζ</param>
        /// <param name="response">초기 반응 r</param>
        public SecondOrderSettings(float frequency, float damping, float response)
        {
            this.frequency = frequency;
            this.damping = damping;
            this.response = response;
        }

        /// <summary>흔들림 없이 한 번에 멈추는 무난한 기본값입니다.</summary>
        public static SecondOrderSettings Default
        {
            get { return new SecondOrderSettings(2f, 1f, 0f); }
        }
    }

    /// <summary>
    /// <b>2차 시스템</b>의 계수와, 이번 프레임의 시간 간격에서 <b>안전하게 쓸 수 있는</b> 계수를 계산합니다.
    ///
    /// <b>무엇을 푸는가.</b> 입력 x(t)를 따라가는 출력 y(t)를 만들되, 즉시 따라붙는 것이 아니라
    /// <b>질량이 있는 것처럼</b> 따라가게 합니다. 지배 방정식은 이렇습니다.
    ///
    /// <code>y + k1·(dy/dt) + k2·(d²y/dt²) = x + k3·(dx/dt)</code>
    ///
    /// 계수는 눈으로 고른 세 숫자에서 나옵니다. (ω = 2πf)
    /// <code>
    /// k1 = ζ / (π f)          — 감쇠 항
    /// k2 = 1 / (2π f)²        — 관성 항
    /// k3 = r ζ / (2π f)       — 입력 속도에 대한 반응 (예비 동작 · 추진)
    /// </code>
    ///
    /// <b>왜 Lerp 가 아니라 이것인가.</b> <c>Lerp(현재, 목표, t)</c>는 1차 시스템이라
    /// 언제나 목표 쪽으로만 다가갑니다. 넘어서지도, 예비 동작을 하지도, 관성으로 흐르지도 못합니다.
    /// 즉 <b>가속도라는 개념 자체가 없습니다.</b> 게다가 프레임률에 묶여 있어
    /// 60fps 와 30fps 에서 다른 속도로 움직입니다. 2차 시스템은 셋 다 해결합니다.
    ///
    /// <b>왜 semi-implicit(반음적) 오일러인가.</b> 명시적 오일러는 위치와 속도를 <b>둘 다 옛값</b>으로
    /// 갱신합니다. 진동계에서 이렇게 하면 매 스텝 에너지를 조금씩 벌어들여 결국 발산합니다.
    /// 위치를 먼저 옛 속도로 밀고, <b>새 위치</b>로 속도를 갱신하면(심플렉틱) 에너지가 유계에 머뭅니다.
    /// 비용은 같고 안정성만 얻습니다. Verlet 계열도 같은 성질을 갖지만
    /// (<see cref="VerletChain"/> 참고) 여기서는 속도를 명시적으로 들고 있어야
    /// 입력 속도 항 k3·(dx/dt) 를 섞기 좋으므로 semi-implicit 을 씁니다.
    ///
    /// <b>그래도 무너지는 구간이 있습니다.</b> 시간 간격 T 가 시스템의 주기에 비해 크면
    /// (f 를 크게 잡았거나 프레임이 튀었거나) 어떤 심플렉틱 적분기도 진동하다 폭발합니다.
    /// 그래서 <see cref="Resolve"/> 가 매 프레임 두 갈래로 나눕니다.
    ///
    ///  1. <b>ωT 가 ζ 보다 작을 때</b> — 아직 여유가 있습니다. k2 를 아래에서 눌러 두는 것으로 충분합니다.
    ///     (k2 는 T²/2 + T·k1/2 이상, 그리고 T·k1 이상) 값이 살짝 느려지는 대신 떨림이 없습니다.
    ///
    ///  2. <b>그 밖</b> — <b>극점 맞추기</b>(matched Z-transform)를 씁니다.
    ///     연속계의 극점 s = −ζω ± iω√(1−ζ²) 를 z = exp(sT) 로 옮긴 뒤,
    ///     <b>그 z 가 우리 재귀식의 특성근이 되도록</b> k1·k2 를 거꾸로 풉니다.
    ///
    /// <b>2번의 유도.</b> semi-implicit 재귀식에서 속도를 소거하고 y 만 남기면 특성다항식이 이렇습니다.
    /// <code>z² − (2 − T·k1/k2 − T²/k2)·z + (1 − T·k1/k2)</code>
    /// 목표는 z² − α·z + β 이고, α = 2·exp(−ζωT)·cos(ωT√(1−ζ²)), β = exp(−2ζωT) 입니다.
    /// 두 다항식의 계수를 맞추면 곧바로 풀립니다.
    /// <code>
    /// 1 − T·k1/k2 = β        →  T·k1/k2 = 1 − β
    /// 2 − (1−β) − T²/k2 = α  →  k2 = T² / (1 + β − α)
    ///                           k1 = T(1 − β) / (1 + β − α)
    /// </code>
    /// ζ 가 1보다 크면 극점이 실수가 되므로 cos 자리에 cosh 가 들어갑니다.
    ///
    /// <b>덕분에</b> 프레임이 드랍되어도, f 를 아주 크게 잡아도 값이 폭발하지 않고
    /// <b>연속계와 같은 속도로 감쇠</b>합니다. 로봇의 다리가 60fps 와 20fps 에서 같게 움직이는 근거가 이것입니다.
    ///
    /// 참고: Matched Z-transform method · Semi-implicit Euler method (Wikipedia),
    /// t3ssel8r, "Giving Personality to Procedural Animations using Math".
    /// </summary>
    public struct SecondOrderCoefficients
    {
        // --- Private Member Variables ---

        /// <summary>고유 각속도 ω = 2πf 입니다.</summary>
        private float w;

        /// <summary>감쇠비 ζ 입니다.</summary>
        private float z;

        /// <summary>감쇠 진동수 ω√|ζ²−1| 입니다. 극점 맞추기에서 각도(또는 지수)로 씁니다.</summary>
        private float d;

        /// <summary>감쇠 항 k1 입니다.</summary>
        private float k1;

        /// <summary>관성 항 k2 입니다.</summary>
        private float k2;

        /// <summary>입력 속도 항 k3 입니다.</summary>
        private float k3;

        // --- Constructors ---

        /// <summary>눈으로 고른 세 숫자에서 계수를 만듭니다.</summary>
        /// <param name="settings">진동수 · 감쇠비 · 초기 반응</param>
        public SecondOrderCoefficients(SecondOrderSettings settings)
        {
            // f 가 0이면 k2 가 무한이 됩니다. 아주 느린 시스템으로 눌러 두고 넘어갑니다.
            float f = Mathf.Max(settings.frequency, MinFrequency);
            float zeta = Mathf.Max(settings.damping, 0f);

            w = 2f * Mathf.PI * f;
            z = zeta;
            d = w * Mathf.Sqrt(Mathf.Abs(zeta * zeta - 1f));

            k1 = zeta / (Mathf.PI * f);
            k2 = 1f / (w * w);
            k3 = settings.response * zeta / w;
        }

        // --- Constants ---

        /// <summary>허용하는 가장 낮은 진동수입니다. 0으로 나누는 것을 막습니다.</summary>
        private const float MinFrequency = 0.001f;

        /// <summary>극점 맞추기에서 분모가 0이 되는 것을 막는 하한입니다.</summary>
        private const float MinDenominator = 1e-7f;

        // --- Public Properties ---

        /// <summary>입력 속도에 곱할 계수 k3 입니다. 예비 동작과 추진이 여기서 나옵니다.</summary>
        public float InputVelocityGain { get { return k3; } }

        // --- Public Methods ---

        /// <summary>
        /// 이번 프레임의 시간 간격에서 <b>발산하지 않는</b> k1·k2 를 돌려줍니다.
        ///
        /// 클래스 설명의 두 갈래가 여기 있습니다. 빠른 쪽(눌러 두기)은 exp·cos 를 부르지 않으므로
        /// 대부분의 프레임에서 비용이 거의 없습니다.
        /// </summary>
        /// <param name="dt">이번 프레임의 시간 간격 T (초)</param>
        /// <param name="stableK1">쓸 수 있는 감쇠 항</param>
        /// <param name="stableK2">쓸 수 있는 관성 항</param>
        public void Resolve(float dt, out float stableK1, out float stableK2)
        {
            if (w * dt < z)
            {
                // 아직 여유가 있습니다. k2 를 아래에서 눌러 두면 떨림 없이 안정합니다.
                stableK1 = k1;
                stableK2 = Mathf.Max(k2, Mathf.Max(dt * dt * 0.5f + dt * k1 * 0.5f, dt * k1));
                return;
            }

            // 시스템이 시간 간격에 비해 너무 빠릅니다. 극점을 z = exp(sT) 로 옮겨 계수를 역산합니다.
            float alpha;
            float beta;

            if (z <= 1f)
            {
                // 진동하는 극점 — 크기 exp(−ζωT), 각도 ωT√(1−ζ²)
                float decay = Mathf.Exp(-z * w * dt);

                alpha = 2f * decay * Mathf.Cos(dt * d);
                beta = decay * decay;
            }
            else
            {
                // 과감쇠 — 극점이 실수라 cos 자리에 cosh 가 들어갑니다.
                //
                // <b>여기서 exp(−ζωT)·cosh(dT) 를 그대로 곱하면 안 됩니다.</b> ζωT 가 커지면
                // 앞은 0으로 죽고 뒤는 무한대로 흘러, float 에서 <b>0 × ∞ = NaN</b> 이 됩니다.
                // (f=20 · ζ=2 · T=0.5 에서 실제로 납니다. double 로 계산하면 멀쩡해서 더 늦게 발견됩니다)
                //
                // cosh 를 펴서 지수를 먼저 합치면 ζ ≥ 1 일 때 두 지수가 모두 음수라
                // 어느 쪽도 넘치지 않습니다. (ζ 가 √(ζ²−1) 보다 항상 크기 때문입니다)
                alpha = Mathf.Exp(-(z * w - d) * dt) + Mathf.Exp(-(z * w + d) * dt);
                beta = Mathf.Exp(-2f * z * w * dt);
            }

            // 감쇠가 0이고 ωT 가 2π 의 배수에 걸리면 분모가 0이 됩니다.
            // 그 자리는 semi-implicit 재귀식이 표현할 수 없는 자세라, 값을 얼리는 쪽으로 물러납니다.
            float scale = dt / Mathf.Max(1f + beta - alpha, MinDenominator);

            stableK1 = (1f - beta) * scale;
            stableK2 = dt * scale;
        }
    }

    /// <summary>
    /// 실수 하나를 2차 시스템으로 따라가게 합니다.
    ///
    /// 쓰는 법은 <c>value = dynamics.Update(Time.deltaTime, target)</c> 한 줄입니다.
    /// 계수의 안전 처리는 <see cref="SecondOrderCoefficients"/>가 전부 맡습니다.
    /// </summary>
    public class SecondOrderDynamics
    {
        // --- Private Member Variables ---

        /// <summary>이 시스템의 계수입니다.</summary>
        private SecondOrderCoefficients coefficients;

        /// <summary>지난 프레임의 입력입니다. 입력 속도를 추정하는 데 씁니다.</summary>
        private float previousInput;

        /// <summary>현재 출력입니다.</summary>
        private float value;

        /// <summary>현재 출력의 변화 속도입니다.</summary>
        private float velocity;

        // --- Constructors ---

        /// <summary>설정과 시작값으로 시스템을 만듭니다.</summary>
        /// <param name="settings">진동수 · 감쇠비 · 초기 반응</param>
        /// <param name="initialValue">시작 시점의 값. 여기서 정지 상태로 출발합니다.</param>
        public SecondOrderDynamics(SecondOrderSettings settings, float initialValue)
        {
            coefficients = new SecondOrderCoefficients(settings);
            previousInput = initialValue;
            value = initialValue;
            velocity = 0f;
        }

        // --- Public Properties ---

        /// <summary>현재 출력입니다.</summary>
        public float Value { get { return value; } }

        /// <summary>현재 출력의 변화 속도입니다.</summary>
        public float Velocity { get { return velocity; } }

        // --- Public Methods ---

        /// <summary>움직이는 도중에 계수를 바꿉니다. 값과 속도는 그대로 둡니다.</summary>
        /// <param name="settings">새 설정</param>
        public void Reconfigure(SecondOrderSettings settings)
        {
            coefficients = new SecondOrderCoefficients(settings);
        }

        /// <summary>
        /// 목표가 0인 시스템을 때렸을 때 <b>원하는 최댓값</b>이 나오도록 넣을 속도를 계산합니다.
        ///
        /// 때린 뒤의 응답은 <c>y(t) = (v/ωd)·exp(−ζωt)·sin(ωd·t)</c> 입니다. 미분해 0이 되는 시각이
        /// <c>ωd·t = atan(ωd / ζω)</c> 이고, 그 자리의 값을 정리하면 <b>ωd 가 약분되어</b> 이렇게 됩니다.
        /// <code>
        /// 최댓값 = (v / ω) · exp(−ζ·θ / √(1−ζ²)),   θ = atan(ωd / ζω)
        /// </code>
        /// 그래서 원하는 최댓값에 이 식의 역수를 곱하면 넣을 속도가 나옵니다.
        ///
        /// <b>왜 필요한가.</b> <see cref="AddVelocity"/> 는 속도를 받는데, 쓰는 쪽이 알고 싶은 것은
        /// "얼마나 크게 흔들릴까"입니다. 이 변환이 없으면 진동수를 바꿀 때마다 흔들림의 크기가
        /// 함께 변해서 <b>둘을 따로 조율할 수 없습니다.</b>
        /// </summary>
        /// <param name="settings">때릴 시스템의 설정. 감쇠비가 1 이상이면 넘나들지 않으므로 0.99로 눌러 씁니다.</param>
        /// <param name="peak">원하는 최댓값</param>
        /// <returns><see cref="AddVelocity"/> 에 넣을 속도</returns>
        public static float ImpulseForPeak(SecondOrderSettings settings, float peak)
        {
            float zeta = Mathf.Clamp(settings.damping, 0f, 0.99f);
            float w = 2f * Mathf.PI * Mathf.Max(settings.frequency, 0.01f);

            float root = Mathf.Sqrt(1f - zeta * zeta);
            float damped = w * root;

            float ratio = Mathf.Exp(-zeta * Mathf.Atan2(damped, zeta * w) / Mathf.Max(root, 1e-4f));

            return peak * w / Mathf.Max(ratio, 0.05f);
        }

        /// <summary>
        /// 값은 그대로 두고 <b>속도만</b> 밀어 넣습니다. 충격을 주는 창구입니다.
        ///
        /// 목표를 그대로 둔 채 속도를 때리면 이 시스템은 <b>종처럼</b> 굽니다.
        /// 목표를 향해 되돌아오려는 힘과 밀어 넣은 속도가 겨루면서 몇 번 넘나들다 잦아듭니다.
        /// 감쇠비를 낮게 잡을수록 오래 흔들립니다.
        /// </summary>
        /// <param name="delta">더할 속도</param>
        public void AddVelocity(float delta)
        {
            velocity += delta;
        }

        /// <summary>값을 그 자리에 세웁니다. 순간이동시킬 때 씁니다.</summary>
        /// <param name="newValue">세울 값</param>
        public void Reset(float newValue)
        {
            previousInput = newValue;
            value = newValue;
            velocity = 0f;
        }

        /// <summary>한 프레임 진행합니다. 입력의 변화 속도는 이전 입력과의 차이로 추정합니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        /// <param name="input">따라갈 목표</param>
        /// <returns>갱신된 출력</returns>
        public float Update(float dt, float input)
        {
            if (dt <= 0f) return value;

            float inputVelocity = (input - previousInput) / dt;
            previousInput = input;

            return Update(dt, input, inputVelocity);
        }

        /// <summary>
        /// 한 프레임 진행합니다. 입력의 변화 속도를 <b>알고 있을 때</b> 쓰는 쪽입니다.
        ///
        /// 목표가 물리로 움직이고 있어 속도를 정확히 아는 경우(리지드바디 속도 등)
        /// 추정보다 이쪽이 매끄럽습니다. 추정은 목표가 순간이동하면
        /// 한 프레임 동안 거대한 속도를 만들어 냅니다.
        /// </summary>
        /// <param name="dt">시간 간격(초)</param>
        /// <param name="input">따라갈 목표</param>
        /// <param name="inputVelocity">목표의 변화 속도</param>
        /// <returns>갱신된 출력</returns>
        public float Update(float dt, float input, float inputVelocity)
        {
            if (dt <= 0f) return value;

            coefficients.Resolve(dt, out float k1, out float k2);

            // 위치를 먼저 옛 속도로 밀고,
            value += dt * velocity;

            // 새 위치로 속도를 갱신합니다. (semi-implicit — 이 순서가 안정성의 전부입니다)
            velocity += dt * (input + coefficients.InputVelocityGain * inputVelocity - value - k1 * velocity) / k2;

            return value;
        }
    }

    /// <summary>
    /// <see cref="Vector3"/> 를 2차 시스템으로 따라가게 합니다. 축마다 같은 계수를 씁니다.
    ///
    /// 축을 나눠 세 개를 두지 않고 하나로 묶은 이유는, 계수 계산(<see cref="SecondOrderCoefficients.Resolve"/>)이
    /// 프레임마다 한 번이면 충분하기 때문입니다. exp·cos 를 세 번 부를 이유가 없습니다.
    /// </summary>
    public class SecondOrderDynamics3
    {
        // --- Private Member Variables ---

        /// <summary>이 시스템의 계수입니다.</summary>
        private SecondOrderCoefficients coefficients;

        /// <summary>지난 프레임의 입력입니다.</summary>
        private Vector3 previousInput;

        /// <summary>현재 출력입니다.</summary>
        private Vector3 value;

        /// <summary>현재 출력의 변화 속도입니다.</summary>
        private Vector3 velocity;

        // --- Constructors ---

        /// <summary>설정과 시작값으로 시스템을 만듭니다.</summary>
        /// <param name="settings">진동수 · 감쇠비 · 초기 반응</param>
        /// <param name="initialValue">시작 위치</param>
        public SecondOrderDynamics3(SecondOrderSettings settings, Vector3 initialValue)
        {
            coefficients = new SecondOrderCoefficients(settings);
            previousInput = initialValue;
            value = initialValue;
            velocity = Vector3.zero;
        }

        // --- Public Properties ---

        /// <summary>현재 출력입니다.</summary>
        public Vector3 Value { get { return value; } }

        /// <summary>현재 출력의 변화 속도입니다.</summary>
        public Vector3 Velocity { get { return velocity; } }

        // --- Public Methods ---

        /// <summary>움직이는 도중에 계수를 바꿉니다.</summary>
        /// <param name="settings">새 설정</param>
        public void Reconfigure(SecondOrderSettings settings)
        {
            coefficients = new SecondOrderCoefficients(settings);
        }

        /// <summary>
        /// 값은 그대로 두고 <b>속도만</b> 밀어 넣습니다. 충격을 주는 창구입니다.
        /// 목표를 그대로 둔 채 속도를 때리면 이 시스템은 <b>종처럼</b> 몇 번 넘나들다 잦아듭니다.
        /// </summary>
        /// <param name="delta">더할 속도</param>
        public void AddVelocity(Vector3 delta)
        {
            velocity += delta;
        }

        /// <summary>값을 그 자리에 세웁니다.</summary>
        /// <param name="newValue">세울 위치</param>
        public void Reset(Vector3 newValue)
        {
            previousInput = newValue;
            value = newValue;
            velocity = Vector3.zero;
        }

        /// <summary>한 프레임 진행합니다. 입력의 변화 속도는 추정합니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        /// <param name="input">따라갈 목표</param>
        /// <returns>갱신된 출력</returns>
        public Vector3 Update(float dt, Vector3 input)
        {
            if (dt <= 0f) return value;

            Vector3 inputVelocity = (input - previousInput) / dt;
            previousInput = input;

            return Update(dt, input, inputVelocity);
        }

        /// <summary>한 프레임 진행합니다. 입력의 변화 속도를 알고 있을 때 쓰는 쪽입니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        /// <param name="input">따라갈 목표</param>
        /// <param name="inputVelocity">목표의 변화 속도</param>
        /// <returns>갱신된 출력</returns>
        public Vector3 Update(float dt, Vector3 input, Vector3 inputVelocity)
        {
            if (dt <= 0f) return value;

            coefficients.Resolve(dt, out float k1, out float k2);

            float gain = coefficients.InputVelocityGain;

            value += dt * velocity;
            velocity += dt * (input + gain * inputVelocity - value - k1 * velocity) / k2;

            return value;
        }
    }

    /// <summary>
    /// 회전을 2차 시스템으로 따라가게 합니다.
    ///
    /// <b>왜 쿼터니언 네 성분에 직접 걸지 않는가.</b> q 와 −q 는 같은 회전입니다(이중 덮개).
    /// 성분마다 용수철을 걸면 목표가 반구를 넘는 순간 부호가 뒤집히면서
    /// <b>같은 자리인데 180° 를 돌아가는</b> 일이 생깁니다. 매 프레임 부호를 맞춰 줄 수도 있지만,
    /// 그러면 각속도가 큰 순간에 판정이 흔들립니다.
    ///
    /// 그래서 <b>앞 방향과 위 방향 두 벡터</b>에 각각 용수철을 걸고, 결과를
    /// <see cref="Quaternion.LookRotation(Vector3,Vector3)"/> 로 다시 회전으로 만듭니다.
    /// 벡터는 이중 덮개가 없으므로 이 문제가 아예 생기지 않고,
    /// 몸통이 기울 때 <b>피치와 롤이 각자의 관성으로</b> 따로 흔들리는 것도 자연스럽게 나옵니다.
    ///
    /// 대신 이것은 <b>축각 공간의 정확한 용수철이 아닙니다.</b> 180° 를 넘는 회전 차이에서는
    /// 도는 방향이 최단 경로와 다를 수 있습니다. 몸통·머리처럼 목표가 연속으로 움직이는 곳에는 문제가 없고,
    /// 순간이동이 필요하면 <see cref="Reset"/> 을 부르면 됩니다.
    /// </summary>
    public class SecondOrderRotation
    {
        // --- Private Member Variables ---

        /// <summary>앞 방향(로컬 +Z)을 따라가는 시스템입니다.</summary>
        private SecondOrderDynamics3 forward;

        /// <summary>위 방향(로컬 +Y)을 따라가는 시스템입니다.</summary>
        private SecondOrderDynamics3 up;

        /// <summary>마지막으로 만들어진 회전입니다. 축이 무너진 프레임에 이 값을 유지합니다.</summary>
        private Quaternion value;

        // --- Constructors ---

        /// <summary>설정과 시작 회전으로 시스템을 만듭니다.</summary>
        /// <param name="settings">진동수 · 감쇠비 · 초기 반응</param>
        /// <param name="initialValue">시작 회전</param>
        public SecondOrderRotation(SecondOrderSettings settings, Quaternion initialValue)
        {
            forward = new SecondOrderDynamics3(settings, initialValue * Vector3.forward);
            up = new SecondOrderDynamics3(settings, initialValue * Vector3.up);
            value = initialValue;
        }

        // --- Public Properties ---

        /// <summary>현재 회전입니다.</summary>
        public Quaternion Value { get { return value; } }

        // --- Public Methods ---

        /// <summary>움직이는 도중에 계수를 바꿉니다.</summary>
        /// <param name="settings">새 설정</param>
        public void Reconfigure(SecondOrderSettings settings)
        {
            forward.Reconfigure(settings);
            up.Reconfigure(settings);
        }

        /// <summary>회전을 그 자리에 세웁니다.</summary>
        /// <param name="newValue">세울 회전</param>
        public void Reset(Quaternion newValue)
        {
            forward.Reset(newValue * Vector3.forward);
            up.Reset(newValue * Vector3.up);
            value = newValue;
        }

        /// <summary>한 프레임 진행합니다.</summary>
        /// <param name="dt">시간 간격(초)</param>
        /// <param name="target">따라갈 목표 회전</param>
        /// <returns>갱신된 회전</returns>
        public Quaternion Update(float dt, Quaternion target)
        {
            if (dt <= 0f) return value;

            Vector3 f = forward.Update(dt, target * Vector3.forward);
            Vector3 u = up.Update(dt, target * Vector3.up);

            // 두 벡터가 나란해지면 LookRotation 이 회전을 만들 수 없습니다.
            // 그런 프레임에는 지난 회전을 그대로 유지합니다. (한 프레임 늦을 뿐 튀지 않습니다)
            Vector3 axis = Vector3.Cross(u, f);
            if (f.sqrMagnitude < Epsilon || axis.sqrMagnitude < Epsilon) return value;

            value = Quaternion.LookRotation(f, u);
            return value;
        }

        // --- Constants ---

        /// <summary>축이 무너졌다고 볼 제곱 길이 문턱입니다.</summary>
        private const float Epsilon = 1e-8f;
    }
}
