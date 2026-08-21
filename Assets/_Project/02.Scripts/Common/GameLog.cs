using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace CarDrive.Common
{
    /// <summary>
    /// 프로젝트 전역에서 쓰는 중앙 로깅 창구입니다.
    ///
    /// <b>왜 만들었는가.</b> 이 프로젝트의 로그는 <c>Debug.Log("WorldStreamer: ...")</c>처럼
    /// <b>클래스 이름을 문자열로 앞에 붙이는</b> 관례로 쓰여 왔습니다. 관례는 잘 지켜졌지만
    /// 두 가지가 막혀 있었습니다.
    ///  1. <b>끌 수가 없습니다.</b> 타일이 켜질 때마다, 상태가 바뀔 때마다 찍히는 로그가
    ///     정작 문제를 볼 때 콘솔을 덮습니다. 끄려면 코드를 고쳐야 했습니다.
    ///  2. <b>릴리즈에 그대로 남습니다.</b> 문자열 연결은 호출되는 한 항상 할당을 만듭니다.
    ///
    /// 그래서 채널로 나누고, <c>[Conditional]</c>로 릴리즈에서 <b>인자 평가까지 통째로</b> 지웁니다.
    /// (<see cref="Error"/>만은 빌드에서도 남깁니다 — 릴리즈에서 나는 오류가 가장 알기 어렵습니다)
    ///
    /// <c>[HideInCallstack]</c> 덕분에 콘솔을 더블클릭하면 이 래퍼가 아니라 <b>실제 호출 지점</b>으로 갑니다.
    /// </summary>
    public static class GameLog
    {
        // --- Public Types ---

        /// <summary>
        /// 로그가 어느 갈래에서 나왔는지 구분합니다. 채널 단위로 켜고 끕니다.
        /// </summary>
        public enum Channel
        {
            /// <summary>부트스트랩·DI·세이브 등 코어 인프라입니다.</summary>
            Core = 0,

            /// <summary>지형 스트리밍·컬링·시야 거리 계열입니다. <b>가장 시끄러운 채널</b>입니다.</summary>
            World,

            /// <summary>시간·날씨·니즈·경제 등 시뮬레이션 계열입니다.</summary>
            Simulation,

            /// <summary>플레이어·차량·상호작용 계열입니다.</summary>
            Player,

            /// <summary>적과 유령 계열입니다.</summary>
            Enemy,

            /// <summary>HUD와 화면 표시 계열입니다.</summary>
            UI
        }

        // --- Constants ---

        /// <summary>채널별 콘솔 표시 색상입니다. 인덱스가 <see cref="Channel"/> 값입니다.</summary>
        private static readonly string[] ChannelColors =
        {
            "#61AFEF", // Core
            "#98C379", // World
            "#56B6C2", // Simulation
            "#E5C07B", // Player
            "#E06C75", // Enemy
            "#C678DD", // UI
        };

        // --- Private Member Variables ---

        /// <summary>채널별 출력 허용 여부입니다. 인덱스가 <see cref="Channel"/> 값입니다.</summary>
        private static readonly bool[] ChannelEnabled;

        // --- Constructors ---

        /// <summary>
        /// 채널 토글 상태를 준비합니다. 에디터에서는 지난번에 꺼 둔 것을 그대로 복원합니다.
        /// </summary>
        static GameLog()
        {
            int count = System.Enum.GetValues(typeof(Channel)).Length;
            ChannelEnabled = new bool[count];

            for (int i = 0; i < count; i++)
            {
#if UNITY_EDITOR
                ChannelEnabled[i] = UnityEditor.EditorPrefs.GetBool(PrefKey((Channel)i), true);
#else
                ChannelEnabled[i] = true;
#endif
            }
        }

        // --- Public Methods : 채널 ---

        /// <summary>
        /// 이 채널의 출력이 켜져 있는지 확인합니다.
        /// </summary>
        /// <param name="channel">확인할 채널</param>
        /// <returns>켜져 있으면 true</returns>
        public static bool IsChannelEnabled(Channel channel)
        {
            return ChannelEnabled[(int)channel];
        }

        /// <summary>
        /// 채널의 출력을 켜거나 끕니다. 에디터에서는 다음 실행에도 유지됩니다.
        /// </summary>
        /// <param name="channel">대상 채널</param>
        /// <param name="enabled">켤지 여부</param>
        public static void SetChannelEnabled(Channel channel, bool enabled)
        {
            ChannelEnabled[(int)channel] = enabled;
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool(PrefKey(channel), enabled);
#endif
        }

        // --- Public Methods : 출력 ---

        /// <summary>
        /// 정보 로그입니다. <b>릴리즈 빌드에서는 호출 자체가 사라집니다.</b>
        /// </summary>
        /// <param name="channel">로그가 나온 갈래</param>
        /// <param name="message">남길 내용</param>
        /// <param name="context">콘솔에서 클릭했을 때 선택될 대상 (선택)</param>
        /// <param name="file">호출한 파일. 컴파일러가 채웁니다 — 직접 넘기지 마세요.</param>
        /// <param name="line">호출한 줄. 컴파일러가 채웁니다.</param>
        [HideInCallstack]
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(Channel channel, string message, Object context = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (!IsChannelEnabled(channel)) return;
            Debug.Log(Format(channel, message, file, line), context);
        }

        /// <summary>
        /// 값 하나를 끼워 넣는 정보 로그입니다. <b>채널이 켜져 있을 때만 문자열을 만듭니다.</b>
        ///
        /// 보간 문자열(<c>$"..."</c>)을 쓰지 마세요. 그것은 채널이 꺼져 있어도 먼저 조립됩니다.
        /// </summary>
        /// <typeparam name="T0">끼워 넣을 값의 타입</typeparam>
        /// <param name="channel">로그가 나온 갈래</param>
        /// <param name="format"><c>{0}</c> 자리표시자를 쓰는 형식 문자열</param>
        /// <param name="arg0">끼워 넣을 값</param>
        /// <param name="context">콘솔에서 클릭했을 때 선택될 대상 (선택)</param>
        /// <param name="file">호출한 파일. 컴파일러가 채웁니다.</param>
        /// <param name="line">호출한 줄. 컴파일러가 채웁니다.</param>
        [HideInCallstack]
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void InfoFormat<T0>(Channel channel, string format, T0 arg0, Object context = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (!IsChannelEnabled(channel)) return;
            Debug.Log(Format(channel, string.Format(format, arg0), file, line), context);
        }

        /// <summary>
        /// 값 둘을 끼워 넣는 정보 로그입니다. 채널이 켜져 있을 때만 문자열을 만듭니다.
        /// </summary>
        /// <typeparam name="T0">첫 번째 값의 타입</typeparam>
        /// <typeparam name="T1">두 번째 값의 타입</typeparam>
        /// <param name="channel">로그가 나온 갈래</param>
        /// <param name="format"><c>{0}</c>, <c>{1}</c> 자리표시자를 쓰는 형식 문자열</param>
        /// <param name="arg0">첫 번째 값</param>
        /// <param name="arg1">두 번째 값</param>
        /// <param name="context">콘솔에서 클릭했을 때 선택될 대상 (선택)</param>
        /// <param name="file">호출한 파일. 컴파일러가 채웁니다.</param>
        /// <param name="line">호출한 줄. 컴파일러가 채웁니다.</param>
        [HideInCallstack]
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void InfoFormat<T0, T1>(Channel channel, string format, T0 arg0, T1 arg1, Object context = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (!IsChannelEnabled(channel)) return;
            Debug.Log(Format(channel, string.Format(format, arg0, arg1), file, line), context);
        }

        /// <summary>
        /// 경고 로그입니다. 릴리즈 빌드에서는 호출 자체가 사라집니다.
        /// </summary>
        /// <param name="channel">로그가 나온 갈래</param>
        /// <param name="message">남길 내용</param>
        /// <param name="context">콘솔에서 클릭했을 때 선택될 대상 (선택)</param>
        /// <param name="file">호출한 파일. 컴파일러가 채웁니다.</param>
        /// <param name="line">호출한 줄. 컴파일러가 채웁니다.</param>
        [HideInCallstack]
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Warn(Channel channel, string message, Object context = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (!IsChannelEnabled(channel)) return;
            Debug.LogWarning(Format(channel, message, file, line), context);
        }

        /// <summary>
        /// 오류 로그입니다. <b>릴리즈 빌드에도 남고, 채널을 꺼도 나옵니다.</b>
        ///
        /// 오류는 "지금 보고 싶지 않아서 끈 것"에 묻히면 안 됩니다.
        /// </summary>
        /// <param name="channel">로그가 나온 갈래</param>
        /// <param name="message">남길 내용</param>
        /// <param name="context">콘솔에서 클릭했을 때 선택될 대상 (선택)</param>
        /// <param name="file">호출한 파일. 컴파일러가 채웁니다.</param>
        /// <param name="line">호출한 줄. 컴파일러가 채웁니다.</param>
        [HideInCallstack]
        public static void Error(Channel channel, string message, Object context = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            Debug.LogError(Format(channel, message, file, line), context);
        }

        // --- Private Methods ---

        /// <summary>
        /// 채널 딱지와 호출 위치 링크를 붙여 최종 문자열을 만듭니다.
        /// </summary>
        /// <param name="channel">로그가 나온 갈래</param>
        /// <param name="message">본문</param>
        /// <param name="file">호출한 파일 경로</param>
        /// <param name="line">호출한 줄</param>
        /// <returns>콘솔에 넘길 문자열</returns>
        private static string Format(Channel channel, string message, string file, int line)
        {
#if UNITY_EDITOR
            // 콘솔에서 클릭하면 그 자리로 가도록 하이퍼링크를 답니다.
            string tag = "<color=" + ChannelColors[(int)channel] + "><b>[" + channel + "]</b></color> ";
            return tag + message + "\n<a href=\"" + file + "\" line=\"" + line + "\"></a>";
#else
            return "[" + channel + "] " + message;
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 채널 토글을 저장할 EditorPrefs 키를 만듭니다.
        /// </summary>
        /// <param name="channel">대상 채널</param>
        /// <returns>저장 키</returns>
        private static string PrefKey(Channel channel)
        {
            return "CarDrive.GameLog." + channel;
        }
#endif
    }
}
