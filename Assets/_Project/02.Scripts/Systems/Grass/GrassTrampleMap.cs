using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Systems
{
    /// <summary>
    /// 풀에 남은 눌린 자국을 담아 두는 지도입니다.
    ///
    /// 위에서 내려다본 한 장의 그림을 들고 다니며, 플레이어 주위의 땅을 덮습니다.
    /// 매 프레임 지난 장을 옮겨 받아 옅게 만들고, 그 위에 이번에 지나간 자리를 찍습니다.
    ///
    /// <b>왜 좌표 목록이 아니라 그림인가.</b>
    /// 겹쳐 있는 동안만 눕히는 것은 좌표 몇 개로 됩니다. 하지만 <b>지나간 길이 남으려면</b>
    /// 차가 달린 궤적을 전부 들고 있어야 하고, 그건 수백 개입니다.
    /// 풀 정점마다 수백 개를 훑을 수는 없습니다. 그림에 칠해 두면 풀은 자기 자리의
    /// 픽셀 하나만 읽으면 됩니다.
    /// </summary>
    public class GrassTrampleMap
    {
        // --- Constants ---

        /// <summary>
        /// 한 번에 찍을 수 있는 자국의 수입니다.
        /// <b>GrassTrampleMap.shader 의 TRAMPLE_SEGMENT_MAX 와 반드시 같아야 합니다.</b>
        /// </summary>
        public const int MaxSegments = 16;

        /// <summary>지도의 한 변 픽셀 수입니다.</summary>
        private const int Resolution = 1024;

        /// <summary>지도가 덮는 땅의 한 변 길이(m)입니다.</summary>
        private const float WorldSize = 150f;

        /// <summary>쓸 셰이더의 이름입니다.</summary>
        private const string ShaderName = "CarDrive/Grass Trample Map";

        // --- Private Member Variables ---

        private readonly Vector4[] segments = new Vector4[MaxSegments];
        private readonly Vector4[] shapes = new Vector4[MaxSegments];

        private RenderTexture front;
        private RenderTexture back;
        private Material material;

        /// <summary>지금 지도가 덮고 있는 땅의 한가운데입니다.</summary>
        private Vector2 center;

        /// <summary>아직 한 번도 그리지 않았는지 여부입니다.</summary>
        private bool started;

        private static readonly int SegmentsId = Shader.PropertyToID("_TrampleSegments");
        private static readonly int ShapeId = Shader.PropertyToID("_TrampleShape");
        private static readonly int CountId = Shader.PropertyToID("_TrampleCount");
        private static readonly int BoundsId = Shader.PropertyToID("_MapBounds");
        private static readonly int ShiftId = Shader.PropertyToID("_MapShift");
        private static readonly int StepId = Shader.PropertyToID("_StepSeconds");
        private static readonly int LifeRangeId = Shader.PropertyToID("_LifeRange");

        private static readonly int MapId = Shader.PropertyToID("_GrassTrampleMap");
        private static readonly int MapBoundsId = Shader.PropertyToID("_GrassTrampleBounds");

        // --- Public Methods ---

        /// <summary>
        /// 지도를 한 장 갱신합니다.
        /// </summary>
        /// <param name="pushers">지금 씬에 있는 누르개들</param>
        /// <param name="eye">지도가 따라갈 자리 (보통 카메라)</param>
        /// <param name="deltaTime">지난 프레임에서 흐른 시간(초)</param>
        public void Step(IReadOnlyList<GrassPusher> pushers, Vector3 eye, float deltaTime)
        {
            if (!EnsureResources()) return;

            // 지도를 <b>픽셀 격자에 맞춰</b> 옮깁니다.
            // 아무 자리에나 옮기면 매 프레임 반 픽셀씩 어긋나며 자국이 부글거립니다.
            float texel = WorldSize / Resolution;

            Vector2 wanted = new Vector2(eye.x, eye.z);
            Vector2 snapped = new Vector2(
                Mathf.Round(wanted.x / texel) * texel,
                Mathf.Round(wanted.y / texel) * texel);

            Vector2 shift = started ? (snapped - center) / WorldSize : Vector2.zero;
            center = snapped;
            started = true;

            int count = CollectSegments(pushers);

            material.SetVectorArray(SegmentsId, segments);
            material.SetVectorArray(ShapeId, shapes);
            material.SetFloat(CountId, count);
            material.SetVector(BoundsId, new Vector4(center.x, center.y, WorldSize, 1f / WorldSize));
            material.SetVector(ShiftId, shift);
            material.SetFloat(StepId, Mathf.Max(deltaTime, 0f));
            material.SetVector(LifeRangeId, new Vector4(LifeMin, LifeMax, 0f, 0f));

            // 지난 장을 읽어 새 장에 그립니다. 같은 그림을 읽으며 쓸 수는 없습니다.
            Graphics.Blit(front, back, material);

            RenderTexture swap = front;
            front = back;
            back = swap;

            Shader.SetGlobalTexture(MapId, front);
            Shader.SetGlobalVector(MapBoundsId,
                new Vector4(center.x, center.y, WorldSize, 1f / WorldSize));
        }

        /// <summary>들고 있던 그림을 놓아 줍니다.</summary>
        public void Release()
        {
            if (front != null) { front.Release(); Object.Destroy(front); front = null; }
            if (back != null) { back.Release(); Object.Destroy(back); back = null; }
            if (material != null) { Object.Destroy(material); material = null; }

            started = false;
        }

        // --- Private Member Variables ---

        /// <summary>자국이 남는 시간의 아래 한계(초)입니다. 셰이더가 이 사이를 오갑니다.</summary>
        private const float LifeMin = 1f;

        /// <summary>자국이 남는 시간의 위 한계(초)입니다.</summary>
        private const float LifeMax = 90f;

        // --- Private Methods ---

        /// <summary>
        /// 자국을 남기는 누르개만 골라 <b>선분</b>으로 담습니다.
        ///
        /// 자국을 남기지 않는 것(차체 등)은 여기 담지 않습니다.
        /// 그쪽은 겹쳐 있는 동안만 눕히면 되고, 그 일은 GrassPushField 가 따로 합니다.
        /// </summary>
        /// <param name="pushers">볼 누르개들</param>
        /// <returns>담은 개수</returns>
        private int CollectSegments(IReadOnlyList<GrassPusher> pushers)
        {
            int count = 0;

            for (int i = 0; i < pushers.Count && count < MaxSegments; i++)
            {
                GrassPusher pusher = pushers[i];
                if (pusher == null || !pusher.leavesMark || pusher.radius <= 0.01f) continue;

                // 지도가 덮는 땅 밖이면 찍어 봐야 버려집니다.
                Vector3 position = pusher.transform.position;
                if (Mathf.Abs(position.x - center.x) > WorldSize || Mathf.Abs(position.z - center.y) > WorldSize) continue;

                Vector3 from, to;
                pusher.GetSweep(out from, out to);

                segments[count] = new Vector4(from.x, from.z, to.x, to.z);

                // 무게에서 나온 수명을 0~1로 접어 넣습니다. 셰이더가 이 값으로 옅어지는 속도를 정합니다.
                float life = Mathf.InverseLerp(LifeMin, LifeMax, pusher.MarkSeconds);
                shapes[count] = new Vector4(pusher.radius, life, 0f, 0f);

                pusher.RememberPosition();
                count++;
            }

            // 남는 자리는 반경 0으로 비웁니다. 셰이더가 개수만 보고 돌지만,
            // 예전 값이 남아 있으면 개수가 늘어난 순간 엉뚱한 자리에 자국이 찍힙니다.
            for (int i = count; i < MaxSegments; i++)
            {
                segments[i] = Vector4.zero;
                shapes[i] = Vector4.zero;
            }

            return count;
        }

        /// <summary>그림 두 장과 머티리얼을 준비합니다.</summary>
        /// <returns>준비되었으면 true입니다.</returns>
        private bool EnsureResources()
        {
            if (material == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogWarning("GrassTrampleMap: " + ShaderName + " 셰이더를 찾지 못해 자국이 남지 않습니다.");
                    return false;
                }

                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }

            if (front == null) front = Create();
            if (back == null) back = Create();

            return front != null && back != null;
        }

        /// <summary>그림 한 장을 만듭니다.</summary>
        /// <returns>만들어진 그림</returns>
        private static RenderTexture Create()
        {
            // R에 눌린 정도, G에 그 자국의 수명을 담습니다. 색은 필요 없습니다.
            RenderTexture rt = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.RGHalf);

            rt.name = "GrassTrampleMap";
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.hideFlags = HideFlags.HideAndDontSave;
            rt.Create();

            // 처음에는 눌린 자국이 없습니다.
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;

            return rt;
        }
    }
}
