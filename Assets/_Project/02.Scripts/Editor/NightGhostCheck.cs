using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

/// <summary>
/// <see cref="NightGhostSpawner"/> 가 <b>정말로 밤에만 내놓고 날이 밝으면 거두는지</b> 잽니다.
///
/// <b>왜 필요한가.</b> 이 컴포넌트의 고장은 전부 <b>조용합니다</b> — 바닥 레이어가 틀리면
/// 자리를 못 찾아 아무것도 안 나오고, 시계가 안 돌면 밤이 오지 않고, 프리팹이 비어 있으면
/// 그냥 안 나옵니다. 셋 다 화면상 "밤인데 조용하다" 와 구별되지 않습니다.
/// 그래서 숫자로 확인합니다.
///
/// 씬은 <b>새로 만듭니다.</b> 본 씬을 쓰면 시계·날씨가 실제로 흐르기를 기다려야 해서
/// 검사에 몇 분이 걸립니다. 여기서는 시계를 가짜로 물려 밤·낮을 즉시 뒤집습니다.
///
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod NightGhostCheck.Run -logFile &lt;로그&gt;
/// </code>
/// ⚠ <c>-nographics</c> 를 붙이면 안 됩니다. 렌더러가 없으면 카메라가 기준으로 서지 않습니다.
/// </summary>
public static class NightGhostCheck
{
    // --- Constants ---

    private const string SessionKey = "NightGhostCheck.Armed";
    private const string OutputDirectory = "Logs/NightGhost";
    private const string GhostPrefabPath = "Assets/_Project/05.Prefabs/Monster/Monster_1.prefab";
    private const int GroundLayer = 11;

    /// <summary>밤을 몇 초 동안 돌려 볼지입니다.</summary>
    private const float NightSeconds = 12f;

    /// <summary>날이 밝은 뒤 거두는 것을 확인할 시간입니다.</summary>
    private const float DaySeconds = 1.5f;

    // --- Private Member Variables ---

    private static StubClock clock;
    private static NightGhostSpawner spawner;
    private static float phaseEnd;
    private static int phase;
    private static int peakAlive;
    private static int spawnEvents;
    private static int lastSeen;
    private static readonly StringBuilder report = new StringBuilder();

    // --- Public Methods ---

    /// <summary>검사 씬을 세우고 플레이 모드로 들어갑니다.</summary>
    [MenuItem("CarDrive/Gameplay/밤 귀신 재기")]
    public static void Run()
    {
        Directory.CreateDirectory(OutputDirectory);

        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        BuildWorld();

        SessionState.SetBool(SessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    // --- Private Methods : 세계 ---

    /// <summary>플레이 모드 전환을 듣습니다.</summary>
    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>바닥 · 카메라 · 스포너뿐인 씬을 만듭니다.</summary>
    private static void BuildWorld()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject ground = new GameObject("Ground") { layer = GroundLayer };
        BoxCollider floor = ground.AddComponent<BoxCollider>();
        floor.size = new Vector3(600f, 4f, 600f);
        ground.transform.position = new Vector3(0f, -2f, 0f);

        GameObject camObj = new GameObject("Camera");
        camObj.AddComponent<Camera>().tag = "MainCamera";
        camObj.transform.position = new Vector3(0f, 1.6f, 0f);

        GameObject holder = new GameObject("NightGhosts");
        NightGhostSpawner s = holder.AddComponent<NightGhostSpawner>();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GhostPrefabPath);
        s.ghostPrefabs.Clear();
        if (prefab != null) s.ghostPrefabs.Add(prefab);

        s.groundMask = 1 << GroundLayer;

        // 검사 시간을 줄이려고 간격만 좁힙니다. 자리와 거리 규칙은 실전 값 그대로 둡니다.
        s.minSpawnInterval = 0.5f;
        s.maxSpawnInterval = 1.2f;
        s.useWeatherActivity = false;
        s.maxAlive = 4;
    }

    // --- Private Methods : 측정 ---

    /// <summary>플레이 모드에 들어가면 가짜 시계를 물리고 재기 시작합니다.</summary>
    /// <param name="change">전환 종류</param>
    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        if (!SessionState.GetBool(SessionKey, false)) return;

        SessionState.SetBool(SessionKey, false);

        spawner = Object.FindAnyObjectByType<NightGhostSpawner>();
        if (spawner == null)
        {
            Finish("NightGhostCheck: 씬에서 NightGhostSpawner 를 찾지 못했습니다.", 2);
            return;
        }

        // 진짜 시계 대신 이것을 물립니다. 밤·낮을 한 줄로 뒤집을 수 있습니다.
        clock = new StubClock { Night = true };
        spawner.Construct(clock, null);

