using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CarDrive.Gameplay;

/// <summary>
/// 무장이 <b>정말로 움직이는지</b>를 잽니다. 프리팹에 부품이 붙은 것과 그 부품이
/// 적어 둔 만큼 움직이는 것은 다른 문제입니다.
///
/// <b>왜 눈으로 보면 안 되는가.</b> 이 부품들의 고장은 대부분 <b>조용합니다</b> —
/// 밀리는 거리가 절반이어도, 회전이 두 배 빨라도, 발사 간격이 어긋나도 오류가
/// 나지 않습니다. 특히 <b>제 포에 자빠지는 것</b>과 <b>줄이 몸을 뚫는 것</b>은
/// 로그에 아무것도 안 남깁니다.
///
/// ⚠ <b>한 번에 하나씩 켭니다.</b> 처음에는 여섯을 같이 돌렸는데, 여섯이 <b>같은
/// 포탑</b>에 반동을 넣는 바람에 포탑이 끊임없이 튀어 조준 오차가 늘 컸습니다.
/// 그래서 발사가 드물어졌고, 중포의 간격이 3.2 초 대신 <b>9.8 초</b>로 나왔습니다.
/// 값이 아니라 <b>재는 방식</b>이 틀린 것이라, 그 숫자를 보고 값을 고쳤으면
/// 멀쩡한 무장을 망가뜨릴 뻔했습니다.
///
/// <code>
/// Unity.exe -batchmode -projectPath . -executeMethod RobotWeaponCheck.Run
/// </code>
/// ⚠ <c>-nographics</c> 를 붙이면 안 됩니다. 재생 모드가 필요합니다.
/// </summary>
public static class RobotWeaponCheck
{
    // --- Constants ---

    private const string SessionKey = "RobotWeaponCheck.Armed";
    private const string OutputDirectory = "Logs/RobotWeapon";
    private const string PrefabPath = "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab";

    /// <summary>무장 하나를 최소 이만큼은 잽니다(초).</summary>
    private const float MinSeconds = 7f;

    /// <summary>발사 간격의 이 배수만큼 잽니다. 세 발은 나와야 간격을 잽니다.</summary>
    private const float IntervalSpan = 2.6f;

    /// <summary>목표를 놓을 거리입니다. 모든 무장의 사거리 안입니다.</summary>
    private const float TargetDistance = 24f;

    /// <summary>바닥의 레이어입니다. 프리팹의 <c>groundMask</c> 안에 있어야 합니다.</summary>
    private const int GroundLayer = 11;

    /// <summary>경계를 재는 동안 쓸 <b>짧은</b> 망각 시간입니다(초).</summary>
    private const float TestForget = 3f;

    /// <summary>경계 단계에서 각 상태를 확인하는 데 쓰는 여유입니다(초).</summary>
    private const float MoodSlack = 1.2f;

    /// <summary>찍는 그림의 한 변입니다(화소).</summary>
    private const int ShotSize = 900;

    /// <summary>
    /// 재기 전에 <b>가라앉히는</b> 시간입니다(초).
    ///
    /// 빈 씬에 떨어뜨린 로봇은 발을 심고 자세를 잡을 때까지 몇 초 동안 움직입니다.
    /// 그동안 잰 속도를 <b>포가 밀어낸 것</b>으로 읽으면 안 됩니다.
    /// </summary>
    private const float SettleSeconds = 3f;

    // --- Private Types ---

    /// <summary>무장 하나를 재는 동안 쌓는 값입니다.</summary>
    private class Track
    {
        public RobotWeapon weapon;
        public float firstShot = -1f;
        public float lastShot = -1f;
        public int shots;

        /// <summary>가장 짧았던 발 사이 간격입니다. 점사의 <b>연사 속도</b>가 이것입니다.</summary>
        public float minGap = float.MaxValue;

        /// <summary>밀린 조각마다 (가장 깊은 거리, 되돌아온 시간)입니다.</summary>
        public readonly List<float[]> recoil = new List<float[]>();
        public readonly List<WeaponRecoil> parts = new List<WeaponRecoil>();

        public float hatchOpen;
        public float swing;
        public float payout;

        /// <summary>이 무장을 재는 동안 몸이 낸 가장 큰 속도(m/s)입니다.</summary>
        public float bodySpeed;

        /// <summary>총구 섬광이 낸 가장 큰 세기입니다. 0 이면 <b>한 번도 안 번쩍였습니다.</b></summary>
        public float flash;

