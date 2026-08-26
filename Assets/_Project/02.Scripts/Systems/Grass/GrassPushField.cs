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
    /// 씬에 무언가를 놓아 둘 필요가 없습니다. <c>WorldRuntimeInstaller</c> 가 하나만 만들어 붙입니다.
    ///
    /// <b>스스로 생겨나지 않습니다.</b> 예전에는 <c>[RuntimeInitializeOnLoadMethod]</c> 로
    /// 자기를 만들었는데, 그러면 값을 보려고 씬에 하나 얹는 순간 <b>둘이 됩니다.</b>
    /// 무엇이 존재하는지는 이제 컴포지션 루트 한 곳에서만 정합니다.
    ///
    /// <b>상태는 인스턴스가 들고 있습니다.</b> 예전에는 자국 지도까지 static 이라,
    /// 둘이 되면 한 장을 번갈아 굴리고 먼저 사라지는 쪽이 <b>남의 것을 반납</b>했습니다.
    /// 지금은 둘이 되어도 각자 자기 지도를 굴리고, <c>Awake</c> 가 그 사실을 알립니다.
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
        private readonly Vector4[] buffer = new Vector4[MaxPushers];

        /// <summary>가까운 것부터 고르기 위해 거리와 함께 담아 두는 임시 목록입니다.</summary>
        private readonly List<Entry> sorted = new List<Entry>(MaxPushers * 2);

        /// <summary>
        /// 가까운 것이 앞에 오도록 하는 비교자입니다.
        ///
        /// <b>메서드 이름을 그대로 Sort 에 넘기면 호출할 때마다 델리게이트가 새로 생깁니다.</b>
        /// 이 코드는 <b>매 프레임</b> 도는데, <see cref="Gameplay.WorldStreamer"/> 와
        /// <see cref="TerrainChunkCuller"/> 는 훨씬 뜸하게 도는데도 이미 이것을 캐시해 두었습니다.
        /// 가장 자주 도는 이곳만 빠져 있었습니다.
        /// </summary>
        private static readonly System.Comparison<Entry> ByDistance = CompareByDistance;

        /// <summary>
        /// 지나간 길에 남는 자국을 담아 두는 지도입니다.
        ///
        /// <b>인스턴스가 들고 있습니다.</b> 예전에는 static 이라, 둘이 되면 한 장을 두 컴포넌트가
        /// 번갈아 굴리고 <c>OnDestroy</c> 에서 <b>남의 것을 반납</b>했습니다.
        /// RenderTexture 두 장이 걸린 자리라 그 실수가 조용히 지나가지 않습니다.
        /// </summary>
        private GrassTrampleMap trample;

        /// <summary>지금 돌고 있는 것입니다. 둘이 되었는지 알아채려고만 둡니다.</summary>
        private static GrassPushField active;

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
        /// 둘이 되었으면 알립니다. 자국 지도가 걸린 자리라 조용히 두면 안 됩니다.
        /// </summary>
        void Awake()
        {
            if (active != null && active != this)
            {
                GameLog.Error(GameLog.Channel.World,
                    "GrassPushField 가 둘입니다. 자국 지도가 한 프레임에 두 번 굴러 " +
                    "자국이 예상보다 빨리 옅어집니다. WorldRuntimeInstaller 를 보세요.", this);
            }

            active = this;
        }

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
        /// </summary>
        private void UploadNow()
        {
            IReadOnlyList<GrassPusher> pushers = GrassPusher.All;

            sorted.Clear();

            for (int i = 0; i < pushers.Count; i++)
            {
                GrassPusher pusher = pushers[i];
                if (pusher == null || pusher.radius <= 0.01f) continue;

                Vector3 position = pusher.transform.position;

                Entry entry;
                entry.distanceSqr = 0f;   // 아직 구하지 않습니다. 아래를 보세요.
                entry.packed = new Vector4(position.x, position.y, position.z, pusher.radius);

                sorted.Add(entry);
            }

            if (sorted.Count == 0)
            {
                Shader.SetGlobalFloat(CountId, 0f);
                return;
            }

            // <b>자리가 모자랄 때만 거리를 구하고 줄을 세웁니다.</b>
            //
            // 줄을 세우는 이유는 하나뿐입니다 — 넘길 자리(16)보다 누르개가 많을 때
            // 가까운 것부터 골라야 하기 때문입니다. 그런데 실제로 땅을 밟는 것은
            // 바퀴 넷·차체·플레이어와 유령 몇뿐이라, <b>대개 자리가 남습니다.</b>
            // 자리가 남으면 순서는 아무 의미가 없는데도 매 프레임 거리를 구하고 정렬했습니다.
            //
            // (<see cref="TerrainChunkCuller"/> 의 예산 처리가 같은 판단을 이미 하고 있습니다)
            if (sorted.Count > MaxPushers)
            {
                Vector3 eye = GameContext.MainCameraPosition;

                for (int i = 0; i < sorted.Count; i++)
                {
                    Entry entry = sorted[i];

                    float dx = entry.packed.x - eye.x;
                    float dy = entry.packed.y - eye.y;
                    float dz = entry.packed.z - eye.z;
                    entry.distanceSqr = dx * dx + dy * dy + dz * dz;

                    sorted[i] = entry;
                }

                sorted.Sort(ByDistance);
            }

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
        /// </summary>
        /// <param name="deltaTime">지난 프레임에서 흐른 시간(초)</param>
        private void StepMapNow(float deltaTime)
        {
            if (trample == null) trample = new GrassTrampleMap();

            trample.Step(GrassPusher.All, GameContext.MainCameraPosition, deltaTime);
        }

        /// <summary>가까운 것이 앞에 오도록 견줍니다.</summary>
        /// <param name="a">왼쪽</param>
        /// <param name="b">오른쪽</param>
        /// <returns>정렬 순서</returns>
        private static int CompareByDistance(Entry a, Entry b)
        {
            return a.distanceSqr.CompareTo(b.distanceSqr);
        }
    }
}
