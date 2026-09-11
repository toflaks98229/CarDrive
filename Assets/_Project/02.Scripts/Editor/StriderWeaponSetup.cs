using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

/// <summary>
/// 스트라이더의 무장 여섯에 <b>움직이는 부품을 답니다.</b>
///
/// <b>왜 손으로 달면 안 되는가.</b> <see cref="WalkerModelSetup"/> 은 프리팹의 메시
/// 자식을 <b>전부 지우고 다시 답니다.</b> 인스펙터에서 붙인 부품은 다음 반입 때
/// 사라집니다. 그래서 배선은 반드시 스크립트에 있어야 합니다.
///
/// <b>왜 <see cref="WalkerModelSetup"/> 안이 아닌가.</b> 그쪽은 <b>리그</b>를 세우는
/// 일이고 기계 넷이 함께 씁니다. 무장 값은 스트라이더 하나의 것이라, 넣으면
/// 저장소에서 가장 큰 파일이 남의 사정으로 더 커집니다.
///
/// <b>값은 보이는 것으로 적습니다.</b> 밀리는 거리(m) · 간격(초) · 각(도).
/// 근거는 <c>09.Docs/스트라이더_무장_작동_기획.md</c> 에 있습니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod WalkerModelSetup.RunStrider
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod StriderWeaponSetup.Run
/// </code>
/// ⚠ <b>순서가 있습니다.</b> 리그를 먼저 세우고 그 위에 답니다.
/// </summary>
public static class StriderWeaponSetup
{
    // --- Constants ---

    private const string PrefabPath = "Assets/_Project/05.Prefabs/Robot/WalkerRobot_Strider.prefab";

    /// <summary>선이 맞을 수 있는 레이어입니다. Ground · Prop · 적입니다.</summary>
    private const int HitLayers = ~0;

    /// <summary>발사음이 있는 곳입니다.</summary>
    private const string SoundDir = "Assets/_Project/06.Sound/Weapon/";

    /// <summary>착탄음이 있는 곳입니다. 금속에 맞는 소리라 이미 있는 것을 씁니다.</summary>
    private const string ImpactDir = "Assets/_Project/06.Sound/Impact/";

    /// <summary>
    /// 맞은 자리에 띄울 파티클입니다.
    ///
    /// 프로젝트에 이미 들어 있는 Cartoon FX Remaster 의 것을 씁니다 — 착탄 자국을
    /// 새로 만들 이유가 없고, 이 팩은 이미 다른 연출에도 쓰이고 있습니다.
    /// </summary>
    private const string ImpactFx =
        "Packages/com.toflaks.vendor.cartoon-fx-remaster/Cartoon FX Remaster/" +
        "CFXR Prefabs/Impacts/CFXR2 Ground Hit.prefab";

    // --- Private Types ---

    /// <summary>밀 조각 하나의 설정입니다.</summary>
    private class Kick
    {
        /// <summary>조각 노드의 이름입니다.</summary>
        public string node;

        /// <summary>가장 많이 밀렸을 때의 거리(m)입니다.</summary>
        public float travel;

        /// <summary>되돌아오는 성격입니다. 진동수와 감쇠비입니다.</summary>
        public float frequency;
        public float damping;
    }

    /// <summary>무장 하나의 설정입니다.</summary>
    private class Loadout
    {
        public string node;
        public RobotWeapon.Fire fire;
        public float interval;
        public int rounds;
        public float cooldown;
        public float range;
        public float damage;
        public float aim;
        public float podKick;
        public float bodyKick;

        /// <summary>번갈아 밀 조각들입니다.</summary>
        public Kick[] recoils = Array.Empty<Kick>();

        /// <summary>같이 밀 조각들입니다.</summary>
        public Kick[] also = Array.Empty<Kick>();

        /// <summary>도는 조각의 노드 이름입니다. 없으면 비웁니다.</summary>
        public string rotor;

        /// <summary>덮개 조각의 노드 이름입니다. 없으면 비웁니다.</summary>
        public string doors;

        /// <summary>날아가는 탄을 쓸 것인가. 비우면 선으로 쏩니다.</summary>
        public bool flies;

        /// <summary>탄의 초속(m)입니다.</summary>
        public float shotSpeed;

        /// <summary>윈치의 드럼·갈고리 노드 이름입니다. 없으면 비웁니다.</summary>
        public string drum;
        public string hook;