        /// <summary>이 무장의 발사 순간을 이미 찍었는지입니다.</summary>
        public bool shot;
    }

    // --- Private Member Variables ---

    private static readonly List<Track> tracks = new List<Track>();
    private static readonly StringBuilder report = new StringBuilder();
    private static float endAt;
    private static Rigidbody body;
    private static int turn = -2;

    /// <summary>가라앉히는 동안 몸이 낸 가장 큰 속도입니다. 바닥선입니다.</summary>
    private static float settleSpeed;

    /// <summary>경계 부품입니다. 마지막 단계에서 켭니다.</summary>
    private static RobotThreat threat;

    /// <summary>경계 단계에서 본 상태들입니다. 순서대로 쌓습니다.</summary>
    private static readonly List<string> moods = new List<string>();

    /// <summary>경계 단계가 끝나는 시각입니다.</summary>
    private static float moodEnd;

    /// <summary>경계를 언제 건드렸는지입니다.</summary>
    private static float provokedAt;

    /// <summary>겨눈 뒤 쏘기까지 걸린 시간입니다(초).</summary>
    private static float warnMeasured = -1f;

    /// <summary>발사 순간을 찍을 카메라입니다.</summary>
    private static Camera shot;

    // --- Public Methods ---

    /// <summary>검사 씬을 세우고 재생 모드로 들어갑니다.</summary>
    [MenuItem("CarDrive/Robot/무장 실측")]
    public static void Run()
    {
        Directory.CreateDirectory(OutputDirectory);

        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        Build();

        SessionState.SetBool(SessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    // --- Private Methods : 세계 ---

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>로봇 하나 · 목표 하나 · 바닥뿐인 씬을 만듭니다.</summary>
    private static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ⚠ 바닥은 <b>Ground 레이어</b>여야 합니다. 기본 레이어에 두었더니 다리가
        // 딛을 곳을 못 찾아 로봇이 <b>계속 떨어졌고</b>, 그 낙하 속도(12.95 m/s)가
        // 공성포가 밀어낸 것으로 잡혔습니다. 프리팹의 <c>groundMask</c> 가
        // 레이어 9 · 11 이므로 여기도 그것을 씁니다.
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(12f, 1f, 12f);
        ground.layer = GroundLayer;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        robot.transform.position = Vector3.zero;

        // 전부 꺼 둡니다. 한 무장씩 켜 가며 잽니다.
        foreach (RobotWeapon weapon in robot.GetComponentsInChildren<RobotWeapon>(true))
        {
            weapon.gameObject.SetActive(false);
        }

        // ⚠ 무장을 재는 동안에는 <b>경계를 끕니다.</b> 켜 두면 그쪽이 무장을 꺼 버려
        // 아무것도 안 나갑니다. 경계는 마지막 단계에서 따로 잽니다.
        foreach (RobotThreat watcher in robot.GetComponentsInChildren<RobotThreat>(true))
        {
            watcher.authoredTarget = null;
            watcher.enabled = false;
        }

        // <b>번쩍이는 그 프레임</b>을 찍을 카메라입니다. 숫자는 섬광이 났다고
        // 말하지만, 그것이 <b>어떻게 보이는지</b>는 그려 봐야만 압니다.
        GameObject camObject = new GameObject("Shot");
        Camera cam = camObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;
        // ⚠ <b>총구가 화면 안에 있어야</b> 합니다. 처음에 몸통을 잡았더니 섬광은
        // 바닥에 번진 빛으로만 보이고 정작 총구는 화면 밖이었습니다. 포드는 축
        // z = 3.8 에 있고 포신이 앞(+Z)으로 뻗으므로 옆에서 그 구간을 잡습니다.
        camObject.transform.position = new Vector3(16f, 5.2f, 4.2f);
        camObject.transform.LookAt(new Vector3(0f, 3.4f, 4.2f));

        GameObject sun = new GameObject("Sun");
        Light key = sun.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 0.35f;
        sun.transform.rotation = Quaternion.Euler(38f, -40f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.10f, 0.12f, 0.16f);
        RenderSettings.ambientEquatorColor = new Color(0.07f, 0.08f, 0.09f);
        RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.05f);

        GameObject mark = new GameObject("Target");
        mark.transform.position = robot.transform.position
                                  + robot.transform.forward * TargetDistance
                                  + Vector3.up * 1.6f;

        foreach (RobotTurret turret in robot.GetComponentsInChildren<RobotTurret>(true))
        {
            turret.target = mark.transform;
        }

    }

