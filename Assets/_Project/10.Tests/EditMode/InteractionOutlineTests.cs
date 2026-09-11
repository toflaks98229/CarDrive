using NUnit.Framework;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.Tests
{
    /// <summary>
    /// <see cref="InteractionOutline"/> 이 <b>대상에게만</b> 선을 두르고 제때 걷는지 봅니다.
    ///
    /// <b>왜 조준을 안 세우는가.</b> 무엇을 칠할지 고르는 일(조준·거리·<c>CanInteract</c>)과
    /// 실제로 칠하는 일은 <see cref="InteractionOutline.Tick"/> 에서 갈라 두었습니다.
    /// 여기서는 <b>칠하는 쪽</b>만 봅니다. 조준을 세우려면 카메라·콜라이더·레이어까지
    /// 다 있어야 하는데, 그것은 이 검사가 지키려는 것과 상관이 없습니다.
    ///
    /// ⚠ 재질은 <b>외곽선 프로퍼티가 있는 셰이더</b>로 만들어야 합니다.
    /// 없으면 컴포넌트가 그 렌더러를 일부러 건너뛰므로(SRP 배칭을 지키려고)
    /// 아무 일도 안 일어나고, 그걸 "고장" 으로 읽게 됩니다.
    /// </summary>
    public class InteractionOutlineTests
    {
        private const string ShaderName = "CarDrive/Toon Lit";
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");

        private GameObject host;
        private GameObject target;
        private Renderer renderer;
        private InteractionOutline outline;
        private Material material;

        [SetUp]
        public void SetUp()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) Assert.Ignore("셰이더를 찾지 못했습니다: " + ShaderName);

            material = new Material(shader);

            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer = target.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            host = new GameObject("outline");
            outline = host.AddComponent<InteractionOutline>();
            outline.width = 0.01f;
            outline.fadeSeconds = 0.1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (target != null) Object.DestroyImmediate(target);
            if (material != null) Object.DestroyImmediate(material);
        }

        /// <summary>지금 렌더러에 들어가 있는 두께입니다.</summary>
        private float Width()
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetFloat(WidthId);
        }

        [Test]
        public void 대상을_가리키면_선이_차오른다()
        {
            Transform t = target.transform;

            Assert.That(Width(), Is.EqualTo(0f).Within(1e-5f), "시작할 때는 선이 없어야 합니다");

            outline.Tick(t, 0.05f);
            float half = Width();
            Assert.That(half, Is.GreaterThan(0f), "한 프레임 뒤에는 선이 그어지기 시작해야 합니다");
            Assert.That(half, Is.LessThan(outline.width), "한 번에 다 차오르면 경계에서 깜빡입니다");

            // 페이드 시간을 넘기면 목표 두께에 닿습니다.
            for (int i = 0; i < 10; i++) outline.Tick(t, 0.05f);

            Assert.That(Width(), Is.EqualTo(outline.width).Within(1e-5f));
        }

        [Test]
        public void 대상에서_눈을_떼면_선이_걷힌다()
        {
            Transform t = target.transform;
            for (int i = 0; i < 10; i++) outline.Tick(t, 0.05f);
            Assert.That(Width(), Is.EqualTo(outline.width).Within(1e-5f));

            outline.Tick(null, 0.05f);
            Assert.That(Width(), Is.LessThan(outline.width), "떼는 즉시 줄기 시작해야 합니다");

            for (int i = 0; i < 10; i++) outline.Tick(null, 0.05f);

            Assert.That(Width(), Is.EqualTo(0f).Within(1e-5f), "다 걷히면 정확히 0 이어야 합니다");
        }

        [Test]
        public void 대상이_바뀌면_이전_것은_꺼진다()
        {
            GameObject other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Renderer otherRenderer = other.GetComponent<Renderer>();
            otherRenderer.sharedMaterial = material;

            try
            {
                for (int i = 0; i < 10; i++) outline.Tick(target.transform, 0.05f);
                Assert.That(Width(), Is.EqualTo(outline.width).Within(1e-5f));

                // 눈길을 옮깁니다. 이전 것은 걷히고 새 것이 차올라야 합니다.
                for (int i = 0; i < 10; i++) outline.Tick(other.transform, 0.05f);

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                otherRenderer.GetPropertyBlock(block);

                Assert.That(block.GetFloat(WidthId), Is.EqualTo(outline.width).Within(1e-5f),
                            "새 대상에 선이 그어져야 합니다");
                Assert.That(Width(), Is.EqualTo(0f).Within(1e-5f),
                            "이전 대상의 선은 걷혀야 합니다");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void 렌더러가_없는_대상은_부모에_선을_두른다()
        {
            // 차 문이 이 모양입니다 — 콜라이더만 있는 빈 자식에 컴포넌트가 붙어 있습니다.
            GameObject marker = new GameObject("InteractionCollider");
            marker.transform.SetParent(target.transform, false);

            try
            {
                for (int i = 0; i < 10; i++) outline.Tick(marker.transform, 0.05f);

                Assert.That(Width(), Is.EqualTo(outline.width).Within(1e-5f),
                            "자기 렌더러가 없으면 부모의 렌더러에 그어야 합니다");
            }
            finally
            {
                Object.DestroyImmediate(marker);
            }
        }

        [Test]
        public void 외곽선이_없는_재질은_건드리지_않는다()
        {
            // ⚠ 프로퍼티 블록을 붙이는 것만으로 그 렌더러가 SRP 배칭에서 빠집니다.
            // 그래서 쓸 수 없는 재질에는 아예 손대지 않아야 합니다.
            Shader plain = Shader.Find("Unlit/Color");
            if (plain == null) Assert.Ignore("Unlit/Color 를 찾지 못했습니다");

            Material bare = new Material(plain);
            renderer.sharedMaterial = bare;

            try
            {
                for (int i = 0; i < 10; i++) outline.Tick(target.transform, 0.05f);

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.isEmpty, Is.True, "손대지 않았어야 합니다");
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }
    }
}
