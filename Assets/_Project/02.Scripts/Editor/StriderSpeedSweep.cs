using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CarDrive.Gameplay;

/// <summary>
/// <b>얼마나 빨리 걸을 수 있는가</b>를 잽니다. 식이 내놓는 한계와 다리가 실제로 견디는 한계는 다릅니다.
///
/// <see cref="WalkerRobot.MaxTravelSpeed"/> 는 <c>계획 보폭 ÷ (걸음 시간 하한 × 묶음 수)</c> 라는
/// <b>산수</b>일 뿐입니다. 그 속도에서 발이 정말 안 끌리는지, IK 가 한계에서 잘리지 않는지는
/// 걸려 봐야 압니다. 그래서 순항 속도를 <b>천천히 올리면서</b> 속도 구간마다 따로 잽니다.
///
/// 속도를 계단으로 올리면 가속 구간의 미끄러짐이 정상 주행의 값으로 섞입니다. 그래서 램프로 올리고
/// <b>실제 속도</b>로 구간을 나눕니다. 명령이 아니라 결과로 나눠야 <c>TravelSpeedLimit</c> 에
/// 묶여 버린 구간이 저절로 드러납니다.
///
/// 환경변수로 손잡이를 돌립니다 — <c>STRIDER_USAGE</c>(작업 반경 사용률) ·
/// <c>STRIDER_MINSTEP</c>(걸음 시간 하한) · <c>STRIDER_SCALE</c>(뻗는 길이 사용률) ·
/// <c>STRIDER_STAND</c>(서는 높이) · <c>STRIDER_TAG</c>(보고서 이름).
///
/// ⚠ 재생 모드가 필요하므로 <c>-nographics</c> 를 붙이면 안 됩니다.
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod StriderSpeedSweep.Run -logFile &lt;로그&gt;
/// </code>
/// </summary>
public static class StriderSpeedSweep
{
    // --- Constants ---

    private const string PrefabPath = "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab";
    private const string OutputDirectory = "Logs/StriderMotion";
    private const string SessionKey = "StriderSpeedSweep.Armed";

    private const int GroundLayer = 11;

    private const float SettleSeconds = 2f;
    private const float RampSeconds = 60f;

    private const float StartSpeed = 2f;
    private const float EndSpeed = 14f;

    /// <summary>속도 구간의 폭입니다.</summary>
    private const float BinWidth = 0.5f;

    private const int BinCount = 30;

    // --- Private Member Variables ---

    private static WalkerRobot _robot;
    private static RobotDriver _driver;
    private static Transform _target;

    private static float _startTime;
    private static int _lastFrame = -1;

    private static Vector3[] _previousFoot;
    private static bool[] _wasStepping;

    private static readonly int[] Samples = new int[BinCount];
    private static readonly float[] SlipMax = new float[BinCount];
    private static readonly double[] SlipSum = new double[BinCount];
    private static readonly double[] ExtensionSum = new double[BinCount];
    private static readonly int[] Saturated = new int[BinCount];
    private static readonly int[] Airborne = new int[BinCount];
    private static readonly double[] CommandSum = new double[BinCount];
    private static readonly int[] Fallen = new int[BinCount];
    private static readonly float[] HeightLow = new float[BinCount];
    private static readonly float[] HeightHigh = new float[BinCount];
    private static readonly double[] StaggerSum = new double[BinCount];
    private static readonly int[] SwingSum = new int[BinCount];

    private static string _tag = "default";

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

    // --- Private Methods ---

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static float Knob(string name, float fallback)
    {
        string raw = System.Environment.GetEnvironmentVariable(name);
        return float.TryParse(raw, out float value) ? value : fallback;
    }

