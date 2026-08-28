using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 소변 줄기가 <b>떨어지는 지점의 연출</b>을 씬에 세웁니다.
///
/// <b>무엇이 없었는가.</b> 줄기 파티클은 월드 충돌이 켜져 있어 땅에 닿기는 하는데
/// (CollisionModule.enabled 1, type World), 서브이미터가 꺼져 있고
/// <c>OnParticleCollision</c> 을 받는 코드도 없어서 <b>닿는 순간 아무 일도 일어나지
/// 않았습니다.</b> 줄기가 땅에 스며들 듯 사라질 뿐이라 어디에 떨어지는지가 안 읽혔습니다.
///
/// <b>왜 서브이미터인가.</b> 충돌 지점을 C# 으로 받아 이펙트를 꺼내 쓸 수도 있지만,
/// 그러면 입자마다 콜백이 돌고 풀에서 꺼내는 비용이 붙습니다. 이 프로젝트는 렌더 스레드
/// 드로우 제출이 이미 병목이라 그 길은 비쌉니다. 서브이미터는 파티클 시스템 안에서
/// 끝나므로 드로우가 한 벌 더 늘 뿐입니다.
///
/// <b>튀는 물방울은 줄기와 같은 재질을 씁니다.</b> 다른 재질을 쓰면 같은 액체가 닿는
/// 순간 다른 물건이 됩니다 — 색도 필터도 같아야 한 줄기로 읽힙니다.
///
/// 쓰는 법:
///   Unity.exe -batchmode -projectPath . -executeMethod UrineStagingSetup.Run
/// </summary>
public static class UrineStagingSetup
{
    private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";
    private const string StreamName = "UrineStream";
    private const string SplashName = "UrineSplash";

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ParticleSystem stream = FindByName(StreamName);
        if (stream == null) { Debug.Log("URINE " + StreamName + " 을 찾지 못함"); EditorApplication.Exit(1); return; }

        ParticleSystemRenderer streamRenderer = stream.GetComponent<ParticleSystemRenderer>();
        Material liquid = streamRenderer != null ? streamRenderer.sharedMaterial : null;
        Debug.Log("URINE 줄기 재질=" + (liquid != null ? liquid.name : "(없음)"));

        ParticleSystem splash = BuildSplash(stream, liquid);
        WireSubEmitter(stream, splash);

        EditorSceneManager.MarkSceneDirty(stream.gameObject.scene);
        EditorSceneManager.SaveScene(stream.gameObject.scene);

