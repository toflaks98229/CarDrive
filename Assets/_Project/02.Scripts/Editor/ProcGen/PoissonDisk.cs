using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.EditorTools.ProcGen
{
    /// <summary>
    /// 서로 일정 거리 이상 떨어진 점들을 평면에 흩뿌립니다.
    ///
    /// Robert Bridson 의 "Fast Poisson Disk Sampling in Arbitrary Dimensions" (2007) 을
    /// 구현한 것입니다. 논문에 공개된 알고리즘이라 코드는 이 프로젝트에 맞춰 새로 썼습니다.
    ///
    /// ── 왜 그냥 무작위로 뿌리지 않는가 ──
    ///
    /// <c>Random.insideUnitCircle</c> 로 뿌리면 <b>뭉치는 곳과 텅 빈 곳</b>이 생깁니다.
    /// 나무를 그렇게 심으면 어떤 데는 세 그루가 겹쳐 서 있고 옆은 휑합니다.
    /// 사람 눈에는 그게 "무작위"가 아니라 "잘못 놓았다"로 보입니다.
    ///
    /// 이 방법은 <b>최소 간격을 지키면서도 촘촘하게</b> 채웁니다.
    /// 자연물이 자라난 것처럼 보이는 이유는 실제 식생도 서로 자리를 다투기 때문입니다.
    ///
    /// ── 어떻게 O(n) 인가 ──
    ///
    /// 격자를 최소 간격 기준으로 잘라 두면 한 칸에 점이 <b>최대 하나</b>만 들어갑니다.
    /// 그래서 새 점이 너무 가까운지 볼 때 주변 다섯 칸만 확인하면 되고,
    /// 이미 놓인 점 전부와 거리를 잴 필요가 없습니다.
    /// </summary>
    public static class PoissonDisk
    {
        /// <summary>한 점당 몇 번 후보를 던져 볼지입니다. 논문의 권장값은 30입니다.</summary>
        private const int DefaultTries = 30;

        // --- Public Methods ---

        /// <summary>
        /// 사각 영역 안에 최소 간격을 지키는 점들을 흩뿌립니다.
        /// </summary>
        /// <param name="size">영역 크기</param>
        /// <param name="minDistance">점 사이 최소 간격. 작을수록 촘촘합니다.</param>
        /// <param name="seed">난수 씨앗. 같은 값이면 항상 같은 배치가 나옵니다.</param>
        /// <param name="tries">한 점당 후보 시도 횟수</param>
        /// <returns>영역 좌표계(0~size)의 점 목록</returns>
        public static List<Vector2> Sample(Vector2 size, float minDistance, int seed, int tries = DefaultTries)
        {
            List<Vector2> result = new List<Vector2>();

            if (minDistance <= 0.001f || size.x <= 0f || size.y <= 0f) return result;

            // 한 칸에 점이 하나만 들어가도록 대각선 길이를 최소 간격에 맞춥니다.
            float cell = minDistance / Mathf.Sqrt(2f);

            int cols = Mathf.Max(1, Mathf.CeilToInt(size.x / cell));
            int rows = Mathf.Max(1, Mathf.CeilToInt(size.y / cell));

            // -1 은 빈 칸입니다. 인덱스를 담아 result 를 가리킵니다.
            int[] grid = new int[cols * rows];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;

            System.Random random = new System.Random(seed);

            // 아직 주변을 더 채울 수 있는 점들입니다. 여기서 하나 꺼내 후보를 던집니다.
            List<int> active = new List<int>();

            Vector2 first = new Vector2(
                (float)random.NextDouble() * size.x,
                (float)random.NextDouble() * size.y);

            Add(first, result, active, grid, cols, cell);

            while (active.Count > 0)
            {
                // 활성 목록에서 무작위로 고릅니다. 앞에서부터 꺼내면 한쪽으로 자라납니다.
                int pick = random.Next(active.Count);
                int index = active[pick];
                Vector2 origin = result[index];

                bool placed = false;

                for (int t = 0; t < tries; t++)
                {
                    // 최소 간격의 1~2배 되는 고리 안에 후보를 던집니다.
                    // 1배 안쪽은 규칙 위반이고, 2배 밖은 굳이 볼 이유가 없습니다.
                    double angle = random.NextDouble() * Mathf.PI * 2.0;
                    double radius = minDistance * (1.0 + random.NextDouble());

                    Vector2 candidate = origin + new Vector2(
                        (float)(Mathf.Cos((float)angle) * radius),
                        (float)(Mathf.Sin((float)angle) * radius));

                    if (candidate.x < 0f || candidate.y < 0f ||
                        candidate.x >= size.x || candidate.y >= size.y) continue;

                    if (!IsFarEnough(candidate, result, grid, cols, rows, cell, minDistance)) continue;

                    Add(candidate, result, active, grid, cols, cell);
                    placed = true;
                    break;
                }

                // 서른 번을 던져도 자리가 없으면 이 점 주변은 다 찬 것입니다.
                if (!placed) active.RemoveAt(pick);
            }

            return result;
        }

        // --- Private Methods ---

        /// <summary>점을 결과·활성 목록·격자에 함께 넣습니다.</summary>
        private static void Add(Vector2 point, List<Vector2> result, List<int> active,
                                int[] grid, int cols, float cell)
        {
            int index = result.Count;

            result.Add(point);
            active.Add(index);

            int cx = (int)(point.x / cell);
            int cy = (int)(point.y / cell);
            grid[cy * cols + cx] = index;
        }

        /// <summary>
        /// 후보가 이미 놓인 점들과 충분히 떨어져 있는지 확인합니다.
        /// 주변 다섯 칸(가로세로 ±2)만 봅니다. 그 밖은 격자 크기상 이미 최소 간격을 넘습니다.
        /// </summary>
        private static bool IsFarEnough(Vector2 candidate, List<Vector2> result, int[] grid,
                                        int cols, int rows, float cell, float minDistance)
        {
            int cx = (int)(candidate.x / cell);
            int cy = (int)(candidate.y / cell);

            int x0 = Mathf.Max(0, cx - 2);
            int x1 = Mathf.Min(cols - 1, cx + 2);
            int y0 = Mathf.Max(0, cy - 2);
            int y1 = Mathf.Min(rows - 1, cy + 2);

            float minSqr = minDistance * minDistance;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int index = grid[y * cols + x];
                    if (index < 0) continue;

                    if ((result[index] - candidate).sqrMagnitude < minSqr) return false;
                }
            }

            return true;
        }
    }
}