    // --- Private Methods : 측정 ---

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        if (!SessionState.GetBool(SessionKey, false)) return;

        SessionState.SetBool(SessionKey, false);

        tracks.Clear();
        report.Length = 0;
        turn = -2;
        settleSpeed = 0f;

        foreach (RobotWeapon weapon in Object.FindObjectsByType<RobotWeapon>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Track track = new Track { weapon = weapon };

            foreach (WeaponRecoil recoil in Gather(weapon))
            {
                track.parts.Add(recoil);
                track.recoil.Add(new float[] { 0f, -1f });
            }

            tracks.Add(track);
        }

        // 몸을 미는 것은 <b>물리 부품이 붙은</b> 리지드바디입니다. 아무거나 잡으면
        // 발이나 다리를 재게 됩니다.
        RobotPhysicsMotor motor = Object.FindAnyObjectByType<RobotPhysicsMotor>();
        body = motor != null ? motor.GetComponent<Rigidbody>() : null;

        shot = Object.FindAnyObjectByType<Camera>();
        threat = Object.FindAnyObjectByType<RobotThreat>(FindObjectsInactive.Include);
        moods.Clear();
        warnMeasured = -1f;

        if (tracks.Count == 0)
        {
            Finish("RobotWeaponCheck: 무장을 하나도 찾지 못했습니다.", 2);
            return;
        }

