using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 이 프로젝트에서 DOTween 트윈을 만들 때 <b>반드시 지켜야 하는 두 가지</b>를 한곳에 모읍니다.
    ///
    /// <b>1. UI 연출은 스케일되지 않은 시간을 씁니다.</b>
    /// Feel 의 Freeze Frame 과 Time Scale 피드백은 <c>Time.timeScale</c> 을 건드립니다.
    /// 귀신을 쓰러뜨릴 때 0.05초 동안 화면이 멈추는데, 그때 UI 슬라이드까지 함께 멈추면
    /// 연출이 끊겨 보입니다. 게임 세계는 멈춰도 <b>인터페이스는 계속 움직여야</b> 합니다.
    ///
    /// <b>2. 오브젝트에 묶습니다.</b>
    /// <c>SetLink</c> 를 걸면 대상이 파괴될 때 트윈도 함께 사라집니다.
    /// 이것이 없으면 파괴된 Transform 을 계속 움직이려다 예외가 납니다.
    /// <b>다만 풀로 회수되는 것은 파괴되지 않으므로</b>, 그쪽은 OnDisable 에서
    /// <c>DOKill</c> 을 따로 불러야 합니다. (CurrencyPickup 참고)
    ///
    /// 규칙을 주석으로만 적어 두면 다음 사람이 빠뜨립니다. 그래서 메서드로 만듭니다.
    /// </summary>
    public static class TweenSetup
    {
        // --- Public Methods ---

        /// <summary>
        /// UI 연출용 기본 설정을 겁니다. 시간 스케일을 무시하고 대상에 묶습니다.
        /// </summary>
        /// <param name="tween">설정할 트윈</param>
        /// <param name="owner">이 트윈의 수명을 따라갈 오브젝트</param>
        /// <returns>설정이 적용된 같은 트윈</returns>
        public static T ForUI<T>(this T tween, Component owner) where T : Tween
        {
            if (tween == null) return null;

            tween.SetUpdate(true);
            if (owner != null) tween.SetLink(owner.gameObject);

            return tween;
        }

        /// <summary>
        /// 게임 세계 연출용 기본 설정을 겁니다.
        /// UI와 달리 <b>시간 스케일을 따릅니다.</b> 화면이 멈추면 세계도 멈춰야 하기 때문입니다.
        /// </summary>
        /// <param name="tween">설정할 트윈</param>
        /// <param name="owner">이 트윈의 수명을 따라갈 오브젝트</param>
        /// <returns>설정이 적용된 같은 트윈</returns>
        public static T ForWorld<T>(this T tween, Component owner) where T : Tween
        {
            if (tween == null) return null;

            tween.SetUpdate(false);
            if (owner != null) tween.SetLink(owner.gameObject);

            return tween;
        }
    }
}
