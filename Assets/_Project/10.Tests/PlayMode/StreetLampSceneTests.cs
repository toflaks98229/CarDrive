using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CarDrive.Gameplay;
using CarDrive.Systems;

namespace CarDrive.Tests
{
    /// <summary>
    /// <b>진짜 씬에서</b> 밤이 되면 등이 켜지는지 봅니다.
    ///
    /// <b>왜 씬을 통째로 여는가.</b> 등이 켜지는 데는 시계와 주입이 다 필요합니다 —
    /// 격리 리그에서는 <see cref="StreetLampNightTests"/> 가 이미 보고 있고, 여기서
    /// 보는 것은 <b>세계에 실제로 놓인 등이 시간을 따라 켜지는가</b>입니다.
    ///
    /// 그리고 한 장 찍어 둡니다. 등이 켜진 화면은 재생 중에만 볼 수 있어서,
    /// 룩 캡처 도구로는 확인할 수 없습니다.
    /// </summary>
    public class StreetLampSceneTests
    {
        [UnityTest]
        public IEnumerator 밤이_되면_길의_등이_켜진다()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            for (int i = 0; i < 12; i++) yield return null;

            TimeSystem time = Object.FindAnyObjectByType<TimeSystem>(FindObjectsInactive.Include);
            Assert.That(time, Is.Not.Null, "시계가 없습니다");

            // 08시에 시작하므로 18시간을 흘려 02시로 보냅니다.
            time.AdvanceMinutes(18f * 60f);

            for (int i = 0; i < 10; i++) yield return null;

            int burning = 0;
            StreetLamp any = null;

            for (int i = 0; i < StreetLamp.All.Count; i++)
            {
                StreetLamp lamp = StreetLamp.All[i];
                if (lamp == null) continue;

                if (!lamp.Burning) continue;

                burning++;

                // ⚠ <b>플레이어 가까운 등을 고릅니다.</b> 먼 길의 등을 찍으면 그곳의
                // 지형 타일이 아직 안 깔려 있어 땅이 무엇인지 알 수 없습니다.
                if (any == null) { any = lamp; continue; }

                Camera eye = Camera.main;
                if (eye == null) continue;

                float now = Vector3.Distance(lamp.transform.position, eye.transform.position);
                float before = Vector3.Distance(any.transform.position, eye.transform.position);

                if (now < before) any = lamp;
            }

            Debug.Log("LAMPSHOT 등 " + StreetLamp.All.Count + " 개 중 " + burning + " 개가 탑니다");

            Assert.That(burning, Is.GreaterThan(0), "밤인데 켜진 등이 하나도 없습니다");

            if (any == null) yield break;

            // 세기를 바꿔 가며 몇 장 찍습니다. 등이 땅에 웅덩이를 만드는지 봅니다.
            float[] tries = { 18f };


            Camera main = Camera.main;
            if (main == null) yield break;

            Camera shot = new GameObject("LampShotCamera").AddComponent<Camera>();
            shot.CopyFrom(main);
            shot.rect = new Rect(0f, 0f, 1f, 1f);

            // ⚠ 후처리 설정은 복사하지 않습니다. 검사 어셈블리는 URP 를 참조하지
            // 않으므로 그 타입을 여기서 쓸 수 없습니다. 등이 켜졌는지 보는 데는
            // 후처리가 필요 없습니다.

            // 켜진 등을 비스듬히 내려다봅니다.
            Vector3 at = any.transform.position;
            shot.transform.position = at + new Vector3(14f, 9f, -14f);
            shot.transform.LookAt(at + Vector3.up * 3f);

            RenderTexture target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            Texture2D png = new Texture2D(960, 540, TextureFormat.RGB24, false);

            Directory.CreateDirectory("Logs/Lamp");

            foreach (float strength in tries)
            {
                for (int i = 0; i < StreetLamp.All.Count; i++)
                {
                    StreetLamp lamp = StreetLamp.All[i];
                    if (lamp != null && lamp.bulb != null) lamp.bulb.intensity = strength;
                }

                shot.targetTexture = target;

                for (int i = 0; i < 3; i++)
                {
                    shot.Render();
                    yield return null;
                }

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                png.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                png.Apply();
                RenderTexture.active = previous;
                shot.targetTexture = null;

                File.WriteAllBytes("Logs/Lamp/night_" + strength.ToString("F0") + ".png",
                                   png.EncodeToPNG());

                // 등 아래 땅이 얼마나 밝아졌는지 잽니다.
                float sum = 0f;
                int n = 0;

                for (int y = 300; y < 420; y++)
                {
                    for (int x = 400; x < 560; x++)
                    {
                        Color c = png.GetPixel(x, png.height - 1 - y);
                        sum += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                        n++;
                    }
                }

                Debug.Log("LAMPSHOT 세기 " + strength.ToString("F0") + " → 등 아래 땅 "
                          + (sum / n).ToString("F4"));
            }

            shot.targetTexture = null;
            Object.Destroy(png);
            target.Release();
            Object.Destroy(target);
            Object.Destroy(shot.gameObject);

            Debug.Log("LAMPSHOT 찍었습니다 — Logs/Lamp/night.png · 등 " + at.ToString("F0"));
        }

        /// <summary>
        /// 세이브에 등이 <b>실제로 담기는지</b> 봅니다.
        ///
        /// ⚠ <b>참여자는 씬에 없습니다.</b> <c>GameBootstrap</c> 이 기동할 때 만듭니다.
        /// 그 배선이 빠지면 저장은 성공하는데 등만 조용히 빠집니다 — 알아채기 가장
        /// 어려운 종류의 고장이라, 격리 검사 말고 <b>진짜 씬에서</b> 한 번 더 봅니다.
        /// </summary>
        [UnityTest]
        public IEnumerator 세이브에_길의_등이_담긴다()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            for (int i = 0; i < 12; i++) yield return null;

            LampSaveParticipant keeper = Object.FindAnyObjectByType<LampSaveParticipant>(
                FindObjectsInactive.Include);

            Assert.That(keeper, Is.Not.Null,
                        "등 세이브 참여자가 없습니다 — GameBootstrap 의 배선을 보십시오");

            SaveData data = new SaveData();
            keeper.CaptureInto(data);

            Debug.Log("LAMPSAVE 담긴 등 " + data.lamps.Count + " 개 · 세계의 등 "
                      + StreetLamp.All.Count + " 개");

            Assert.That(data.lamps.Count, Is.EqualTo(StreetLamp.All.Count),
                        "세계의 등과 담긴 등의 수가 다릅니다");

            Assert.That(data.lamps.Count, Is.GreaterThan(0), "담긴 등이 하나도 없습니다");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene sample = SceneManager.GetSceneByName("SampleScene");
            if (!sample.isLoaded) yield break;

            Scene empty = SceneManager.CreateScene("검사가 쓰고 비워 둔 씬");
            SceneManager.SetActiveScene(empty);

            yield return SceneManager.UnloadSceneAsync(sample);
        }
    }
}