        Next();
        EditorApplication.update += Tick;
    }

    /// <summary>매 프레임 값을 쌓고, 시간이 다 되면 보고서를 씁니다.</summary>
    private static void Tick()
    {
        if (turn < 0)
        {
            if (body != null)
            {
                float speed = body.linearVelocity.magnitude;
                if (speed > settleSpeed) settleSpeed = speed;
            }
        }
        else if (turn < tracks.Count)
        {
            Sample(tracks[turn]);
        }
        else
        {
            Mood();
            return;
        }

        if (Time.realtimeSinceStartup < endAt) return;

        Next();
    }

    /// <summary>
    /// <b>경계</b>를 잽니다. 건드리기 전에는 쳐다보기만 하고, 건드린 뒤에도
    /// 경고 시간이 지나야 쏘고, 잊으면 다시 내려놓는지를 봅니다.
    ///
    /// 이 셋이 이 기계의 성격 전부입니다. 하나라도 빠지면 스트라이더가 아니라
    /// <b>보자마자 쏘는 포탑</b>이 됩니다.
    /// </summary>
    private static void Mood()
    {
        if (threat == null)
        {
            Write();
            return;
        }

        string now = threat.State.ToString();
        if (moods.Count == 0 || moods[moods.Count - 1] != now) moods.Add(now);

        if (warnMeasured < 0f && threat.State == RobotThreat.Mood.Engage && provokedAt > 0f)
        {
            warnMeasured = Time.realtimeSinceStartup - provokedAt;
        }

        // 앞의 한 토막은 <b>안 건드리고</b> 봅니다. 여기서 겨누면 그것이 결함입니다.
        if (provokedAt <= 0f && Time.realtimeSinceStartup >= moodEnd)
        {
            provokedAt = Time.realtimeSinceStartup;
            threat.Provoke();
            moodEnd = Time.realtimeSinceStartup + threat.warnSeconds + TestForget + MoodSlack;
            return;
        }

        if (Time.realtimeSinceStartup < moodEnd) return;

        Write();
    }

    /// <summary>지금 재고 있는 무장의 값을 한 프레임분 쌓습니다.</summary>
    /// <param name="track">재고 있는 무장</param>
    private static void Sample(Track track)
    {
        {
            RobotWeapon weapon = track.weapon;
            if (weapon == null) return;

            if (weapon.ShotCount > track.shots)
            {
                if (track.firstShot < 0f) track.firstShot = weapon.LastShotTime;
                else
                {
                    float gap = weapon.LastShotTime - track.lastShot;
                    if (gap > 0f && gap < track.minGap) track.minGap = gap;
                }

                track.lastShot = weapon.LastShotTime;
                track.shots = weapon.ShotCount;
            }

            for (int i = 0; i < track.parts.Count; i++)
            {
                WeaponRecoil part = track.parts[i];
                if (part == null) continue;

                float[] slot = track.recoil[i];
                if (part.PeakOffset > slot[0]) slot[0] = part.PeakOffset;

                // 되돌아온 시간은 <b>가장 깊었던 순간부터</b> 5% 아래로 내려올 때까지입니다.
                if (slot[0] > 0f && slot[1] < 0f && part.Offset <= slot[0] * 0.05f)
                {
                    slot[1] = Time.time - weapon.LastShotTime;
                }
            }

            if (weapon.hatch != null && weapon.hatch.Openness > track.hatchOpen)
            {
                track.hatchOpen = weapon.hatch.Openness;
            }

            if (weapon.winch != null)
            {
                if (weapon.winch.SwingAngle > track.swing) track.swing = weapon.winch.SwingAngle;
                track.payout = weapon.winch.Payout;
            }

            if (weapon.flash != null && weapon.flash.PeakSeen > track.flash)
            {
                track.flash = weapon.flash.PeakSeen;
            }

            // ⚠ <b>번쩍이는 동안에만</b> 찍힙니다. 0.04~0.10 초짜리라 한 무장에
            // 한 번만 잡으면 되고, 놓치면 그 무장은 그림이 없습니다.
            if (!track.shot && weapon.flash != null && weapon.flash.Lit) Snap(weapon.name);
            if (weapon.flash != null && weapon.flash.Lit) track.shot = true;

            if (body != null)
            {
                float speed = body.linearVelocity.magnitude;
                if (speed > track.bodySpeed) track.bodySpeed = speed;
            }
        }
    }

    /// <summary>지금 무장을 끄고 다음 무장을 켭니다. 다 돌면 보고서를 씁니다.</summary>
    private static void Next()
    {
        if (turn >= 0 && turn < tracks.Count && tracks[turn].weapon != null)
        {
            tracks[turn].weapon.gameObject.SetActive(false);
        }

        turn++;

        // 첫 단계는 <b>아무 무장도 안 켜고</b> 가라앉힙니다.
        if (turn < 0)
        {
            endAt = Time.realtimeSinceStartup + SettleSeconds;
            return;
        }

        if (turn >= tracks.Count)
        {
            OpenMood();
            return;
        }

        RobotWeapon weapon = tracks[turn].weapon;
        if (weapon == null)
        {
            endAt = Time.realtimeSinceStartup;
            return;
        }

        weapon.gameObject.SetActive(true);

        // 프리팹에서는 <b>꺼진 채로</b> 나옵니다 - 경계가 겨누고 경고 시간이 지난
        // 뒤에 켜는 것이 규약이기 때문입니다. 여기서는 손으로 켭니다.
        weapon.enabled = true;

        // 견인 갈고리는 쏘지 않으므로 줄을 풀어야 잴 것이 생깁니다.
        // ⚠ 재생 모드에 들어온 <b>뒤에</b> 불러야 합니다 - 편집 모드에서 부르면
        // 도메인이 다시 올라가면서 값이 지워집니다.
        if (weapon.winch != null) weapon.winch.Lower();

        endAt = Time.realtimeSinceStartup
                + Mathf.Max(MinSeconds, weapon.interval * IntervalSpan);
    }

    /// <summary>경계 단계를 엽니다. 기본 무장 하나만 켜고 경계에 맡깁니다.</summary>
    private static void OpenMood()
    {
        if (threat == null)
        {
            Write();
            return;
        }

        // 기본 무장 하나만 켭니다. 나머지는 꺼진 채 둡니다.
        foreach (Track track in tracks)
        {
            if (track.weapon == null) continue;

            bool basic = track.weapon.name == "Gun_HeavyCannon";
            track.weapon.gameObject.SetActive(basic);
            track.weapon.enabled = false;
        }

        GameObject mark = GameObject.Find("Target");

        threat.enabled = true;
        threat.authoredTarget = mark != null ? mark.transform : null;
        threat.forgetSeconds = TestForget;

        provokedAt = 0f;
        moodEnd = Time.realtimeSinceStartup + MoodSlack;
    }

    /// <summary>지금 화면을 한 장 남깁니다.</summary>
    /// <param name="name">파일 이름에 쓸 무장 이름</param>
    private static void Snap(string name)
    {
        if (shot == null) return;

        RenderTexture buffer = RenderTexture.GetTemporary(ShotSize, ShotSize, 24);
        RenderTexture keep = shot.targetTexture;

        shot.targetTexture = buffer;
        shot.Render();
        shot.targetTexture = keep;

        RenderTexture active = RenderTexture.active;
        RenderTexture.active = buffer;

        Texture2D image = new Texture2D(ShotSize, ShotSize, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, ShotSize, ShotSize), 0, 0);
        image.Apply();

        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(buffer);

        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"),
                           image.EncodeToPNG());

        Object.DestroyImmediate(image);
    }

    /// <summary>보고서를 만듭니다.</summary>
    private static void Write()
    {
        int bad = 0;

        report.AppendLine("== 스트라이더 무장 실측 ==");
        report.AppendLine("재는 법   : 무장을 하나씩 켜 가며 잽니다");
        report.AppendLine("목표 거리 : " + TargetDistance.ToString("F0") + " m");
        report.AppendLine("가라앉힘  : " + SettleSeconds.ToString("F0") + " 초 · 그동안 최고 "
                          + settleSpeed.ToString("F2") + " m/s");
        report.AppendLine();

        foreach (Track track in tracks)
        {
            RobotWeapon weapon = track.weapon;
            if (weapon == null) continue;

            report.AppendLine("[" + weapon.name + "] " + weapon.fire);
            report.AppendLine("  발수        : " + track.shots);

            if (track.shots >= 2)
            {
                // ⚠ <b>평균은 점사에서 거짓말합니다.</b> 묶음 안의 0.14 초와 묶음
                // 사이의 1.6 초가 섞여, 연장포가 0.383 초마다 쏘는 것처럼 나왔습니다.
                // 연사 속도는 <b>가장 짧았던 간격</b>입니다.
                bool ok = Mathf.Abs(track.minGap - weapon.interval) <= weapon.interval * 0.05f;
                if (!ok) bad++;

                report.AppendLine("  연사 간격   : " + track.minGap.ToString("F3") + " 초"
                                  + "  (적은 값 " + weapon.interval.ToString("F2") + ")"
                                  + (ok ? "" : "   ← 벗어남"));
            }

            for (int i = 0; i < track.parts.Count; i++)
            {
                WeaponRecoil part = track.parts[i];
                if (part == null) continue;

                float[] slot = track.recoil[i];
                float ratio = part.travel > 0f ? slot[0] / part.travel : 0f;
                bool ok = ratio >= 0.9f && ratio <= 1.1f;
                if (!ok) bad++;

                report.AppendLine("  " + part.name);
                report.AppendLine("    밀린 거리 : " + slot[0].ToString("F3") + " m"
                                  + "  (적은 값 " + part.travel.ToString("F2")
                                  + " · 비율 " + ratio.ToString("F2") + ")"
                                  + (ok ? "" : "   ← 벗어남"));
                // 연발 무장은 <b>다음 발이 겹쳐</b> 되돌아온 시간을 잴 수 없습니다.
                // 0.02 초 같은 값이 나오는데, 그것은 스프링이 빠른 것이 아니라
                // 다음 발이 밀어 놓은 자리를 재고 있는 것입니다.
                bool single = weapon.fire == RobotWeapon.Fire.Single;

                report.AppendLine("    되돌아옴  : "
                                  + (!single ? "연발이라 못 잼"
                                     : slot[1] < 0f ? "아직" : slot[1].ToString("F2") + " 초"));
            }

            if (weapon.spinner != null)
            {
                bool ok = weapon.spinner.SpinUpElapsed > 0f
                          && Mathf.Abs(weapon.spinner.SpinUpElapsed - weapon.spinner.spinUp) <= 0.1f;
                if (!ok) bad++;

                report.AppendLine("  회전 가속   : "
                                  + weapon.spinner.SpinUpElapsed.ToString("F2") + " 초"
                                  + "  (적은 값 " + weapon.spinner.spinUp.ToString("F2") + ")"
                                  + (ok ? "" : "   ← 벗어남"));
            }

            if (weapon.hatch != null)
            {
                bool ok = track.hatchOpen >= 0.999f;
                if (!ok) bad++;

                report.AppendLine("  덮개 열림   : " + track.hatchOpen.ToString("F2")
                                  + (ok ? "" : "   ← 다 안 열렸습니다"));
            }

            if (weapon.winch != null)
            {
                bool ok = track.swing <= weapon.winch.maxSwing + 0.5f;
                if (!ok) bad++;

                report.AppendLine("  줄 길이     : " + track.payout.ToString("F2") + " m"
                                  + "  (다 " + weapon.winch.maxPayout.ToString("F2") + ")");
                report.AppendLine("  흔들린 각   : " + track.swing.ToString("F1") + "°"
                                  + "  (한계 " + weapon.winch.maxSwing.ToString("F0") + ")"
                                  + (ok ? "" : "   ← 넘었습니다"));
            }

            // ⚠ 쏘는 것이 <b>화면에 보이는지</b>는 따로 재야 합니다. 포신이 밀리는
            // 것만으로는 발사인지 알 수 없고, 반동이 없는 미사일 랙은 아무 표시도
            // 없습니다. 섬광과 소리가 붙었는지, 그리고 실제로 번쩍였는지 봅니다.
            if (weapon.fire != RobotWeapon.Fire.None)
            {
                bool lit = track.flash > 0f;
                bool heard = weapon.fireClips != null && weapon.fireClips.Length > 0;
                if (!lit) bad++;
                if (!heard) bad++;

                report.AppendLine("  총구 섬광   : "
                                  + (lit ? track.flash.ToString("F0") : "안 번쩍임")
                                  + (lit ? "" : "   ← 벗어남"));
                report.AppendLine("  발사음      : "
                                  + (heard ? weapon.fireClips.Length + "종" : "없음")
                                  + (heard ? "" : "   ← 벗어남"));
            }

            // 몸 속도는 <b>모든 무장에</b> 남깁니다. 밀지 않는 무장에서도 큰 값이
            // 나오면 그것은 포가 아니라 <b>로봇이 넘어지고 있는 것</b>입니다.
            report.AppendLine("  몸 최고속도 : " + track.bodySpeed.ToString("F2") + " m/s"
                              + (weapon.bodyKick > 0f
                                 ? "  (적은 값 " + weapon.bodyKick.ToString("F2") + ")"
                                 : ""));

            if (weapon.bodyKick > 0f)
            {
                float kicked = track.bodySpeed - settleSpeed;
                bool ok = kicked >= weapon.bodyKick * 0.5f
                          && kicked <= weapon.bodyKick * 2f;
                if (!ok) bad++;

                report.AppendLine("    가라앉힌 뒤 늘어난 것 : " + kicked.ToString("F2") + " m/s"
                                  + (ok ? "" : "   ← 벗어남"));
            }

            report.AppendLine();
        }

        if (threat != null)
        {
            report.AppendLine("[경계] " + string.Join(" → ", moods));

            bool watched = moods.Contains("Watch");
            bool engaged = moods.Contains("Engage");
            bool calmed = moods.LastIndexOf("Watch") > moods.IndexOf("Engage") && engaged;

            if (!watched) bad++;
            if (!engaged) bad++;
            if (!calmed) bad++;

            report.AppendLine("  안 건드렸을 때 쳐다보기만 : " + (watched ? "예" : "아니오"));
            report.AppendLine("  건드린 뒤 쏘았는가        : " + (engaged ? "예" : "아니오"));
            report.AppendLine("  잊고 내려놓았는가         : " + (calmed ? "예" : "아니오"));

            if (warnMeasured >= 0f)
            {
                bool ok = Mathf.Abs(warnMeasured - threat.warnSeconds) <= 0.5f;
                if (!ok) bad++;

                report.AppendLine("  경고 시간                 : "
                                  + warnMeasured.ToString("F2") + " 초"
                                  + "  (적은 값 " + threat.warnSeconds.ToString("F2") + ")"
                                  + (ok ? "" : "   ← 벗어남"));
            }

            report.AppendLine();
        }

        report.AppendLine(bad == 0 ? "전부 적어 둔 값 안" : "벗어난 항목 " + bad + "개");

        Finish(report.ToString(), bad == 0 ? 0 : 2);
    }

    /// <summary>이 무장이 미는 조각을 전부 모읍니다.</summary>
    /// <param name="weapon">무장</param>
    /// <returns>미는 조각들</returns>
    private static IEnumerable<WeaponRecoil> Gather(RobotWeapon weapon)
    {
        if (weapon.recoils != null)
        {
            foreach (WeaponRecoil one in weapon.recoils)
            {
                if (one != null) yield return one;
            }
        }

        if (weapon.alsoRecoil != null)
        {
            foreach (WeaponRecoil one in weapon.alsoRecoil)
            {
                if (one != null) yield return one;
            }
        }
    }

    /// <summary>보고서를 남기고 끝냅니다.</summary>
    /// <param name="text">남길 본문</param>
    /// <param name="code">종료 코드</param>
    private static void Finish(string text, int code)
    {
        EditorApplication.update -= Tick;

        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, "weapons.txt"), text);
        Debug.Log(text);

        EditorApplication.isPlaying = false;

        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
