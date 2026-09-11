using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 지금 <b>손댈 수 있는 것</b>에만 외곽선을 두릅니다.
    ///
    /// <b>왜 항상 두르지 않는가.</b> 외곽선은 셰이더에 진작 있었지만 재질 31 개가 전부
    /// 두께 0 이었습니다. 세계의 모든 물체에 선을 두르면 로우폴리 실루엣이 만화가 아니라
    /// <b>장난감</b>처럼 보이고, 무엇보다 <b>아무것도 가리키지 못합니다</b> — 다 굵으면
    /// 어느 것도 눈에 띄지 않습니다.
    ///
    /// 선은 <b>지금 누를 수 있는 것</b>을 가리킬 때 값을 합니다. 그래서 조준점에 걸린
    /// 대상에게만, 그것도 실제로 <c>CanInteract</c> 인 동안에만 켭니다.
    /// 문에 다가가면 문만 선을 얻고, 차에 타면 운전대만 얻습니다.
    ///
    /// <b>어떻게 켜는가.</b> 재질을 복제하지 않고 <see cref="MaterialPropertyBlock"/> 으로
    /// 그 렌더러의 <c>_OutlineWidth</c> 만 덮어씁니다. 재질을 복제하면 대상마다 사본이
    /// 생겨 배칭이 깨지고 메모리가 늘며, 무엇보다 <b>같은 재질을 쓰는 다른 물체까지</b>
    /// 같이 켜집니다(공유 재질이므로).
    ///
    /// ⚠ 프로퍼티 블록이 붙은 렌더러는 <b>SRP 배처에서 빠집니다.</b> 그래서 한 번에 한둘만
    /// 켜는 지금 쓰임에서는 괜찮지만, 이것으로 화면의 많은 물체를 칠하려 들면 안 됩니다.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [AddComponentMenu("CarDrive/상호작용 외곽선 (InteractionOutline)")]
    public class InteractionOutline : MonoBehaviour
    {
        // --- Constants ---

        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");

        // --- Public Member Variables ---

        /// <summary>비워 두면 씬에서 찾습니다.</summary>
        [Header("연동")]
        [Tooltip("비워 두면 GameContext 에서 찾습니다")]
        public PlayerInteractor interactor;

        /// <summary>
        /// 선의 두께입니다.
        ///
        /// 셰이더가 이 값에 <b>카메라까지의 거리를 곱하므로</b> 화면에서 보이는 굵기는
        /// 멀어져도 비슷하게 유지됩니다. 그래서 단위는 미터가 아니라 각도에 가깝습니다.
        /// </summary>
        [Header("선")]
        [Tooltip("셰이더가 거리를 곱하므로 화면에서 보이는 굵기입니다. 0.003~0.006 이 쓸 만합니다")]
        [Range(0f, 0.02f)]
        public float width = 0.004f;

        /// <summary>
        /// 켜지고 꺼지는 데 걸리는 시간(초)입니다.
        ///
        /// ⚠ <b>0 으로 두지 마십시오.</b> 조준점은 물체 경계에서 <b>떨렸다 붙었다</b> 합니다.
        /// 즉시 켜고 끄면 선이 깜빡여서 조준이 불안해 보입니다. 0.08 초만 줘도
        /// 그 떨림이 눈에 안 띕니다.
        /// </summary>
        [Tooltip("켜지고 꺼지는 데 걸리는 시간(초). 0 이면 경계에서 깜빡입니다")]
        [Range(0f, 0.5f)]
        public float fadeSeconds = 0.08f;

        /// <summary>
        /// 선의 색입니다.
        ///
        /// ⚠ <b>재질의 색을 그대로 쓰면 안 됩니다.</b> 재질에 적힌 외곽선 색은
        /// 그림용으로 거의 검정(0.08, 0.07, 0.10)인데, 이 게임의 차는 검습니다.
        /// 검은 차에 검은 선을 둘러 봐야 아무것도 안 보입니다 — 실제로 그렇게 찍혔습니다.
        ///
        /// 여기 선은 <b>그림이 아니라 신호</b>입니다. 밤에도 읽히도록 밝은 쪽으로 둡니다.
        /// </summary>
        [Tooltip("재질의 외곽선 색을 덮어씁니다. 이 선은 그림이 아니라 신호라 밝아야 합니다")]
        [ColorUsage(false, false)]
        public Color color = new Color(0.95f, 0.92f, 0.80f, 1f);

        // --- Private Member Variables ---

        /// <summary>선이 켜져 있거나 꺼지는 중인 렌더러들입니다.</summary>
        private readonly List<Mark> marks = new List<Mark>();

        /// <summary>직전 프레임의 대상입니다. 바뀔 때만 렌더러를 다시 모읍니다.</summary>
        private Component watched;

        private MaterialPropertyBlock block;

        // --- Private Types ---

        /// <summary>렌더러 하나와 지금 그 렌더러에 들어간 두께입니다.</summary>
        private struct Mark
        {
            public Renderer Renderer;
            public Component Owner;   // 이 렌더러가 누구의 것인가 (대상이 바뀌면 꺼야 합니다)
            public float Value;
        }

        // --- Unity Event Functions ---

        void Start()
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (interactor == null) interactor = GameContext.Resolve<PlayerInteractor>(this);

            if (interactor == null)
            {
                Debug.LogWarning("InteractionOutline: PlayerInteractor 를 찾지 못했습니다. 선을 두르지 않습니다.");
                enabled = false;
            }
        }

        /// <summary>
        /// <b>LateUpdate 입니다.</b> <see cref="PlayerInteractor"/> 가 Update 에서 조준을
        /// 다시 잽니다. 같은 프레임의 Update 에서 읽으면 실행 순서에 따라 <b>한 프레임 늦은</b>
        /// 대상을 보게 됩니다.
        /// </summary>
        void LateUpdate()
        {
            Tick(Target(), Time.deltaTime);
        }

        /// <summary>꺼질 때 두르던 선을 걷습니다. 안 걷으면 렌더러에 남습니다.</summary>
        void OnDisable()
        {
            for (int i = 0; i < marks.Count; i++) Push(marks[i].Renderer, 0f);

            marks.Clear();
            watched = null;
        }

        // --- Public Methods ---

        /// <summary>
        /// 한 프레임 진행합니다. <see cref="LateUpdate"/> 가 부르고, 테스트도 직접 부릅니다.
        ///
        /// <b>왜 갈라 두는가.</b> "무엇을 칠할지" 는 조준·거리·<c>CanInteract</c> 가 엮인
        /// 판단이고, "어떻게 칠할지" 는 렌더러에 값을 넣는 일입니다. 둘이 붙어 있으면
        /// 칠하는 쪽을 확인하려고 플레이어와 카메라와 콜라이더를 다 세워야 합니다.
        /// </summary>
        /// <param name="target">이번 프레임에 선을 두를 대상. 없으면 null</param>
        /// <param name="deltaTime">지난 시간(초)</param>
        public void Tick(Component target, float deltaTime)
        {
            if (block == null) block = new MaterialPropertyBlock();

            if (target != watched)
            {
                watched = target;
                Gather(target);
            }

            Fade(target, deltaTime);
        }

        // --- Private Methods ---

        /// <summary>
        /// 지금 선을 두를 대상입니다.
        ///
        /// <c>HasTarget</c> 이 <b>살아 있는지</b>와 <b>지금 누를 수 있는지</b>를 모두 봅니다.
        /// 그래서 다 마신 빈 병이나, 차 밖에서 바라본 운전대에는 선이 안 켜집니다.
        /// </summary>
        private Component Target()
        {
            if (interactor == null || interactor.IsBlocked || !interactor.HasTarget) return null;

            // IInteractable 은 인터페이스라 실물이 아닐 수 있습니다(테스트 대역 등).
            return interactor.CurrentInteractable as Component;
        }

        /// <summary>
        /// 대상의 렌더러를 모읍니다. 대상이 바뀔 때만 돕니다.
        ///
        /// ⚠ <b>대상이 자기 렌더러를 안 가진 경우가 있습니다.</b> 차 문이 그렇습니다 —
        /// <c>VehicleDoorInteractable</c> 은 <c>InteractionCollider</c> 라는 빈 오브젝트에
        /// 붙어 있고 거기엔 콜라이더뿐입니다. 그것만 보고 끝내면 <b>문에는 선이 영영
        /// 안 켜집니다.</b> 실제로 처음에 그렇게 만들어 놓고 "안 보인다" 고 할 뻔했습니다.
        ///
        /// 그럴 때는 <b>위로 올라가</b> 렌더러를 가진 가장 가까운 조상을 씁니다.
        /// 상호작용 콜라이더는 대개 "이 물체의 손잡이" 로 붙은 자식이므로,
        /// 사람이 만지려는 것은 그 부모입니다 — 문을 열면 차가 열립니다.
        /// </summary>
        private void Gather(Component target)
        {
            if (target == null) return;

            Transform at = target.transform;
            Renderer[] found = at.GetComponentsInChildren<Renderer>(false);

            while (!Usable(found) && at.parent != null)
            {
                at = at.parent;
                found = at.GetComponentsInChildren<Renderer>(false);
            }

            for (int i = 0; i < found.Length; i++)
            {
                Renderer r = found[i];

                // 외곽선 패스가 없는 재질에 넣어도 아무 일도 안 일어나지만,
                // 프로퍼티 블록을 붙이는 것만으로 <b>SRP 배칭에서 빠집니다.</b>
                // 그래서 쓸 재질인지 먼저 봅니다.
                if (r.sharedMaterial == null || !r.sharedMaterial.HasProperty(WidthId)) continue;

                if (Find(r) >= 0) continue;

                marks.Add(new Mark { Renderer = r, Owner = target, Value = 0f });
            }
        }

        /// <summary>외곽선을 그릴 수 있는 렌더러가 하나라도 있는지 봅니다.</summary>
        private static bool Usable(Renderer[] found)
        {
            for (int i = 0; i < found.Length; i++)
            {
                Renderer r = found[i];
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(WidthId)) return true;
            }

            return false;
        }

        /// <summary>이번 대상의 렌더러는 켜고, 남은 것은 끕니다. 0 이 되면 목록에서 뺍니다.</summary>
        private void Fade(Component target, float deltaTime)
        {
            float step = fadeSeconds > 0.0001f ? deltaTime / fadeSeconds : 1f;

            for (int i = marks.Count - 1; i >= 0; i--)
            {
                Mark m = marks[i];

                // 렌더러가 그 사이에 사라졌으면(집어 든 물건이 파괴되는 등) 그냥 버립니다.
                if (m.Renderer == null)
                {
                    marks.RemoveAt(i);
                    continue;
                }

                float want = (m.Owner == target && target != null) ? width : 0f;
                m.Value = Mathf.MoveTowards(m.Value, want, step * Mathf.Max(width, 0.0001f));

                Push(m.Renderer, m.Value);

                if (m.Value <= 0.0001f && want <= 0f)
                {
                    Push(m.Renderer, 0f);
                    marks.RemoveAt(i);
                    continue;
                }

                marks[i] = m;
            }
        }

        /// <summary>목록에서 그 렌더러의 자리를 찾습니다. 없으면 -1.</summary>
        private int Find(Renderer r)
        {
            for (int i = 0; i < marks.Count; i++)
            {
                if (marks[i].Renderer == r) return i;
            }

            return -1;
        }

        /// <summary>
        /// 그 렌더러에만 두께를 넣습니다.
        ///
        /// ⚠ <b>먼저 읽고 씁니다.</b> 다른 곳에서 이미 프로퍼티 블록을 쓰고 있을 수 있는데
        /// (풀 밀림·얼룩 등이 그렇습니다) 새 블록을 그냥 덮으면 그 값들이 <b>지워집니다.</b>
        /// </summary>
        private void Push(Renderer r, float value)
        {
            if (r == null) return;

            r.GetPropertyBlock(block);
            block.SetFloat(WidthId, value);
            block.SetColor(ColorId, color);
            r.SetPropertyBlock(block);
        }
    }
}
