using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 씬에 하나씩만 있는 것들을 모아 두는 곳입니다.
    ///
    /// 예전에는 각 컴포넌트가 <c>Start()</c>에서 <c>FindAnyObjectByType</c>으로
    /// 협력자를 직접 찾았습니다. 스물다섯 군데였습니다. 편하지만 대가가 셋이었습니다.
    ///  1. 찾는 비용이 씬 크기에 비례하고, 찾는 쪽이 늘수록 그만큼 곱해집니다.
    ///     (음료는 <b>병 하나마다</b> 씬을 훑었습니다)
    ///  2. 협력자가 없을 때의 실패가 스물다섯 개의 서로 다른 경고로 흩어졌습니다.
    ///  3. 씬 없이는 조립할 수 없어 테스트에서 손댈 수 없었습니다.
    ///
    /// 그래서 <b>스스로 등록하고, 찾을 때는 표에서 꺼냅니다.</b>
    /// 등록은 Awake에서, 조회는 Start에서 합니다. Unity가 모든 Awake를 끝낸 뒤
    /// Start를 부르므로 이 순서는 보장됩니다.
    ///
    /// <b>등록되지 않은 것을 물으면 씬을 한 번 찾아보고 경고를 남깁니다.</b>
    /// 배선이 아직 옮겨지지 않은 씬에서도 게임이 돌아가야 하기 때문입니다.
    /// 경고가 보인다면 그 타입에 등록 코드가 빠진 것입니다. 조용히 넘어가지 않습니다.
    ///
    /// 비활성 오브젝트에 대해서도 예전보다 낫습니다. 등록은 참조를 들고 있으므로,
    /// 차에 타서 도보 리그가 꺼져도 그 위의 컴포넌트를 계속 찾을 수 있습니다.
    /// (예전 <c>FindAnyObjectByType</c>은 꺼진 오브젝트를 찾지 못했습니다)
    /// </summary>
    public static class GameContext
    {
        // --- Private Member Variables ---

        /// <summary>타입 하나에 인스턴스 하나입니다.</summary>
        private static readonly Dictionary<Type, Component> services = new Dictionary<Type, Component>();

        /// <summary>메인 카메라입니다. 파괴되면 다시 찾습니다.</summary>
        private static Camera mainCamera;

        // --- Public Properties ---

        /// <summary>
        /// 메인 카메라입니다.
        ///
        /// <c>Camera.main</c>은 태그로 찾는 조회라 매 프레임 부를 것이 못 됩니다.
        /// 실제로 <see cref="GrassPushField"/>는 LateUpdate마다 부르고 있었습니다.
        /// 여기서 한 번 찾아 들고 있다가, 파괴되면 그때 다시 찾습니다.
        /// </summary>
        public static Camera MainCamera
        {
            get
            {
                if (mainCamera == null) mainCamera = Camera.main;
                return mainCamera;
            }
        }

        /// <summary>메인 카메라의 Transform입니다. 카메라가 없으면 null입니다.</summary>
        public static Transform MainCameraTransform
        {
            get
            {
                Camera camera = MainCamera;
                return camera != null ? camera.transform : null;
            }
        }

        /// <summary>메인 카메라의 위치입니다. 카메라가 없으면 원점입니다.</summary>
        public static Vector3 MainCameraPosition
        {
            get
            {
                Camera camera = MainCamera;
                return camera != null ? camera.transform.position : Vector3.zero;
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// 자신을 등록합니다. Awake에서 부르세요.
        /// </summary>
        /// <param name="service">등록할 인스턴스</param>
        /// <returns>
        /// 등록되었으면 true입니다. 이미 <b>다른</b> 인스턴스가 등록되어 있으면
        /// 경고를 남기고 false를 돌려주므로, 받은 쪽이 자신을 끄면 됩니다.
        /// </returns>
        public static bool Register<T>(T service) where T : Component
        {
            if (service == null) return false;

            Type key = typeof(T);

            Component existing;
            if (services.TryGetValue(key, out existing) && existing != null)
            {
                // 같은 것을 두 번 등록하는 것은 문제가 아닙니다.
                if (ReferenceEquals(existing, service)) return true;

                Debug.LogWarning("GameContext: " + key.Name + "이(가) 씬에 두 개 이상 있습니다. " +
                                 "나중 것은 등록되지 않습니다.", service);
                return false;
            }

            services[key] = service;
            return true;
        }

        /// <summary>
        /// 등록을 해제합니다. OnDestroy에서 부르세요.
        ///
        /// <b>OnDisable이 아니라 OnDestroy입니다.</b> 도보 리그는 차에 타면 꺼지는데,
        /// 그때 등록까지 풀리면 차 안에서는 플레이어 체력을 찾을 수 없게 됩니다.
        /// </summary>
        /// <param name="service">해제할 인스턴스. 등록된 것과 다르면 아무 일도 하지 않습니다.</param>
        public static void Unregister<T>(T service) where T : Component
        {
            if (service == null) return;

            Type key = typeof(T);

            Component existing;
            if (services.TryGetValue(key, out existing) && ReferenceEquals(existing, service))
            {
                services.Remove(key);
            }
        }

        /// <summary>
        /// 등록된 것을 꺼냅니다. <b>표만 봅니다. 씬을 뒤지지 않습니다.</b>
        ///
        /// 시스템이 없어도 게임이 돌아가야 하는 자리에 쓰세요.
        /// (<c>NeedsSystem.Report</c>, <c>WeatherSystem.GetRainIntensity</c> 같은 정적 접근자)
        /// 그런 자리는 시스템이 없을 때 <b>자주</b> 불리므로, 없을 때일수록 싸야 합니다.
        /// </summary>
        /// <returns>등록된 인스턴스. 없으면 null입니다.</returns>
        public static T Get<T>() where T : Component
        {
            Type key = typeof(T);

            Component existing;
            if (!services.TryGetValue(key, out existing)) return null;

            // Unity의 파괴된 오브젝트는 == null 로만 걸러집니다.
            if (existing != null) return (T)existing;

            services.Remove(key);
            return null;
        }

        /// <summary>
        /// 협력자를 찾아 연결합니다. 등록되어 있지 않으면 씬을 <b>한 번</b> 찾아보고
        /// 경고를 남긴 뒤 그 결과를 기억합니다.
        ///
        /// Start에서 참조를 채울 때 쓰세요. 경고가 보인다면 그 타입에 등록 코드가 빠진 것입니다.
        /// </summary>
        /// <param name="asker">누가 찾는지 로그에 남길 대상</param>
        /// <returns>찾은 인스턴스. 씬에도 없으면 null입니다.</returns>
        public static T Resolve<T>(Component asker) where T : Component
        {
            T registered = Get<T>();
            if (registered != null) return registered;

            // 등록이 빠진 경우를 위한 안전망입니다. 한 번만 찾고 결과를 기억합니다.
            T scanned = UnityEngine.Object.FindAnyObjectByType<T>();
            if (scanned == null) return null;

            Debug.LogWarning("GameContext: " + typeof(T).Name + "이(가) 등록되지 않아 씬을 검색했습니다. " +
                             "이 타입의 Awake에서 GameContext.Register를 부르세요.", asker);
            services[typeof(T)] = scanned;
            return scanned;
        }

        /// <summary>
        /// 반드시 있어야 하는 것을 찾습니다. 없으면 <b>오류</b>를 남깁니다.
        /// 없어도 게임이 돌아가는 것에는 <see cref="Resolve{T}"/>를 쓰세요.
        /// </summary>
        /// <param name="asker">누가 찾다가 실패했는지 로그에 남길 대상</param>
        /// <returns>찾은 인스턴스. 없으면 null입니다.</returns>
        public static T Require<T>(Component asker) where T : Component
        {
            T found = Resolve<T>(asker);
            if (found == null)
            {
                Debug.LogError("GameContext: " + typeof(T).Name + "을(를) 찾지 못했습니다. " +
                               "씬에 배치되어 있는지 확인하세요.", asker);
            }
            return found;
        }

        /// <summary>
        /// 등록을 모두 비웁니다. 씬을 다시 불러들일 때처럼 상태가 꼬였을 때 씁니다.
        /// </summary>
        public static void Clear()
        {
            services.Clear();
            mainCamera = null;
        }

        // --- Private Methods ---

        /// <summary>
        /// 플레이 모드에 들어갈 때 정적 상태를 비웁니다.
        /// 에디터에서 도메인 리로드를 꺼 두면 static 값이 지난 실행에서 그대로 남기 때문입니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            services.Clear();
            mainCamera = null;
        }
    }
}
