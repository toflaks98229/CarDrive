using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CarDrive.Gameplay;

/// <summary>
/// 스트라이더가 <b>실제로 어떻게 걷는지</b>를 잽니다. 눈이 아니라 숫자로 답합니다.
///
/// <b>왜 재생 중에 재는가.</b> 이 로봇에는 애니메이션 클립이 없습니다. 걸음은 매 프레임
/// 발 자리와 IK 에서 나오므로, 프리팹을 아무리 들여다봐도 걸을 수 있는지 알 수 없습니다.
/// 특히 <see cref="WalkerRobot.MaxTravelSpeed"/> 는 다리 기하에서 나오는데, 그것이
/// <see cref="RobotDriver.cruiseSpeed"/> 보다 낮으면 <b>발이 끌립니다.</b>
///
/// <b>무엇을 재는가.</b>
///  1. 리그가 낼 수 있는 속도 — 작업 반경 · 계획 보폭 · 최고 속도
///  2. 실제 이동 속도와 명령 속도의 차이
///  3. <b>발 미끄러짐</b> — 딛고 있는 발이 프레임 사이에 움직인 거리. 0 이어야 합니다
///  4. <b>IK 포화</b> — 다리가 끝까지 뻗어 잘린 프레임의 비율
///  5. 보폭 · 걸음 주기 · 접지율 · 발이 들리는 높이
///  6. 몸통 높이 · 피치 · 롤의 흔들림 폭
///  7. 무릎이 몸통보다 높이 솟아 있는가 (스트라이더의 실루엣)
///
/// ⚠ 재생 모드와 렌더링이 필요하므로 <c>-nographics</c> 를 붙이면 안 됩니다.
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod StriderMotionCheck.Run -logFile &lt;로그&gt;
/// </code>
/// 결과: <c>Logs/StriderMotion/report.txt</c> 와 걸음 연속 사진 <c>gait_*.png</c>
/// </summary>
public static class StriderMotionCheck
{
    // --- Constants ---

    private const string DefaultPrefab = "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab";

    /// <summary>STRIDER_PREFAB 으로 다른 기계를 재도록 바꿀 수 있습니다.</summary>
    private static string PrefabPath
    {
        get
        {
            string raw = System.Environment.GetEnvironmentVariable("STRIDER_PREFAB");
            return string.IsNullOrEmpty(raw) ? DefaultPrefab : raw;
        }
    }
    private const string OutputDirectory = "Logs/StriderMotion";
    private const string SessionKey = "StriderMotionCheck.Armed";

    private const int GroundLayer = 11;

    /// <summary>가는 길에 놓는 턱의 높이입니다. 발이 지형을 따라가는지 보려면 평지로는 모자랍니다.</summary>
    private static float LedgeHeight { get { return Knob("STRIDER_LEDGE", 1.2f); } }

    /// <summary>턱을 놓는 자리입니다.</summary>
    private const float LedgeZ = 25f;

    /// <summary>자리 잡을 시간입니다. 스폰 직후의 낙하·정착은 걸음이 아닙니다.</summary>
    private const float SettleSeconds = 2f;

    /// <summary>직진으로 재는 시간입니다.</summary>
    private const float WalkSeconds = 20f;

    /// <summary>돌아서게 하고 재는 시간입니다.</summary>
    private const float TurnSeconds = 16f;

    /// <summary>걸음 사진을 찍는 간격입니다.</summary>
    private const float ShotInterval = 0.45f;

    private const int ShotCount = 8;
    private const int ShotSize = 720;

    // --- Private Member Variables ---

    private static WalkerRobot _robot;
    private static RobotDriver _driver;
    private static Transform _target;
    private static Camera _shotCamera;

    private static float _startTime;
    private static int _lastFrame = -1;

    private static Vector3 _previousRoot;
    private static Vector3[] _previousFoot;
    private static bool[] _wasStepping;
    private static Vector3[] _lastPlant;
    private static float[] _swingApex;

