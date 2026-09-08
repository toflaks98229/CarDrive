namespace CarDrive.Systems
{
    /// <summary>
    /// 재화를 <b>읽고 적는</b> 계약입니다. 잔액 조회와 표기까지만 하고, 값을 바꾸지 못합니다.
    ///
    /// <b>왜 표기가 여기 있는가.</b> 접두·접미와 천 단위 규칙은 <see cref="CurrencySetting"/>에
    /// 적혀 있고, 그것을 읽는 길은 지갑 하나입니다. 값을 쓰는 쪽이 각자 "₩" 를 붙이기 시작하면
    /// 가격표와 계산 문구의 표기가 갈라집니다. 그래서 조회와 표기를 한 계약에 둡니다.
    ///
    /// 이 계약을 보는 쪽은 값을 <b>보여 주기만</b> 합니다 — 가격표·점원의 안내 문구·재화 UI.
    /// 그들에게 지출 능력을 주지 않으려고 <see cref="ICurrencyStore"/>와 나누어 두었습니다.
    /// </summary>
    public interface ICurrencyBalance
    {
        /// <summary>지금 가진 양입니다.</summary>
        /// <param name="type">확인할 재화 종류</param>
        int Get(CurrencyType type);

        /// <summary>해당 재화의 설정입니다. 이름·색·표기 형식을 읽을 때 씁니다.</summary>
        /// <param name="type">찾을 재화 종류</param>
        /// <returns>설정. 등록되어 있지 않으면 null입니다.</returns>
        CurrencySetting GetSetting(CurrencyType type);

        /// <summary>지금 가진 양을 표기 규칙에 맞춰 글자로 만듭니다.</summary>
        /// <param name="type">표기할 재화 종류</param>
        /// <returns>접두·접미가 붙은 문자열. 예) "₩1,250"</returns>
        string Format(CurrencyType type);

        /// <summary>보유량이 아니라 <b>임의의 값</b>을 표기 규칙에 맞춰 글자로 만듭니다. (가격표·합계)</summary>
        /// <param name="type">표기 규칙을 가져올 재화 종류</param>
        /// <param name="amount">표기할 값</param>
        /// <returns>접두·접미가 붙은 문자열</returns>
        string Format(CurrencyType type, int amount);
    }

    /// <summary>
    /// 조회에 더해 <b>값을 치르는</b> 계약입니다.
    ///
    /// <b>왜 <see cref="ICurrencySink"/>를 넓히지 않았는가.</b> 그쪽은 바닥에 떨어진 덩어리를
    /// 줍는 <c>CurrencyPickup</c>이 보는 계약입니다. 거기에 지출을 얹으면 <b>줍는 물건이
    /// 돈을 쓸 수 있게</b> 됩니다. 그래서 넣기·읽기·쓰기를 세 계약으로 나누어 두고,
    /// 무엇이 무엇을 할 수 있는지를 <see cref="CarDrive.Composition.SimulationScope"/>의
    /// 등록 줄과 각 소비자의 <c>Construct</c> 시그니처만 보고도 알 수 있게 했습니다.
    ///
    /// <b>이 계약을 보는 쪽은 지금 계산대 하나뿐입니다.</b> 게임에서 돈이 나가는 길이
    /// 하나라는 뜻이고, 그것을 시그니처가 증명합니다. 두 번째 소비자가 생겼다면
    /// 그것이 정말 값을 치르는 자리인지 먼저 확인하세요 — 대개는 표기만 필요합니다.
    /// </summary>
    public interface ICurrencyStore : ICurrencyBalance
    {
        /// <summary>
        /// 재화를 씁니다. <b>모자라면 아무것도 빼지 않고 false를 돌려줍니다.</b>
        /// 잔액을 먼저 묻고 따로 빼면 두 호출 사이에 값이 바뀌어 음수가 될 수 있으므로,
        /// 확인과 차감을 한 번에 합니다.
        /// </summary>
        /// <param name="type">쓸 재화 종류</param>
        /// <param name="amount">쓸 양. 0 이하면 아무 일도 하지 않고 true입니다.</param>
        /// <returns>지불에 성공했으면 true입니다.</returns>
        bool TrySpend(CurrencyType type, int amount);
    }
}
