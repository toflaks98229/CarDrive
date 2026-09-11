using System.Collections;
using NUnit.Framework;
using UnityEngine;
using System.Text;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarDrive.Tests
{
    /// <summary>
    /// 물줄기 입자가 <b>플레이어 자신에게 부딪혀 태어나자마자 죽는지</b> 재 봅니다.
    ///
    /// <b>왜 필요한가.</b> 충돌 마스크에 레이어 0(Default)을 넣었더니 물줄기가 통째로
    /// 사라졌습니다. 이 게임의 벽이 레이어 0 이라 넣은 것인데, <b>플레이어의
    /// CharacterController 도 레이어 0</b> 이고 노즐은 그 캡슐 <b>안쪽</b>에 있습니다
    /// (반지름 0.32m, 노즐은 축에서 0.22m). 게다가 착지 소멸(lifetimeLoss 1)까지 켜 두었으니,
    /// 자기 몸에 부딪히는 순간 입자가 죽습니다.
    ///
    /// <b>왜 추측이 아니라 재는가.</b> 레이캐스트는 콜라이더 <b>안</b>에서 출발하면 히트를
    /// 보고하지 않습니다. 파티클 충돌도 같은지는 문서로 단정할 수 없어서, 눈으로 볼 수 없는
    /// 배치모드에서는 재는 것이 유일하게 믿을 수 있는 길입니다.
    ///
    /// <b>왜 PlayMode 인가.</b> 에디트 모드의 ParticleSystem.Simulate 는 월드 충돌을
    /// 돌리지 않습니다. 물리 질의가 진행되지 않기 때문입니다.
    /// </summary>
    public class UrineStreamCollisionTests
    {
        private GameObject body;
        private GameObject ground;
        private GameObject nozzle;
        private ParticleSystem stream;

        /// <summary>씬의 CharacterController 와 같은 치수입니다.</summary>
        private const float CapsuleRadius = 0.32f;
        private const float CapsuleHeight = 1.8f;

        /// <summary>씬의 노즐 자리입니다. 캡슐 축에서 0.22m — 반지름 안쪽입니다.</summary>
        private static readonly Vector3 NozzleLocal = new Vector3(0f, 0.95f, 0.22f);

        [SetUp]
        public void SetUp()
        {
            // 플레이어 몸통을 흉내 냅니다. 레이어 0(Default), 캡슐 콜라이더.
            // <b>CapsuleCollider 가 아니라 CharacterController 입니다.</b> 씬의 플레이어가
            // 그것을 쓰고, 파티클 충돌이 둘을 같게 다루는지는 확인된 바 없습니다.
            // 처음에 CapsuleCollider 로 재고 "괜찮다" 고 판정했는데, 그건 다른 물건이었습니다.
            body = new GameObject("FakePlayer");
            body.layer = 0;
            CharacterController cc = body.AddComponent<CharacterController>();
            cc.radius = CapsuleRadius;
            cc.height = CapsuleHeight;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.08f;

            nozzle = new GameObject("Nozzle");
            nozzle.transform.SetParent(body.transform, false);
            nozzle.transform.localPosition = NozzleLocal;

            stream = nozzle.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = stream.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 3f;
            main.startSpeed = 15f;
            main.gravityModifier = 1.1f;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = stream.emission;
            emission.enabled = false;

            // 실제 씬처럼 땅도 깔아 둡니다. 땅이 없으면 입자가 영원히 날아가
            // "살아남았다" 는 판정이 아무것도 증명하지 못합니다.
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = 11;
            ground.transform.localScale = new Vector3(12f, 1f, 12f);

            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (body != null) Object.DestroyImmediate(body);
            if (ground != null) Object.DestroyImmediate(ground);
        }

        /// <summary>충돌 모듈을 씬과 같게 맞춥니다.</summary>
        private void Collide(int mask, float lifetimeLoss)
        {
            ParticleSystem.CollisionModule c = stream.collision;
            c.enabled = true;
            c.type = ParticleSystemCollisionType.World;
            c.mode = ParticleSystemCollisionMode.Collision3D;
            c.quality = ParticleSystemCollisionQuality.High;
            c.dampen = 1f;
            c.bounce = 0f;
            c.lifetimeLoss = lifetimeLoss;
            c.collidesWith = mask;
        }

        /// <summary>입자를 뿜고 몇 프레임 돌린 뒤 살아남은 수를 셉니다.</summary>
        private IEnumerator EmitAndCount(int count, int frames)
        {
            stream.Emit(count);
            for (int i = 0; i < frames; i++) yield return null;
            Debug.Log("STREAMTEST 뿜은 " + count + " 개 중 " + stream.particleCount + " 개 생존");
        }

        /// <summary>
        /// <b>진짜 씬에서</b> 물줄기가 <b>자기 몸에</b> 부딪히는지 봅니다.
        ///
        /// 격리 리그에서는 두 번 다 재현이 안 됐습니다(캡슐로도, CharacterController 로도).
        /// 그러면 리그가 놓친 것이 씬에 있다는 뜻이고, 남은 길은 씬을 통째로 띄워
        /// 실제 콜라이더·실제 레이어 배치에서 재는 것뿐입니다.
        ///
        /// ⚠ <b>살아남은 수를 세지 않습니다.</b> 처음에는 "90 개를 뿜어 30 개 넘게 남는가"
        /// 로 재었는데, 그것은 충돌이 아니라 <b>그날의 프레임 시간</b>을 재는 수였습니다.
        /// 입자는 15 m/s 로 날고 부딪히면 즉사하므로, 한 프레임이 길수록 같은 거리를
        /// <b>더 적은 프레임</b>에 지나갑니다. 간격을 고정해 재 보면 그대로 나옵니다 —
        /// 8.3 ms 에 42 개, 20.1 ms 에 21 개, 33.3 ms 에 9 개. 씬에 물건을 몇 개 더
        /// 놓았을 뿐인데 이 검사가 빨간불이 된 이유가 그것입니다.
        ///
        /// 그래서 <b>무엇에 부딪혔는지 이름을 받아</b> 봅니다. 원래 잡으려던 고장이
        /// "자기 몸에 부딪혀 죽는다" 였으니, 부딪힌 것의 이름이 곧 답입니다.
        /// 마을 집에 부딪히는 것은 고장이 아니라 <b>벽</b>입니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 실제_씬에서_물줄기가_자기_몸에_안_부딪힌다()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++) yield return null;

            ParticleSystem real = null;
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ps.name == "UrineStream") { real = ps; break; }

            Assert.IsNotNull(real, "씬에서 UrineStream 을 못 찾음");

            // <b>먼저 살아 있는지 봅니다.</b> 이 게임은 차에서 시작하므로 도보 리그가
            // 꺼져 있을 수 있고, 꺼진 오브젝트의 파티클은 Emit 해도 돌지 않습니다.
            // 이걸 안 보고 "0개 생존" 을 원인으로 읽으면 엉뚱한 곳을 고치게 됩니다.
            StringBuilder chain = new StringBuilder();
            for (Transform t = real.transform; t != null; t = t.parent)
                chain.Insert(0, "/" + t.name + (t.gameObject.activeSelf ? "" : "(꺼짐)"));
            Debug.Log("STREAMTEST 부모 사슬: " + chain);

            // 켜고 재생시킵니다. 여기서 재려는 것은 충돌이지 모드 전환이 아닙니다.
            Transform rig = real.transform;
            for (Transform t = real.transform; t != null; t = t.parent)
            {
                t.gameObject.SetActive(true);
                if (t.GetComponent<CharacterController>() != null) rig = t;
            }

            real.Play();
            yield return null;

            ParticleSystem.CollisionModule c = real.collision;
            Debug.Log(string.Format(
                "STREAMTEST 씬 충돌: 켜짐={0} 마스크={1} 수명손실={2:0.00} 감쇠={3:0.00}",
                c.enabled, c.collidesWith.value, c.lifetimeLossMultiplier, c.dampenMultiplier));

            // ⚠ <b>부딪힌 것의 이름을 받으려면 켜야 합니다.</b> 씬에 구워진 설정은
            // 이것이 꺼져 있습니다 — 게임에서는 아무도 안 듣기 때문입니다.
            c.sendCollisionMessages = true;

            Ear ear = real.gameObject.AddComponent<Ear>();

            // <b>충돌 설정은 씬에 구워진 것 그대로 씁니다.</b> 여기서 값을 덮어쓰면 실제로
            // 배포되는 설정이 아니라 테스트가 만든 설정을 검사하게 되어, 정작 고장은 못 잡습니다.
            ParticleSystem.MainModule main = real.main;
            main.startSpeed = 15f;
            main.startLifetime = 3f;

            for (int f = 0; f < 30; f++)
            {
                real.Emit(3);
                yield return null;
            }

            Assert.Greater(ear.Hits.Count, 0,
                "아무것에도 안 부딪혔습니다. 충돌이 꺼졌거나 입자가 안 나간 것이므로, " +
                "이 검사는 아무것도 못 봅니다");

            foreach (System.Collections.Generic.KeyValuePair<string, int> hit in ear.Hits)
            {
                Debug.Log("STREAMTEST 부딪힌 것 — " + hit.Key + " · " + hit.Value + " 번");
            }

            // 진짜 고장은 이것 하나입니다 — <b>자기 몸</b>이나 <b>자기 차</b>에 부딪히는가.
            foreach (Transform hit in ear.What)
            {
                Assert.IsFalse(Under(hit, rig),
                    "물줄기가 <b>자기 몸에</b> 부딪힙니다 — " + hit.name
                    + ". 충돌 마스크에 플레이어의 레이어가 들어갔거나 몸통 콜라이더가 켜져 있습니다");

                Assert.IsNull(hit.GetComponentInParent<CarDrive.Gameplay.Vehicle>(),
                    "물줄기가 <b>차에</b> 부딪힙니다 — " + hit.name
                    + ". 도보 리그가 차 안에서 켜졌거나 차가 사람 위에 세워져 있습니다");
            }
        }

        // --- Private Methods ---

        /// <summary>그것이 이 뿌리 아래에 있는가.</summary>
        private static bool Under(Transform what, Transform root)
        {
            for (Transform t = what; t != null; t = t.parent)
            {
                if (t == root) return true;
            }

            return false;
        }

        /// <summary>부딪힌 것의 이름을 받아 적습니다.</summary>
        private class Ear : MonoBehaviour
        {
            public readonly System.Collections.Generic.Dictionary<string, int> Hits =
                new System.Collections.Generic.Dictionary<string, int>();

            public readonly System.Collections.Generic.List<Transform> What =
                new System.Collections.Generic.List<Transform>();

            void OnParticleCollision(GameObject other)
            {
                string key = other.name + " (레이어 " + other.layer + ")";

                Hits.TryGetValue(key, out int had);
                Hits[key] = had + 1;

                if (had == 0) What.Add(other.transform);
            }
        }
    }
}