    private static readonly List<float> Speeds = new List<float>();
    private static readonly List<float> Slips = new List<float>();
    private static readonly List<float> Heights = new List<float>();
    private static readonly List<float> Pitches = new List<float>();
    private static readonly List<float> Rolls = new List<float>();

    /// <summary>턱에서 멀리 떨어진 <b>평지 직진</b>에서만 모은 기울기입니다.</summary>
    private static readonly List<float> FlatPitches = new List<float>();
    private static readonly List<float> FlatRolls = new List<float>();

    /// <summary>롤이 크게 기운 프레임 수입니다. 최댓값 한 점만으로는 잠깐 튄 것인지 알 수 없습니다.</summary>
    private static int _rollOver15;
    private static int _rollOver25;
    private static readonly List<float> KneeOverBody = new List<float>();
    private static readonly List<float> TurnRates = new List<float>();
    private static readonly List<float> Supports = new List<float>();
    private static readonly List<float> LedgeSpeeds = new List<float>();

    /// <summary>딛고 있는 발 가운데 가장 높이 올라선 높이입니다. 지지면 평균으로는 턱을 알 수 없습니다.</summary>
    private static float _plantedFootPeak;
    private static readonly List<float>[] Strides = { new List<float>(), new List<float>(), new List<float>() };
    private static readonly List<float>[] Lifts = { new List<float>(), new List<float>(), new List<float>() };
    private static readonly List<float>[] Extensions = { new List<float>(), new List<float>(), new List<float>() };

    private static int[] _steps;
    private static int[] _swingFrames;
    private static int[] _saturatedFrames;
    private static int _frames;
    private static int _walkFrames;
    private static int _airborneFrames;

    private static float _turnStartHeading;
    private static float _turnStartTime;
    private static float _turnFinishedTime = -1f;
    private static float _turnPeak;

    private static int _shotsTaken;
    private static float _nextShot;

    private static readonly StringBuilder Report = new StringBuilder();

    /// <summary>몸통 높이가 방향을 바꾼 횟수입니다. 목표를 넘어섰다 되돌아오는 <b>울림</b>의 세기입니다.</summary>
    private static int _heightReversals;

    private static float _previousHeight;
    private static int _heightSign;
    private static int _fallenFrames;


    // --- Public Methods ---

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

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void BuildWorld()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject ground = new GameObject("Ground") { layer = GroundLayer };
        BoxCollider floor = ground.AddComponent<BoxCollider>();
        floor.size = new Vector3(600f, 4f, 600f);
        ground.transform.position = new Vector3(0f, -2f, 0f);

        // 가는 길에 1.2 m 턱을 하나 놓습니다. 평지만 재면 발이 지형을 따라가는지 알 수 없습니다.
        GameObject ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ledge.name = "Ledge";
        ledge.layer = GroundLayer;
        ledge.transform.position = new Vector3(0f, LedgeHeight * 0.5f, LedgeZ);
        ledge.transform.localScale = new Vector3(40f, LedgeHeight, 8f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Plane);
        visual.name = "GroundVisual";
        visual.transform.localScale = new Vector3(60f, 1f, 60f);
        UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        robot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        GameObject target = new GameObject("Destination");
        target.transform.position = new Vector3(0f, 0f, 80f);

        RobotDriver driver = robot.GetComponent<RobotDriver>();
        driver.destinationTarget = target.transform;

        WalkerRobot walker = robot.GetComponent<WalkerRobot>();

