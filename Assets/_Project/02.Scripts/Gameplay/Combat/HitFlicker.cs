using System.Collections;
using UnityEngine;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 피격 시 렌더러를 깜빡이는 연출입니다.
    ///
    /// EnemyController와 AttachedGhostController가 이 코드를 각각 복사해서 갖고 있었고,
    /// 그 결과 한쪽에만 널 검사가 들어가고 점멸 종료 상태도 서로 달라졌습니다.
    /// 그래서 코루틴 본문만 여기로 모읍니다.
    ///
    /// <b>일부러 MonoBehaviour가 아닙니다.</b> 컴포넌트로 만들면 각 적 프리팹에
    /// 새 컴포넌트를 붙이고 렌더러·라이트를 다시 연결해야 하는데, 지금 그럴 이유가 없습니다.
    /// 호출하는 쪽은 인스펙터 필드를 그대로 두고 값만 넘기면 됩니다.
    /// </summary>
    public static class HitFlicker
    {
        /// <summary>
        /// 렌더러와 라이트를 번갈아 껐다 켭니다.
        /// 끝나면 렌더러는 반드시 켜고, 라이트는 <b>시작할 때의 상태로</b> 되돌립니다.
        /// (켜져 있던 것을 꺼 버리면 이후로 계속 꺼진 채 남습니다)
        ///
        /// 둘 다 널이어도 안전합니다. 그저 시간만 흐릅니다.
        /// </summary>
        public static IEnumerator Play(Renderer renderer, Light light, float duration, float interval)
        {
            // 간격이 0이면 무한 루프가 되므로 최소값을 둡니다.
            float step = Mathf.Max(0.01f, interval);
            bool lightWasEnabled = light != null && light.enabled;

            float timer = 0f;
            while (timer < duration)
            {
                if (renderer != null) renderer.enabled = false;
                if (light != null) light.enabled = false;
                yield return new WaitForSeconds(step);
                timer += step;

                if (renderer != null) renderer.enabled = true;
                if (light != null) light.enabled = lightWasEnabled;
                yield return new WaitForSeconds(step);
                timer += step;
            }

            if (renderer != null) renderer.enabled = true;
            if (light != null) light.enabled = lightWasEnabled;
        }
    }
}
