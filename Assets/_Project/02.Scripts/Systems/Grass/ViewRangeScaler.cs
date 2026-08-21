using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// <see cref="CarDriveWorldSettings.rangeScale"/> 를 <b>보이는 쪽</b>에 적용합니다.
    /// 안개 끝, 카메라 파클립, 나무 그리는 거리 셋입니다.
    ///
    /// <b>왜 셋을 함께 만지는가.</b> 이 프로젝트는 <c>ViewDistanceSetup</c> 이
    /// <b>안개 끝을 기준</b>으로 파클립과 나무 거리를 파생시켜 맞춰 둡니다.
    /// 셋 중 하나만 줄이면 그 관계가 깨집니다 — 안개만 당기면 안개 밖 허공에 나무가 서 있고,
    /// 파클립만 당기면 안개가 끝나기도 전에 지형이 잘립니다.
    ///
    /// <b>기준값은 처음 본 것을 기억합니다.</b> 설정을 바꿔도 그 기준에 배율을 곱할 뿐이라,
    /// 배율을 1 로 되돌리면 원래대로 돌아옵니다. 값을 깎아 나가지 않습니다.
    ///
    /// <b>재질은 건드리지 않습니다.</b> 나무의 디더 페이드는 재질에 들어 있는데,
    /// 실행 중에 공유 재질을 고치면 <b>에디터에서 그 변경이 에셋에 저장됩니다.</b>
    /// 이 프로젝트가 ScriptableObject 로 이미 겪은 사고와 같은 종류입니다.
    /// 안개가 먼저 끝나므로 페이드가 그대로여도 눈에 띄지 않습니다.
    ///
    /// 씬에 없으면 게임이 시작될 때 스스로 하나 생겨납니다.
    /// </summary>
    [DefaultExecutionOrder(-97)]
    public class ViewRangeScaler : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>배율이 바뀌었는지 확인하는 주기(초)입니다. 옵션은 자주 바뀌지 않습니다.</summary>
        private const float CheckSeconds = 0.5f;

        // --- Private Member Variables ---

        /// <summary>마지막으로 반영한 배율입니다.</summary>
        private static float applied = -1f;

        /// <summary>다음 확인 시각입니다.</summary>
        private static float nextCheck;

        /// <summary>기준 안개 끝 거리입니다. 처음 본 값을 기억합니다.</summary>
        private static float baseFogEnd = -1f;

        /// <summary>기준 안개 시작 거리입니다.</summary>
        private static float baseFogStart = -1f;

        /// <summary>기준 안개 밀도입니다. 지수 안개일 때 씁니다.</summary>
        private static float baseFogDensity = -1f;

        /// <summary>기준 파클립 거리입니다.</summary>
        private static float baseFarClip = -1f;

        // --- Unity Event Functions ---

        /// <summary>주기마다 배율이 바뀌었는지 보고, 바뀌었으면 적용합니다.</summary>
        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckSeconds;

            CarDriveWorldSettings settings = CarDriveWorldSettings.Instance;
            float scale = Mathf.Clamp(settings.rangeScale, 0.05f, 1f);

            if (Mathf.Approximately(applied, scale)) return;

            Apply(scale);
            applied = scale;
        }

        // --- Private Methods ---

        /// <summary>
        /// 배율을 안개·파클립·나무 거리에 적용합니다.
        /// </summary>
        /// <param name="scale">곱할 배율</param>
        private static void Apply(float scale)
        {
            CaptureBaselineIfNeeded();

            // --- 안개 ---
            //
            // 선형이면 거리를 줄이고, 지수면 밀도를 키웁니다.
            // 지수 안개의 도달 거리는 밀도에 반비례하므로 나누는 것이 곱하는 것과 같습니다.
            if (RenderSettings.fogMode == FogMode.Linear)
            {
                if (baseFogEnd > 0f)
                {
                    RenderSettings.fogStartDistance = baseFogStart * scale;
                    RenderSettings.fogEndDistance = baseFogEnd * scale;
                }
            }
            else if (baseFogDensity > 0f)
            {
                RenderSettings.fogDensity = baseFogDensity / Mathf.Max(scale, 0.05f);
            }

            // --- 파클립 ---
            //
            // 안개가 끝나는 곳보다 조금 멀어야 합니다. 같거나 가까우면
            // 안개가 다 덮기도 전에 지형이 잘려 <b>하늘이 뚫려 보입니다.</b>
            Camera camera = GameContext.MainCamera;
            if (camera != null && baseFarClip > 0f)
            {
                camera.farClipPlane = Mathf.Max(10f, baseFarClip * scale);
            }

            // --- 나무 ---
            //
            // 안개 끝에 맞춥니다. ViewDistanceSetup 이 정한 관계를 그대로 지킵니다.
            // 여기서 안개 끝보다 짧게 잡으면 나무가 안개에 묻히기 전에 타일 단위로 사라집니다.
            float treeDistance = RenderSettings.fogMode == FogMode.Linear && baseFogEnd > 0f
                ? baseFogEnd * scale
                : (baseFarClip > 0f ? baseFarClip * scale : 0f);

            if (treeDistance > 0f)
            {
                Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
                for (int i = 0; i < terrains.Length; i++)
                {
                    if (terrains[i] == null) continue;
                    terrains[i].treeDistance = treeDistance;
                }
            }
        }

        /// <summary>
        /// 기준값을 처음 한 번만 기억합니다.
        ///
        /// <b>배율을 곱한 뒤의 값을 다시 기준으로 삼으면 안 됩니다.</b>
        /// 그러면 배율을 두 번 적용할 때마다 거리가 계속 깎여 나가고, 1 로 되돌려도
        /// 원래대로 돌아오지 않습니다.
        /// </summary>
        private static void CaptureBaselineIfNeeded()
        {
            if (baseFogEnd < 0f)
            {
                baseFogStart = RenderSettings.fogStartDistance;
                baseFogEnd = RenderSettings.fogEndDistance;
                baseFogDensity = RenderSettings.fogDensity;
            }

            if (baseFarClip < 0f)
            {
                Camera camera = GameContext.MainCamera;
                if (camera != null) baseFarClip = camera.farClipPlane;
            }
        }

        /// <summary>씬에 없으면 게임이 시작될 때 스스로 하나 생겨납니다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            GameObject go = new GameObject("ViewRangeScaler");
            go.hideFlags = HideFlags.DontSave;

            go.AddComponent<ViewRangeScaler>();
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때 정적 상태를 비웁니다.
        /// 기준값이 지난 실행에서 남으면 배율이 곱해진 값을 기준으로 삼게 됩니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            applied = -1f;
            nextCheck = 0f;
            baseFogEnd = -1f;
            baseFogStart = -1f;
            baseFogDensity = -1f;
            baseFarClip = -1f;
        }
    }
}
