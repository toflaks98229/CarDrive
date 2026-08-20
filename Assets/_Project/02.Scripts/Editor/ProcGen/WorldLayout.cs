using System.Collections.Generic;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.EditorTools.ProcGen
{
    /// <summary>
    /// 월드의 <b>비워 둬야 할 자리</b>를 알려 줍니다. 길과 마을입니다.
    ///
    /// 나무를 심을 때도, 산을 올릴 때도 같은 답이 나와야 합니다.
    /// 두 도구가 도로 거리를 따로 계산하면 언젠가 한쪽만 고쳐져
    /// 길 위에 산이 솟거나 나무가 절벽에 걸립니다. 그래서 한 곳에 둡니다.
    ///
    /// 길은 <see cref="WorldStreamer.routes"/> 에 적힌 그대로입니다.
    /// 터레인을 구울 때 쓴 것과 같은 정보라 결과가 어긋나지 않습니다.
    /// </summary>
    internal sealed class WorldLayout
    {
        /// <summary>마을 중심입니다.</summary>
        public Vector3 VillageCenter { get; private set; }

        /// <summary>마을 반경(m)입니다.</summary>
        public float VillageRadius { get; private set; }

        /// <summary>길 선분들입니다.</summary>
        private readonly List<Segment> roads = new List<Segment>();

        /// <summary>길 개수입니다.</summary>
        public int RoadCount { get { return roads.Count; } }

        // --- Public Methods ---

        /// <summary>WorldStreamer 설정을 읽어 배치 정보를 만듭니다.</summary>
        /// <param name="world">읽어 올 스트리머</param>
        /// <returns>배치 정보</returns>
        public static WorldLayout From(WorldStreamer world)
        {
            WorldLayout layout = new WorldLayout();

            layout.VillageCenter = world.origin != null ? world.origin.position : world.transform.position;
            layout.VillageRadius = world.villageRadius;

            for (int i = 0; i < world.routes.Count; i++)
            {
                WorldRoute route = world.routes[i];

                Vector3 dir = route.direction.sqrMagnitude > 0.0001f
                    ? route.direction.normalized : Vector3.forward;

                Vector3 from = layout.VillageCenter + route.startOffset;
                Vector3 to = from + dir * (world.fallbackTileSize * route.tileCount);

                layout.roads.Add(new Segment(from, to));
            }

            return layout;
        }

        /// <summary>가장 가까운 길까지의 거리(m)입니다. 길이 없으면 무한대입니다.</summary>
        /// <param name="wx">월드 X</param>
        /// <param name="wz">월드 Z</param>
        /// <returns>거리(m)</returns>
        public float DistanceToRoad(float wx, float wz)
        {
            float best = float.MaxValue;
            Vector2 p = new Vector2(wx, wz);

            for (int i = 0; i < roads.Count; i++)
            {
                best = Mathf.Min(best, roads[i].DistanceTo(p));
            }

            return best;
        }

        /// <summary>어느 길에든 지정 거리 안쪽인지 확인합니다.</summary>
        /// <param name="wx">월드 X</param>
        /// <param name="wz">월드 Z</param>
        /// <param name="clearance">가까움의 기준(m)</param>
        /// <returns>한 곳이라도 가까우면 true 입니다.</returns>
        public bool NearRoad(float wx, float wz, float clearance)
        {
            return DistanceToRoad(wx, wz) < clearance;
        }

        /// <summary>마을 중심까지의 평면 거리(m)입니다.</summary>
        /// <param name="wx">월드 X</param>
        /// <param name="wz">월드 Z</param>
        /// <returns>거리(m)</returns>
        public float DistanceToVillage(float wx, float wz)
        {
            return new Vector2(wx - VillageCenter.x, wz - VillageCenter.z).magnitude;
        }

        // --- Private Types ---

        /// <summary>길 하나를 선분으로 담습니다. 거리를 자주 물으므로 미리 풀어 둡니다.</summary>
        private readonly struct Segment
        {
            /// <summary>시작점(평면)</summary>
            private readonly Vector2 a;

            /// <summary>시작점에서 끝점으로 가는 벡터</summary>
            private readonly Vector2 ab;

            /// <summary>ab 길이의 제곱. 나눗셈에 쓰므로 미리 구해 둡니다.</summary>
            private readonly float sqrLength;

            /// <summary>선분 하나를 만듭니다.</summary>
            /// <param name="from">시작점</param>
            /// <param name="to">끝점</param>
            public Segment(Vector3 from, Vector3 to)
            {
                a = new Vector2(from.x, from.z);
                Vector2 b = new Vector2(to.x, to.z);

                ab = b - a;
                sqrLength = Mathf.Max(0.0001f, ab.sqrMagnitude);
            }

            /// <summary>선분까지의 최단 거리입니다.</summary>
            /// <param name="p">알고 싶은 자리</param>
            /// <returns>거리(m)</returns>
            public float DistanceTo(Vector2 p)
            {
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqrLength);
                return (a + ab * t - p).magnitude;
            }
        }
    }
}