    private static void BuildWorld()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject ground = new GameObject("Ground") { layer = GroundLayer };
        BoxCollider floor = ground.AddComponent<BoxCollider>();
        floor.size = new Vector3(400f, 4f, 3000f);
        ground.transform.position = new Vector3(0f, -2f, 1400f);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        robot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        WalkerRobot walker = robot.GetComponent<WalkerRobot>();
        walker.strideUsage = Knob("STRIDER_USAGE", walker.strideUsage);
        walker.strideScale = Knob("STRIDER_SCALE", walker.strideScale);
        walker.minStepDuration = Knob("STRIDER_MINSTEP", walker.minStepDuration);
        walker.standHeight = Knob("STRIDER_STAND", walker.standHeight);
        walker.footfallImpact = Knob("STRIDER_FOOTFALL", walker.footfallImpact);
        walker.swingTilt = Knob("STRIDER_SWINGTILT", walker.swingTilt);
        walker.balanceFeedback = Knob("STRIDER_BALANCE", walker.balanceFeedback);
        walker.bodySpringGaitRatio = Knob("STRIDER_SPRINGRATIO", walker.bodySpringGaitRatio);
        walker.bodyPositionSpring = new CarDrive.Common.SecondOrderSettings(
            Knob("STRIDER_POSF", walker.bodyPositionSpring.frequency),
            Knob("STRIDER_POSZ", walker.bodyPositionSpring.damping),
            Knob("STRIDER_POSR", walker.bodyPositionSpring.response));

        string gaitName = System.Environment.GetEnvironmentVariable("STRIDER_GAIT");
        if (!string.IsNullOrEmpty(gaitName)) walker.gait = (WalkerGaitType)System.Enum.Parse(typeof(WalkerGaitType), gaitName);

