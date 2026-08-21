using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 사운드 컨트롤러를 프리팹과 씬에 붙이고 AudioSource 를 배선합니다.
    ///
    /// <b>왜 필요한가.</b> 사운드 컨트롤러 다섯 개가 코드로는 완성되어 있는데
    /// <b>어떤 프리팹에도 붙어 있지 않았습니다.</b> (GUID 를 Assets 전체에서 찾아 확인했습니다)
    /// 그래서 <c>CarController</c>·<c>PlayerAttacker</c> 등의 <c>soundController</c> 필드가
    /// 전부 null 로 남고, 코드가 "있으면 쓰고 없으면 조용히 넘어갑니다"로 처리하므로
    /// <b>아무 소리도 나지 않는데 아무 경고도 뜨지 않는</b> 상태였습니다.
    ///
    /// <b>이 도구는 클립을 넣지 않습니다.</b> 구조만 만듭니다.
    /// 클립 슬롯은 비워 두므로, 오디오를 구해 오면 인스펙터에서 끼우기만 하면 됩니다.
    /// 클립이 없는 동안에도 <c>AudioUtility</c> 가 null 을 걸러내므로 예외는 나지 않습니다.
    ///
    /// <b>AudioSource 배치 규칙.</b> 일회성 효과음은 <b>본체</b>에, 루프는 <b>이름 있는 자식</b>에 둡니다.
    /// 루프는 설정이 다르고(loop, 차량은 피치까지 흔듭니다) 따로 멈추고 시작해야 해서,
    /// 인스펙터에서 어느 쪽인지 이름으로 구분되는 편이 낫기 때문입니다.
    ///
    /// 여러 번 눌러도 안전합니다. 이미 붙어 있으면 건너뛰고 설정만 다시 맞춥니다.
    /// </summary>
    public static class SoundWiringSetup
    {
        // --- Constants ---

        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>차량 프리팹 경로입니다.</summary>
        private const string CarPrefabPath = "Assets/_Project/05.Prefabs/Player/PlayerCar.prefab";

        /// <summary>추적형 적 프리팹 경로입니다.</summary>
        private const string EnemyPrefabPath = "Assets/_Project/05.Prefabs/Monster/Monster_1.prefab";

        /// <summary>부착형 귀신 프리팹 경로들입니다.</summary>
        private static readonly string[] GhostPrefabPaths =
        {
            "Assets/_Project/05.Prefabs/Monster/Monster_2.prefab",
            "Assets/_Project/05.Prefabs/Monster/Monster_3.prefab",
        };

        /// <summary>장애물 프리팹 경로입니다.</summary>
        private const string ObstaclePrefabPath = "Assets/_Project/05.Prefabs/Map/Terrain.prefab";

        /// <summary>루프용 자식 오브젝트의 이름입니다.</summary>
        private const string LoopChildName = "LoopSource";

        /// <summary>차량 엔진 루프용 자식 오브젝트의 이름입니다.</summary>
        private const string EngineChildName = "EngineSource";

        /// <summary>3D 사운드가 최대 볼륨을 유지하는 거리입니다.</summary>
        private const float MinDistance = 3f;

        /// <summary>3D 사운드가 들리지 않게 되는 거리입니다.</summary>
        private const float MaxDistance = 45f;

        /// <summary>가져온 CC0 오디오가 있는 폴더입니다. 출처는 06.Sound/CREDITS.md 를 보세요.</summary>
        private const string SoundRoot = "Assets/_Project/06.Sound/";

        /// <summary>
        /// 엔진 루프로 쓸 클립입니다.
        ///
        /// 여섯 개는 같은 소리의 피치 변형이라 하나만 물리면 됩니다.
        /// <c>CarSoundController</c> 가 RPM 에 따라 피치를 다시 조절하므로,
        /// 가운데 것을 골라 위아래로 흔들 여지를 남깁니다.
        /// </summary>
        private const string EngineLoopClip = SoundRoot + "Vehicle/engine_loop_2.wav";

        // --- Public Methods ---

        /// <summary>에디터 메뉴에서 실행합니다.</summary>
        [MenuItem("CarDrive/Gameplay/사운드 배선 설정")]
        public static void Setup()
        {
            List<string> report = new List<string>();

            SetupCar(report);
            SetupEnemy(report);
            SetupGhosts(report);
            SetupObstacle(report);
            SetupPlayer(report);

            report.Add("");
            report.Add("클립은 넣지 않았습니다. 인스펙터의 빈 슬롯에 끼우면 소리가 납니다.");

            AssetDatabase.SaveAssets();

            Debug.Log("SoundWiringSetup:" + System.Environment.NewLine +
                      string.Join(System.Environment.NewLine, report));
        }

        /// <summary>
        /// 명령줄에서 씬을 열고 배선한 뒤 저장합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod CarDrive.EditorTools.SoundWiringSetup.SetupFromCommandLine</c>
        /// </summary>
        public static void SetupFromCommandLine()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Setup();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // --- Private Methods : 대상별 배선 ---

        /// <summary>
        /// 차량에 엔진·효과음 컨트롤러를 붙입니다.
        ///
        /// 엔진 루프는 RPM 에 따라 피치가 변하므로 전용 자식에 둡니다.
        /// </summary>
        /// <param name="report">결과를 적어 넣을 목록</param>
        private static void SetupCar(List<string> report)
        {
            EditPrefab(CarPrefabPath, report, root =>
            {
                CarController controller = root.GetComponentInChildren<CarController>(true);
                if (controller == null)
                {
                    report.Add("  건너뜀 — CarController 를 찾지 못했습니다.");
                    return false;
                }

                CarSoundController sound = Ensure<CarSoundController>(controller.gameObject, report);

                sound.effectsSource = EnsureSourceOnSelf(controller.gameObject, false, 1f);
                sound.engineSource = EnsureSourceOnChild(controller.gameObject, EngineChildName, true, 1f);

                AssignClip(ref sound.engineLoopClip, EngineLoopClip, report);
                AssignClips(ref sound.collisionClips, "Impact", "metal_hit_", report);

                // 시동·정지음은 아직 없습니다. 비워 두면 AudioUtility 가 걸러내므로 조용히 넘어갑니다.
                report.Add("  남은 슬롯 — engineStartClip · engineStopClip");

                return true;
            });
        }

        /// <summary>추적형 적에 사운드 컨트롤러를 붙입니다.</summary>
        /// <param name="report">결과를 적어 넣을 목록</param>
        private static void SetupEnemy(List<string> report)
        {
            EditPrefab(EnemyPrefabPath, report, root =>
            {
                EnemyController controller = root.GetComponentInChildren<EnemyController>(true);
                if (controller == null)
                {
                    report.Add("  건너뜀 — EnemyController 를 찾지 못했습니다.");
                    return false;
                }

                EnemySoundController sound = Ensure<EnemySoundController>(controller.gameObject, report);

                sound.effectsSource = EnsureSourceOnSelf(controller.gameObject, false, 1f);
                sound.loopSource = EnsureSourceOnChild(controller.gameObject, LoopChildName, true, 1f);

                AssignClips(ref sound.takeDamageClips, "Ghost", "qubodup-GhostMoan0", report);
                AssignClip(ref sound.deathClip, SoundRoot + "Ghost/qubodup-GhostMoan03.wav", report);

                // chaseLoop 은 비워 둡니다. 가진 것은 한 번씩 우는 소리라 이어 붙이면 이음매가 들립니다.
                report.Add("  남은 슬롯 — spawnSound · chaseLoop (루프용 소리가 아직 없습니다)");

                return true;
            });
        }

        /// <summary>부착형 귀신 프리팹들에 사운드 컨트롤러를 붙입니다.</summary>
        /// <param name="report">결과를 적어 넣을 목록</param>
        private static void SetupGhosts(List<string> report)
        {
            for (int i = 0; i < GhostPrefabPaths.Length; i++)
            {
                EditPrefab(GhostPrefabPaths[i], report, root =>
                {
                    AttachedGhostController controller = root.GetComponentInChildren<AttachedGhostController>(true);
                    if (controller == null)
                    {
                        report.Add("  건너뜀 — AttachedGhostController 를 찾지 못했습니다.");
                        return false;
                    }

                    AttachedGhostSoundController sound =
                        Ensure<AttachedGhostSoundController>(controller.gameObject, report);

                    sound.effectsSource = EnsureSourceOnSelf(controller.gameObject, false, 1f);
                    sound.loopSource = EnsureSourceOnChild(controller.gameObject, LoopChildName, true, 1f);

                    AssignClips(ref sound.attackImpactClips, "Impact", "metal_hit_", report);
                    AssignClips(ref sound.takeDamageClips, "Ghost", "qubodup-GhostMoan0", report);
                    AssignClip(ref sound.deathClip, SoundRoot + "Ghost/qubodup-GhostMoan05.wav", report);

                    report.Add("  남은 슬롯 — spawnSound · attackLoop (루프용 소리가 아직 없습니다)");

                    return true;
                });
            }
        }

        /// <summary>장애물에 충돌음 컨트롤러를 붙입니다. 루프는 없습니다.</summary>
        /// <param name="report">결과를 적어 넣을 목록</param>
        private static void SetupObstacle(List<string> report)
        {
            EditPrefab(ObstaclePrefabPath, report, root =>
            {
                ObstacleController controller = root.GetComponentInChildren<ObstacleController>(true);
                if (controller == null)
                {
                    report.Add("  건너뜀 — ObstacleController 를 찾지 못했습니다.");
                    return false;
                }

                EnvironmentSoundController sound = Ensure<EnvironmentSoundController>(controller.gameObject, report);
                sound.effectsSource = EnsureSourceOnSelf(controller.gameObject, false, 1f);

                AssignClips(ref sound.hitClips, "Impact", "wood_hit_", report);

                return true;
            });
        }

        /// <summary>
        /// 씬의 플레이어에 사운드 컨트롤러를 붙입니다.
        ///
        /// <b>반드시 PlayerAttacker 와 같은 오브젝트여야 합니다.</b> 쓰는 쪽 셋
        /// (PlayerAttacker · PlayerInteractor · BeverageConsumer)이 전부
        /// <c>GetComponent</c> 로 찾기 때문에, 다른 오브젝트에 있으면 영영 만나지 못합니다.
        ///
        /// 플레이어 자신의 소리라 2D 로 둡니다. 내 손에 든 앙크 소리가
        /// 고개를 돌린다고 왼쪽에서 들리면 안 되기 때문입니다.
        /// </summary>
        /// <param name="report">결과를 적어 넣을 목록</param>
        private static void SetupPlayer(List<string> report)
        {
            PlayerAttacker attacker = Object.FindAnyObjectByType<PlayerAttacker>(FindObjectsInactive.Include);
            if (attacker == null)
            {
                report.Add("씬: 건너뜀 — PlayerAttacker 를 찾지 못했습니다. SampleScene 을 열고 다시 실행하세요.");
                return;
            }

            report.Add("씬: " + attacker.gameObject.name);

            PlayerSoundController sound = Ensure<PlayerSoundController>(attacker.gameObject, report);

            sound.effectsSource = EnsureSourceOnSelf(attacker.gameObject, false, 0f);
            sound.ankhLoopSource = EnsureSourceOnChild(attacker.gameObject, LoopChildName, true, 0f);

            EditorUtility.SetDirty(sound);
            EditorSceneManager.MarkSceneDirty(attacker.gameObject.scene);
        }

        // --- Private Methods : 도구 ---

        /// <summary>
        /// 프리팹을 열어 수정하고 저장합니다. 수정 함수가 false 를 돌려주면 저장하지 않습니다.
        /// </summary>
        /// <param name="path">프리팹 에셋 경로</param>
        /// <param name="report">결과를 적어 넣을 목록</param>
        /// <param name="edit">프리팹 루트를 받아 수정하는 함수. 저장할 값이 있으면 true 를 돌려주세요.</param>
        private static void EditPrefab(string path, List<string> report, System.Func<GameObject, bool> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                report.Add(path + ": 건너뜀 — 프리팹이 없습니다.");
                return;
            }

            report.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ":");

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (edit(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                // 열어 둔 프리팹 사본은 반드시 닫아야 합니다. 예외가 나도 마찬가지입니다.
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 클립 슬롯 하나를 채웁니다.
        ///
        /// <b>이미 무언가 들어 있으면 건드리지 않습니다.</b> 이 도구는 여러 번 실행되는데,
        /// 그때마다 사람이 골라 넣은 클립을 기본값으로 되돌리면 도구가 아니라 훼방입니다.
        /// </summary>
        /// <param name="slot">채울 클립 필드</param>
        /// <param name="assetPath">넣을 클립의 에셋 경로</param>
        /// <param name="report">결과를 적어 넣을 목록</param>
        private static void AssignClip(ref AudioClip slot, string assetPath, List<string> report)
        {
            if (slot != null) return;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
            {
                report.Add("  클립 없음 — " + assetPath);
                return;
            }

            slot = clip;
            report.Add("  클립 — " + clip.name);
        }

        /// <summary>
        /// 클립 배열을 폴더에서 이름 앞부분이 맞는 것들로 채웁니다.
        ///
        /// 배열로 두는 자리는 전부 "여러 개 중 무작위로 하나"를 쓰는 곳입니다.
        /// 같은 소리가 반복되면 금방 질리기 때문에, 후보를 여러 개 넣어 둡니다.
        /// </summary>
        /// <param name="slot">채울 클립 배열 필드</param>
        /// <param name="folder">06.Sound 아래의 폴더 이름</param>
        /// <param name="namePrefix">고를 파일 이름의 앞부분</param>
        /// <param name="report">결과를 적어 넣을 목록</param>
        private static void AssignClips(ref AudioClip[] slot, string folder, string namePrefix, List<string> report)
        {
            if (slot != null && slot.Length > 0) return;

            string directory = SoundRoot + folder;
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { directory });

            List<AudioClip> found = new List<AudioClip>();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!System.IO.Path.GetFileName(path).StartsWith(namePrefix)) continue;

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) found.Add(clip);
            }

            if (found.Count == 0)
            {
                report.Add("  클립 없음 — " + directory + "/" + namePrefix + "*");
                return;
            }

            // 파일 이름 순으로 세웁니다. 검색 순서는 보장되지 않아 실행할 때마다 배열이 뒤바뀝니다.
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            slot = found.ToArray();
            report.Add("  클립 " + found.Count + "개 — " + namePrefix + "*");
        }

        /// <summary>
        /// 컴포넌트가 없으면 붙이고, 있으면 그대로 씁니다.
        /// </summary>
        /// <param name="target">붙일 오브젝트</param>
        /// <param name="report">결과를 적어 넣을 목록</param>
        /// <returns>확보된 컴포넌트</returns>
        private static T Ensure<T>(GameObject target, List<string> report) where T : Component
        {
            T existing = target.GetComponent<T>();
            if (existing != null)
            {
                report.Add("  " + typeof(T).Name + " — 이미 있음");
                return existing;
            }

            report.Add("  " + typeof(T).Name + " — 붙임");
            return target.AddComponent<T>();
        }

        /// <summary>
        /// 이 오브젝트의 AudioSource 를 확보하고 설정을 맞춥니다.
        ///
        /// 일부 컨트롤러는 <c>[RequireComponent(typeof(AudioSource))]</c> 라 이미 하나가 붙어 있습니다.
        /// 그것을 그대로 효과음용으로 씁니다. 새로 만들면 정체가 같은 컴포넌트가 둘이 됩니다.
        /// </summary>
        /// <param name="target">AudioSource 를 둘 오브젝트</param>
        /// <param name="loop">반복 재생할지 여부</param>
        /// <param name="spatialBlend">0이면 2D, 1이면 3D입니다.</param>
        /// <returns>설정이 맞춰진 AudioSource</returns>
        private static AudioSource EnsureSourceOnSelf(GameObject target, bool loop, float spatialBlend)
        {
            AudioSource source = target.GetComponent<AudioSource>();
            if (source == null) source = target.AddComponent<AudioSource>();

            Configure(source, loop, spatialBlend);
            return source;
        }

        /// <summary>
        /// 이름이 정해진 자식에 AudioSource 를 확보하고 설정을 맞춥니다.
        /// 자식이 없으면 만듭니다.
        /// </summary>
        /// <param name="parent">부모 오브젝트</param>
        /// <param name="childName">자식 오브젝트 이름</param>
        /// <param name="loop">반복 재생할지 여부</param>
        /// <param name="spatialBlend">0이면 2D, 1이면 3D입니다.</param>
        /// <returns>설정이 맞춰진 AudioSource</returns>
        private static AudioSource EnsureSourceOnChild(GameObject parent, string childName, bool loop, float spatialBlend)
        {
            Transform child = parent.transform.Find(childName);
            if (child == null)
            {
                GameObject created = new GameObject(childName);
                created.transform.SetParent(parent.transform, false);
                child = created.transform;
            }

            return EnsureSourceOnSelf(child.gameObject, loop, spatialBlend);
        }

        /// <summary>
        /// AudioSource 설정을 이 프로젝트의 규칙에 맞춥니다.
        ///
        /// <b>playOnAwake 는 반드시 끕니다.</b> 켜 두면 클립을 끼우는 순간
        /// 씬을 시작하자마자 모든 적과 차가 동시에 소리를 냅니다.
        /// 재생 시점은 전부 코드가 정합니다.
        /// </summary>
        /// <param name="source">설정할 AudioSource</param>
        /// <param name="loop">반복 재생할지 여부</param>
        /// <param name="spatialBlend">0이면 2D, 1이면 3D입니다.</param>
        private static void Configure(AudioSource source, bool loop, float spatialBlend)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatialBlend;

            if (spatialBlend > 0f)
            {
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = MinDistance;
                source.maxDistance = MaxDistance;
            }

            EditorUtility.SetDirty(source);
        }
    }
}
