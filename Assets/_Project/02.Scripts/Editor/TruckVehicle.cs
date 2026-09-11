using UnityEditor;
using UnityEngine;
using CarDrive.Gameplay;

/// <summary>
/// Kenney 트럭을 <b>몰 수 있는 차</b>로 만듭니다.
///
/// <b>왜 새로 조립하지 않는가.</b> <c>PlayerCar</c> 는 바퀴 콜라이더 넷, 동력계,
/// 입력, 좌석, 내구도, 거울 카메라 셋, 계기판, 헤드라이트, 상호작용 콜라이더까지
/// 엮인 큰 프리팹입니다. 그것을 손으로 다시 세우면 <b>빠뜨린 것 하나가 조용히
/// 망가집니다.</b> 그래서 <b>복제하고 겉모습만 갈아 끼웁니다.</b>
///
/// ⚠ <b>실내는 세단의 것을 그대로 씁니다.</b> Kenney 차량 킷은 <b>겉껍데기뿐</b>이라
/// 운전석이 없습니다. 이 게임은 1인칭이므로 실내가 없으면 아무것도 안 보입니다.
/// 바깥에서는 트럭이고 안에서는 세단의 계기판입니다 — 알고 한 타협입니다.
///
/// <code>
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod TruckVehicle.Survey
/// Unity.exe -batchmode -nographics -projectPath . -executeMethod TruckVehicle.Build
/// </code>
/// </summary>
public static class TruckVehicle
{
    private const string CarPath = "Assets/_Project/05.Prefabs/Player/PlayerCar.prefab";
    private const string TruckPath =
        "Packages/com.toflaks.vendor.kenney-car-kit/CarKit/Models/truck.fbx";
    private const string OutPath = "Assets/_Project/05.Prefabs/Player/PlayerTruck.prefab";

    // --- Public Methods ---

    /// <summary>둘의 바퀴가 어디 있는지만 잽니다. 아무것도 안 만듭니다.</summary>
    public static void Survey()
    {
        GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(CarPath);
        GameObject truck = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPath);

        if (car == null || truck == null)
        {
            Debug.Log("TRUCKCAR ⚠ 프리팹이나 모델을 못 찾았습니다");
            EditorApplication.Exit(1);
            return;
        }

        GameObject carCopy = (GameObject)PrefabUtility.InstantiatePrefab(car);
        GameObject truckCopy = (GameObject)PrefabUtility.InstantiatePrefab(truck);

        try
        {
            CarVisuals visuals = carCopy.GetComponent<CarVisuals>();

            if (visuals != null)
            {
                Report("세단 바퀴 콜라이더 FL", carCopy.transform, visuals.frontLeftWheelCollider);
                Report("세단 바퀴 콜라이더 FR", carCopy.transform, visuals.frontRightWheelCollider);
                Report("세단 바퀴 콜라이더 RL", carCopy.transform, visuals.rearLeftWheelCollider);
                Report("세단 바퀴 콜라이더 RR", carCopy.transform, visuals.rearRightWheelCollider);
            }

            Rigidbody body = carCopy.GetComponent<Rigidbody>();
            if (body != null)
            {
                Debug.Log("TRUCKCAR 세단 질량 " + body.mass.ToString("F0") + " kg · 무게중심 "
                          + body.centerOfMass.ToString("F2"));
            }

            Collider hull = carCopy.GetComponent<Collider>();
            if (hull != null)
            {
                Debug.Log("TRUCKCAR 세단 몸 콜라이더 — " + hull.GetType().Name
                          + " · " + hull.bounds.size.ToString("F2"));
            }

            foreach (Transform t in truckCopy.GetComponentsInChildren<Transform>())
            {
                if (t == truckCopy.transform) continue;

                Renderer r = t.GetComponent<Renderer>();
                string size = r != null ? r.bounds.size.ToString("F2") : "-";

                Debug.Log("TRUCKCAR 트럭 " + t.name + " — 로컬 "
                          + truckCopy.transform.InverseTransformPoint(t.position).ToString("F2")
                          + " · 크기 " + size);
            }
        }
        finally
        {
            Object.DestroyImmediate(truckCopy);
            Object.DestroyImmediate(carCopy);
        }