        /// <summary>발사음 파일 이름들입니다. 여럿이면 번갈아 납니다.</summary>
        public string[] sounds = Array.Empty<string>();

        /// <summary>총구 섬광의 세기 · 거리 · 길이입니다. 세기가 0 이면 안 답니다.</summary>
        public float flashPeak;
        public float flashReach;
        public float flashTime;
    }

    /// <summary>
    /// 무장 여섯의 값입니다. <b>여기가 유일한 출처</b>입니다.
    ///
    /// 성격이 여기서 갈립니다 — 중포는 오래 기다렸다 한 발이 무겁고, 연장포는
    /// 두 줄이 번갈아 떨며, 회전포는 돌기까지 기다려야 하고, 미사일 랙은
    /// 덮개가 예고이며, 공성포는 기계 자신을 밀어냅니다.
    /// </summary>
    private static Loadout[] Table()
    {
        return new[]
        {
            new Loadout
            {
                node = "Gun_HeavyCannon", fire = RobotWeapon.Fire.Single,
                sounds = new[] { "cannon_01.ogg", "cannon_03.ogg" },
                flashPeak = 46f, flashReach = 18f, flashTime = 0.07f,
                interval = 3.2f, range = 70f, damage = 26f, aim = 2.5f,
                podKick = 4.5f,
                recoils = new[]
                {
                    new Kick { node = "Gun_HeavyCannon_Barrel",
                               travel = 0.38f, frequency = 1.2f, damping = 0.5f },
                },
            },

            new Loadout
            {
                node = "Gun_Autocannon", fire = RobotWeapon.Fire.Burst,
                sounds = new[] { "shot_01.ogg", "shot_02.ogg" },
                flashPeak = 22f, flashReach = 11f, flashTime = 0.04f,
                interval = 0.14f, rounds = 5, cooldown = 1.6f,
                range = 55f, damage = 7f, aim = 3.5f, podKick = 0.8f,
                recoils = new[]
                {
                    new Kick { node = "Gun_Autocannon_BarrelL",
                               travel = 0.11f, frequency = 3.6f, damping = 0.6f },
                    new Kick { node = "Gun_Autocannon_BarrelR",
                               travel = 0.11f, frequency = 3.6f, damping = 0.6f },
                },
            },

            new Loadout
            {
                node = "Gun_Rotary", fire = RobotWeapon.Fire.Continuous,
                sounds = new[] { "shot_03.ogg" },
                flashPeak = 16f, flashReach = 9f, flashTime = 0.035f,
                interval = 0.07f, range = 45f, damage = 3.5f, aim = 4.5f,
                podKick = 0.35f, rotor = "Gun_Rotary_Rotor",
            },

            new Loadout
            {
                node = "Gun_MissileRack", fire = RobotWeapon.Fire.Salvo,
                sounds = new[] { "bang_06.ogg", "bang_08.ogg" },
                flashPeak = 26f, flashReach = 13f, flashTime = 0.09f,
                interval = 0.22f, rounds = 6, cooldown = 8f,
                range = 90f, damage = 18f, aim = 6f, podKick = 0f,
                doors = "Gun_MissileRack_Doors",

                // 미사일만 날아갑니다. 90 m 를 55 로 가면 1.6 초 —
                // 그 사이가 차를 몰고 벗어날 시간입니다.
                flies = true, shotSpeed = 55f,
            },

            new Loadout
            {
                node = "Gun_Siege", fire = RobotWeapon.Fire.Single,
                sounds = new[] { "cannon_02.ogg" },
                flashPeak = 64f, flashReach = 24f, flashTime = 0.10f,
                interval = 5.5f, range = 40f, damage = 55f, aim = 3f,
                podKick = 9f, bodyKick = 1.2f,
                recoils = new[]
                {
                    new Kick { node = "Gun_Siege_Barrel",
                               travel = 0.52f, frequency = 0.85f, damping = 0.42f },
                },
                also = new[]
                {
                    // 실린더는 포신의 <b>절반만</b> 접힙니다. 같이 밀면 실린더가
                    // 아니라 그냥 막대입니다.
                    new Kick { node = "Gun_Siege_Cylinders",
                               travel = 0.26f, frequency = 0.85f, damping = 0.42f },
                },
            },

            new Loadout
            {
                node = "Gun_TowHook", fire = RobotWeapon.Fire.None,
                interval = 1f, range = 10f, damage = 0f, aim = 90f, podKick = 0f,
                drum = "Gun_TowHook_Drum", hook = "Gun_TowHook_Hook",
            },
        };
    }

