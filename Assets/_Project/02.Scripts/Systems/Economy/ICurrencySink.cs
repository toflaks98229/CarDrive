namespace CarDrive.Systems
{
    /// <summary>
    /// 재화를 <b>넣는</b> 계약입니다.
    ///
    /// <b>왜 넣기만 있는가.</b> 지금 이 계약을 보는 쪽은 바닥에 떨어진 덩어리를 줍는
    /// <c>CurrencyPickup</c> 하나이고, 줍는 물건이 잔액을 조회하거나 돈을 쓸 이유는 없습니다.
    /// 값을 읽거나 치러야 하는 쪽은 <see cref="ICurrencyBalance"/>와 <see cref="ICurrencyStore"/>를
    /// 봅니다. 이 계약을 넓히지 않고 그 둘을 따로 둔 이유가 그것입니다 —
    /// 넓혔다면 <b>줍는 물건이 돈을 쓸 수 있게</b> 됩니다.
    /// </summary>
    public interface ICurrencySink
    {
        /// <summary>
        /// 재화를 그만큼 넣습니다.
        /// </summary>
        /// <param name="type">넣을 재화 종류</param>
        /// <param name="amount">넣을 양</param>
        /// <returns>상한에 걸리지 않고 실제로 들어간 양</returns>
        int Add(CurrencyType type, int amount);
    }

    /// <summary>
    /// 지갑이 없을 때 그 자리를 채우는 <b>받지 않는 지갑</b>입니다.
    ///
    /// 예전 <c>Wallet.Report()</c>는 <c>Instance</c>가 없으면 0을 돌려주고 지나갔습니다.
    /// 그 성질을 그대로 옮겼으므로, 지갑을 빼고 실행해도 덩어리는 정상적으로 사라집니다.
    /// </summary>
    public sealed class NullCurrencySink : ICurrencySink
    {
        /// <summary>모두가 공유하는 하나뿐인 인스턴스입니다. 상태가 없으므로 나눠 써도 안전합니다.</summary>
        public static readonly NullCurrencySink Instance = new NullCurrencySink();

        private NullCurrencySink() { }

        /// <summary>받을 지갑이 없으므로 아무것도 들어가지 않습니다.</summary>
        /// <param name="type">무시됩니다.</param>
        /// <param name="amount">무시됩니다.</param>
        /// <returns>항상 0</returns>
        public int Add(CurrencyType type, int amount) { return 0; }
    }
}
