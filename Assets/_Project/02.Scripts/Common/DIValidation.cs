using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 설치자(Installer)가 인스펙터로 받는 <b>필수 참조의 누락을 기동 시점에 드러내는</b> 검증 헬퍼입니다.
    ///
    /// <b>왜 필요한가.</b> DI로 옮기면 참조가 비었을 때의 증상이 나빠집니다.
    /// 예전에는 <c>Start</c>에서 <c>GameContext.Require</c>가 "무엇을 못 찾았는지" 말해 주었는데,
    /// 컨테이너는 해석에 실패한 <b>타입</b>만 말하고 <b>어느 인스펙터 칸이 비었는지</b>는 말하지 않습니다.
    /// 그래서 등록하기 <b>전에</b> 먼저 확인하고, 비었으면 설치자 이름과 필드 이름을 함께 남깁니다.
    ///
    /// 이름은 <c>nameof(field)</c>로 넘기세요. 필드 이름을 바꿔도 로그가 따라옵니다.
    /// </summary>
    public static class DIValidation
    {
        /// <summary>
        /// 필수 참조가 비어 있으면(또는 이미 파괴되었으면) 오류를 남기고 false를 돌려줍니다.
        /// </summary>
        /// <param name="owner">이 참조를 들고 있는 설치자. 로그를 클릭하면 이것이 선택됩니다.</param>
        /// <param name="reference">확인할 참조</param>
        /// <param name="fieldName">필드 이름. <c>nameof(field)</c>를 넘기세요.</param>
        /// <returns>참조가 살아 있으면 true</returns>
        public static bool RequireRef(Object owner, Object reference, string fieldName)
        {
            if (reference != null) return true;

            string ownerName = owner != null ? owner.GetType().Name : "UnknownInstaller";
            GameLog.Error(GameLog.Channel.Core,
                "[" + ownerName + "] 필수 참조 '" + fieldName + "'가 인스펙터에 연결되지 않았습니다. " +
                "이대로 두면 컨테이너 해석이 실패하거나 런타임에 NullReference가 납니다.", owner);

            return false;
        }

        /// <summary>
        /// 없어도 되는 참조를 확인합니다. 비어 있으면 <b>경고만</b> 남깁니다.
        ///
        /// 선택적 참조를 조용히 넘기지 않는 이유가 있습니다. "선택"과 "연결을 잊음"은
        /// 구분되지 않는데, 그 둘의 결과는 완전히 다릅니다.
        /// </summary>
        /// <param name="owner">이 참조를 들고 있는 설치자</param>
        /// <param name="reference">확인할 참조</param>
        /// <param name="fieldName">필드 이름. <c>nameof(field)</c>를 넘기세요.</param>
        /// <param name="consequence">비어 있을 때 무엇이 동작하지 않는지 한 줄로</param>
        /// <returns>참조가 살아 있으면 true</returns>
        public static bool OptionalRef(Object owner, Object reference, string fieldName, string consequence)
        {
            if (reference != null) return true;

            string ownerName = owner != null ? owner.GetType().Name : "UnknownInstaller";
            GameLog.Warn(GameLog.Channel.Core,
                "[" + ownerName + "] 선택 참조 '" + fieldName + "'가 비어 있습니다. " + consequence, owner);

            return false;
        }
    }
}