        driver.cruiseSpeed = Knob("STRIDER_CRUISE", driver.cruiseSpeed);
        walker.minStepDuration = Knob("STRIDER_MINSTEP", walker.minStepDuration);
        walker.strideUsage = Knob("STRIDER_USAGE", walker.strideUsage);
        walker.bodyBob = Knob("STRIDER_BOB", walker.bodyBob);
        walker.stepHeight = Knob("STRIDER_STEPHEIGHT", walker.stepHeight);
        walker.leanIntoTurn = Knob("STRIDER_LEAN", walker.leanIntoTurn);
        walker.pitchIntoAccel = Knob("STRIDER_PITCHACCEL", walker.pitchIntoAccel);
        walker.stepTriggerFraction = Knob("STRIDER_TRIGGER", walker.stepTriggerFraction);
        walker.footfallImpact = Knob("STRIDER_FOOTFALL", walker.footfallImpact);
        walker.swingTilt = Knob("STRIDER_SWINGTILT", walker.swingTilt);
        walker.balanceFeedback = Knob("STRIDER_BALANCE", walker.balanceFeedback);
        walker.bodySpringGaitRatio = Knob("STRIDER_SPRINGRATIO", walker.bodySpringGaitRatio);

        string gait = System.Environment.GetEnvironmentVariable("STRIDER_GAIT");
        if (!string.IsNullOrEmpty(gait)) walker.gait = (WalkerGaitType)System.Enum.Parse(typeof(WalkerGaitType), gait);

        walker.bodyPositionSpring = Spring("STRIDER_POSSPRING", walker.bodyPositionSpring);
        walker.bodyRotationSpring = Spring("STRIDER_ROTSPRING", walker.bodyRotationSpring);

