using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarDrive.Gameplay;

namespace CarDrive.EditorTools
{
    /// <summary>
    /// 던진 물건이 지면을 뚫고 떨어지는 문제를 <b>추측하지 않고 물어봅니다.</b>
    ///
    /// 병은 프리팹 안의 프리팹 안에 들어 있어서, 파일을 열어 보는 것만으로는
    /// 실제로 어느 레이어에 놓이는지 확신할 수 없습니다. 중간 어디선가 덮어썼을 수 있습니다.
    /// 그래서 씬을 실제로 열어 놓인 값을 읽고, 물리 엔진에게 직접 묻습니다.
    /// </summary>
    public static class PropDropDiagnostics
    {
        /// <summary>메인 씬 경로입니다.</summary>
        private const string ScenePath = "Assets/_Project/01.Scenes/SampleScene.unity";

        /// <summary>
        /// 병과 지면의 레이어, 둘이 서로 무시하도록 설정되어 있는지, 그리고 실제로
        /// 아래로 광선을 쏘면 지면에 닿는지를 확인합니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod PropDropDiagnostics.RunFromCommandLine</c>
        /// </summary>
        public static void RunFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            StringBuilder log = new StringBuilder();

            Terrain terrain = Object.FindAnyObjectByType<Terrain>();
            int terrainLayer = terrain != null ? terrain.gameObject.layer : -1;

            log.AppendLine("PROP 지면 레이어: " + Describe(terrainLayer));

            // 병은 콜라이더가 붙어 있는 오브젝트의 레이어로 충돌합니다.
            // 스크립트가 붙은 오브젝트와 다를 수 있으므로 콜라이더 쪽을 봅니다.
            Beverage[] bottles = Object.FindObjectsByType<Beverage>(FindObjectsInactive.Include);
            log.AppendLine("PROP 씬에서 찾은 음료: " + bottles.Length + "개");

            HashSet<int> layers = new HashSet<int>();
            Beverage sample = null;

            for (int i = 0; i < bottles.Length; i++)
            {
                Collider col = bottles[i].GetComponentInChildren<Collider>(true);
                int layer = col != null ? col.gameObject.layer : bottles[i].gameObject.layer;

                layers.Add(layer);
                if (sample == null) sample = bottles[i];

                if (i < 3)
                {
                    Rigidbody body = bottles[i].GetComponentInParent<Rigidbody>();

                    log.AppendLine("PROP   " + bottles[i].name +
                                   " | 스크립트 레이어 " + Describe(bottles[i].gameObject.layer) +
                                   " | 콜라이더 " + (col == null ? "없음" : col.GetType().Name + " 레이어 " + Describe(layer)) +
                                   " | Rigidbody " + (body == null ? "없음" :
                                       "질량 " + body.mass + ", 충돌검사 " + body.collisionDetectionMode));
                }
            }

            foreach (int layer in layers)
            {
                bool ignored = terrainLayer >= 0 && Physics.GetIgnoreLayerCollision(layer, terrainLayer);

                log.AppendLine("PROP 충돌 여부: " + Describe(layer) + " x " + Describe(terrainLayer) +
                               " -> " + (ignored ? "무시함 (뚫고 지나갑니다)" : "충돌함 (정상)"));
            }

            // 실제로 광선을 쏘아 지면이 잡히는지 봅니다.
            if (sample != null)
            {
                Vector3 from = sample.transform.position + Vector3.up * 60f;
                RaycastHit hit;

                if (Physics.Raycast(from, Vector3.down, out hit, 400f, ~0, QueryTriggerInteraction.Ignore))
                {
                    log.AppendLine("PROP 아래로 쏜 광선: " + hit.collider.GetType().Name +
                                   " (" + hit.collider.name + ", 레이어 " + Describe(hit.collider.gameObject.layer) +
                                   ") 에 " + hit.distance.ToString("F1") + "m 에서 맞음");
                }
                else
                {
                    log.AppendLine("PROP 아래로 쏜 광선: 아무것도 맞지 않음 — 그 자리에 지면 콜라이더가 없습니다.");
                }
            }

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 원경 캡처에 보이는 화면의 특정 자리에 <b>무엇이 있는지</b> 짚어 봅니다.
        ///
        /// 그림만 봐서는 하늘인지, 물체인지, 다른 머티리얼을 쓴 지형인지 알 수 없습니다.
        /// 그 픽셀로 광선을 쏘아 이름을 직접 받아 옵니다.
        /// <c>Unity.exe -batchmode -quit -executeMethod PropDropDiagnostics.ProbeFromCommandLine</c>
        /// </summary>
        public static void ProbeFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 06_멀리서 캡처와 같은 자리, 같은 각도로 세웁니다.
            Terrain terrain = Object.FindAnyObjectByType<Terrain>();
            if (terrain == null) return;

            Vector3 spot = terrain.transform.position + new Vector3(50f, 0f, 50f);
            spot.y = terrain.SampleHeight(spot) + terrain.transform.position.y;

            GameObject camGo = new GameObject("ProbeCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.farClipPlane = 1200f;
            cam.transform.position = spot + Vector3.up * 25f;
            cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            cam.pixelRect = new Rect(0, 0, 640, 360);

            HashSet<string> seen = new HashSet<string>();

            // 화면을 훑어 무엇이 잡히는지 모읍니다.
            for (int y = 0; y < 360; y += 20)
            {
                for (int x = 0; x < 640; x += 20)
                {
                    Ray ray = cam.ScreenPointToRay(new Vector3(x, y, 0f));
                    RaycastHit hit;

                    if (!Physics.Raycast(ray, out hit, 1200f, ~0, QueryTriggerInteraction.Collide)) continue;

                    Renderer r = hit.collider.GetComponent<Renderer>();
                    string mat = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "-";

                    seen.Add(hit.collider.name + " | " + hit.collider.GetType().Name + " | 머티리얼 " + mat);
                }
            }

            foreach (string line in seen) Debug.Log("PROBE " + line);
            Debug.Log("PROBE 끝. 잡힌 종류 " + seen.Count + "가지");

            Object.DestroyImmediate(camGo);
        }

        /// <summary>레이어 번호를 이름과 함께 적습니다.</summary>
        /// <param name="layer">레이어 번호</param>
        /// <returns>"9(Prop)" 같은 문자열</returns>
        private static string Describe(int layer)
        {
            if (layer < 0) return "없음";

            string name = LayerMask.LayerToName(layer);
            return layer + "(" + (string.IsNullOrEmpty(name) ? "이름없음" : name) + ")";
        }
    }
}
