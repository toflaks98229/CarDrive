using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 게임이 인식하는 행동 하나입니다.
    ///
    /// <b>키가 아니라 뜻으로 부릅니다.</b> 컴포넌트는 "LeftShift를 눌렀는가"가 아니라
    /// "달리려 하는가"를 묻습니다. 그래야 키를 바꿔도 묻는 쪽이 그대로입니다.
    /// </summary>
    public enum GameAction
    {
        /// <summary>상호작용 (문·운전대·음료·침대)</summary>
        Interact,

        /// <summary>달리기</summary>
        Sprint,

        /// <summary>점프</summary>
        Jump,

        /// <summary>앉기</summary>
        Crouch,

        /// <summary>차량 제동</summary>
        Brake,

        /// <summary>배뇨 해소</summary>
        Relieve,

        /// <summary>앙크 공격</summary>
        Attack,

        /// <summary>물건 들기·내려놓기</summary>
        Carry
    }

    /// <summary>
    /// 게임 입력을 읽는 <b>유일한 창구</b>입니다.
    ///
    /// <b>왜 만들었는가.</b> 예전에는 입력을 열두 개 파일이 각자 읽었습니다.
    /// <c>KeyCode</c> 필드가 컴포넌트마다 하나씩 흩어져 열세 개였고, 축 이름
    /// ("Horizontal", "Mouse X")도 여섯 군데에 문자열로 박혀 있었습니다. 그래서 셋이 막혀 있었습니다.
    ///  1. <b>키 재설정</b> — 고칠 곳이 열세 군데라 사실상 불가능했습니다.
    ///  2. <b>게임패드·Input System</b> — 갈아탈 경계 자체가 없었습니다.
    ///  3. <b>조작 안내 UI</b> — 어떤 행동에 어떤 키가 걸려 있는지 물어볼 곳이 없었습니다.
    ///
    /// 그리고 더 나빴던 것이 하나 더 있습니다. <see cref="GameInputGate"/> 확인을
    /// <b>호출부가 저마다 손으로 반복</b>했습니다. 한 곳이라도 잊으면 오버레이 위에서
    /// 앙크가 발사되거나 시점이 돌아갑니다. 실수가 조용히 새어 나가는 구조였습니다.
    ///
    /// <b>이제 게이트 검사는 여기 안에만 있습니다.</b> 아래 프로퍼티들은 입력이 막혀 있으면
    /// 0과 false를 돌려주므로, 읽는 쪽은 게이트를 몰라도 됩니다.
    ///
    /// <b>게이트를 알아야 하는 경우도 있습니다.</b> 누르고 있던 것을 놓아야 하거나
    /// (앙크를 내린다) 지금 자세를 그대로 얼려야 할 때(앉은 채로 유지)는 값이 0이 되는 것만으로
    /// 부족합니다. 그런 곳은 <see cref="Suspended"/>를 직접 보세요.
    ///
    /// <b>이 클래스는 읽기만 합니다.</b> 입력을 <em>막는</em> 것은 <see cref="GameInputGate"/>가
    /// 계속 맡습니다. (오버레이가 Push/Pop) 읽기와 막기를 한 클래스에 섞지 않습니다.
    ///
    /// <b>나중에 Input System으로 갈아탈 때</b>는 이 파일 안만 고치면 됩니다.
    /// 호출부는 이미 키가 아니라 뜻으로 묻고 있기 때문입니다.
    /// </summary>
    public static class GameInput
    {
        // --- Constants ---

        /// <summary>좌우 이동·조향 축입니다. (Project Settings의 Input Manager 이름)</summary>
        private const string AxisMoveX = "Horizontal";

        /// <summary>전후 이동·스로틀 축입니다.</summary>
        private const string AxisMoveZ = "Vertical";

        /// <summary>마우스 좌우 축입니다.</summary>
        private const string AxisLookX = "Mouse X";

        /// <summary>마우스 상하 축입니다.</summary>
        private const string AxisLookY = "Mouse Y";

        // --- Private Member Variables ---

        /// <summary>
        /// 행동별로 어떤 키가 걸려 있는지입니다. <see cref="GameAction"/>의 값을 색인으로 씁니다.
        ///
        /// 마우스 버튼도 <c>KeyCode.Mouse0</c>으로 함께 담습니다. 유니티에서
        /// <c>GetKey(KeyCode.Mouse0)</c>과 <c>GetMouseButton(0)</c>은 같은 것을 보므로,
        /// 굳이 두 갈래로 나눌 이유가 없습니다. 하나로 담아 두면 재설정 UI도 한 종류만 다루면 됩니다.
        /// </summary>
        private static KeyCode[] bindings = CreateDefaultBindings();

        // --- Public Properties : 상태 ---

        /// <summary>
        /// 지금 게임 입력이 막혀 있는지입니다.
        ///
        /// 아래 프로퍼티들이 이미 이 값을 반영하므로 <b>보통은 볼 필요가 없습니다.</b>
        /// 막히는 순간 무언가를 <em>정리</em>해야 하는 곳에서만 쓰세요.
        /// (누르고 있던 앙크 내리기, 앉은 자세 유지하기)
        /// </summary>
        public static bool Suspended { get { return GameInputGate.Suspended; } }

        // --- Public Properties : 이동 (도보) ---

        /// <summary>
        /// 도보 좌우 이동입니다. -1, 0, 1 중 하나로 <b>즉시</b> 바뀝니다.
        /// 걸을 때는 가속을 <see cref="Gameplay.PlayerFootMotor"/>가 직접 다루므로
        /// 축에서까지 부드럽게 만들면 반응이 두 번 뭉개집니다.
        /// </summary>
        public static float MoveX { get { return Suspended ? 0f : Input.GetAxisRaw(AxisMoveX); } }

        /// <summary>도보 전후 이동입니다. <see cref="MoveX"/>와 같은 이유로 즉시 바뀝니다.</summary>
        public static float MoveZ { get { return Suspended ? 0f : Input.GetAxisRaw(AxisMoveZ); } }

        // --- Public Properties : 주행 ---

        /// <summary>
        /// 차량 조향입니다. -1에서 1까지 <b>서서히</b> 오갑니다.
        /// 도보와 달리 축의 완만함을 그대로 씁니다. 핸들은 원래 즉시 꺾이지 않습니다.
        /// </summary>
        public static float Steer { get { return Suspended ? 0f : Input.GetAxis(AxisMoveX); } }

        /// <summary>차량 스로틀입니다. 음수면 후진입니다.</summary>
        public static float Throttle { get { return Suspended ? 0f : Input.GetAxis(AxisMoveZ); } }

        /// <summary>차량 제동 중인지입니다.</summary>
        public static bool Brake { get { return GetKey(GameAction.Brake); } }

        // --- Public Properties : 시점 ---

        /// <summary>
        /// 이번 프레임에 마우스가 좌우로 움직인 양입니다.
        ///
        /// <b>속도가 아니라 이동량입니다.</b> 여기에 <c>Time.deltaTime</c>을 곱하면 안 됩니다.
        /// 이유는 <see cref="Gameplay.PlayerCameraController"/>의 주석에 적어 두었습니다.
        /// </summary>
        public static float LookX { get { return Suspended ? 0f : Input.GetAxis(AxisLookX); } }

        /// <summary>이번 프레임에 마우스가 상하로 움직인 양입니다.</summary>
        public static float LookY { get { return Suspended ? 0f : Input.GetAxis(AxisLookY); } }

        // --- Public Properties : 행동 ---

        /// <summary>이번 프레임에 상호작용 키를 눌렀는지입니다.</summary>
        public static bool InteractPressed { get { return GetKeyDown(GameAction.Interact); } }

        /// <summary>달리기 키를 누르고 있는지입니다.</summary>
        public static bool Sprint { get { return GetKey(GameAction.Sprint); } }

        /// <summary>이번 프레임에 점프 키를 눌렀는지입니다.</summary>
        public static bool JumpPressed { get { return GetKeyDown(GameAction.Jump); } }

        /// <summary>앉기 키를 누르고 있는지입니다. (누르는 동안만 앉는 방식에 씁니다)</summary>
        public static bool Crouch { get { return GetKey(GameAction.Crouch); } }

        /// <summary>이번 프레임에 앉기 키를 눌렀는지입니다. (토글 방식에 씁니다)</summary>
        public static bool CrouchPressed { get { return GetKeyDown(GameAction.Crouch); } }

        /// <summary>배뇨 키를 누르고 있는지입니다.</summary>
        public static bool Relieve { get { return GetKey(GameAction.Relieve); } }

        /// <summary>이번 프레임에 배뇨 키를 눌렀는지입니다. (연타 압력에 씁니다)</summary>
        public static bool RelievePressed { get { return GetKeyDown(GameAction.Relieve); } }

        /// <summary>이번 프레임에 공격 버튼을 눌렀는지입니다.</summary>
        public static bool AttackPressed { get { return GetKeyDown(GameAction.Attack); } }

        /// <summary>이번 프레임에 공격 버튼을 뗐는지입니다.</summary>
        public static bool AttackReleased { get { return GetKeyUp(GameAction.Attack); } }

        /// <summary>이번 프레임에 들기·내려놓기 버튼을 눌렀는지입니다.</summary>
        public static bool CarryPressed { get { return GetKeyDown(GameAction.Carry); } }

        // --- Public Methods : 일반 조회 ---

        /// <summary>
        /// 이 행동의 키를 누르고 있는지 확인합니다. 입력이 막혀 있으면 false입니다.
        /// </summary>
        /// <param name="action">확인할 행동</param>
        /// <returns>누르고 있으면 true</returns>
        public static bool GetKey(GameAction action)
        {
            if (Suspended) return false;
            return Input.GetKey(GetBinding(action));
        }

        /// <summary>
        /// 이 행동의 키를 이번 프레임에 눌렀는지 확인합니다. 입력이 막혀 있으면 false입니다.
        /// </summary>
        /// <param name="action">확인할 행동</param>
        /// <returns>이번 프레임에 눌렀으면 true</returns>
        public static bool GetKeyDown(GameAction action)
        {
            if (Suspended) return false;
            return Input.GetKeyDown(GetBinding(action));
        }

        /// <summary>
        /// 이 행동의 키를 이번 프레임에 뗐는지 확인합니다. 입력이 막혀 있으면 false입니다.
        /// </summary>
        /// <param name="action">확인할 행동</param>
        /// <returns>이번 프레임에 뗐으면 true</returns>
        public static bool GetKeyUp(GameAction action)
        {
            if (Suspended) return false;
            return Input.GetKeyUp(GetBinding(action));
        }

        // --- Public Methods : 바인딩 ---

        /// <summary>
        /// 이 행동에 걸린 키를 돌려줍니다.
        /// </summary>
        /// <param name="action">조회할 행동</param>
        /// <returns>걸려 있는 키. 알 수 없는 행동이면 <c>KeyCode.None</c>입니다.</returns>
        public static KeyCode GetBinding(GameAction action)
        {
            int index = (int)action;
            if (index < 0 || index >= bindings.Length) return KeyCode.None;
            return bindings[index];
        }

        /// <summary>
        /// 이 행동에 걸린 키를 바꿉니다. 재설정 UI가 부를 자리입니다.
        /// </summary>
        /// <param name="action">바꿀 행동</param>
        /// <param name="key">새로 걸 키</param>
        public static void SetBinding(GameAction action, KeyCode key)
        {
            int index = (int)action;
            if (index < 0 || index >= bindings.Length) return;
            bindings[index] = key;
        }

        /// <summary>모든 키를 기본값으로 되돌립니다.</summary>
        public static void ResetBindings()
        {
            bindings = CreateDefaultBindings();
        }

        /// <summary>
        /// 안내 문구에 넣을 키 이름을 돌려줍니다.
        ///
        /// 마우스 버튼은 <c>Mouse0</c>이 아니라 "좌클릭"으로 보여 줍니다.
        /// 화면에 그대로 내보내도 읽히는 이름이어야 하기 때문입니다.
        /// </summary>
        /// <param name="action">이름을 물어볼 행동</param>
        /// <returns>사람이 읽을 키 이름</returns>
        public static string GetBindingName(GameAction action)
        {
            KeyCode key = GetBinding(action);
            switch (key)
            {
                case KeyCode.Mouse0: return "좌클릭";
                case KeyCode.Mouse1: return "우클릭";
                case KeyCode.Mouse2: return "휠클릭";
                case KeyCode.Space: return "스페이스";
                case KeyCode.LeftShift: return "Shift";
                case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl: return "Ctrl";
                case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt: return "Alt";
                case KeyCode.RightAlt: return "Alt";
                case KeyCode.Escape: return "ESC";
                case KeyCode.Return: return "Enter";
                default: return key.ToString();
            }
        }

        // --- Public Methods : 게이트를 무시하는 조회 ---

        /// <summary>
        /// 게이트를 <b>무시하고</b> 키를 확인합니다.
        ///
        /// 오버레이를 여닫는 키 전용입니다. 오버레이가 스스로 게이트를 걸어 두므로,
        /// 게이트를 지키면 <b>한 번 연 오버레이를 닫을 수 없게 됩니다.</b>
        /// 게임 플레이 입력에는 쓰지 마세요. 그러라고 있는 것이 위의 프로퍼티들입니다.
        /// </summary>
        /// <param name="key">확인할 키. <c>KeyCode.None</c>이면 항상 false입니다.</param>
        /// <returns>이번 프레임에 눌렀으면 true</returns>
        public static bool GetKeyDownRaw(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            return Input.GetKeyDown(key);
        }

        // --- Private Methods ---

        /// <summary>
        /// 기본 키 배치를 만듭니다.
        /// 배열의 자리는 <see cref="GameAction"/>의 순서와 반드시 같아야 합니다.
        /// </summary>
        /// <returns>행동 수만큼의 기본 키 배열</returns>
        private static KeyCode[] CreateDefaultBindings()
        {
            KeyCode[] defaults = new KeyCode[System.Enum.GetValues(typeof(GameAction)).Length];

            defaults[(int)GameAction.Interact] = KeyCode.E;
            defaults[(int)GameAction.Sprint] = KeyCode.LeftShift;
            defaults[(int)GameAction.Jump] = KeyCode.Space;
            defaults[(int)GameAction.Crouch] = KeyCode.LeftControl;
            defaults[(int)GameAction.Brake] = KeyCode.Space;
            defaults[(int)GameAction.Relieve] = KeyCode.P;
            defaults[(int)GameAction.Attack] = KeyCode.Mouse0;
            defaults[(int)GameAction.Carry] = KeyCode.Mouse0;

            return defaults;
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 키 배치를 기본값으로 되돌립니다.
        /// 에디터에서 도메인 리로드를 꺼 두면 static 값이 지난 실행에서 그대로 남기 때문입니다.
        ///
        /// 나중에 재설정을 저장하게 되면 <b>여기서 불러오면 됩니다.</b>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            bindings = CreateDefaultBindings();
        }
    }
}
