using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 조준의 기준이 될 Transform을 고르는 규칙을 한 곳에 모읍니다.
    ///
    /// PlayerInteractor·PlayerAttacker·PlayerCarrier는 모두 "카메라 오브젝트에 붙어 있음"을
    /// 가정하고 <c>transform</c>을 그대로 조준 기준으로 썼습니다. 그런데 그 가정은
    /// 주석에만 있었고 코드가 강제하지 않았습니다. 프리팹을 정리하다 스크립트가 다른
    /// 오브젝트로 옮겨가면 <b>컴파일 에러도 예외도 없이 엉뚱한 방향으로 조준</b>합니다.
    ///
    /// 그래서 규칙을 명시합니다.
    ///  1. 인스펙터에서 직접 지정했으면 그것을 씁니다.
    ///  2. 이 오브젝트가 카메라면 자기 자신을 씁니다. (지금까지의 동작)
    ///  3. 둘 다 아니면 Camera.main을 쓰고 <b>경고를 남깁니다.</b>
    /// </summary>
    public static class PlayerAim
    {
        /// <summary>
        /// 조준 기준으로 쓸 Transform을 정합니다.
        /// </summary>
        /// <param name="explicitSource">인스펙터에서 직접 지정한 기준. 지정하지 않았으면 null입니다.</param>
        /// <param name="owner">규칙을 묻는 컴포넌트. 경고 로그의 대상으로도 쓰입니다.</param>
        /// <returns>조준에 쓸 Transform. 카메라를 끝내 찾지 못하면 owner 자신의 Transform입니다.</returns>
        public static Transform Resolve(Transform explicitSource, Component owner)
        {
            if (explicitSource != null) return explicitSource;
            if (owner == null) return null;

            // 지금까지의 동작입니다. 이 스크립트들은 카메라 오브젝트에 붙어 있습니다.
            if (owner.GetComponent<Camera>() != null) return owner.transform;

            if (GameContext.MainCamera != null)
            {
                Debug.LogWarning(owner.GetType().Name + ": 카메라 오브젝트에 붙어 있지 않아 " +
                                 "Camera.main을 조준 기준으로 씁니다. 의도한 것이 아니라면 이 컴포넌트를 " +
                                 "카메라로 옮기거나 조준 기준을 직접 지정하세요.", owner);
                return GameContext.MainCameraTransform;
            }

            Debug.LogWarning(owner.GetType().Name + ": 조준 기준이 될 카메라를 찾지 못해 " +
                             "이 오브젝트의 방향으로 조준합니다.", owner);
            return owner.transform;
        }
    }
}
