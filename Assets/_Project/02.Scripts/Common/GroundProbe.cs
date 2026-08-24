using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 어떤 자리 <b>아래의 땅</b>을 찾습니다. 레이캐스트로 먼저 보고, 놓치면 터레인 높이맵에 직접 묻습니다.
    ///
    /// <b>왜 레이캐스트만으로는 부족한가.</b> 이 월드의 지면은 y=0 이 아닙니다.
    /// 터레인 높이 범위가 70m 이고 도로·마을이 놓이는 기준 높이가 0.28 이라,
    /// <b>평지의 표면이 이미 y≈19.6m</b> 에 있습니다. 그런데 프리팹을 씬에 끌어다 놓으면
    /// 보통 원점 근처에 떨어집니다. 그 자리는 지면보다 <b>20m 아래</b>입니다.
    ///
    /// 아래로 쏘는 레이는 사거리를 아무리 늘려도 위에 있는 표면에 닿지 않습니다.
    /// 위로도 쏘면 되지 않느냐 싶지만, 터레인 콜라이더는 아래에서 올려다볼 때 잡히지 않습니다.
    /// 그래서 <b>레이캐스트로는 원리적으로 못 찾는 자리</b>가 생깁니다.
    /// 실제로 사족보행 로봇이 지형을 감지하지 못한 원인이 이것이었습니다.
    ///
    /// <b>높이맵에 직접 묻는 쪽은 이 문제가 없습니다.</b> <see cref="Terrain.SampleHeight"/> 는
    /// 콜라이더도, 사거리도, 위아래 관계도 보지 않습니다. 좌표를 주면 그 자리의 표면 높이를 돌려줍니다.
    /// 그래서 순서를 이렇게 둡니다.
    ///
    ///  1. <b>레이캐스트</b> — 도로·바위·지붕처럼 <b>터레인 위에 놓인 것</b>을 딛으려면 이쪽이어야 합니다.
    ///  2. <b>높이맵</b> — 1번이 놓쳤을 때. 지하에 있든 하늘에 있든 표면을 찾아냅니다.
    ///
    /// <b>터레인 목록은 캐시합니다.</b> <see cref="Terrain.activeTerrains"/> 는 부를 때마다 배열을
    /// 새로 만듭니다. 이 월드는 타일이 103장이고 발이 넷이라 매 프레임 네 번 부르게 되는데,
    /// 그대로 두면 초당 240개의 배열이 쓰레기가 됩니다. 그리고 로봇은 타일 하나 위에 머무르므로
    /// <b>지난번에 맞았던 타일을 먼저</b> 봅니다. 거의 항상 첫 번째에 끝납니다.
    /// </summary>
    public static class GroundProbe
    {
        // --- Private Member Variables ---

        /// <summary>캐시해 둔 터레인 목록입니다.</summary>
        private static Terrain[] terrains;

        /// <summary>목록을 다시 읽을 시각입니다.</summary>
        private static float nextRefreshTime;

        /// <summary>지난번에 맞았던 타일의 번호입니다. 다음에도 여기부터 봅니다.</summary>
        private static int lastHitIndex;

        // --- Constants ---

        /// <summary>터레인 목록을 다시 읽는 간격(초)입니다.</summary>
        private const float RefreshInterval = 5f;

        // --- Public Methods ---

        /// <summary>
        /// 어떤 자리 아래의 땅을 찾습니다.
        /// </summary>
        /// <param name="around">찾을 자리. 높이는 무시하고 <b>수평 위치만</b> 씁니다.</param>
        /// <param name="probeUp">레이가 이 자리보다 얼마나 위에서 출발할지. 오를 수 있는 턱의 높이입니다.</param>
        /// <param name="probeDown">레이가 아래로 보는 거리</param>
        /// <param name="mask">땅으로 볼 레이어</param>
        /// <param name="point">찾은 지면 위치</param>
        /// <param name="normal">찾은 지면 법선</param>
        /// <returns>땅을 찾았으면 true. 못 찾았으면 point 는 <paramref name="around"/> 그대로입니다.</returns>
        public static bool Sample(Vector3 around, float probeUp, float probeDown, LayerMask mask,
            out Vector3 point, out Vector3 normal)
        {
            Vector3 origin = around + Vector3.up * probeUp;
            float distance = probeUp + probeDown;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }

            if (SampleTerrain(around, out point, out normal)) return true;

            point = around;
            normal = Vector3.up;
            return false;
        }

        /// <summary>
        /// 터레인 높이맵에만 물어봅니다. 콜라이더도 사거리도 보지 않습니다.
        /// </summary>
        /// <param name="around">찾을 자리. 수평 위치만 씁니다.</param>
        /// <param name="point">찾은 지면 위치</param>
        /// <param name="normal">찾은 지면 법선</param>
        /// <returns>이 자리를 덮는 터레인이 있으면 true</returns>
        public static bool SampleTerrain(Vector3 around, out Vector3 point, out Vector3 normal)
        {
            Terrain[] all = GetTerrains();

            if (all != null && all.Length > 0)
            {
                // 지난번에 맞았던 타일부터 봅니다. 로봇은 타일 하나 위에 머무르므로 대개 여기서 끝납니다.
                if (lastHitIndex >= 0 && lastHitIndex < all.Length && TryTerrain(all[lastHitIndex], around, out point, out normal))
                {
                    return true;
                }

                for (int i = 0; i < all.Length; i++)
                {
                    if (i == lastHitIndex) continue;
                    if (!TryTerrain(all[i], around, out point, out normal)) continue;

                    lastHitIndex = i;
                    return true;
                }
            }

            point = around;
            normal = Vector3.up;
            return false;
        }

        /// <summary>
        /// 캐시해 둔 터레인 목록을 버립니다. 지형을 새로 깔거나 씬을 바꾼 뒤에 부릅니다.
        /// </summary>
        public static void Invalidate()
        {
            terrains = null;
            nextRefreshTime = 0f;
            lastHitIndex = 0;
        }

        // --- Private Methods ---

        /// <summary>터레인 목록을 돌려줍니다. 오래되었으면 다시 읽습니다.</summary>
        /// <returns>지금 살아 있는 터레인들</returns>
        private static Terrain[] GetTerrains()
        {
            // 에디터에서는 시간이 흐르지 않으므로 캐시를 믿을 수 없습니다. 그때는 매번 다시 읽습니다.
            bool stale = terrains == null || terrains.Length == 0 || !Application.isPlaying ||
                         Time.unscaledTime >= nextRefreshTime;

            if (!stale) return terrains;

            terrains = Terrain.activeTerrains;
            nextRefreshTime = Time.unscaledTime + RefreshInterval;

            return terrains;
        }

        /// <summary>
        /// 터레인 하나가 이 자리를 덮는지 보고, 덮으면 높이와 법선을 돌려줍니다.
        /// </summary>
        /// <param name="terrain">확인할 터레인</param>
        /// <param name="around">찾을 자리</param>
        /// <param name="point">찾은 지면 위치</param>
        /// <param name="normal">찾은 지면 법선</param>
        /// <returns>이 터레인이 덮고 있으면 true</returns>
        private static bool TryTerrain(Terrain terrain, Vector3 around, out Vector3 point, out Vector3 normal)
        {
            point = around;
            normal = Vector3.up;

            if (terrain == null) return false;

            TerrainData data = terrain.terrainData;
            if (data == null) return false;

            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;

            float localX = around.x - origin.x;
            float localZ = around.z - origin.z;

            if (localX < 0f || localZ < 0f || localX > size.x || localZ > size.z) return false;

            // SampleHeight 는 터레인 기준 높이라 원점을 더해야 월드 좌표가 됩니다.
            point = new Vector3(around.x, terrain.SampleHeight(around) + origin.y, around.z);
            normal = data.GetInterpolatedNormal(localX / size.x, localZ / size.z);

            return true;
        }
    }
}