    // --- Public Methods ---

    /// <summary>프리팹을 열어 무장 부품을 새로 답니다.</summary>
    [MenuItem("CarDrive/Robot/스트라이더 무장 배선")]
    public static void Run()
    {
        int errors = 0;
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            RobotTurret turret = FindGunTurret(root);
            if (turret == null) throw new Exception("Gun 포탑을 찾지 못했습니다");

            Dictionary<string, Transform> nodes = Index(root.transform);

            Strip(root);

            foreach (Loadout one in Table())
            {
                if (!nodes.TryGetValue(one.node, out Transform host))
                {
                    Debug.LogError("StriderWeaponSetup: " + one.node + " 노드가 없습니다");
                    errors++;
                    continue;
                }

                errors += Wire(one, host, turret, nodes);
            }

            errors += Watch(root, turret, nodes);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("StriderWeaponSetup: 완료 — 실패 " + errors + "건");

        if (Application.isBatchMode) EditorApplication.Exit(errors == 0 ? 0 : 2);
    }

    // --- Private Methods ---

    /// <summary>
    /// <b>무엇을 볼지</b> 정하는 부품을 답니다.
    ///
    /// 이것이 없으면 포탑에 겨눌 곳을 주는 코드가 <b>아무 데도 없습니다.</b>
    /// 조준도 무장도 다 만들어 놓고 게임에서는 한 번도 돌지 않습니다.
    /// </summary>
    /// <param name="root">로봇 뿌리</param>
    /// <param name="gun">포 포탑</param>
    /// <param name="nodes">이름으로 찾는 마디들</param>
    /// <returns>실패한 개수</returns>
    private static int Watch(GameObject root, RobotTurret gun,
                             Dictionary<string, Transform> nodes)
    {
        RobotThreat threat = root.GetComponent<RobotThreat>();
        if (threat == null) threat = root.AddComponent<RobotThreat>();

        threat.gun = gun;
        threat.head = null;

        foreach (RobotTurret one in root.GetComponentsInChildren<RobotTurret>(true))
        {
            if (one != gun) threat.head = one;
        }

        // <b>무장 여섯을 다 넘깁니다.</b> 꺼진 다섯은 어차피 안 쏘지만, 무장을
        // 갈아 끼웠을 때 배선을 다시 하지 않아도 되게 해 둡니다.
        threat.weapons = root.GetComponentsInChildren<RobotWeapon>(true);

        // 스트라이더는 <b>교통</b>입니다. 건드려야만 겨눕니다.
        threat.provokedOnly = true;
        threat.watchRadius = 55f;
        threat.warnSeconds = 2f;
        threat.forgetSeconds = 14f;

        if (threat.head == null)
        {
            Debug.LogError("StriderWeaponSetup: 쳐다볼 마디(센서 캡)를 찾지 못했습니다");
            return 1;
        }

        return 0;
    }

    /// <summary>무장 하나에 부품을 답니다.</summary>
    /// <param name="one">무장 설정</param>
    /// <param name="host">무장 노드</param>
    /// <param name="turret">이 무장이 달린 포탑</param>
    /// <param name="nodes">이름으로 찾는 마디들</param>
    /// <returns>실패한 개수</returns>
    private static int Wire(Loadout one, Transform host, RobotTurret turret,
                            Dictionary<string, Transform> nodes)
    {
        int errors = 0;

        RobotWeapon weapon = host.gameObject.AddComponent<RobotWeapon>();
        weapon.turret = turret;
        weapon.fire = one.fire;
        weapon.interval = one.interval;
        weapon.roundsPerBurst = Mathf.Max(1, one.rounds);
        weapon.burstCooldown = one.cooldown;
        weapon.range = one.range;
        weapon.damage = one.damage;
        weapon.aimTolerance = one.aim;
        weapon.podKick = one.podKick;
        weapon.bodyKick = one.bodyKick;
        weapon.hitMask = HitLayers;

        // ⚠ <b>꺼 둔 채로 답니다.</b> <see cref="RobotThreat"/> 이 겨누고 경고 시간이
        // 지난 뒤에 켭니다. 켠 채로 두면 경고 없이 첫 발이 나갑니다.
        weapon.enabled = false;

        // 총구는 <b>이 무장 밑</b>에 있습니다. 무장을 갈아 끼우면 총구도 같이 갑니다.
        weapon.muzzle = Muzzle(host);
        if (weapon.muzzle == null)
        {
            Debug.LogError("StriderWeaponSetup: " + one.node + " 에 총구가 없습니다");
            errors++;
        }

        weapon.projectile = one.flies ? Missile(one.shotSpeed, ref errors) : null;
        weapon.fireClips = Clips(SoundDir, one.sounds, ref errors);
        weapon.impactClips = Clips(ImpactDir, new[]
        {
            "metal_hit_01.ogg", "metal_hit_02.ogg", "metal_hit_03.ogg",
        }, ref errors);
        weapon.impactEffect = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactFx) is GameObject fx
            ? fx.GetComponent<ParticleSystem>()
            : null;