        Debug.Log("URINE 착지 연출 배선 완료");
        EditorApplication.Exit(0);
    }

    /// <summary>충돌 지점에서 한 번 튀는 물방울입니다.</summary>
    /// <summary>
    /// 물이 부딪히는 면입니다.
    ///
    /// <b>젖은 자국이 붙는 면과 같아야 합니다.</b> 물이 젖힐 수 있는 면 = 물이 부딪히는 면이니까요.
    /// 달랐을 때 벽에는 자국만 남고 튀김은 없는 상태가 됐습니다. 그래서 자국 쪽 값
    /// (<see cref="UrineSplatSetup.SurfaceMask"/>)을 한 곳에서 가져옵니다 — 숫자를 두 군데
    /// 적어 두면 다음에 또 어긋납니다.
    ///
    /// 적(레이어 7)만 더 봅니다. 걸어 다니는 것에는 <b>자국을 안 붙이지만</b>(판이 제자리에
    /// 남아 몸에서 떨어져 나갑니다) 물방울이 튀는 것은 맞기 때문입니다.
    /// </summary>
    private const int CollisionMask = UrineSplatSetup.SurfaceMask | (1 << 7);

    private static ParticleSystem BuildSplash(ParticleSystem stream, Material liquid)
    {
        Transform existing = stream.transform.Find(SplashName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(SplashName);
        if (existing == null) go.transform.SetParent(stream.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        if (ps == null) ps = go.AddComponent<ParticleSystem>();

        // 서브이미터는 부모가 불러 줄 때만 나와야 합니다. 스스로 돌면 발밑에서 계속 튑니다.
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // <b>짧게.</b> 0.30~0.62 였을 때, 오줌이 끊긴 뒤에도 물방울이 눈에 띄게 남았습니다.
        // 꼬리의 나머지 절반은 아직 날아가던 물이 계속 착지하는 몫입니다(그건 물리적으로 맞습니다).
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.9f);
        // 줄기 알갱이(0.045)보다 잘게. 튀는 물은 원래 더 잘게 부서집니다.
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);
        main.gravityModifier = 1.4f;
        main.maxParticles = 120;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.878f, 0.78f, 0.40f, 1f),
            new Color(0.98f, 0.92f, 0.62f, 1f));

        // <b>서브이미터는 이 모듈의 버스트로 뿜습니다.</b> 여기를 꺼 두면 부모가 충돌을
        // 알려도 뿜을 양이 정의돼 있지 않아 <b>한 방울도 안 나옵니다.</b> 실제로 그렇게
        // 만들었다가 착지 지점이 계속 비어 있었습니다.
        // rateOverTime 은 0 이라 스스로 흘리지는 않고, 불릴 때 버스트만 터집니다.
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5, 9, 1, 0.01f) });

        // 위로 반구. 땅에 부딪혀 튀는 방향입니다.
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f;
        shape.radius = 0.02f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        // 튄 물방울도 땅에 다시 닿으면 멈춥니다. 공중에 뜬 채 사라지면 가짜로 보입니다.
        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.dampen = 0.6f;
        collision.bounce = 0.15f;
        collision.lifetimeLoss = 0.25f;

        // <b>줄기와 같은 면만 봅니다.</b> 그냥 두면 이 값이 Everything 이라, 줄기는 그대로
        // 통과하는 벽에 물방울만 부딪혀 튕깁니다 — 물이 벽을 뚫고 지나갔는데 그 벽에서
        // 물방울이 튀는 앞뒤 안 맞는 그림이 나옵니다.
        collision.collidesWith = CollisionMask;


        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        if (r != null && liquid != null)
        {
            r.sharedMaterial = liquid;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = 0;
        }

        Debug.Log("URINE 튀는 물방울 준비 (" + (existing == null ? "새로 만듦" : "기존 갱신") + ")");
        return ps;
    }

    /// <summary>줄기의 충돌에 물방울을 물립니다.</summary>
    private static void WireSubEmitter(ParticleSystem stream, ParticleSystem splash)
    {
        ParticleSystem.SubEmittersModule sub = stream.subEmitters;
        sub.enabled = true;

        // 이미 물려 있으면 지우고 다시 답니다. 두 번 돌리면 물방울이 두 배로 튑니다.
        for (int i = sub.subEmittersCount - 1; i >= 0; i--) sub.RemoveSubEmitter(i);

        sub.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision,
                          ParticleSystemSubEmitterProperties.InheritNothing);

        // 충돌한 입자마다 다 튀면 땅이 물바다가 됩니다. 셋에 하나 정도만.
        sub.SetSubEmitterEmitProbability(0, 0.34f);

        // 부모 쪽 충돌도 다시 확인합니다. 꺼져 있으면 서브이미터가 영원히 안 불립니다.
        ParticleSystem.CollisionModule c = stream.collision;
        c.enabled = true;
        c.type = ParticleSystemCollisionType.World;
        c.mode = ParticleSystemCollisionMode.Collision3D;
        c.sendCollisionMessages = false;   // C# 콜백은 쓰지 않습니다.

        // <b>이 두 줄이 없어서 물줄기가 벽을 통과했습니다.</b>
        //
        // 씬에 저장돼 있던 마스크는 2944(Enemy·Car·Prop·Ground)라 <b>레이어 0(Default)이
        // 빠져 있었습니다.</b> 그런데 이 게임의 건물 벽·실내 바닥·바위가 전부 레이어 0 입니다.
        // 그래서 벽에 누면 젖은 자국은 벽에 즉시 찍히는데 물은 벽을 뚫고 지나가,
        // 훨씬 뒤의 지면에서 뒤늦게 튀거나 아예 안 튀었습니다.
        // 사용자가 본 "시작 후 얼마간 지나야 나온다" 의 가장 큰 몫이 이것입니다.
        c.collidesWith = CollisionMask;

        // <b>움직이는 콜라이더는 뺍니다 — 안 그러면 물줄기가 통째로 사라집니다.</b>
        //
        // 위에서 레이어 0 을 넣자마자 물줄기가 안 보이게 됐습니다. 이 게임의 벽이 레이어 0
        // 이라 넣은 것인데, <b>플레이어의 CharacterController 도 레이어 0</b> 이고 노즐은
        // 그 캡슐 안쪽(반지름 0.32m, 노즐은 축에서 0.22m)에 있습니다. 착지 소멸까지 켜 두었으니
        // 입자가 태어나자마자 제 몸에 부딪혀 죽었습니다.
        //
        // 실제 씬에서 재 보니 180개를 뿜어 <b>3개</b>만 살아남았고, 이 줄을 넣으면 51개입니다.
        // (격리된 시험 리그로는 재현되지 않았습니다 — 진짜 씬에서 재야 보였습니다.)
        //
        // <b>대가:</b> 움직이는 것(차·적)에는 물방울이 안 튑니다. 파티클 충돌은 레이어
        // 마스크밖에 없어서 "레이어 0 중 플레이어만" 을 골라낼 방법이 없습니다.
        // 그것까지 살리려면 플레이어를 전용 레이어로 옮겨야 하는데, 레이어 0 을 보는
        // 다른 시스템들을 함께 살펴야 하는 별개의 일입니다.
        c.enableDynamicColliders = false;

        // <b>닿으면 죽습니다.</b> 예전에는 dampen 1 로 멈추기만 하고 수명(2~3초)을 다 살아서,
        // 착지 지점에 알갱이가 카펫처럼 쌓인 채 매 프레임 다시 충돌해 서브이미터를 계속
        // 터뜨렸습니다. 오줌이 끊긴 뒤에도 물방울이 몇 초씩 보글거린 이유입니다.
        // 1 이면 첫 접촉에서 소멸하므로 입자 하나가 정확히 한 번만 튑니다.
        c.lifetimeLoss = 1f;

        Debug.Log("URINE 서브이미터 배선: 충돌, 확률 0.34, 마스크 " + CollisionMask);
    }

    private static ParticleSystem FindByName(string name)
    {
        foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (ps.name == name) return ps;
        return null;
    }
}