        GameObject target = new GameObject("Destination");
        target.transform.position = new Vector3(0f, 0f, 400f);
        robot.GetComponent<RobotDriver>().destinationTarget = target.transform;
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
        }

        if (_robot == null)
        {
            Debug.LogError("StriderSpeedSweep: 로봇을 찾지 못했습니다");
            EditorApplication.Exit(2);
            return;
        }

        string raw = System.Environment.GetEnvironmentVariable("STRIDER_TAG");
        if (!string.IsNullOrEmpty(raw)) _tag = raw;

        int legs = _robot.legs.Length;
        _previousFoot = new Vector3[legs];
        _wasStepping = new bool[legs];
        for (int i = 0; i < legs; i++) _previousFoot[i] = _robot.legs[i].FootPosition;

        Time.captureDeltaTime = 1f / 60f;
        _startTime = Time.time;

        EditorApplication.update += Sample;
    }

    private static void Sample()
    {
        if (!EditorApplication.isPlaying) return;
        if (Time.frameCount == _lastFrame) return;
        _lastFrame = Time.frameCount;

        float t = Time.time - _startTime;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (t < SettleSeconds)
        {
            for (int i = 0; i < _robot.legs.Length; i++) _previousFoot[i] = _robot.legs[i].FootPosition;
            return;
        }

        float ramp = Mathf.Clamp01((t - SettleSeconds) / RampSeconds);
        _driver.cruiseSpeed = Mathf.Lerp(StartSpeed, EndSpeed, ramp);

        // 목표를 계속 앞에 둡니다. 도착하면 감속해 버려 램프가 끊깁니다.
        Vector3 root = _robot.transform.position;
        _target.position = new Vector3(0f, 0f, root.z + 300f);

        float speed = new Vector3(_robot.SmoothVelocity.x, 0f, _robot.SmoothVelocity.z).magnitude;
        int bin = Mathf.Clamp(Mathf.FloorToInt(speed / BinWidth), 0, BinCount - 1);

        Samples[bin]++;
        CommandSum[bin] += _driver.cruiseSpeed;
        if (!_robot.HasGround) Airborne[bin]++;
        if (!_robot.IsStanding) Fallen[bin]++;

        StaggerSum[bin] += _robot.GaitStagger;
        for (int k = 0; k < _robot.legs.Length; k++)
        {
            if (_robot.legs[k].IsStepping) SwingSum[bin]++;
        }

        float height = _robot.body.position.y - _robot.SupportHeight;
        if (HeightLow[bin] <= 0f || height < HeightLow[bin]) HeightLow[bin] = height;
        if (height > HeightHigh[bin]) HeightHigh[bin] = height;

        for (int i = 0; i < _robot.legs.Length; i++)
        {
            WalkerLeg leg = _robot.legs[i];
            Vector3 foot = leg.FootPosition;

            float span = Vector3.Distance(leg.HipPosition, leg.ankleBone != null ? leg.ankleBone.position : foot);
            float extension = span / Mathf.Max(leg.upperLength + leg.lowerLength, 0.001f);
            ExtensionSum[bin] += extension;
            if (extension > 0.98f) Saturated[bin]++;

            if (!leg.IsStepping && !_wasStepping[i] && leg.ankleBone != null)
            {
                // ⚠ FootPosition 은 WalkerLeg 이 들고 있는 상태값이라 딛는 동안 정의상 안 움직입니다.
                // 그것을 재면 언제나 0 입니다. 진짜 끌림은 IK 가 한계에서 잘려 <b>그려지는 발끝</b>이
                // 그 자리에서 벗어날 때 생깁니다. 그 차이를 잽니다.
                Vector3 drawn = leg.ankleBone.position + leg.ankleBone.forward * leg.ankleLength;
                float slip = Vector3.Distance(drawn, foot);
                SlipSum[bin] += slip;
                if (slip > SlipMax[bin]) SlipMax[bin] = slip;
            }

            _wasStepping[i] = leg.IsStepping;
            _previousFoot[i] = foot;
        }

        if (t < SettleSeconds + RampSeconds) return;

        EditorApplication.update -= Sample;
        Finish();
    }

    private static void Finish()
    {
        StringBuilder report = new StringBuilder();
        int legs = _robot.legs.Length;

        report.Append($"== 속도 스윕 [{_tag}] ==\n");
        report.Append($"작업 반경 사용률(strideUsage)  : {_robot.strideUsage:F2}\n");
        report.Append($"뻗는 길이 사용률(strideScale)  : {_robot.strideScale:F2}\n");
        report.Append($"걸음 시간 하한(minStepDuration): {_robot.minStepDuration:F2} s\n");
        report.Append($"서는 높이(standHeight)         : {_robot.standHeight:F2} m\n");
        report.Append($"계획 보폭                      : {_robot.PlannedStride:F2} m\n");
        report.Append($"식이 내놓는 최고 속도          : {_robot.MaxTravelSpeed:F2} m/s\n");
        report.Append("\n실제속도  명령   프레임   발끝오차(평균/최대,mm)   뻗은정도  IK포화  공중\n");

        float reached = 0f;
        float cleanCeiling = 0f;

        for (int i = 0; i < BinCount; i++)
        {
            if (Samples[i] < 30) continue;

            float lo = i * BinWidth;
            double slipMean = SlipSum[i] / (Samples[i] * legs);
            double extension = ExtensionSum[i] / (Samples[i] * legs);
            float saturation = (float)Saturated[i] / (Samples[i] * legs);

            reached = lo + BinWidth;

            // 그려지는 발끝이 1cm 넘게 벗어나면 끌리는 것으로 봅니다.
            if (slipMean * 1000.0 < 10.0 && saturation < 0.01f && Fallen[i] == 0) cleanCeiling = lo + BinWidth;

            report.Append($"{lo,5:F1}~{lo + BinWidth,-4:F1} {CommandSum[i] / Samples[i],5:F1}  {Samples[i],6}   " +
                          $"{slipMean * 1000.0,8:F2} / {SlipMax[i] * 1000f,-8:F2}  " +
                          $"{extension,7:P0}  {saturation,6:P1}  " +
                          $"{HeightHigh[i] - HeightLow[i],5:F2}  {StaggerSum[i] / Samples[i],5:F2}  " +
                          $"{1.0 - (double)SwingSum[i] / (Samples[i] * legs),6:P0}  " +
                          $"{(float)Fallen[i] / Samples[i],6:P0}\n");
        }

        report.Append($"\n실제로 닿은 최고 속도          : {reached:F1} m/s\n");
        report.Append($"발이 안 끌리는 최고 속도       : {cleanCeiling:F1} m/s\n");

        string path = Path.Combine(OutputDirectory, "sweep_" + _tag + ".txt");
        File.WriteAllText(path, report.ToString());

        Debug.Log("StriderSpeedSweep 결과\n" + report);

        EditorApplication.Exit(0);
    }
}