        GameObject sun = new GameObject("Sun");
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.40f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.20f, 0.18f);
        RenderSettings.fog = false;

        GameObject camObject = new GameObject("Shot");
        Camera cam = camObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.15f, 0.17f);
        cam.fieldOfView = 34f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 500f;
        cam.enabled = false;
    }

    private static float Knob(string name, float fallback)
    {
        string raw = System.Environment.GetEnvironmentVariable(name);
        return float.TryParse(raw, out float value) ? value : fallback;
    }

    /// <summary>"진동수,감쇠비,반응" 세 숫자를 그대로 받습니다.</summary>
    private static CarDrive.Common.SecondOrderSettings Spring(string name, CarDrive.Common.SecondOrderSettings fallback)
    {
        string raw = System.Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(raw)) return fallback;

        string[] parts = raw.Split(',');
        if (parts.Length != 3) return fallback;

        return new CarDrive.Common.SecondOrderSettings(
            float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        if (!SessionState.GetBool(SessionKey, false)) return;

        SessionState.SetBool(SessionKey, false);

        Scene scene = SceneManager.GetActiveScene();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (_robot == null) _robot = root.GetComponentInChildren<WalkerRobot>();
            if (_driver == null) _driver = root.GetComponentInChildren<RobotDriver>();
            if (root.name == "Destination") _target = root.transform;
            if (root.name == "Shot") _shotCamera = root.GetComponent<Camera>();
        }

        if (_robot == null || _driver == null)
        {
            Debug.LogError("StriderMotionCheck: 로봇을 찾지 못했습니다");
            EditorApplication.Exit(2);
            return;
        }

        int legs = _robot.legs.Length;
        _previousFoot = new Vector3[legs];
        _wasStepping = new bool[legs];
        _lastPlant = new Vector3[legs];
        _swingApex = new float[legs];
        _steps = new int[legs];
        _swingFrames = new int[legs];
        _saturatedFrames = new int[legs];

        for (int i = 0; i < legs; i++)
        {
            _previousFoot[i] = _robot.legs[i].FootPosition;
            _lastPlant[i] = _robot.legs[i].FootPosition;
        }

        _previousRoot = _robot.transform.position;
        Time.captureDeltaTime = 1f / 60f;
        _startTime = Time.time;
        _nextShot = SettleSeconds + 2f;

        WriteRigSection();

        EditorApplication.update += Sample;
    }

    // --- Private Methods : 잰다 ---

    /// <summary>리그 자체가 낼 수 있는 값입니다. 걷기 전에 한 번 적습니다.</summary>
    private static void WriteRigSection()
    {
        Line("== 리그가 정하는 한계 ==");
        Line($"서는 높이(standHeight)      : {_robot.standHeight:F2} m");
        Line($"가장 좁은 작업 반경          : {_robot.StrideRadius:F2} m");
        Line($"계획 보폭(PlannedStride)     : {_robot.PlannedStride:F2} m");
        Line($"다리가 낼 수 있는 최고 속도  : {_robot.MaxTravelSpeed:F2} m/s");
        Line($"명령한 순항 속도(cruiseSpeed): {_driver.cruiseSpeed:F2} m/s");
        Line($"실제로 묶이는 속도           : {_driver.TravelSpeedLimit:F2} m/s");
        Line($"명령한 선회 속도(turnRate)   : {_driver.turnRate:F0} °/s");
        Line($"몸통 위치 스프링             : f {_robot.bodyPositionSpring.frequency:F2} · ζ {_robot.bodyPositionSpring.damping:F2} · r {_robot.bodyPositionSpring.response:F2}");
        Line($"몸통 회전 스프링             : f {_robot.bodyRotationSpring.frequency:F2} · ζ {_robot.bodyRotationSpring.damping:F2} · r {_robot.bodyRotationSpring.response:F2}");
        Line($"발디딤 {_robot.footfallImpact:F2} · 흔들기울임 {_robot.swingTilt:F2} · 균형되먹임 {_robot.balanceFeedback:F2} · 용수철배수 {_robot.bodySpringGaitRatio:F2} · 보행 {_robot.gait} · bodyBob {_robot.bodyBob:F2} · stepHeight {_robot.stepHeight:F2} · strideUsage {_robot.strideUsage:F2} · minStepDuration {_robot.minStepDuration:F2}");
        Line("");

        for (int i = 0; i < _robot.legs.Length; i++)
        {
            WalkerLeg leg = _robot.legs[i];
            float reach = leg.MaxReach;
            Line($"다리 {i} {leg.name}: 마디 {leg.upperLength:F2}+{leg.lowerLength:F2}+{leg.ankleLength:F2} " +
                 $"= 뻗는 길이 {reach:F2} m | 작업 반경 {leg.StrideRadius:F2} m | " +
                 $"서 있을 때 쓰는 비율 {(_robot.standHeight / reach):P0}");
        }

        Line("");
    }

    private static void Sample()
    {
        if (!EditorApplication.isPlaying) return;
        if (Time.frameCount == _lastFrame) return;
        _lastFrame = Time.frameCount;

        float t = Time.time - _startTime;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 1단계: 자리 잡기. 재지 않습니다.
        if (t < SettleSeconds)
        {
            _previousRoot = _robot.transform.position;
            for (int i = 0; i < _robot.legs.Length; i++) _previousFoot[i] = _robot.legs[i].FootPosition;
            return;
        }

        // 2단계: 직진. 3단계: 돌아서기.
        if (t >= SettleSeconds + WalkSeconds && _turnFinishedTime < 0f)
        {
            _target.position = new Vector3(0f, 0f, _robot.transform.position.z - 60f);
            _turnStartHeading = _robot.transform.eulerAngles.y;
            _turnStartTime = t;
            _turnFinishedTime = 0f;
            _turnPeak = 0f;
        }

        bool walking = t < SettleSeconds + WalkSeconds;

        Vector3 root = _robot.transform.position;
        Vector3 delta = root - _previousRoot;
        _previousRoot = root;

        if (walking)
        {
            Speeds.Add(new Vector3(delta.x, 0f, delta.z).magnitude / dt);
        }
        else
        {
            float turned = Mathf.Abs(Mathf.DeltaAngle(_turnStartHeading, _robot.transform.eulerAngles.y));
            TurnRates.Add(turned);

            if (turned > _turnPeak) _turnPeak = turned;
            if (_turnFinishedTime <= 0f && turned >= 170f) _turnFinishedTime = t - _turnStartTime;
        }

        _frames++;
        if (walking) _walkFrames++;
        if (!_robot.HasGround) _airborneFrames++;

        Transform body = _robot.body;
        float height = body.position.y - _robot.SupportHeight;
        Heights.Add(height);

        if (!_robot.IsStanding) _fallenFrames++;

        int sign = height > _previousHeight ? 1 : height < _previousHeight ? -1 : _heightSign;
        if (_heightSign != 0 && sign != 0 && sign != _heightSign) _heightReversals++;
        _heightSign = sign;
        _previousHeight = height;

        if (walking)
        {
            Supports.Add(_robot.SupportHeight);
            if (Mathf.Abs(root.z - LedgeZ) < 7f) LedgeSpeeds.Add(new Vector3(delta.x, 0f, delta.z).magnitude / dt);
        }

        Vector3 euler = body.rotation.eulerAngles;
        float pitch = Mathf.DeltaAngle(0f, euler.x);
        float roll = Mathf.DeltaAngle(0f, euler.z);
        Pitches.Add(pitch);
        Rolls.Add(roll);

        // 턱을 넘을 때는 발 높이가 갈려 몸이 크게 기웁니다 — 그것이 정상이지만, 평지에서
        // 걸음이 만드는 흔들림과 섞이면 어느 쪽 숫자인지 알 수 없습니다. 갈라서 모읍니다.
        if (walking && Mathf.Abs(root.z - LedgeZ) > 7f)
        {
            FlatPitches.Add(pitch);
            FlatRolls.Add(roll);
        }

        if (Mathf.Abs(roll) > 15f) _rollOver15++;
        if (Mathf.Abs(roll) > 25f) _rollOver25++;

        float highestKnee = float.MinValue;

        for (int i = 0; i < _robot.legs.Length; i++)
        {
            WalkerLeg leg = _robot.legs[i];
            Vector3 foot = leg.FootPosition;

            // 다리가 얼마나 뻗어 있는가. 1.0 이면 IK 가 한계에서 잘리고 있습니다.
            float span = Vector3.Distance(leg.HipPosition, leg.ankleBone != null ? leg.ankleBone.position : foot);
            float extension = span / Mathf.Max(leg.upperLength + leg.lowerLength, 0.001f);
            Extensions[i].Add(extension);
            if (extension > 0.98f) _saturatedFrames[i]++;

            if (leg.lowerBone != null) highestKnee = Mathf.Max(highestKnee, leg.lowerBone.position.y);

            if (leg.IsStepping)
            {
                if (walking) _swingFrames[i]++;
                _swingApex[i] = Mathf.Max(_swingApex[i], foot.y - _robot.SupportHeight);
            }
            else if (!_wasStepping[i])
            {
                if (walking) _plantedFootPeak = Mathf.Max(_plantedFootPeak, foot.y);

                // ⚠ FootPosition 은 WalkerLeg 이 들고 있는 상태값이라 딛는 동안 정의상 안 움직입니다.
                // 그것을 재면 언제나 0 입니다. 진짜 끌림은 IK 가 한계에서 잘려 <b>그려지는 발끝</b>이
                // 그 자리에서 벗어날 때 생깁니다. 그 차이를 잽니다.
                if (leg.ankleBone != null)
                {
                    Vector3 drawn = leg.ankleBone.position + leg.ankleBone.forward * leg.ankleLength;
                    Slips.Add(Vector3.Distance(drawn, foot));
                }
            }

            if (_wasStepping[i] && !leg.IsStepping)
            {

                // 선회 중에는 발이 회전 반경(7.4m)을 따라 크게 돌므로 보폭 통계가 오염됩니다.
                if (walking)
                {
                    _steps[i]++;
                    Strides[i].Add(Vector3.Distance(foot, _lastPlant[i]));
                    Lifts[i].Add(_swingApex[i]);
                }

                _lastPlant[i] = foot;
                _swingApex[i] = 0f;
            }

            _wasStepping[i] = leg.IsStepping;
            _previousFoot[i] = foot;
        }

        if (highestKnee > float.MinValue) KneeOverBody.Add(highestKnee - body.position.y);

        if (walking && _shotsTaken < ShotCount && t >= _nextShot)
        {
            Shoot(_shotsTaken);
            _shotsTaken++;
            _nextShot += ShotInterval;
        }

        if (t < SettleSeconds + WalkSeconds + TurnSeconds) return;

        EditorApplication.update -= Sample;
        Finish();
    }

    private static void Shoot(int index)
    {
        Vector3 focus = _robot.body.position;
        _shotCamera.transform.position = focus + new Vector3(30f, 4f, 0f);
        _shotCamera.transform.rotation = Quaternion.LookRotation(focus - _shotCamera.transform.position, Vector3.up);

        RenderTexture rt = new RenderTexture(ShotSize, ShotSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        _shotCamera.targetTexture = rt;
        _shotCamera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D shot = new Texture2D(ShotSize, ShotSize, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, ShotSize, ShotSize), 0, 0);
        shot.Apply();

        RenderTexture.active = previous;
        _shotCamera.targetTexture = null;

        string tag = System.Environment.GetEnvironmentVariable("STRIDER_TAG");
        if (string.IsNullOrEmpty(tag)) tag = "default";
        File.WriteAllBytes(Path.Combine(OutputDirectory, $"gait_{tag}_{index}.png"), shot.EncodeToPNG());

        UnityEngine.Object.DestroyImmediate(shot);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }

    // --- Private Methods : 적는다 ---

    private static void Finish()
    {
        Line("== 직진 실측 ==");
        Line($"프레임                       : 직진 {_walkFrames} · 전체 {_frames}");
        Line($"실제 이동 속도               : 평균 {Mean(Speeds):F2} · 최대 {Max(Speeds):F2} m/s");
        Line($"명령 대비                    : {(Mean(Speeds) / Mathf.Max(_driver.TravelSpeedLimit, 0.001f)):P0}");
        Line($"발끝 오차(그려진 발끝 vs 발자리): 평균 {Mean(Slips) * 1000f:F1} · 최대 {Max(Slips) * 1000f:F1} mm");
        Line($"발이 땅을 못 찾은 프레임     : {_airborneFrames} ({(float)_airborneFrames / Mathf.Max(_frames, 1):P1})");
        Line($"걸음 빈도                    : 다리마다 {(_steps[0] / Mathf.Max(WalkSeconds, 0.001f)):F2} 걸음/초 (한 바퀴 {(WalkSeconds / Mathf.Max(_steps[0], 1)):F2} s)");
        Line("");

        for (int i = 0; i < _robot.legs.Length; i++)
        {
            float duty = 1f - (float)_swingFrames[i] / Mathf.Max(_walkFrames, 1);
            Line($"다리 {i} {_robot.legs[i].name}: 걸음 {_steps[i]}회 | 보폭 평균 {Mean(Strides[i]):F2} m " +
                 $"(최대 {Max(Strides[i]):F2}) | 접지율 {duty:P0} | 발 들림 평균 {Mean(Lifts[i]):F2} m | " +
                 $"뻗은 정도 평균 {Mean(Extensions[i]):P0} (최대 {Max(Extensions[i]):P0}) | " +
                 $"한계에서 잘린 프레임 {(float)_saturatedFrames[i] / Mathf.Max(_frames, 1):P1}");
        }

        Line("");
        Line("== 몸통 ==");
        Line($"지지면 위 높이               : 평균 {Mean(Heights):F2} · {Min(Heights):F2} ~ {Max(Heights):F2} m (진폭 {Max(Heights) - Min(Heights):F2})");
        Line($"높이가 방향을 바꾼 횟수      : {_heightReversals}회 ({_heightReversals / Mathf.Max(_frames / 60f, 0.001f):F1} 회/초) — 울림의 세기");
        Line($"쓰러진 프레임                : {_fallenFrames} ({(float)_fallenFrames / Mathf.Max(_frames, 1):P1})");
        Line($"피치 (평지 직진)             : {Min(FlatPitches):F1} ~ {Max(FlatPitches):F1}° (폭 {Max(FlatPitches) - Min(FlatPitches):F1})");
        Line($"롤   (평지 직진)             : {Min(FlatRolls):F1} ~ {Max(FlatRolls):F1}° (폭 {Max(FlatRolls) - Min(FlatRolls):F1})");
        Line($"피치 (턱·선회 포함 전체)     : {Min(Pitches):F1} ~ {Max(Pitches):F1}°");
        Line($"롤   (턱·선회 포함 전체)     : {Min(Rolls):F1} ~ {Max(Rolls):F1}°");
        Line($"롤이 크게 기운 시간          : 15° 초과 {_rollOver15} 프레임 ({_rollOver15 / 60f:F2} s) · " +
             $"25° 초과 {_rollOver25} 프레임 ({_rollOver25 / 60f:F2} s)");
        Line($"가장 높은 무릎 − 몸통        : 평균 {Mean(KneeOverBody):F2} · {Min(KneeOverBody):F2} ~ {Max(KneeOverBody):F2} m");
        Line("");

        Line("== 1.2 m 턱 넘기 ==");
        Line($"딛은 발이 올라선 최고 높이   : {_plantedFootPeak:F2} m (턱 높이 {LedgeHeight:F2})");
        Line($"지지면 평균 높이             : {Min(Supports):F2} ~ {Max(Supports):F2} m (발 셋의 평균이라 1.20 에 닿을 수 없습니다)");
        Line($"턱 부근 속도                 : 평균 {Mean(LedgeSpeeds):F2} m/s (전체 평균 {Mean(Speeds):F2})");
        Line($"넘었는가                     : {(_plantedFootPeak >= LedgeHeight * 0.9f ? "예 — 발이 턱 위에 올라서 딛었습니다" : "아니오")}");
        Line("");

        Line("== 선회 실측 ==");
        bool turned = _turnFinishedTime > 0f;
        Line($"돌아선 각도                  : {_turnPeak:F0}°");
        Line($"170°까지 걸린 시간           : {(turned ? _turnFinishedTime.ToString("F1") + " s" : "> " + TurnSeconds.ToString("F0") + " s (못 돌았습니다)")}");
        Line($"평균 각속도                  : {(turned ? (170f / _turnFinishedTime) : (_turnPeak / TurnSeconds)):F1} °/s (명령 {_driver.turnRate:F0})");
        Line("");

        string tag = System.Environment.GetEnvironmentVariable("STRIDER_TAG");
        if (string.IsNullOrEmpty(tag)) tag = "default";
        string path = Path.Combine(OutputDirectory, "motion_" + tag + ".txt");
        File.WriteAllText(path, Report.ToString());

        Debug.Log("StriderMotionCheck 결과\n" + Report);
        Debug.Log("StriderMotionCheck: " + path);

        EditorApplication.Exit(0);
    }

    private static void Line(string text)
    {
        Report.Append(text).Append('\n');
    }

    private static float Mean(List<float> values)
    {
        if (values.Count == 0) return 0f;
        float sum = 0f;
        for (int i = 0; i < values.Count; i++) sum += values[i];
        return sum / values.Count;
    }

    private static float Max(List<float> values)
    {
        if (values.Count == 0) return 0f;
        float best = float.MinValue;
        for (int i = 0; i < values.Count; i++) best = Mathf.Max(best, values[i]);
        return best;
    }

    private static float Min(List<float> values)
    {
        if (values.Count == 0) return 0f;
        float best = float.MaxValue;
        for (int i = 0; i < values.Count; i++) best = Mathf.Min(best, values[i]);
        return best;
    }
}