        if (one.flashPeak > 0f && weapon.muzzle != null)
        {
            weapon.flash = Flash(weapon.muzzle, one);
        }

        weapon.recoils = Recoils(one.recoils, nodes, ref errors);
        weapon.alsoRecoil = Recoils(one.also, nodes, ref errors);

        if (!string.IsNullOrEmpty(one.rotor))
        {
            Transform part = Node(one.rotor, nodes, ref errors);
            if (part != null)
            {
                WeaponSpinner spinner = part.gameObject.AddComponent<WeaponSpinner>();
                spinner.part = part;
                weapon.spinner = spinner;
            }
        }

        if (!string.IsNullOrEmpty(one.doors))
        {
            Transform part = Node(one.doors, nodes, ref errors);
            if (part != null)
            {
                WeaponHatch gate = part.gameObject.AddComponent<WeaponHatch>();
                gate.part = part;
                weapon.hatch = gate;
            }
        }

        if (!string.IsNullOrEmpty(one.hook))
        {
            Transform hook = Node(one.hook, nodes, ref errors);
            Transform drum = Node(one.drum, nodes, ref errors);

            if (hook != null)
            {
                WeaponWinch winch = host.gameObject.AddComponent<WeaponWinch>();
                winch.hook = hook;
                winch.drum = drum;
                weapon.winch = winch;
            }
        }

