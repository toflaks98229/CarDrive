namespace CarDrive.Common
{
    /// <summary>
    /// 화면 중앙(조준점)으로 바라봤을 때 상호작용할 수 있는 대상의 공통 규약입니다.
    ///
    /// PlayerInteractor가 카메라 정면으로 레이캐스트를 쏘아 이 인터페이스를 구현한
    /// 컴포넌트를 찾습니다. 즉 <b>조준점에 걸리지 않으면 상호작용은 일어나지 않습니다.</b>
    ///
    /// 콜라이더는 트리거(isTrigger)로 두고 상호작용 레이어에 올려야 합니다.
    /// 트리거가 아니면 차량 Rigidbody의 복합 콜라이더에 섞여 주행 물리가 망가집니다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>지금 상호작용할 수 있는 상태인지 여부입니다.</summary>
        bool CanInteract();

        /// <summary>
        /// 화면에 표시할 동작 이름입니다. (예: "탑승", "시동 걸기")
        /// 키 이름은 PlayerInteractor가 앞에 붙여 줍니다.
        /// </summary>
        string GetInteractionLabel();

        /// <summary>실제 상호작용을 수행합니다.</summary>
        void Interact();
    }
}
