using UnityEditor;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 보행 로봇 프리팹에 <b>전투 부품</b>을 붙입니다.
    ///
    /// <b>왜 도구인가.</b> 프리팹을 손으로 고치면 무엇을 왜 그렇게 두었는지가 남지 않습니다.
    /// 체력을 질량에서 뽑는 규칙이 여기 적혀 있으면, 나중에 네 번째 템플릿이 생겨도
    /// 같은 규칙으로 붙습니다. 이미 붙어 있으면 건드리지 않으므로 몇 번 눌러도 안전합니다.
    ///
    /// 붙이는 것은 둘입니다.
    ///  1. <see cref="EnemyHealth"/>  — 체력. <see cref="Health"/> 를 타고 IDamageable 이 됩니다.
    ///  2. <see cref="RobotCombatant"/> — 적대 표시와 사망 처리.
    /// </summary>
    public static class RobotCombatSetup
    {
        // --- Constants ---

        /// <summary>로봇 프리팹이 있는 폴더입니다.</summary>
        private const string RobotFolder = "Assets/_Project/05.Prefabs/Robot";

        /// <summary>
        /// 질량 1kg 당 체력입니다.
        ///
        /// 드레드노트(900kg)가 90, 스트라이더(2000kg)가 200이 됩니다. 앙크가 초당 20을 준다면
        /// 각각 4.5초 · 10초입니다 — 귀신보다 오래 버티되 <b>혼자서도 넘어뜨릴 수 있는</b> 정도입니다.
        /// 무게가 곧 위압감이므로, 눈에 보이는 크기와 버티는 시간이 같은 값에서 나오게 둡니다.
        /// </summary>
        private const float HealthPerKilogram = 0.1f;

        /// <summary>질량을 알 수 없을 때 쓸 체력입니다.</summary>
        private const float FallbackHealth = 100f;

        // --- Public Methods ---

        /// <summary>
        /// 폴더 안의 모든 보행 로봇 프리팹에 전투 부품을 붙입니다.
        /// </summary>
        [MenuItem("CarDrive/Gameplay/보행 로봇/전투 부품 붙이기")]
        public static void AttachToAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { RobotFolder });

            int touched = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Attach(path)) touched++;
            }

            AssetDatabase.SaveAssets();

            GameLog.InfoFormat(GameLog.Channel.Enemy,
                "[RobotCombatSetup] 프리팹 {0}개 중 {1}개에 전투 부품을 붙였습니다.", guids.Length, touched);
        }

        // --- Private Methods ---

        /// <summary>
        /// 프리팹 하나에 전투 부품을 붙입니다. 이미 있으면 아무것도 하지 않습니다.
        /// </summary>
        /// <param name="path">프리팹 경로</param>
        /// <returns>무언가 바뀌었으면 true</returns>
        private static bool Attach(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return false;

            try
            {
                // 보행 로봇이 아닌 프리팹은 건너뜁니다.
                if (root.GetComponentInChildren<WalkerRobot>(true) == null) return false;

                bool changed = false;

                EnemyHealth health = root.GetComponent<EnemyHealth>();
                if (health == null)
                {
                    health = root.AddComponent<EnemyHealth>();
                    health.maxHealth = HealthFromMass(root);
                    changed = true;

                    GameLog.InfoFormat(GameLog.Channel.Enemy,
                        "[RobotCombatSetup] {0}: 체력 {1} 로 붙였습니다.", root.name, health.maxHealth);
                }

                if (root.GetComponent<RobotCombatant>() == null)
                {
                    root.AddComponent<RobotCombatant>();
                    changed = true;
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);

                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 로봇의 질량에서 체력을 뽑습니다. 무거울수록 오래 버팁니다.
        /// </summary>
        /// <param name="root">로봇 프리팹의 루트</param>
        /// <returns>최대 체력</returns>
        private static float HealthFromMass(GameObject root)
        {
            Rigidbody body = root.GetComponentInChildren<Rigidbody>(true);
            if (body == null) return FallbackHealth;

            return Mathf.Round(body.mass * HealthPerKilogram);
        }
    }
}
