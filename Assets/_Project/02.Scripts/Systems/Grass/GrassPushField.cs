using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>
    /// <see cref="GrassPusher"/>들의 자리를 모아 풀 셰이더에 넘깁니다.
    ///
    /// 화면을 위에서 찍어 마스크 텍스처를 만드는 방식도 있지만, 이 게임에는 과합니다.
    /// 땅을 밟는 것은 차 한 대와 플레이어, 유령 몇뿐이라 <b>좌표를 그대로 넘기는 편</b>이
    /// 훨씬 싸고, 렌더 타깃도 카메라도 더 필요하지 않습니다.
    ///
    /// 씬에 무언가를 놓아 둘 필요가 없습니다. 게임이 시작될 때 스스로 생겨납니다.
    /// 씬에 둔 오브젝트는 언젠가 실수로 지워지지만 이건 그럴 일이 없습니다.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class GrassPushField : MonoBehaviour
    {
        // --- Constants ---

        /// <summary>
        /// 한 번에 넘길 수 있는 최대 개수입니다.
        /// <b>셰이더에 적힌 수와 반드시 같아야 합니다.</b> (LowPolyGrass.shader 의 GRASS_PUSHER_MAX)
        /// </summary>
        public const int MaxPushers = 16;

        // --- Private Member Variables ---

        /// <summary>셰이더에 넘길 자리들입니다. xyz가 위치, w가 반경입니다.</summary>
        private static readonly Vector4[] buffer = new Vector4[MaxPushers];

        /// <summary>가까운 것부터 고르기 위해 거리와 함께 담아 두는 임시 목록입니다.</summary>
        private static readonly List<Entry> sorted = new List<Entry>(MaxPushers * 2);

        /// <summary>지나간 길에 남는 자국을 담아 두는 지도입니다.</summary>
        private static GrassTrampleMap trample;

        private static readonly int PushersId = Shader.PropertyToID("_GrassPushers");
        private static readonly int CountId = Shader.PropertyToID("_GrassPusherCount");

        /// <summary>거리로 줄 세우기 위한 한 항목입니다.</summary>
        private struct Entry
        {
            public float distanceSqr;
            public Vector4 packed;
        }

        // --- Unity Event Functions ---

        /// <summary>
        /// 움직임이 모두 끝난 뒤에 자리를 넘깁니다.
        /// 실행 순서를 뒤로 미뤄 둔 것도 같은 이유입니다. 먼저 넘기면 한 프레임 늦은 자리가
        /// 셰이더로 가서, 빠르게 달릴 때 눕는 자리가 차보다 뒤처져 보입니다.
        /// </summary>
        void LateUpdate()
        {
            UploadNow();
            StepMapNow(Time.deltaTime);
        }

        /// <summary>들고 있던 그림을 놓아 줍니다.</summary>
        void OnDestroy()
        {
            if (trample != null)
            {
                trample.Release();
                trample = null;
            }
        }

        // --- Private Methods ---

        /// <summary>
        /// 지금 있는 것들 중 <b>카메라에 가까운 것부터</b> 골라 셰이더에 넘깁니다.
        ///
        /// 멀리 있는 것은 어차피 풀이 눕는 게 보이지 않고, 넘길 수 있는 자리는 한정되어 있습니다.
        ///
        /// 밖에서도 부를 수 있게 열어 두었습니다. 편집 중에는 이 컴포넌트가 돌지 않아
        /// 풀이 눕는 모습을 확인할 수 없는데, 확인 도구가 <b>이 코드를 그대로</b> 부르면
        /// 게임에서 도는 것과 같은 결과를 볼 수 있습니다.
        /// </summary>
        public static void UploadNow()
        {
            IReadOnlyList<GrassPusher> pushers = GrassPusher.All;

            Vector3 eye = GameContext.MainCameraPosition;

            sorted.Clear();

            for (int i = 0; i < pushers.Count; i++)
            {
                GrassPusher pusher = pushers[i];
                if (pusher == null || pusher.radius <= 0.01f) continue;

                Vector3 position = pusher.transform.position;

                Entry entry;
                entry.distanceSqr = (position - eye).sqrMagnitude;
                entry.packed = new Vector4(position.x, position.y, position.z, pusher.radius);

                sorted.Add(entry);
            }

            if (sorted.Count == 0)
            {
                Shader.SetGlobalFloat(CountId, 0f);
                return;
            }

            sorted.Sort(CompareByDistance);

            int count = Mathf.Min(sorted.Count, MaxPushers);
            for (int i = 0; i < count; i++)
            {
                buffer[i] = sorted[i].packed;
            }

            // 남는 자리는 반경 0으로 채웁니다. 셰이더가 개수만 보고 돌지만,
            // 예전 값이 남아 있으면 개수가 늘어난 순간 엉뚱한 자리가 눌립니다.
            for (int i = count; i < MaxPushers; i++) buffer[i] = Vector4.zero;

            Shader.SetGlobalVectorArray(PushersId, buffer);
            Shader.SetGlobalFloat(CountId, count);
        }

        /// <summary>
        /// 지나간 길에 남는 자국 지도를 한 장 갱신합니다.
        ///
        /// 겹쳐 있는 동안만 눕히는 일(UploadNow)과 나눠 둔 이유가 있습니다.
        /// 차체처럼 <b>자국을 남기면 안 되는 것</b>은 앞쪽만 타고, 바퀴처럼 남겨야 하는 것은
        /// 양쪽을 다 탑니다. 차체까지 자국을 남기면 차 폭만큼 넓은 띠가 생겨
        /// 바퀴 자국이 아니라 불도저가 지나간 자리처럼 보입니다.
        ///
        /// 밖에서도 부를 수 있게 열어 두었습니다. 확인 도구가 이 코드를 그대로 부릅니다.
        /// </summary>
        /// <param name="deltaTime">지난 프레임에서 흐른 시간(초)</param>
        public static void StepMapNow(float deltaTime)
        {
            StepMapNow(deltaTime, GameContext.MainCameraPosition);
        }

        /// <summary>
        /// 지도가 따라갈 자리를 직접 정해 한 장 갱신합니다.
        ///
        /// 편집 중에는 Camera.main 이 없을 수 있어, 확인 도구가 자리를 직접 넘깁니다.
        /// </summary>
        /// <param name="deltaTime">지난 프레임에서 흐른 시간(초)</param>
        /// <param name="eye">지도가 덮을 땅의 한가운데</param>
        public static void StepMapNow(float deltaTime, Vector3 eye)
        {
            if (trample == null) trample = new GrassTrampleMap();

            trample.Step(GrassPusher.All, eye, deltaTime);
        }

        /// <summary>가까운 것이 앞에 오도록 견줍니다.</summary>
        /// <param name="a">왼쪽</param>
        /// <param name="b">오른쪽</param>
        /// <returns>정렬 순서</returns>
        private static int CompareByDistance(Entry a, Entry b)
        {
            return a.distanceSqr.CompareTo(b.distanceSqr);
        }

        /// <summary>
        /// 게임이 시작될 때 스스로 하나 생겨납니다. 씬에 둘 필요가 없습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            // 씬을 옮겨도 살아남아야 합니다. 매번 다시 만들면 첫 프레임에 풀이 튑니다.
            GameObject go = new GameObject("GrassPushField");
            go.hideFlags = HideFlags.HideAndDontSave;

            go.AddComponent<GrassPushField>();
            DontDestroyOnLoad(go);
        }
    }
}
