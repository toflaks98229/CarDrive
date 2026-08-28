using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// 전역 젖음 지도가 <b>실제로 칠해지고 실제로 마르는지</b> 텍스처를 읽어 확인합니다.
    ///
    /// <b>왜 읽어서 보는가.</b> 컴퓨트 셰이더는 조용히 실패합니다 — 커널 이름이 틀려도,
    /// 디스패치 그룹 수가 0 이어도, 버퍼 stride 가 어긋나 값이 밀려도 예외가 안 납니다.
    /// 화면에 아무것도 안 나오는 것으로만 알 수 있는데, 배치모드에는 화면이 없습니다.
    /// 그래서 지도를 CPU 로 되읽어 텍셀 값을 직접 셉니다.
    ///
    /// <b>왜 PlayMode 인가.</b> 컴퓨트 디스패치와 GPU 되읽기가 필요합니다.
    /// 그래픽 장치가 있어야 하므로 -nographics 로는 못 돌립니다.
    /// </summary>
    public class SplatManagerTests
    {
        private GameObject host;
        private SplatManager manager;

        /// <summary>시험용 지도가 덮는 사각형입니다. 원점에 100m 정사각.</summary>
        private static readonly Vector2 Origin = new Vector2(0f, 0f);
        private static readonly Vector2 Size = new Vector2(100f, 100f);

        [SetUp]
        public void SetUp()
        {
            GameContext.Clear();

            // <b>꺼 둔 채로 값을 넣고 켭니다.</b> AddComponent 는 그 자리에서 Awake 를 돌리므로,
            // 붙인 뒤에 설정하면 이미 만들어진 지도에는 반영되지 않습니다.
            host = new GameObject("SplatHost");
            host.SetActive(false);

            manager = host.AddComponent<SplatManager>();
            Configure(manager);

            host.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            GameContext.Clear();
        }

        /// <summary>직렬화 필드를 시험용 값으로 맞춥니다.</summary>
        private static void Configure(SplatManager m)
        {
            SerializedLike(m, "coverage", 0);              // Fixed
            SerializedLike(m, "fixedOrigin", Origin);
            SerializedLike(m, "fixedSize", Size);
            SerializedLike(m, "resolution", 256);
            SerializedLike(m, "dryDuration", 1f);
            SerializedLike(m, "fadeInterval", 0.02f);
        }

        /// <summary>
        /// private 직렬화 필드를 리플렉션으로 세웁니다.
        ///
        /// 시험을 위해 필드를 public 으로 여는 것보다 낫습니다 — 그러면 시험 편의를 위해
        /// 게임 코드의 경계가 넓어지고, 다음 사람이 그것을 정식 API 로 오해합니다.
        /// </summary>
        private static void SerializedLike(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(f, "필드를 못 찾음: " + field);
            f.SetValue(target, value);
        }

        /// <summary>지도를 CPU 로 되읽어 가장 진한 값을 냅니다.</summary>
        private static float PeakWetness()
        {
            Texture map = Shader.GetGlobalTexture("_GlobalSplatMap");
            if (map == null) return -1f;

            RenderTexture rt = map as RenderTexture;
            if (rt == null) return -1f;

            Texture2D read = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            read.Apply();
            RenderTexture.active = prev;

            float peak = 0f;
            Color[] px = read.GetPixels();
            for (int i = 0; i < px.Length; i++) if (px[i].r > peak) peak = px[i].r;

            Object.DestroyImmediate(read);
            return peak;
        }

        /// <summary>젖은(0.05 넘는) 텍셀 수를 셉니다.</summary>
        private static int WetTexels()
        {
            Texture map = Shader.GetGlobalTexture("_GlobalSplatMap");
            RenderTexture rt = map as RenderTexture;
            if (rt == null) return -1;

            Texture2D read = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            read.Apply();
            RenderTexture.active = prev;

            int n = 0;
            Color[] px = read.GetPixels();
            for (int i = 0; i < px.Length; i++) if (px[i].r > 0.05f) n++;

            Object.DestroyImmediate(read);
            return n;
        }

        /// <summary>
        /// 브러시 반경이 <b>미터로</b> 먹히는지 봅니다.
        ///
        /// 처음에는 반경을 UV 로 넘겼는데, 창 128m / 2048 텍셀에서 7cm 는 텍셀 1.1개라
        /// 아무리 오래 눠도 <b>점 하나만 젖었습니다.</b> 사용자가 "아주 적은 부분만 젖는다" 고
        /// 보고한 것이 이것입니다. 넓이를 재면 눈으로 보지 않고도 잡힙니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 반경이_미터로_먹힌다()
        {
            yield return null;
            if (!manager.IsReady) yield break;

            // 시험 지도는 100m 를 256 텍셀로 덮으므로 2.56 텍셀/m.
            // 반경 5m 면 지름 10m = 25.6 텍셀, 넓이는 대략 파이 x 12.8² ≒ 515 텍셀.
            manager.Paint(new Vector3(50f, 0f, 50f), 5f, 1f);
            yield return null;

            int wet = WetTexels();
            Debug.Log("SPLATMAP 반경 5m -> 젖은 텍셀 " + wet + " 개");

            // 감쇠가 반경의 45% 부터 시작하므로 온전한 원보다 작습니다. 넉넉히 잡아 둡니다.
            Assert.Greater(wet, 200, "반경 5m 를 칠했는데 " + wet + " 텍셀뿐입니다. " +
                                     "반경이 미터가 아니라 UV 로 먹히고 있습니다.");
            Assert.Less(wet, 900, "반경 5m 인데 " + wet + " 텍셀이나 젖었습니다. 너무 넓습니다.");
        }

        /// <summary>반경을 키우면 젖는 넓이도 함께 커져야 합니다.</summary>
        [UnityTest]
        public IEnumerator 반경을_키우면_넓이도_커진다()
        {
            yield return null;
            if (!manager.IsReady) yield break;

            manager.Paint(new Vector3(25f, 0f, 25f), 2f, 1f);
            yield return null;
            int small = WetTexels();

            manager.Paint(new Vector3(75f, 0f, 75f), 8f, 1f);
            yield return null;
            int both = WetTexels();

            int big = both - small;
            Debug.Log("SPLATMAP 2m -> " + small + " 텍셀, 8m -> " + big + " 텍셀");

            // 넓이는 반경의 제곱에 비례하므로 4배 반경이면 대략 16배여야 합니다.
            Assert.Greater(big, small * 8, "반경을 4배로 했는데 넓이가 " +
                                           ((float)big / Mathf.Max(small, 1)).ToString("0.0") +
                                           "배밖에 안 늘었습니다.");
        }

        [UnityTest]
        public IEnumerator 칠하면_지도가_젖는다()
        {
            yield return null;

            if (!manager.IsReady)
            {
                Debug.Log("SPLATMAP 이 기기에서 지도를 못 씁니다. 건너뜁니다.");
                yield break;
            }

            Assert.AreEqual(0f, PeakWetness(), 1e-3f, "칠하기 전에 이미 젖어 있습니다.");

            // 지도 한가운데를 흠뻑.
            manager.Paint(new Vector3(50f, 0f, 50f), 3f, 1f);

            // LateUpdate 에서 한 번에 보냅니다. 한 프레임 기다려야 합니다.
            yield return null;

            Assert.Greater(PeakWetness(), 0.5f,
                "칠했는데 지도가 안 젖었습니다. 컴퓨트가 조용히 실패했습니다.");
        }

        [UnityTest]
        public IEnumerator 지도_밖은_칠해지지_않는다()
        {
            yield return null;
            if (!manager.IsReady) yield break;

            manager.Paint(new Vector3(-50f, 0f, -50f), 3f, 1f);
            yield return null;

            Assert.AreEqual(0f, PeakWetness(), 1e-3f,
                "지도 밖을 칠했는데 안쪽이 젖었습니다. 가장자리로 번지고 있습니다.");
        }

        [UnityTest]
        public IEnumerator 시간이_지나면_마른다()
        {
            yield return null;
            if (!manager.IsReady) yield break;

            manager.Paint(new Vector3(50f, 0f, 50f), 3f, 1f);
            yield return null;

            float wet = PeakWetness();
            Assert.Greater(wet, 0.5f, "칠해지지 않아 마름을 시험할 수 없습니다.");

            // dryDuration 이 1초이므로 그보다 넉넉히 기다립니다.
            float waited = 0f;
            while (waited < 2.5f && PeakWetness() > 0.01f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.Less(PeakWetness(), 0.05f,
                "다 말라야 할 시간이 지났는데 아직 젖어 있습니다.");
        }

        [UnityTest]
        public IEnumerator GameContext_에서_찾을_수_있다()
        {
            yield return null;

            Assert.AreSame(manager, GameContext.Get<SplatManager>(),
                           "SplatManager 가 GameContext 에 등록되지 않았습니다.");
        }
    }
}
