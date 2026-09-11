using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CarDrive.Diagnostics
{
    /// <summary>
    /// <b>스탠드얼론 빌드에서</b> 프레임이 어디에 묶여 있는지 잽니다.
    ///
    /// 에디터 프로파일은 <c>EditorLoop</c> 가 지배해서 정상 상태의 병목을 못 봅니다.
    /// 그리고 이 프로젝트에서 지금까지 "드로우"라고 세어 온 값은 <b>모델</b>입니다 -
    /// 렌더러 x 서브메시 x 패스를 손으로 곱한 것이고, 지형·나무·풀·UI 는 아예
    /// 빠져 있습니다. 여기서 재는 것은 엔진이 실제로 내보낸 수입니다.
    ///
    /// <b>무엇으로 CPU/GPU 바운드를 가르는가.</b> 마커 눈대중이 아니라
    /// <see cref="FrameTimingManager"/> 입니다 - 메인 스레드·렌더 스레드·GPU 시간을
    /// 한 프레임에서 함께 내주므로 셋을 비교하면 됩니다. 마커는 그래픽스 잡이
    /// 켜지면 제출이 렌더 스레드 밖으로 옮겨가고, GPU 가 밀리면 제출 마커 안에서
    /// 드라이버가 막혀 CPU 비용처럼 보이는 함정이 있습니다.
    ///
    /// <b>어떤 카운터가 있는지도 함께 적습니다.</b> 이름을 추측해서 물으면 조용히
    /// 0 이 나오고, 그 0 을 결과로 착각하게 됩니다(에디터에서 UnityStats 로 이미
    /// 한 번 겪었습니다). 그래서 첫 구간에서 <b>쓸 수 있는 카운터 이름을 전부</b>
    /// 파일에 남깁니다.
    ///
    /// <b>후처리가 얼마나 먹는지</b>도 여기서 잽니다. <c>-megaprofile-post</c> 를 주면
    /// 자리마다 <b>같은 장면을 두 번</b> 잽니다 — 팔레트 기능을 켠 채로, 그리고 끈 채로.
    /// 그 차이가 곧 후처리 값입니다.
    ///
    /// ⚠ <b>빌드를 두 번 하지 않습니다.</b> 두 빌드를 비교하면 셰이더 캐시·창 크기·
    /// 드라이버 상태가 달라 차이에 잡음이 섞입니다. 한 실행 안에서 껐다 켜면 그
    /// 모든 것이 같습니다.
    ///
    /// <code>
    /// CarDrive.exe -megaprofile -megaprofile-post -megaprofile-out C:\out.json
    /// </code>
    /// </summary>
    public static class MegaProfileProbe
    {
        private const string Flag = "-megaprofile";
        private const string OutFlag = "-megaprofile-out";
        private const string DeckFlag = "-megaprofile-deck";
        private const string PostFlag = "-megaprofile-post";

        /// <summary>렌더러에 붙어 있는 팔레트 기능의 이름입니다.</summary>
        private const string PaletteFeature = "CarDrive Palette";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains(Flag)) return;

            GameObject host = new GameObject("MegaProfileProbe");
            UnityEngine.Object.DontDestroyOnLoad(host);

            Runner runner = host.AddComponent<Runner>();
            runner.Output = Argument(args, OutFlag) ?? "megaprofile.json";
            runner.DeckTop = float.TryParse(Argument(args, DeckFlag),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float deck) ? deck : 35.2f;
            runner.PostAb = args.Contains(PostFlag);
        }

        private static string Argument(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        /// <summary>재는 자리 하나입니다.</summary>
        private struct Spot
        {
            public string Name;
            public Vector3 At;
            public bool Move;

            /// <summary>
            /// 재는 동안 <b>달릴지</b>. 정지 상태로 재면 지형 스트리밍과 나무 갱신이
            /// 놀고 있어 <b>있지도 않은 가벼운 프레임</b>을 재게 됩니다. 이 게임의
            /// 앞선 측정(2026-08-25)이 주행 중이었으므로 비교하려면 달려야 합니다.
            /// </summary>
            public bool Drive;
        }

        private sealed class Runner : MonoBehaviour
        {
            public string Output;
            public float DeckTop;
            public bool PostAb;

            private Rigidbody driving;
            private Vector3 heading;

            /// <summary>한 자리에서 모을 프레임 수. 첫 프레임들은 아직 안 익습니다.</summary>
            private const int Settle = 90;
            private const int Sample = 150;

            /// <summary>물리 단계마다 속도를 다시 밀어 넣습니다. 차의 제어기가
            /// 무엇을 하든 <b>일정한 속도로</b> 지형을 지나가게 하려는 것입니다.</summary>
            private void FixedUpdate()
            {
                if (driving == null) return;

                driving.linearVelocity = heading * 22f + Vector3.up * driving.linearVelocity.y;
            }

            private IEnumerator Start()
            {
                // 세계가 스트리밍되기를 기다립니다. 타일이 덜 들어온 채로 재면
                // <b>있지도 않은 가벼운 프레임</b>을 재게 됩니다.
                yield return new WaitForSeconds(4f);

                StringBuilder report = new StringBuilder();
                // 카운터 이름은 <b>한 번 찍어서 알아냈습니다.</b> 매번 남기면
                // 이름 400 개가 결과를 덮어 정작 숫자가 안 보입니다.
                report.Append("{\n  \"spots\": [\n");

                Transform spine = GameObject.Find("Megastructure")?.transform;
                Transform player = GameObject.FindWithTag("Player")?.transform;

                List<Spot> spots = new List<Spot>
                {
                    new Spot { Name = "출발 지점 · 정지", Move = false },
                    new Spot { Name = "출발 지점 · 주행", Move = false, Drive = true },
                };

                if (spine != null)
                {
                    Vector3 along = spine.forward;

                    spots.Add(new Spot
                    {
                        Name = "데크 위 · 정지",
                        At = spine.position + Vector3.up * (DeckTop + 1.2f),
                        Move = true,
                    });

                    spots.Add(new Spot
                    {
                        Name = "데크 위 · 주행",
                        At = spine.position + Vector3.up * (DeckTop + 1.2f) - along * 300f,
                        Move = true,
                        Drive = true,
                    });

                    spots.Add(new Spot
                    {
                        Name = "데크 밑 · 정지",
                        At = spine.position + Vector3.up * 2f - along * 120f,
                        Move = true,
                    });
                }

                for (int i = 0; i < spots.Count; i++)
                {
                    Spot spot = spots[i];

                    if (spot.Move && player != null)
                    {
                        Rigidbody body = player.GetComponentInChildren<Rigidbody>();

                        if (body != null)
                        {
                            body.linearVelocity = Vector3.zero;
                            body.angularVelocity = Vector3.zero;
                        }

                        player.position = spot.At;
                        yield return new WaitForSeconds(3f);
                    }

                    if (spot.Drive && player != null)
                    {
                        driving = player.GetComponentInChildren<Rigidbody>();
                        heading = spine != null ? spine.forward : player.forward;

                        // 달리기 시작한 직후는 타일이 아직 안 들어옵니다.
                        yield return new WaitForSeconds(2f);
                    }

                    bool last = i == spots.Count - 1;

                    if (PostAb)
                    {
                        // 같은 자리에서 두 번. 켠 것을 먼저 재야 끈 쪽이 캐시 덕을
                        // 보는 일이 없습니다.
                        Palette(true);
                        yield return Measure(spot.Name + " · 후처리 켬", report, false);

                        Palette(false);
                        yield return Measure(spot.Name + " · 후처리 끔", report, last);

                        Palette(true);
                    }
                    else
                    {
                        yield return Measure(spot.Name, report, last);
                    }

                    driving = null;
                }

                report.Append("  ]\n}\n");

                try
                {
                    File.WriteAllText(Output, report.ToString());
                    Debug.Log("MegaProfileProbe: 저장 — " + Output);
                }
                catch (Exception e)
                {
                    Debug.LogError("MegaProfileProbe: " + e);
                }

                Application.Quit();
            }

            /// <summary>
            /// 팔레트 후처리를 켜고 끕니다.
            ///
            /// ⚠ <b>렌더러 기능은 에셋이라 런타임에 참조를 얻을 길이 마땅치 않습니다.</b>
            /// URP 에셋이 들고 있는 렌더러 데이터가 공개 API 로 안 열립니다. 그래서
            /// 이미 메모리에 올라와 있는 것을 이름으로 찾습니다 — 진단 도구에서만
            /// 쓰는 방법이고, 게임 코드에서 이렇게 하면 안 됩니다.
            /// </summary>
            /// <param name="on">켤 것인가</param>
            private static void Palette(bool on)
            {
                ScriptableRendererFeature[] all =
                    Resources.FindObjectsOfTypeAll<ScriptableRendererFeature>();

                int touched = 0;

                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null || all[i].name != PaletteFeature) continue;

                    all[i].SetActive(on);
                    touched++;
                }

                if (touched == 0)
                {
                    Debug.LogWarning("MegaProfileProbe: 팔레트 기능을 못 찾았습니다 — "
                                     + "끈 쪽과 켠 쪽이 같은 그림이 됩니다");
                }
            }

            /// <summary>쓸 수 있는 카운터 이름 전부. 추측 대신 물어봅니다.</summary>
            private static IEnumerable<string> Available()
            {
                List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(handles);

                return handles
                    .Select(h => ProfilerRecorderHandle.GetDescription(h))
                    .Where(d => d.Category == ProfilerCategory.Render
                                || d.Category == ProfilerCategory.Internal)
                    .Select(d => d.Category.Name + "/" + d.Name)
                    .Distinct()
                    .OrderBy(n => n);
            }

            private IEnumerator Measure(string name, StringBuilder report, bool last)
            {
                // <b>"Draw Calls Count" 라는 카운터는 없습니다.</b> 첫 실행에서
                // 0 이 나왔고, 쓸 수 있는 이름을 전부 찍어 보고 나서야 알았습니다 -
                // 이 버전은 총합이 없고 <b>어떻게 그렸는지</b>로 쪼개 놓았습니다.
                // 조각을 더해야 하고, 그 조각 자체가 <b>SRP Batcher 가 얼마나
                // 일하는지</b>를 알려 줍니다.
                ProfilerRecorder[] kinds =
                {
                    Recorder("Standard Draw Calls Count"),
                    Recorder("SRP Batcher Draw Calls Count"),
                    Recorder("Static Batched Draw Calls Count"),
                    Recorder("Instanced Batched Draw Calls Count"),
                    Recorder("Dynamic Batched Draw Calls Count"),
                    Recorder("BRG Draw Calls Count"),
                };

                string[] labels = { "그냥", "SRP배처", "정적", "인스턴싱", "동적", "BRG" };

                ProfilerRecorder setPass = Recorder("SetPass Calls Count");
                ProfilerRecorder batches = Recorder("Shadow Casters Count");
                ProfilerRecorder tris = Recorder("Triangles Count");

                for (int i = 0; i < Settle; i++) yield return null;

                double[] kindSum = new double[kinds.Length];
                double sSum = 0, bSum = 0, tSum = 0;
                double main = 0, render = 0, gpu = 0, frame = 0;
                int taken = 0;

                FrameTiming[] timings = new FrameTiming[1];

                for (int i = 0; i < Sample; i++)
                {
                    yield return new WaitForEndOfFrame();

                    for (int k = 0; k < kinds.Length; k++)
                    {
                        kindSum[k] += kinds[k].LastValue;
                    }

                    sSum += setPass.LastValue;
                    bSum += batches.LastValue;
                    tSum += tris.LastValue;

                    FrameTimingManager.CaptureFrameTimings();

                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                    {
                        main += timings[0].cpuMainThreadFrameTime;
                        render += timings[0].cpuRenderThreadFrameTime;
                        gpu += timings[0].gpuFrameTime;
                        frame += timings[0].cpuFrameTime;
                        taken++;
                    }
                }

                foreach (ProfilerRecorder r in kinds) r.Dispose();
                setPass.Dispose();
                batches.Dispose();
                tris.Dispose();

                double n = Sample;
                double m = Math.Max(1, taken);

                double dSum = kindSum.Sum();

                string byKind = string.Join(", ", kinds.Select((_, k) =>
                    string.Format(CultureInfo.InvariantCulture,
                        "\"{0}\": {1:F0}", labels[k], kindSum[k] / n)));

                report.AppendFormat(CultureInfo.InvariantCulture,
                    "    {{ \"자리\": \"{0}\", \"드로우\": {1:F0}, \"SetPass\": {2:F0}, " +
                    "\"드로우내역\": {{ {11} }}, \"그림자캐스터\": {3:F0}, \"삼각형\": {4:F0}, " +
                    "\"프레임ms\": {5:F2}, \"메인ms\": {6:F2}, \"렌더ms\": {7:F2}, " +
                    "\"GPUms\": {8:F2}, \"타이밍표본\": {9} }}{10}\n",
                    name, dSum / n, sSum / n, bSum / n, tSum / n,
                    frame / m, main / m, render / m, gpu / m, taken, last ? "" : ",",
                    byKind);

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "MegaProfileProbe: {0} — 드로우 {1:F0} · SetPass {2:F0} · 삼각형 {3:F0} · " +
                    "메인 {4:F2} ms · 렌더 {5:F2} ms · GPU {6:F2} ms",
                    name, dSum / n, sSum / n, tSum / n, main / m, render / m, gpu / m));
            }

            private static ProfilerRecorder Recorder(string counter)
            {
                return ProfilerRecorder.StartNew(ProfilerCategory.Render, counter);
            }
        }
    }
}