        report.Length = 0;
        report.AppendLine("== 밤 귀신 실측 ==");
        report.AppendLine("프리팹        : " + spawner.ghostPrefabs.Count + "개");
        report.AppendLine("간격          : " + spawner.minSpawnInterval.ToString("F2") + " ~ " +
                          spawner.maxSpawnInterval.ToString("F2") + " 초 (검사용으로 좁힘)");
        report.AppendLine("거리          : " + spawner.minSpawnDistance.ToString("F0") + " ~ " +
                          spawner.maxSpawnDistance.ToString("F0") + " m · 거두기 " +
                          spawner.despawnDistance.ToString("F0") + " m");
        report.AppendLine("동시 상한     : " + spawner.maxAlive);
        report.AppendLine();

        peakAlive = 0;
        spawnEvents = 0;
        lastSeen = 0;
        phase = 0;
        phaseEnd = Time.realtimeSinceStartup + NightSeconds;

        EditorApplication.update += Tick;
    }

    /// <summary>밤을 재고, 날을 밝히고, 거두는지 확인합니다.</summary>
    private static void Tick()
    {
        if (spawner == null)
        {
            Finish("NightGhostCheck: 스포너가 사라졌습니다.", 2);
            return;
        }

        int now = spawner.AliveCount;
        if (now > lastSeen) spawnEvents += now - lastSeen;
        lastSeen = now;
        if (now > peakAlive) peakAlive = now;

        if (Time.realtimeSinceStartup < phaseEnd) return;

        if (phase == 0)
        {
            report.AppendLine("밤 " + NightSeconds.ToString("F0") + "초");
            report.AppendLine("  나온 횟수    : " + spawnEvents);
            report.AppendLine("  동시 최대    : " + peakAlive + " (상한 " + spawner.maxAlive + ")");
            report.AppendLine("  끝났을 때    : " + now + " 마리 살아 있음");
            report.AppendLine();

            clock.Night = false;
            phase = 1;
            phaseEnd = Time.realtimeSinceStartup + DaySeconds;
            return;
        }

        report.AppendLine("날이 밝은 뒤 " + DaySeconds.ToString("F1") + "초");
        report.AppendLine("  남은 마릿수  : " + now);
        report.AppendLine();

        bool spawned = spawnEvents > 0;
        bool capped = peakAlive <= spawner.maxAlive;
        bool retired = now == 0;

        report.AppendLine("밤에 나왔는가   : " + (spawned ? "예" : "아니오"));
        report.AppendLine("상한을 지켰는가 : " + (capped ? "예" : "아니오"));
        report.AppendLine("날이 밝자 거뒀나: " + (retired ? "예" : "아니오"));

        int code = spawned && capped && retired ? 0 : 2;
        Finish(report.ToString(), code);
    }

    /// <summary>보고서를 남기고 끝냅니다.</summary>
    /// <param name="text">남길 본문</param>
    /// <param name="code">종료 코드</param>
    private static void Finish(string text, int code)
    {
        EditorApplication.update -= Tick;

        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, "night_ghost.txt"), text);
        Debug.Log(text);

        EditorApplication.isPlaying = false;

        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    // --- Private Types ---

    /// <summary>밤·낮을 손으로 뒤집을 수 있는 가짜 시계입니다.</summary>
    private sealed class StubClock : IGameClock
    {
        /// <summary>지금이 밤인지입니다. 검사가 직접 뒤집습니다.</summary>
        public bool Night;

        /// <summary>흐른 시간입니다. 이 검사에서는 쓰지 않습니다.</summary>
        public float TotalMinutes { get { return 0f; } }

        /// <summary>낮 밝기입니다. 밤이면 0입니다.</summary>
        public float Daylight { get { return Night ? 0f : 1f; } }

        /// <summary>지금이 밤인지 여부입니다.</summary>
        public bool IsNight { get { return Night; } }

        /// <summary>이 시계는 돌고 있다고 답합니다. 그래야 스포너가 일합니다.</summary>
        public bool IsRunning { get { return true; } }

        /// <summary>부르는 쪽의 기본값을 그대로 돌려줍니다.</summary>
        /// <param name="fallback">시계가 없을 때 쓸 배율</param>
        /// <returns><paramref name="fallback"/> 그대로</returns>
        public float GetMinutesPerSecond(float fallback) { return fallback; }

        /// <summary>시간을 건너뜁니다. 이 검사에서는 아무 일도 하지 않습니다.</summary>
        /// <param name="minutes">건너뛸 게임 시간(분)</param>
        public void AdvanceMinutes(float minutes) { }
    }
}
