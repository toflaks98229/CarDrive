using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 마실 수 있는 병 하나입니다. 상자에서 꺼내 마시거나, 굴러 나온 것을 주워 마십니다.
    ///
    /// <b>소비 절차는 <see cref="ConsumableItem"/>에 있습니다.</b> 여기 남은 것은 음료만의 사정,
    /// 즉 <b>상자에 속해 있다</b>는 사실 하나뿐입니다. 음식은 봉투에서 나오므로 이 개념이 없습니다.
    /// </summary>
    public class Beverage : ConsumableItem
    {
        // --- Public Properties ---

        /// <summary>이 병이 속한 상자입니다. 상자 밖으로 나왔다면 null입니다.</summary>
        public BeverageBox Box { get; private set; }

        // --- Protected Properties ---

        /// <summary>인스펙터를 비워 두었을 때 쓸 문구입니다.</summary>
        protected override string DefaultPromptLabel { get { return "마시기"; } }

        // --- Public Properties ---

        /// <summary>이것은 마시는 물건입니다. 마시는 연출과 소리가 붙습니다.</summary>
        public override bool IsDrink { get { return true; } }

        // --- Public Methods ---

        /// <summary>
        /// 이 병이 어느 상자에 속하는지 알려 줍니다. 상자가 시작할 때 자식들에게 부릅니다.
        /// </summary>
        /// <param name="owner">이 병을 담고 있는 상자</param>
        public void SetBox(BeverageBox owner)
        {
            Box = owner;
        }

        /// <summary>
        /// 상자에서 빠져나옵니다. 굴러 나갔을 때와 마시기 시작할 때 모두 이 길을 지납니다.
        ///
        /// <b>먼저 참조를 끊고 상자에 알립니다.</b> 순서를 뒤집으면 상자가 목록을 훑는 도중에
        /// 다시 이 병에 닿아 같은 일이 두 번 일어날 수 있습니다.
        /// </summary>
        public void LeaveBox()
        {
            if (Box == null) return;

            BeverageBox owner = Box;
            Box = null;
            owner.Release(this);
        }

        /// <summary>
        /// 마시기 시작하면 상자에서 빠집니다. 상자가 빈 자리를 계속 세지 않도록 합니다.
        /// </summary>
        public override void OnConsumeStarted()
        {
            LeaveBox();
        }
    }
}
