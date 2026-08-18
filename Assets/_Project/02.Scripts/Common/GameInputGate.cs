using System;

/// <summary>
/// 디버그 오버레이나 UI가 떠 있는 동안 게임 입력을 잠시 막는 스위치입니다.
///
/// 오버레이 버튼을 누르려면 마우스 커서를 풀어야 하는데, 그대로 두면 그 클릭이
/// 공격·물건 들기로도 들어가고 마우스를 움직이면 시점까지 돌아갑니다.
/// 그래서 커서를 푸는 쪽이 Push()를 걸고, 게임 입력을 읽는 쪽은 Suspended를 확인합니다.
///
/// 여러 곳에서 동시에 걸 수 있도록 개수를 세기 때문에, 나중에 인벤토리나 대화창이
/// 생겨도 같은 방식으로 쓰면 됩니다.
/// </summary>
public static class GameInputGate
{
    // --- Public Member Variables ---

    /// <summary>막힘 상태가 바뀔 때 알립니다. (커서 잠금 처리에 씁니다)</summary>
    public static event Action<bool> Changed;

    // --- Public Properties ---

    /// <summary>지금 게임 입력을 막아야 하는지 여부입니다.</summary>
    public static bool Suspended { get { return suspendCount > 0; } }

    // --- Private Member Variables ---

    /// <summary>
    /// 지금 걸려 있는 입력 막기의 개수입니다.
    /// 여러 곳에서 동시에 걸 수 있으므로 불리언이 아니라 개수로 셉니다.
    /// </summary>
    private static int suspendCount;

    // --- Public Methods ---

    /// <summary>입력 막기를 하나 겁니다.</summary>
    public static void Push()
    {
        suspendCount++;
        if (suspendCount == 1) Raise(true);
    }

    /// <summary>입력 막기를 하나 풉니다.</summary>
    public static void Pop()
    {
        if (suspendCount <= 0) return;

        suspendCount--;
        if (suspendCount == 0) Raise(false);
    }

    /// <summary>
    /// 전부 풉니다. 씬을 다시 불러올 때처럼 상태가 꼬였을 때 씁니다.
    /// (static 값은 플레이 모드를 나가도 남을 수 있습니다)
    /// </summary>
    public static void Reset()
    {
        bool was = Suspended;
        suspendCount = 0;
        if (was) Raise(false);
    }

    // --- Private Methods ---

    /// <summary>
    /// 막힘 상태가 바뀌었음을 구독자에게 알립니다.
    /// </summary>
    /// <param name="suspended">바뀐 뒤의 막힘 상태</param>
    private static void Raise(bool suspended)
    {
        if (Changed != null) Changed(suspended);
    }
}