        EditorApplication.Exit(0);
    }

    /// <summary>겉모습을 트럭으로 갈아 끼운 차를 만듭니다.</summary>
    public static void Build()
    {
        GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(CarPath);
        GameObject truck = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPath);

        if (car == null || truck == null)
        {
            Debug.Log("TRUCKCAR ⚠ 프리팹이나 모델을 못 찾았습니다");
            EditorApplication.Exit(1);
            return;
        }

        // ⚠ <b>좌석 값은 씬에서 읽어야 합니다.</b> <c>VehicleSeat</c> 은 프리팹에 없고
        // 씬 쪽에 붙어 있습니다. 처음에는 씬 인스턴스에만 좌석을 달았는데, 그러면
        // <b>프리팹에는 없어서</b> 다른 데 놓는 순간 운전석이 차의 원점(땅바닥)이
        // 됩니다. 플레이 모드 검사가 그것을 잡았습니다.
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/_Project/01.Scenes/SampleScene.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        VehicleSeat sample = null;

        foreach (Vehicle v in Object.FindObjectsByType<Vehicle>(FindObjectsInactive.Include,
                                                               FindObjectsSortMode.None))
        {
            if (v.name.Contains("Truck")) continue;

            sample = v.GetComponentInChildren<VehicleSeat>(true);
            if (sample != null) { SeatFrom(v.transform, sample); break; }
        }

        if (sample == null)
        {
            Debug.Log("TRUCKCAR ⚠ 세단의 좌석을 못 찾아 운전석을 못 만듭니다");
            EditorApplication.Exit(1);
            return;
        }

        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(car);
        made.name = "PlayerTruck";

        // ⚠ <b>프리팹 연결을 끊습니다.</b> 안 끊으면 세단 프리팹을 고칠 때마다
        // 트럭의 겉모습이 <b>되살아납니다.</b>
        PrefabUtility.UnpackPrefabInstanceAndReturnNewOutermostRoots(
            made, PrefabUnpackMode.Completely);

        try
        {
            GameObject shell = (GameObject)PrefabUtility.InstantiatePrefab(truck, made.transform);
            shell.name = "TruckShell";
            shell.transform.localPosition = Vector3.zero;
            shell.transform.localRotation = Quaternion.identity;

            PrefabUtility.UnpackPrefabInstanceAndReturnNewOutermostRoots(
                shell, PrefabUnpackMode.Completely);

            Hide(made);
            int moved = Wheels(made, shell);
            Hull(made, shell);
            Weight(made);
            Sit(made);

            Vehicle facade = made.GetComponent<Vehicle>();
            if (facade != null) facade.displayName = "트럭";

            Debug.Log("TRUCKCAR 바퀴 " + moved + " 개를 옮겼습니다");
        }
        catch (System.Exception e)
        {
            Debug.Log("TRUCKCAR ⚠ " + e);
            Object.DestroyImmediate(made);
            EditorApplication.Exit(1);
            return;
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(made, OutPath);
        Object.DestroyImmediate(made);

        if (saved == null)
        {
            Debug.Log("TRUCKCAR ⚠ 저장하지 못했습니다 — " + OutPath);
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("TRUCKCAR 만들었습니다 — " + OutPath);
        EditorApplication.Exit(0);
    }

    /// <summary>만든 트럭을 마을에 세우고 한 장 찍습니다.</summary>
    public static void Place()
    {
        EditorSettings.asyncShaderCompilation = false;
        ShaderUtil.allowAsyncCompilation = false;

        System.IO.Directory.CreateDirectory("Logs/Truck");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/_Project/01.Scenes/SampleScene.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutPath);

        if (prefab == null)
        {
            Debug.Log("TRUCKCAR ⚠ 트럭 프리팹이 없습니다 — 먼저 Build 를 도십시오");
            EditorApplication.Exit(1);
            return;
        }

        // 플레이어 차 옆에 세웁니다. 걸어가서 탈 수 있어야 합니다.
        //
        // ⚠ <b>아무 차나 잡으면 안 됩니다.</b> 두 번째부터는 씬에 트럭이 이미 있어서
        // <c>FindAnyObjectByType</c> 가 <b>트럭을 집어 옵니다.</b> 그것을 기준으로
        // 삼으면 바로 아래에서 그 트럭을 지우고, 지워진 것의 자리를 묻게 됩니다.
        Vehicle mine = null;
        GameObject had = null;

        foreach (Vehicle v in Object.FindObjectsByType<Vehicle>(FindObjectsInactive.Include,
                                                               FindObjectsSortMode.None))
        {
            if (v.name == prefab.name) had = v.gameObject;
            else if (mine == null) mine = v;
        }

        if (mine == null)
        {
            Debug.Log("TRUCKCAR ⚠ 기존 차를 못 찾았습니다");
            EditorApplication.Exit(1);
            return;
        }

        Transform holder = mine.transform.parent;

        if (had != null) Object.DestroyImmediate(had);

        // ⚠ <b>지운 것을 물리에 알립니다.</b> 아래에서 빈자리를 상자로 재는데,
        // 이전 트럭이 아직 물리 씬에 남아 있으면 <b>자기 자신에 걸려</b> 모든 후보가
        // 막힌 것으로 나옵니다.
        Physics.SyncTransforms();

        if (!Spot(mine.transform, prefab, out Vector3 beside))
        {
            Debug.Log("TRUCKCAR ⚠ 차 둘레에 트럭이 들어갈 빈자리가 없습니다");
            EditorApplication.Exit(1);
            return;
        }

        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        made.name = prefab.name;

        made.transform.position = beside;
        made.transform.rotation = mine.transform.rotation;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(made.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        // 운전석이 트럭 껍데기 안에 들어가 있는지 봅니다.
        Transform anchor = made.GetComponent<Vehicle>() != null
                           ? made.GetComponent<Vehicle>().DriverAnchor : null;

        if (anchor != null)
        {
            Debug.Log("TRUCKCAR 운전석 — 차 기준 "
                      + made.transform.InverseTransformPoint(anchor.position).ToString("F2"));
        }

        Shoot(made.transform, "Logs/Truck/drivable.png");

        Debug.Log("TRUCKCAR 세웠습니다 — " + beside.ToString("F0"));
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 트럭을 세울 <b>빈자리</b>입니다. 차 둘레를 돌며 찾습니다.
    ///
    /// ⚠ <b>"차 옆 6 m" 로 고정하면 안 됩니다.</b> 처음에 그렇게 뒀더니 거기가
    /// <b>도보 리그가 서 있는 자리</b>였습니다. 트럭의 몸(3.2 × 2.3 × 6.1 m)이
    /// 플레이어를 0.1 m 앞에서 감쌌고, 오줌 줄기 입자가 트럭에 부딪혀 90 개 중
    /// <b>21 개만</b> 살아남았습니다. 플레이모드 테스트가 그것을 잡았습니다.
    ///
    /// 그래서 자리를 <b>재서</b> 고릅니다 — 땅이 있고, 몸이 아무것에도 안 겹치고,
    /// 도보 리그와 떨어진 곳. 세단 오른쪽부터 보므로 자리가 비어 있으면 늘 거기입니다.
    /// </summary>
    /// <param name="car">기준이 되는 세단</param>
    /// <param name="prefab">세울 트럭 프리팹</param>
    /// <param name="at">찾은 자리</param>
    /// <returns>찾았는지 여부</returns>
    private static bool Spot(Transform car, GameObject prefab, out Vector3 at)
    {
        at = car.position;

        BoxCollider body = prefab.GetComponent<BoxCollider>();
        Vector3 size = body != null ? body.size : new Vector3(3.2f, 2.3f, 6.1f);

        // 도보 리그는 <b>꺼져 있습니다.</b> 꺼진 콜라이더는 물리 씬에 없으므로
        // 상자 검사로는 안 잡힙니다. 자리를 직접 빼 둡니다.
        PlayerFootMotor onFoot = Object.FindAnyObjectByType<PlayerFootMotor>(
            FindObjectsInactive.Include);

        Vector3 walker = onFoot != null ? onFoot.transform.position : car.position;

        // 땅(11)과 상호작용 트리거(6)는 겹쳐도 됩니다. 나머지는 안 됩니다.
        int block = ~((1 << 11) | (1 << 6));

        foreach (float radius in new[] { 6f, 8f, 10f })
        {
            for (int step = 0; step < 12; step++)
            {
                Vector3 way = Quaternion.AngleAxis(step * 30f, Vector3.up) * car.right;
                Vector3 want = car.position + way * radius;

                if (!Physics.Raycast(want + Vector3.up * 50f, Vector3.down,
                                     out RaycastHit hit, 200f, 1 << 11,
                                     QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                Vector3 on = hit.point + Vector3.up * 0.1f;

                // 걸어 나온 사람 위에 세우지 않습니다.
                Vector3 gap = on - walker;
                gap.y = 0f;
                if (gap.magnitude < size.x * 0.5f + 2.5f) continue;

                // 몸이 들어갈 자리가 있는가. 조금 부풀려 재서 딱 붙는 자리는 거릅니다.
                Vector3 middle = on + Vector3.up * (size.y * 0.5f + 0.15f);

                if (Physics.CheckBox(middle, size * 0.55f, car.rotation, block,
                                     QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                at = on;

                Debug.Log("TRUCKCAR 자리 — 차에서 " + radius.ToString("F0") + " m · "
                          + (step * 30f).ToString("F0") + "° · 도보 리그와 "
                          + gap.magnitude.ToString("F1") + " m");
                return true;
            }
        }

        return false;
    }

    /// <summary>그 물체를 한 장 찍습니다.</summary>
    private static void Shoot(Transform what, string path)
    {
        Camera camera = new GameObject("TruckCamera").AddComponent<Camera>();

        try
        {
            Bounds box = new Bounds(what.position, Vector3.one);
            bool first = true;

            foreach (Renderer r in what.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (first) { box = r.bounds; first = false; } else box.Encapsulate(r.bounds);
            }

            float reach = Mathf.Max(box.extents.magnitude * 2.2f, 7f);
            camera.transform.position = box.center
                                        + new Vector3(reach * 0.7f, reach * 0.35f, reach * 0.7f);
            camera.transform.LookAt(box.center);

            RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D shot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            camera.targetTexture = target;

            for (int i = 0; i < 3; i++)
            {
                camera.Render();
                while (ShaderUtil.anythingCompiling) System.Threading.Thread.Sleep(50);
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            shot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(shot);
            target.Release();
            Object.DestroyImmediate(target);
        }
        finally
        {
            Object.DestroyImmediate(camera.gameObject);
        }
    }

    /// <summary>
    /// 트럭에 <b>운전석</b>을 답니다.
    ///
    /// ⚠ <b><see cref="VehicleSeat"/> 은 프리팹에 없습니다.</b> 씬 쪽에 붙어 있어서,
    /// 프리팹만 복제하면 <c>Vehicle.seat</c> 가 비어 있고 <c>DriverAnchor</c> 가
    /// 차의 원점(0,0,0)을 돌려줍니다 — <b>땅바닥에 앉은 채로 운전</b>하게 됩니다.
    ///
    /// 그래서 씬에서 세단의 좌석을 읽어 트럭에 같은 모양으로 만듭니다.
    /// </summary>
    public static void Seat()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/_Project/01.Scenes/SampleScene.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        Vehicle sedan = null;
        Vehicle truck = null;

        foreach (Vehicle v in Object.FindObjectsByType<Vehicle>(FindObjectsInactive.Include,
                                                               FindObjectsSortMode.None))
        {
            if (v.name.Contains("Truck")) truck = v;
            else if (sedan == null) sedan = v;
        }

        // ⚠ <b><c>Vehicle.seat</c> 는 에디터에서 비어 있습니다.</b> <c>ResolveParts</c> 가
        // <c>Awake</c> 에 자기 계층에서 찾아 채우기 때문입니다. 그것을 모르고
        // <c>sedan.seat</c> 를 봤다가 "좌석을 못 찾았다" 로 한 번 멈췄습니다.
        VehicleSeat old = sedan != null
                          ? sedan.GetComponentInChildren<VehicleSeat>(true) : null;

        if (sedan == null || truck == null || old == null)
        {
            Debug.Log("TRUCKCAR ⚠ 세단 " + (sedan != null ? sedan.name : "(없음)")
                      + " · 트럭 " + (truck != null ? truck.name : "(없음)")
                      + " · 세단 좌석 " + (old != null ? "있음" : "없음"));
            EditorApplication.Exit(1);
            return;
        }

        Transform from = sedan.transform;

        Transform anchor = old.GetDriverAnchor();
        Vector3 sit = anchor != null ? from.InverseTransformPoint(anchor.position) : Vector3.zero;

        Debug.Log("TRUCKCAR 세단 운전석 — " + sit.ToString("F2")
                  + " · 내리는 자리 " + old.exitPoints.Count + " 곳");

        // ⚠ <b>트럭은 더 높습니다.</b> 세단의 높이가 1.41 m, 트럭이 2.31 m 라
        // 앉는 자리를 그대로 두면 <b>대시보드 아래</b>에 앉습니다.
        // 차체 높이 차이의 절반쯤을 올립니다.
        Vector3 lift = new Vector3(0f, 0.45f, 0f);

        GameObject seatHost = new GameObject("Seat");
        seatHost.transform.SetParent(truck.transform, false);

        GameObject sitHere = new GameObject("DriverAnchor");
        sitHere.transform.SetParent(seatHost.transform, false);
        sitHere.transform.localPosition = sit + lift;

        VehicleSeat made = seatHost.AddComponent<VehicleSeat>();
        made.driverAnchor = sitHere.transform;
        made.enterDistance = old.enterDistance;
        made.exitClearanceRadius = old.exitClearanceRadius;
        made.exitClearanceHeight = old.exitClearanceHeight;
        made.exitBlockMask = old.exitBlockMask;

        // 내리는 자리도 같은 모양으로 옮깁니다. 없으면 문 옆에 못 내립니다.
        foreach (Transform point in old.exitPoints)
        {
            if (point == null) continue;

            GameObject copy = new GameObject(point.name);
            copy.transform.SetParent(seatHost.transform, false);

            // 트럭이 더 넓으므로(3.15 m 대 2.05 m) 옆으로 조금 더 밀어 둡니다.
            Vector3 local = from.InverseTransformPoint(point.position);
            copy.transform.localPosition = new Vector3(local.x * 1.4f, local.y, local.z);

            made.exitPoints.Add(copy.transform);
        }

        truck.seat = made;

        EditorUtility.SetDirty(truck);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(truck.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("TRUCKCAR 트럭 운전석 — " + sitHere.transform.localPosition.ToString("F2")
                  + " · 내리는 자리 " + made.exitPoints.Count + " 곳");
        EditorApplication.Exit(0);
    }

    // --- Private Methods ---

    /// <summary>세단에서 읽어 둔 좌석 값입니다.</summary>
    private static Vector3 seatAt;
    private static readonly System.Collections.Generic.List<Vector3> seatExits =
        new System.Collections.Generic.List<Vector3>();
    private static float seatEnter = 3.5f;
    private static float seatRadius = 0.4f;
    private static float seatHeight = 1.8f;
    private static LayerMask seatBlock = ~0;

    /// <summary>세단의 좌석 값을 적어 둡니다.</summary>
    private static void SeatFrom(Transform root, VehicleSeat seat)
    {
        Transform anchor = seat.GetDriverAnchor();
        seatAt = anchor != null ? root.InverseTransformPoint(anchor.position) : Vector3.zero;

        seatExits.Clear();
        foreach (Transform point in seat.exitPoints)
        {
            if (point != null) seatExits.Add(root.InverseTransformPoint(point.position));
        }

        seatEnter = seat.enterDistance;
        seatRadius = seat.exitClearanceRadius;
        seatHeight = seat.exitClearanceHeight;
        seatBlock = seat.exitBlockMask;

        Debug.Log("TRUCKCAR 세단 운전석 — " + seatAt.ToString("F2")
                  + " · 내리는 자리 " + seatExits.Count + " 곳");
    }

    /// <summary>트럭에 운전석을 만듭니다.</summary>
    private static void Sit(GameObject root)
    {
        GameObject host = new GameObject("Seat");
        host.transform.SetParent(root.transform, false);

        GameObject anchor = new GameObject("DriverAnchor");
        anchor.transform.SetParent(host.transform, false);

        // ⚠ <b>트럭은 더 높습니다.</b> 세단이 1.41 m, 트럭이 2.31 m 라 앉는 자리를
        // 그대로 두면 <b>대시보드 아래</b>에 앉습니다.
        anchor.transform.localPosition = seatAt + new Vector3(0f, 0.45f, 0f);

        VehicleSeat seat = host.AddComponent<VehicleSeat>();
        seat.driverAnchor = anchor.transform;
        seat.enterDistance = seatEnter;
        seat.exitClearanceRadius = seatRadius;
        seat.exitClearanceHeight = seatHeight;
        seat.exitBlockMask = seatBlock;

        foreach (Vector3 at in seatExits)
        {
            GameObject point = new GameObject("ExitPoint");
            point.transform.SetParent(host.transform, false);

            // 트럭이 더 넓으므로(3.15 m 대 2.05 m) 옆으로 조금 더 밀어 둡니다.
            point.transform.localPosition = new Vector3(at.x * 1.4f, at.y, at.z);
            seat.exitPoints.Add(point.transform);
        }

        Vehicle facade = root.GetComponent<Vehicle>();
        if (facade != null) facade.seat = seat;

        Debug.Log("TRUCKCAR 트럭 운전석 — " + anchor.transform.localPosition.ToString("F2")
                  + " · 내리는 자리 " + seat.exitPoints.Count + " 곳");
    }

    /// <summary>
    /// 세단의 <b>겉껍데기만</b> 감춥니다.
    ///
    /// ⚠ <b>지우지 않고 끕니다.</b> 바퀴 오브젝트에는 <c>GrassPusher</c> 가 붙어 있고
    /// 문에는 상호작용 콜라이더가 딸려 있습니다. 지우면 그것들이 같이 사라집니다.
    /// </summary>
    private static void Hide(GameObject root)
    {
        string[] outside =
        {
            "Door_L", "Door_R", "DoorRear_L", "DoorRear_R",
            "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR",
        };

        MeshRenderer body = root.GetComponent<MeshRenderer>();
        if (body != null) body.enabled = false;

        int hidden = body != null ? 1 : 0;

        foreach (string name in outside)
        {
            Transform part = Find(root.transform, name);
            if (part == null) continue;

            foreach (MeshRenderer r in part.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.enabled = false;
                hidden++;
            }
        }

        Debug.Log("TRUCKCAR 세단 겉껍데기 " + hidden + " 개를 껐습니다");
    }

    /// <summary>바퀴 콜라이더를 트럭 자리로 옮기고, 트럭 바퀴를 그 밑에 답니다.</summary>
    private static int Wheels(GameObject root, GameObject shell)
    {
        CarVisuals visuals = root.GetComponent<CarVisuals>();
        if (visuals == null) return 0;

        (WheelCollider Collider, Transform Visual, string Node)[] pairs =
        {
            (visuals.frontLeftWheelCollider,  visuals.frontLeftWheelTransform,  "wheel_frontLeft"),
            (visuals.frontRightWheelCollider, visuals.frontRightWheelTransform, "wheel_frontRight"),
            (visuals.rearLeftWheelCollider,   visuals.rearLeftWheelTransform,   "wheel_backLeft"),
            (visuals.rearRightWheelCollider,  visuals.rearRightWheelTransform,  "wheel_backRight"),
        };

        int moved = 0;

        foreach ((WheelCollider collider, Transform visual, string node) in pairs)
        {
            Transform from = Find(shell.transform, node);
            if (collider == null || from == null) continue;

            Vector3 local = root.transform.InverseTransformPoint(from.position);

            collider.transform.localPosition = local;

            // 반지름은 모델에서 잽니다. 짐작해서 넣으면 바퀴가 땅에 묻히거나 뜹니다.
            Renderer mesh = from.GetComponent<Renderer>();
            if (mesh != null) collider.radius = mesh.bounds.size.y * 0.5f;

            // 트럭 바퀴를 세단의 바퀴 오브젝트 밑으로 옮깁니다.
            // <c>CarVisuals</c> 가 그 오브젝트를 <c>GetWorldPose</c> 로 통째로 몰기 때문에,
            // 밑에 달아 두면 저절로 따라 돕니다.
            if (visual != null)
            {
                from.SetParent(visual, false);
                from.localPosition = Vector3.zero;
                from.localRotation = Quaternion.identity;
            }

            moved++;
        }

        return moved;
    }

    /// <summary>몸통 콜라이더를 트럭 크기의 상자로 바꿉니다.</summary>
    private static void Hull(GameObject root, GameObject shell)
    {
        Transform body = Find(shell.transform, "body");
        Renderer mesh = body != null ? body.GetComponent<Renderer>() : null;
        if (mesh == null) return;

        // ⚠ <b>메시 콜라이더를 지웁니다.</b> 세단 몸체의 것이라 트럭과 모양이 다릅니다.
        // 남겨 두면 <b>보이지 않는 세단</b>이 계속 부딪힙니다.
        MeshCollider old = root.GetComponent<MeshCollider>();
        if (old != null) Object.DestroyImmediate(old);

        BoxCollider hull = root.AddComponent<BoxCollider>();
        hull.center = root.transform.InverseTransformPoint(mesh.bounds.center);
        hull.size = mesh.bounds.size;

        Debug.Log("TRUCKCAR 몸 콜라이더 — 상자 " + hull.size.ToString("F2")
                  + " · 가운데 " + hull.center.ToString("F2"));
    }

    /// <summary>트럭은 무겁습니다.</summary>
    private static void Weight(GameObject root)
    {
        Rigidbody body = root.GetComponent<Rigidbody>();
        if (body == null) return;

        // 세단 1500 kg 에 견줘 실은 것 없는 소형 트럭쯤입니다.
        body.mass = 2400f;

        // ⚠ <b>무게중심을 낮춰 둡니다.</b> 트럭은 세단보다 높은데(2.31 m 대 1.41 m)
        // 무게중심을 그대로 두면 코너에서 <b>뒤집힙니다.</b>
        body.centerOfMass = new Vector3(0f, 0.55f, -0.1f);

        Debug.Log("TRUCKCAR 질량 " + body.mass.ToString("F0") + " kg · 무게중심 "
                  + body.centerOfMass.ToString("F2"));
    }

    /// <summary>이름으로 자손을 찾습니다.</summary>
    private static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }

        return null;
    }

    private static void Report(string label, Transform root, WheelCollider wheel)
    {
        if (wheel == null)
        {
            Debug.Log("TRUCKCAR ⚠ " + label + " 가 비어 있습니다");
            return;
        }

        Debug.Log("TRUCKCAR " + label + " — 로컬 "
                  + root.InverseTransformPoint(wheel.transform.position).ToString("F2")
                  + " · 반지름 " + wheel.radius.ToString("F2")
                  + " · 서스펜션 " + wheel.suspensionDistance.ToString("F2"));
    }
}
