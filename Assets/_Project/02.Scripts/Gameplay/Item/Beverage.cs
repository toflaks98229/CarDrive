using UnityEngine;

/// <summary>
/// 마실 수 있는 음료 한 병입니다.
///
/// 예전에는 BeverageBox가 찾아내기 위한 표식일 뿐이었고, 마시는 일은 상자만 할 수 있었습니다.
/// 그래서 상자에서 굴러 나온 병은 주울 수는 있어도 마실 수 없었습니다.
/// 이제 병 자체가 상호작용 대상이라 <b>어디에 있든 조준해서 마실 수 있습니다.</b>
///
/// 다 마시면 <see cref="BeverageConsumer"/>가 이 컴포넌트를 떼어 냅니다.
/// 컴포넌트가 없는 병은 상호작용 대상이 아니므로 그것이 곧 <b>빈 병</b>입니다.
/// </summary>
public class Beverage : MonoBehaviour, IInteractable
{
    // --- Public Member Variables ---

    /// <summary>
    /// 조준했을 때 표시할 문구입니다.
    ///
    /// 상자를 조준하든 병을 조준하든 <b>같은 문구가 나와야 합니다.</b>
    /// 그래서 문구는 여기 한 곳에만 두고, 상자는 자기가 내줄 병의 것을 그대로 씁니다.
    /// (BeverageBox.GetInteractionLabel 참고)
    /// </summary>
    [Header("표시")]
    [Tooltip("조준했을 때 표시할 문구. 상자를 조준했을 때도 이 문구가 나옵니다.")]
    public string promptLabel = "음료 마시기";

    // --- Public Properties ---

    /// <summary>
    /// 이 병이 들어 있는 상자입니다. 상자 밖으로 나왔으면 null입니다.
    /// 상자가 자기 목록을 만들 때 채워 줍니다.
    /// </summary>
    public BeverageBox Box { get; private set; }

    // --- Private Member Variables ---

    /// <summary>마시기를 처리할 컴포넌트입니다. 처음 필요할 때 찾아 기억해 둡니다.</summary>
    private static BeverageConsumer consumer;

    // --- Public Methods ---

    /// <summary>
    /// 이 병이 어느 상자에 들어 있는지 알려 줍니다. BeverageBox가 호출합니다.
    /// </summary>
    /// <param name="owner">이 병을 담고 있는 상자</param>
    public void SetBox(BeverageBox owner)
    {
        Box = owner;
    }

    /// <summary>
    /// 상자에서 빠져나옵니다. 상자의 목록에서도 스스로를 뺍니다.
    /// 굴러 나갔을 때와 마실 때 모두 이 경로를 씁니다.
    /// </summary>
    public void LeaveBox()
    {
        if (Box == null) return;

        BeverageBox owner = Box;
        Box = null;
        owner.Release(this);
    }

    // --- IInteractable ---

    /// <summary>
    /// 지금 마실 수 있는지 확인합니다.
    /// </summary>
    /// <returns>
    /// 마시기를 처리할 컴포넌트가 있고, 다른 병을 마시는 중이 아니며,
    /// 차 안의 병이라면 그 차에 타고 있을 때 true를 반환합니다.
    /// </returns>
    public bool CanInteract()
    {
        BeverageConsumer c = ResolveConsumer();
        if (c == null || c.IsBusy) return false;

        return IsReachable();
    }

    /// <summary>화면에 표시할 동작 이름입니다.</summary>
    /// <returns>인스펙터에서 설정한 안내 문구</returns>
    public string GetInteractionLabel()
    {
        return promptLabel;
    }

    /// <summary>이 병을 마십니다.</summary>
    public void Interact()
    {
        BeverageConsumer c = ResolveConsumer();
        if (c == null) return;

        c.Drink(this);
    }

    // --- Private Methods ---

    /// <summary>
    /// 차량 안에 실려 있는 병이라면 그 차에 타고 있어야 손이 닿습니다.
    ///
    /// 상호작용 레이캐스트는 차체를 그대로 통과하기 때문에, 이 검사가 없으면
    /// 차 밖에 서서 창 너머로 조준하는 것만으로 안에 있는 음료를 마실 수 있습니다.
    /// </summary>
    /// <returns>차량 밖에 있거나, 그 차에 타고 있으면 true입니다.</returns>
    private bool IsReachable()
    {
        Vehicle vehicle = GetComponentInParent<Vehicle>();
        return vehicle == null || vehicle.IsOccupied;
    }

    /// <summary>
    /// 마시기를 처리할 컴포넌트를 찾습니다.
    /// 플레이어에 하나뿐이라 정적으로 기억해 두고 모든 병이 함께 씁니다.
    /// </summary>
    /// <returns>찾은 컴포넌트. 씬에 없으면 null입니다.</returns>
    private static BeverageConsumer ResolveConsumer()
    {
        if (consumer != null) return consumer;

        consumer = FindAnyObjectByType<BeverageConsumer>();
        return consumer;
    }

    /// <summary>
    /// 플레이 모드에 들어갈 때 정적 참조를 비웁니다.
    /// 에디터에서 도메인 리로드를 꺼 두면 지난 실행의 값이 그대로 남기 때문입니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        consumer = null;
    }
}