        return errors;
    }

    /// <summary>
    /// 총구에 <b>번쩍이는 빛</b>을 답니다.
    ///
    /// 빛으로 하는 이유는 이 게임의 후처리가 밝기를 여섯 단계로 계단화하고 채도를
    /// 깎기 때문입니다 — <b>색은 화면까지 못 가고 휘도만 살아남습니다.</b>
    /// 밤에는 이 빛이 포신과 발밑을 같이 비추어 어디서 쐈는지를 말합니다.
    /// </summary>
    /// <param name="muzzle">총구</param>
    /// <param name="one">무장 설정</param>
    /// <returns>단 섬광 부품</returns>
    /// <summary>
    /// 날아가는 탄의 프리팹입니다. 없으면 만듭니다.
    ///
    /// <b>왜 도구가 만드는가.</b> 손으로 만든 프리팹은 다음에 이 도구를 돌릴 때
    /// 무엇이 들어 있었는지 알 수 없습니다. 여기에 적혀 있으면 판단이 남습니다.
    ///
    /// 생김새는 <b>길쭉한 상자 하나와 자국</b>입니다. 초속 55 m 면 한 프레임에
    /// 0.9 m 를 가므로, 몸통만으로는 <b>점선으로 끊겨 보입니다.</b>
    /// 뒤에 남는 자국이 있어야 하나로 이어집니다.
    /// </summary>
    /// <param name="speed">초속(m)</param>
    /// <param name="errors">못 만들었으면 늘립니다</param>
    private static GameObject Missile(float speed, ref int errors)
    {
        const string path = "Assets/_Project/05.Prefabs/Robot/RobotMissile.prefab";

        GameObject made = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (made != null)
        {
            WeaponMissile had = made.GetComponent<WeaponMissile>();
            if (had != null && Mathf.Abs(had.speed - speed) > 0.01f)
            {
                had.speed = speed;
                EditorUtility.SetDirty(made);
                AssetDatabase.SaveAssets();
            }

            return made;
        }

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "RobotMissile";

        // 콜라이더는 뗍니다. 맞는 판정은 스스로 선을 쏴서 합니다 —
        // ⚠ 콜라이더를 달아 두면 <b>자기 레이캐스트에 자기가 걸립니다.</b>
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

        body.transform.localScale = new Vector3(0.16f, 0.16f, 0.62f);

        Material steel = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/_Project/04.Art/00.Materials/RobotSteel.mat");
        if (steel != null) body.GetComponent<MeshRenderer>().sharedMaterial = steel;

        TrailRenderer tail = body.AddComponent<TrailRenderer>();
        tail.time = 0.35f;
        tail.startWidth = 0.22f;
        tail.endWidth = 0.0f;
        tail.minVertexDistance = 0.4f;
        tail.numCapVertices = 2;
        tail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tail.receiveShadows = false;
        tail.sharedMaterial = TrailMaterial(ref errors);

        WeaponMissile flying = body.AddComponent<WeaponMissile>();
        flying.speed = speed;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(body, path);
        UnityEngine.Object.DestroyImmediate(body);

        if (saved == null)
        {
            Debug.LogError("STRIDER 탄 프리팹을 만들지 못했습니다 — " + path);
            errors++;
            return null;
        }

        Debug.Log("STRIDER 탄 프리팹을 만들었습니다 — " + path);
        return saved;
    }

    /// <summary>
    /// 자국에 쓸 재질입니다. 없으면 만듭니다.
    ///
    /// ⚠ <b>재질을 안 주면 자홍색으로 나옵니다.</b> 자국은 조명을 받을 이유가 없으므로
    /// 언릿이고, 뒤로 갈수록 사라져야 하니 반투명입니다.
    /// </summary>
    private static Material TrailMaterial(ref int errors)
    {
        const string path = "Assets/_Project/04.Art/00.Materials/MissileTrail.mat";

        Material had = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (had != null) return had;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");

        if (unlit == null)
        {
            Debug.LogError("STRIDER URP Unlit 셰이더를 찾지 못했습니다");
            errors++;
            return null;
        }

        Material made = new Material(unlit);
        made.name = "MissileTrail";

        // 반투명 · 더하기. 밤에 지나가는 불꽃으로 읽혀야 합니다.
        made.SetFloat("_Surface", 1f);
        made.SetFloat("_Blend", 1f);
        made.SetFloat("_ZWrite", 0f);
        made.renderQueue = 3000;
        made.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        made.SetColor("_BaseColor", new Color(1f, 0.62f, 0.28f, 1f));

        AssetDatabase.CreateAsset(made, path);
        AssetDatabase.SaveAssets();

        Debug.Log("STRIDER 자국 재질을 만들었습니다 — " + path);
        return made;
    }

    private static WeaponFlash Flash(Transform muzzle, Loadout one)
    {
        GameObject go = new GameObject("Flash");
        go.transform.SetParent(muzzle, false);
        go.layer = muzzle.gameObject.layer;

        Light glow = go.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.92f, 0.76f);
        glow.shadows = LightShadows.None;
        glow.enabled = false;
        glow.intensity = 0f;

        WeaponFlash flash = go.AddComponent<WeaponFlash>();
        flash.glow = glow;
        flash.peak = one.flashPeak;
        flash.reach = one.flashReach;
        flash.duration = one.flashTime;

        return flash;
    }

    /// <summary>소리 파일들을 불러옵니다. 없는 것은 세어 둡니다.</summary>
    /// <param name="folder">찾을 폴더</param>
    /// <param name="names">파일 이름들</param>
    /// <param name="errors">실패 개수</param>
    /// <returns>불러온 클립들</returns>
    private static AudioClip[] Clips(string folder, string[] names, ref int errors)
    {
        if (names == null || names.Length == 0) return Array.Empty<AudioClip>();

        List<AudioClip> made = new List<AudioClip>();

        foreach (string name in names)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(folder + name);

            if (clip == null)
            {
                Debug.LogError("StriderWeaponSetup: 소리가 없습니다 — " + folder + name);
                errors++;
                continue;
            }

            made.Add(clip);
        }

        return made.ToArray();
    }

    /// <summary>밀 조각들에 <see cref="WeaponRecoil"/> 을 답니다.</summary>
    /// <param name="list">설정</param>
    /// <param name="nodes">이름으로 찾는 마디들</param>
    /// <param name="errors">실패 개수</param>
    /// <returns>단 부품들</returns>
    private static WeaponRecoil[] Recoils(Kick[] list, Dictionary<string, Transform> nodes,
                                          ref int errors)
    {
        if (list == null || list.Length == 0) return Array.Empty<WeaponRecoil>();

        List<WeaponRecoil> made = new List<WeaponRecoil>();

        foreach (Kick kick in list)
        {
            Transform part = Node(kick.node, nodes, ref errors);
            if (part == null) continue;

            WeaponRecoil recoil = part.gameObject.AddComponent<WeaponRecoil>();
            recoil.part = part;
            recoil.travel = kick.travel;
            recoil.spring = new SecondOrderSettings(kick.frequency, kick.damping, 0f);
            made.Add(recoil);
        }

        return made.ToArray();
    }

    /// <summary>이름으로 마디를 찾습니다. 없으면 세어 둡니다.</summary>
    /// <param name="name">찾을 이름</param>
    /// <param name="nodes">이름으로 찾는 마디들</param>
    /// <param name="errors">실패 개수</param>
    /// <returns>찾은 마디. 없으면 null</returns>
    private static Transform Node(string name, Dictionary<string, Transform> nodes,
                                  ref int errors)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (nodes.TryGetValue(name, out Transform found)) return found;

        Debug.LogError("StriderWeaponSetup: " + name + " 노드가 없습니다");
        errors++;
        return null;
    }

    /// <summary>무장 밑의 총구를 찾습니다.</summary>
    /// <param name="host">무장 노드</param>
    /// <returns>총구. 없으면 null</returns>
    private static Transform Muzzle(Transform host)
    {
        foreach (Transform child in host)
        {
            if (child.name.StartsWith("Muzzle")) return child;
        }

        return null;
    }

    /// <summary>앞서 단 무장 부품을 전부 걷어냅니다. 이 스크립트는 멱등해야 합니다.</summary>
    /// <param name="root">로봇 뿌리</param>
    private static void Strip(GameObject root)
    {
        foreach (RobotWeapon stale in root.GetComponentsInChildren<RobotWeapon>(true))
        {
            UnityEngine.Object.DestroyImmediate(stale);
        }

        foreach (WeaponRecoil stale in root.GetComponentsInChildren<WeaponRecoil>(true))
        {
            UnityEngine.Object.DestroyImmediate(stale);
        }

        foreach (WeaponSpinner stale in root.GetComponentsInChildren<WeaponSpinner>(true))
        {
            UnityEngine.Object.DestroyImmediate(stale);
        }

        foreach (WeaponHatch stale in root.GetComponentsInChildren<WeaponHatch>(true))
        {
            UnityEngine.Object.DestroyImmediate(stale);
        }

        foreach (WeaponWinch stale in root.GetComponentsInChildren<WeaponWinch>(true))
        {
            UnityEngine.Object.DestroyImmediate(stale);
        }

        // 섬광은 <b>오브젝트째</b> 지웁니다. 부품만 지우면 빈 Flash 오브젝트가
        // 돌릴 때마다 하나씩 쌓입니다.
        foreach (WeaponFlash stale in root.GetComponentsInChildren<WeaponFlash>(true))
        {
            if (stale == null) continue;
            UnityEngine.Object.DestroyImmediate(stale.gameObject);
        }
    }

    /// <summary>이름 -> 마디 표를 만듭니다. 같은 이름이 둘이면 앞의 것을 씁니다.</summary>
    /// <param name="root">로봇 뿌리</param>
    /// <returns>이름으로 찾는 마디들</returns>
    private static Dictionary<string, Transform> Index(Transform root)
    {
        Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();

        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
        {
            if (!nodes.ContainsKey(node.name)) nodes[node.name] = node;
        }

        return nodes;
    }

    /// <summary>포 포탑을 찾습니다. 센서 캡 포탑이 아니라 <see cref="RobotTurret.muzzle"/> 이 있는 쪽입니다.</summary>
    /// <param name="root">로봇 뿌리</param>
    /// <returns>포 포탑. 없으면 null</returns>
    private static RobotTurret FindGunTurret(GameObject root)
    {
        foreach (RobotTurret turret in root.GetComponentsInChildren<RobotTurret>(true))
        {
            if (turret.name.Contains("Gun")) return turret;
        }

        return null;
    }
}
