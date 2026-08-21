using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// "플레이어가 지금 어느 장소에 있는가"를 주기적으로 다시 판정합니다.
    ///
    /// <b>왜 따로 만들었는가.</b> 예전에는 이 판정을 <see cref="WorldStreamer"/>가 타일을
    /// 켜고 끄는 김에 함께 했습니다. 두 가지가 한 호출에 묶여 있어서 문제가 셋이었습니다.
    ///  1. <b>지형이 멈추면 장소도 멈췄습니다.</b> 스트리머는 따라다닐 대상을 찾지 못하면
    ///     그 자리에서 반환하는데, 그 반환이 장소 판정까지 데리고 나갔습니다.
    ///  2. <b>기준이 카메라였습니다.</b> 카메라는 타고 내릴 때 부모가 바뀌고 두리번거리면
    ///     위치까지 흔들립니다. 지형을 켜고 끄는 데는 충분해도 "마을에 들어왔는가"를
    ///     가르는 기준으로는 불안정합니다.
    ///  3. 앞으로 의뢰·상점·대화가 전부 장소에 매달릴 텐데, 그것들이 <b>지형 스트리밍의
    ///     부산물</b>에 매달리게 됩니다.
    ///
    /// 그래서 판정을 자기 주기로 옮기고, 기준도 바꿨습니다.
    /// <see cref="PlayerModeController.PickupAnchor"/>는 이 프로젝트가 이미 갖고 있는
    /// <b>"플레이어가 있는 자리"의 단일 정의</b>입니다. 주행 중이면 차량, 도보면 도보 리그를
    /// 가리키므로 상태가 바뀌어도 흔들리지 않습니다.
    ///
    /// <b>씬에 두어도 되고 두지 않아도 됩니다.</b> 두면 그것을 쓰고, 없으면 게임이 시작될 때
    /// 스스로 하나 생겨납니다. 장소 판정이 "누가 씬에 넣는 것을 잊었는가"에 좌우되면
    /// 위 1번을 다른 방식으로 되풀이하는 셈이기 때문입니다.
    /// (<see cref="Systems.TerrainChunkCuller"/>가 같은 방식으로 생겨납니다)
    /// </summary>
    public class WorldLocationTracker : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>
        /// 판정 주기(초)입니다.
        ///
        /// 매 프레임 할 이유가 없습니다. 장소는 반경 수십 미터짜리라 사람이든 차든
        /// 한 틱 사이에 가로지를 수 없습니다. 예전 <see cref="WorldStreamer"/>가 쓰던 값과
        /// 같게 두어, 이 변경으로 판정 빈도가 달라지지 않게 했습니다.
        /// </summary>
        [Header("판정")]
        [Tooltip("장소를 다시 판정하는 주기(초). 매 프레임 할 필요가 없습니다.")]
        public float checkInterval = 0.25f;

        // --- Private Member Variables ---

        /// <summary>기준 위치를 물어볼 컨트롤러입니다. 없으면 카메라로 물러섭니다.</summary>
        private PlayerModeController mode;

        /// <summary>다음 판정까지 남은 시간(초)입니다. 0으로 시작해 첫 프레임에 한 번 돕니다.</summary>
        private float timer;

        // --- Unity Event Functions ---

        /// <summary>자신을 등록합니다. 이미 다른 것이 있으면 자신을 끕니다.</summary>
        void Awake()
        {
            // 등록이 거부되면 이미 다른 것이 있다는 뜻입니다. (경고는 GameContext가 남깁니다)
            if (!GameContext.Register(this))
            {
                enabled = false;
                return;
            }
        }

        /// <summary>등록을 해제합니다.</summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
        }

        /// <summary>주기가 되면 지금 자리로 장소를 다시 판정합니다.</summary>
        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            timer = Mathf.Max(0.02f, checkInterval);

            Transform anchor = ResolveAnchor();
            if (anchor == null) return;

            WorldLocation.UpdateCurrent(anchor.position);
        }

        // --- Private Methods ---

        /// <summary>
        /// 거리를 잴 기준을 정합니다.
        ///
        /// <see cref="PlayerModeController.PickupAnchor"/>가 첫째입니다. 주행 중이면 차량,
        /// 도보면 도보 리그를 돌려주므로 탑승 상태가 바뀌어도 자리가 튀지 않습니다.
        ///
        /// 컨트롤러가 없으면 카메라로 물러섭니다. 예전 동작이 그랬으므로, 도보 리그 없이
        /// 지형만 확인하는 씬에서도 장소 판정이 계속 돌아갑니다.
        ///
        /// <c>Get</c>을 쓰고 <c>Resolve</c>를 쓰지 않는 것은 <b>없어도 되기 때문</b>입니다.
        /// 없을 때마다 씬을 뒤지고 경고를 남길 자리가 아닙니다.
        /// </summary>
        /// <returns>거리를 잴 기준 Transform. 카메라조차 없으면 null입니다.</returns>
        private Transform ResolveAnchor()
        {
            if (mode == null) mode = GameContext.Get<PlayerModeController>();
            if (mode != null) return mode.PickupAnchor;

            return GameContext.MainCameraTransform;
        }

        /// <summary>
        /// 씬에 없으면 게임이 시작될 때 스스로 하나 생겨납니다.
        ///
        /// <c>AfterSceneLoad</c>는 씬에 놓인 것들의 <c>Awake</c>가 모두 끝난 뒤에 불립니다.
        /// 그래서 여기서 등록부를 보면 <b>씬에 이미 있는지</b>를 정확히 알 수 있습니다.
        ///
        /// 숨기지 않는 것은 의도입니다. <see cref="Systems.TerrainChunkCuller"/>는
        /// <c>HideAndDontSave</c>로 하이어라키에서 감추지만, 그쪽은 눈에 보이지 않는 렌더링
        /// 도우미입니다. 이쪽은 게임 규칙에 관여하므로, 장소 이벤트가 안 뜰 때
        /// <b>하이어라키에서 이것이 도는지 눈으로 확인할 수 있어야</b> 합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnIfMissing()
        {
            if (GameContext.Get<WorldLocationTracker>() != null) return;

            GameObject go = new GameObject("WorldLocationTracker (자동 생성)");
            go.hideFlags = HideFlags.DontSave;

            go.AddComponent<WorldLocationTracker>();
            DontDestroyOnLoad(go);
        }
    }
}
