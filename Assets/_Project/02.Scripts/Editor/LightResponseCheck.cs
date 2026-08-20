using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Systems;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 시간대와 구름에 따라 <b>광량이 실제로 바뀌는지</b> 재어 봅니다.
    ///
    /// 설정만 확인하는 것으로는 부족합니다. 값이 켜져 있어도 계산 어딘가에서 막혀 있으면
    /// 화면은 그대로입니다. 그래서 실제로 렌더링해 지면 밝기를 잽니다.
    ///
    /// 조명 계산을 여기서 다시 구현하지 않습니다. 그렇게 하면 <b>제가 짠 식을 제가 확인하는 것</b>이라
    /// 아무것도 검증하지 못합니다. <see cref="SkyController"/>의 실제 메서드를 그대로 불러 씁니다.
    /// 게임이 매 프레임 부르는 바로 그 코드입니다.
    /// </summary>
    public static class LightResponseCheck
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>찍은 그림을 둘 폴더입니다.</summary>
        private const string OutDir = "Logs/LightResponse";

        /// <summary>
        /// 여러 시각과 구름 상태로 렌더링해 지면 밝기를 잽니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod LightResponseCheck.RunFromCommandLine</c>
        /// </summary>
        public static void RunFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            SkyController sky = Object.FindAnyObjectByType<SkyController>();
            if (sky == null)
            {
                Debug.LogError("LIGHT: SkyController 가 없습니다.");
                EditorApplication.Exit(1);
                return;
            }

            // 편집 중에는 Awake 가 돌지 않아 참조가 비어 있습니다. 직접 채웁니다.
            if (sky.sun == null) sky.sun = RenderSettings.sun;
            if (sky.sun == null)
            {
                Debug.LogError("LIGHT: 태양광을 찾지 못했습니다.");
                EditorApplication.Exit(1);
                return;
            }

            MethodInfo applySun = Method(typeof(SkyController), "ApplySun");
            MethodInfo applyAmbient = Method(typeof(SkyController), "ApplyAmbient");
            MethodInfo applyFog = Method(typeof(SkyController), "ApplyFogColor");
            MethodInfo applySky = Method(typeof(SkyController), "ApplySky");

            if (applySun == null || applyAmbient == null)
            {
                Debug.LogError("LIGHT: SkyController 의 조명 메서드를 찾지 못했습니다. 이름이 바뀌었을 수 있습니다.");
                EditorApplication.Exit(1);
                return;
            }

            // 편집 중에는 Instance 가 비어 있어 구름 분기를 타지 않습니다. 직접 세웁니다.
            WeatherSystem weather = Object.FindAnyObjectByType<WeatherSystem>();
            SetStatic(typeof(WeatherSystem), "Instance", weather);

            Vector3 spot = FindGrassSpot();
            Directory.CreateDirectory(OutDir);

            // 이름, 낮 정도(0 한밤 ~ 1 한낮), 해의 고도(도), 구름 어둡기(0 맑음 ~ 1 짙음)
            float[][] cases =
            {
                new[] { 1.00f,  60f, 0.0f },
                new[] { 0.45f,  20f, 0.0f },
                new[] { 0.15f,   4f, 0.0f },
                new[] { 0.00f, -30f, 0.0f },
                new[] { 1.00f,  60f, 0.5f },
                new[] { 1.00f,  60f, 1.0f }
            };

            string[] names =
            {
                "1_한낮_맑음", "2_아침_맑음", "3_해질녘_맑음", "4_한밤_맑음",
                "5_한낮_반흐림", "6_한낮_짙은구름"
            };

            for (int i = 0; i < cases.Length; i++)
            {
                float daylight = cases[i][0];
                float elevation = cases[i][1];
                float darkness = cases[i][2];

                if (weather != null) SetProperty(weather, "Darkness", darkness);

                sky.sun.transform.rotation = Quaternion.Euler(elevation, 150f, 0f);

                // 게임이 매 프레임 부르는 그 메서드들입니다.
                // ApplySky 를 빼면 하늘만 낮에 머물러, 밤 그림이 실제와 달라집니다.
                if (applySky != null) applySky.Invoke(sky, new object[] { daylight });
                applySun.Invoke(sky, new object[] { daylight });
                applyAmbient.Invoke(sky, new object[] { daylight });
                if (applyFog != null) applyFog.Invoke(sky, new object[] { daylight });

                float brightness = Shoot(names[i], spot);

                Debug.Log("LIGHT " + names[i] +
                          ": 지면 밝기 " + brightness.ToString("F1") + "%" +
                          " | 해 세기 " + sky.sun.intensity.ToString("F3") +
                          " | 주변광 " + ColorText(RenderSettings.ambientSkyColor));
            }

            SetStatic(typeof(WeatherSystem), "Instance", null);
            Debug.Log("LIGHT: 완료 -> " + OutDir);
        }

        // --- Private Methods ---

        /// <summary>비공개 인스턴스 메서드를 찾습니다.</summary>
        /// <param name="type">찾을 형식</param>
        /// <param name="name">메서드 이름</param>
        /// <returns>찾은 메서드. 없으면 null입니다.</returns>
        private static MethodInfo Method(System.Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        }

        /// <summary>비공개 setter를 가진 정적 프로퍼티에 값을 넣습니다.</summary>
        /// <param name="type">프로퍼티가 있는 형식</param>
        /// <param name="name">프로퍼티 이름</param>
        /// <param name="value">넣을 값</param>
        private static void SetStatic(System.Type type, string name, object value)
        {
            PropertyInfo prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (prop == null) return;

            MethodInfo setter = prop.GetSetMethod(true);
            if (setter != null) setter.Invoke(null, new object[] { value });
        }

        /// <summary>비공개 setter를 가진 인스턴스 프로퍼티에 값을 넣습니다.</summary>
        /// <param name="target">대상 객체</param>
        /// <param name="name">프로퍼티 이름</param>
        /// <param name="value">넣을 값</param>
        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return;

            MethodInfo setter = prop.GetSetMethod(true);
            if (setter != null) setter.Invoke(target, new object[] { value });
        }

        /// <summary>풀이 자라는 자리를 하나 찾습니다.</summary>
        /// <returns>지면 위 월드 좌표</returns>
        private static Vector3 FindGrassSpot()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            for (int i = 0; i < terrains.Length; i++)
            {
                TerrainData data = terrains[i].terrainData;
                if (data == null) continue;

                int res = data.alphamapResolution;
                float[,,] alpha = data.GetAlphamaps(0, 0, res, res);
                Vector3 origin = terrains[i].transform.position;

                for (int z = 0; z < res; z += 4)
                {
                    for (int x = 0; x < res; x += 4)
                    {
                        if (alpha[z, x, 0] < 0.97f) continue;

                        Vector3 world = new Vector3(
                            origin.x + (x + 0.5f) / res * data.size.x,
                            0f,
                            origin.z + (z + 0.5f) / res * data.size.z);

                        world.y = terrains[i].SampleHeight(world) + origin.y;
                        return world;
                    }
                }
            }

            return Vector3.zero;
        }

        /// <summary>한 장을 찍어 저장하고 화면 아래 절반의 밝기를 잽니다.</summary>
        /// <param name="name">파일 이름</param>
        /// <param name="spot">지면 위 자리</param>
        /// <returns>지면 평균 밝기(백분율)</returns>
        private static float Shoot(string name, Vector3 spot)
        {
            const int W = 480;
            const int H = 270;

            GameObject camGo = new GameObject("LightCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            cam.transform.position = spot + Vector3.up * 1.7f;
            cam.transform.rotation = Quaternion.Euler(7f, 60f, 0f);

            RenderTexture rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            Texture2D shot = new Texture2D(W, H, TextureFormat.RGB24, false);

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            shot.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            shot.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(OutDir + "/" + name + ".png", shot.EncodeToPNG());

            Color[] px = shot.GetPixels(0, 0, W, H / 2);
            double sum = 0.0;
            for (int i = 0; i < px.Length; i++) sum += px[i].grayscale;

            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(shot);
            rt.Release();
            Object.DestroyImmediate(rt);

            return (float)(sum / px.Length * 100.0);
        }

        /// <summary>색을 짧게 적습니다.</summary>
        /// <param name="c">적을 색</param>
        /// <returns>"0.48/0.57/0.66" 형태의 문자열</returns>
        private static string ColorText(Color c)
        {
            return c.r.ToString("F2") + "/" + c.g.ToString("F2") + "/" + c.b.ToString("F2");
        }
    }
}
