using UnityEngine;

#if CONSOLEDISPLAY_INPUTSYSTEM
using UnityEngine.InputSystem;
#endif

namespace ConsoleDisplay.Showcase
{
    /// <summary>
    /// 어느 입력 시스템이 켜져 있든 같은 방식으로 입력을 읽습니다.
    ///
    /// <b>왜 이런 게 필요한가.</b> 유니티 프로젝트는 <b>구 Input Manager</b>, <b>새 Input System</b>,
    /// 또는 둘 다를 쓸 수 있습니다. 데모가 한쪽만 지원하면 <b>구매자 프로젝트의 절반에서
    /// 아무 반응 없이 조용히 안 움직입니다.</b> 에셋 데모에서 그것만큼 나쁜 첫인상이 없습니다.
    ///
    /// <c>CONSOLEDISPLAY_INPUTSYSTEM</c>은 asmdef의 versionDefines가 붙여 줍니다.
    /// 새 Input System 패키지가 없으면 그 코드는 컴파일조차 되지 않습니다.
    /// </summary>
    internal static class ShowcaseInput
    {
        /// <summary>앞뒤/좌우 이동입니다. x는 좌우, y는 앞뒤입니다.</summary>
        public static Vector2 Move
        {
            get
            {
#if CONSOLEDISPLAY_INPUTSYSTEM
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    float x = 0f;
                    float y = 0f;
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                    return new Vector2(x, y);
                }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
                return Vector2.zero;
#endif
            }
        }

        /// <summary>마우스가 이번 프레임에 움직인 양입니다.</summary>
        public static Vector2 Look
        {
            get
            {
#if CONSOLEDISPLAY_INPUTSYSTEM
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    // 새 입력 시스템의 delta는 프레임 단위 픽셀입니다. 구 시스템 감도에 맞춰 줄입니다.
                    return mouse.delta.ReadValue() * 0.05f;
                }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
                return Vector2.zero;
#endif
            }
        }

        /// <summary>
        /// 상호작용 키를 이번 프레임에 눌렀는지입니다.
        ///
        /// <b>좌클릭은 일부러 뺐습니다.</b> 콘솔 창이 뜨면 포커스가 그쪽으로 가는데,
        /// 게임으로 돌아오려고 화면을 클릭하면 그 클릭이 버튼을 다시 누른 것으로 읽혀
        /// <b>방금 띄운 창이 곧바로 닫히고 다시 열립니다.</b> 실제로 그 증상을 겪었습니다.
        /// 창을 띄우는 키와 게임으로 돌아오는 동작은 겹치면 안 됩니다.
        /// </summary>
        public static bool InteractPressed
        {
            get
            {
#if CONSOLEDISPLAY_INPUTSYSTEM
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                {
                    return true;
                }

                if (keyboard != null)
                {
                    return false;
                }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.E);
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// 화면을 클릭했는지입니다. 풀린 마우스 잠금을 다시 잡는 데 씁니다.
        ///
        /// 상호작용과 <b>겹치지 않아야</b> 합니다. 겹치면 게임으로 돌아오려는 클릭이
        /// 버튼을 누른 것으로 읽혀 창이 다시 열립니다. 그래서 상호작용은 <c>E</c>만 받습니다.
        /// </summary>
        public static bool PointerPressed
        {
            get
            {
#if CONSOLEDISPLAY_INPUTSYSTEM
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    return mouse.leftButton.wasPressedThisFrame;
                }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButtonDown(0);
#else
                return false;
#endif
            }
        }

        /// <summary>마우스 잠금을 풀고 싶다는 신호입니다.</summary>
        public static bool CancelPressed
        {
            get
            {
#if CONSOLEDISPLAY_INPUTSYSTEM
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    return keyboard.escapeKey.wasPressedThisFrame;
                }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Escape);
#else
                return false;
#endif
            }
        }
    }
}
