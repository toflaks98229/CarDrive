using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 체력 <b>상태</b>를 소유합니다. 표시는 하지 않습니다.
///
/// 예전에는 TextHealthBar 하나가 상태와 TMP 표시를 겸했고, 그 결과
/// 차량 내구도와 플레이어 체력이 같은 타입이 되어 서로 구분할 수 없었습니다.
/// (음료를 마셨는데 차가 수리되는 문제가 여기서 나왔습니다)
///
/// 그래서 이 클래스는 <b>추상 클래스</b>이며, 실제로 씬에 붙는 것은
/// PlayerHealth 또는 VehicleHealth입니다. 그러면 인스펙터에서
/// "플레이어 체력" 자리에 차량 내구도를 넣는 일이 <b>타입 때문에 불가능</b>해집니다.
///
/// 표시는 TextHealthBar·HealthBarImage가 이 값을 읽어 그립니다. (읽기 전용)
/// </summary>
public abstract class Health : MonoBehaviour, IDamageable
{
    // --- Public Member Variables ---

    /// <summary>최대 체력입니다. 게임을 시작할 때 현재 체력이 이 값으로 채워집니다.</summary>
    [Header("체력")]
    [Tooltip("최대 체력")]
    public float maxHealth = 100f;

    /// <summary>체력이 0이 되었을 때 한 번 호출됩니다.</summary>
    [Header("이벤트")]
    [Tooltip("체력이 0이 되었을 때 한 번 호출됩니다.")]
    public UnityEvent onDeath;

    // --- Public Properties ---

    /// <summary>현재 체력입니다.</summary>
    public float CurrentHealth { get { return currentHealth; } }

    /// <summary>0~1로 정규화한 체력입니다.</summary>
    public float HealthNormalized { get { return maxHealth > 0f ? currentHealth / maxHealth : 0f; } }

    /// <summary>체력이 0인 상태인지 여부입니다.</summary>
    public bool IsDead { get { return currentHealth <= 0f; } }

    /// <summary>
    /// 값이 바뀔 때마다 증가하는 번호입니다.
    /// 표시 쪽은 이 번호가 달라졌을 때만 다시 그리면 되므로,
    /// 이벤트를 인스펙터로 연결하지 않고도 불필요한 갱신을 피할 수 있습니다.
    /// </summary>
    public int Revision { get { return revision; } }

    // --- Private Member Variables ---

    /// <summary>현재 체력입니다. 0과 maxHealth 사이로 유지됩니다.</summary>
    private float currentHealth;

    /// <summary>체력이 바뀔 때마다 증가하는 번호입니다. 표시 쪽이 갱신 여부를 판단하는 데 씁니다.</summary>
    private int revision;

    // --- Unity Event Functions ---

    // Update보다 먼저 값이 준비되어야 합니다.
    // (니즈 시스템이 첫 프레임부터 체력을 깎을 수 있습니다)
    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        revision++;
    }

    // --- Public Methods ---

    /// <summary>
    /// 정수 피해를 입힙니다. (기존 호출부 호환용)
    /// </summary>
    /// <param name="damage">입힐 피해량. 0 이하면 무시됩니다.</param>
    public void TakeDamage(int damage)
    {
        TakeDamage((float)damage);
    }

    /// <summary>
    /// 피해를 입힙니다.
    /// 니즈 한계 초과처럼 초당 1 미만으로 지속되는 피해는 정수로 받으면 0으로 잘리므로
    /// 실수로 처리합니다.
    /// </summary>
    /// <param name="damage">입힐 피해량. 0 이하이거나 이미 사망 상태면 무시됩니다.</param>
    public void TakeDamage(float damage)
    {
        if (damage <= 0f || IsDead) return;

        currentHealth -= damage;
        revision++;

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;

            Debug.Log(gameObject.name + ": 체력이 모두 소진되었습니다.");
            if (onDeath != null) onDeath.Invoke();
        }
    }

    /// <summary>
    /// 체력을 회복합니다. 사망 상태에서는 회복되지 않습니다. (되살리려면 Revive를 쓰세요)
    /// </summary>
    /// <param name="amount">회복량. 최대 체력을 넘지 않도록 잘립니다.</param>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        revision++;
    }

    /// <summary>
    /// 사망 상태에서 지정한 체력으로 되살립니다.
    /// </summary>
    /// <param name="health">되살아날 때의 체력. 0과 최대 체력 사이로 잘립니다.</param>
    public void Revive(float health)
    {
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        revision++;
    }
}
