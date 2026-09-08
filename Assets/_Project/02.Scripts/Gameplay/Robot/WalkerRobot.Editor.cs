// 이 파일은 <b>에디터에서만</b> 컴파일됩니다. 출시 빌드에는 한 줄도 들어가지 않습니다.
//
// 여기 있는 것은 전부 "재생하지 않고 리그를 만드는 일"입니다 — 관절 하나를 끌어 옮기면 나머지가
// 따라오게 하고(SyncRig), 재생 없이도 제 자세로 서 있게 하고(PoseInEditor), 발자리와 작업 반경을
// 씬에 그립니다(OnDrawGizmos). <b>게임이 도는 데는 하나도 필요하지 않습니다.</b>
//
// <b>왜 갈랐는가.</b> WalkerRobot 은 이 저장소에서 가장 큰 파일(2,041줄)이었고, 그중 290줄이
// 빌드에 실려 가면서도 빌드에서는 결코 실행되지 않는 코드였습니다 — 출시본에서는
// Application.isPlaying 이 언제나 참이라 PoseInEditor 로 가는 분기 자체가 없습니다.
// 이 프로젝트는 이미 에디터 툴을 CarDrive.Editor 어셈블리로 떼어 출시본에서 걷어냈는데,
// 이 파일만 그 원칙 밖에 있었습니다.
//
// <b>왜 어셈블리가 아니라 partial 인가.</b> 여기 있는 코드는 WalkerRobot 의 private 상태
// (legs · ready · standHeight · strideScale …)를 그대로 씁니다. 어셈블리를 넘으면 그 전부를
// 열어야 하므로, 접근을 넓히는 대신 같은 클래스로 두고 <b>파일만</b> 갈랐습니다.
#if UNITY_EDITOR

using UnityEngine;

namespace CarDrive.Gameplay
{
    public partial class WalkerRobot
    {
        // --- Private Member Variables : 편집 중에만 쓰는 상태 ---

        /// <summary>인스펙터에서 무언가 바뀌어 리그를 다시 읽어야 하는지입니다. 편집 중에만 씁니다.</summary>
        private bool editorRigDirty;

        /// <summary>지난 그리기 때의 넓적마디 길이입니다. 무엇이 바뀌었는지 알아내는 데 씁니다.</summary>
        private float[] syncUpper;

        /// <summary>지난 그리기 때의 종아리마디 길이입니다.</summary>
        private float[] syncLower;

        /// <summary>지난 그리기 때의 고관절 자리(몸통 기준)입니다.</summary>
        private Vector3[] syncHip;

        /// <summary>지난 그리기 때의 발자리입니다. 좌우 짝을 찾는 기준이기도 합니다.</summary>
        private Vector3[] syncHome;

        /// <summary>기억해 둔 모양이 쓸 만한지입니다. 처음 불러왔을 때는 비교할 것이 없습니다.</summary>
        private bool syncReady;

        /// <summary>리그가 달라졌다고 볼 최소 차이(m)입니다. 부동소수 잡음보다 크고 손으로 옮긴 것보다 작습니다.</summary>
        private const float RigEpsilon = 1e-4f;

        /// <summary>좌우 짝으로 볼 발자리 차이(m)입니다.</summary>
        private const float MirrorTolerance = 0.05f;

        // --- Unity Methods ---

        /// <summary>인스펙터에서 값이 바뀌면 다음 그리기 때 리그를 다시 읽습니다.</summary>
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            editorRigDirty = true;
        }

        // --- Private Methods : 리그 편집 ---

        /// <summary>
        /// <b>재생하지 않고도</b> 리그가 제 자세로 서 있게 합니다.
        ///
        /// 관절 하나를 끌어 옮기면 나머지가 따라오게 하려는 것입니다. 그러려면 두 가지가 필요합니다.
        ///  1. 옮긴 결과를 <b>마디 길이로 받아들이기</b> (<see cref="WalkerLeg.MeasureFromRig"/>)
        ///  2. 그 길이로 <b>IK 를 다시 풀어</b> 아래 마디들을 제자리에 놓기
        /// 그래서 무릎을 당기면 종아리마디가 따라 늘고, 고관절을 옮기면 다리 전체가 따라옵니다.
        ///
        /// <b>스프링은 돌리지 않습니다.</b> 2차 시스템은 시간이 지나야 목표에 닿는데 편집 중에는
        /// 시간이 흐르지 않습니다. 대신 몸통을 곧장 서 있는 높이에 놓습니다. 지면 탐침도 하지 않습니다 —
        /// 편집 중에는 발밑에 지형이 없을 수도 있고, 매 그리기마다 레이를 쏘면 씬이 무거워집니다.
        ///
        /// 계산이 언제나 같은 답을 내므로, 한 번 자리를 잡은 뒤에는 같은 값을 다시 쓸 뿐입니다.
        /// 그래서 가만히 두면 씬이 계속 더러워지지 않습니다.
        /// </summary>
        private void PoseInEditor()
        {
            if (editorRigDirty)
            {
                editorRigDirty = false;
                Awake();
            }

            if (!ready || body == null) return;

            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null || !legs[i].IsWired) return;
            }

            body.SetPositionAndRotation(transform.TransformPoint(new Vector3(0f, standHeight, 0f)),
                transform.rotation * Quaternion.Euler(standPitch, 0f, 0f));

            for (int i = 0; i < legs.Length; i++) legs[i].MeasureFromRig();

            SyncRig();

            for (int i = 0; i < legs.Length; i++)
            {
                legs[i].Initialize(transform, strideScale);
                legs[i].PlaceAt(transform.TransformPoint(legs[i].homeOffset), transform.up);
                legs[i].Solve();
            }
        }

        /// <summary>
        /// <b>고친 다리 하나를 찾아 나머지에 옮깁니다.</b>
        ///
        /// 다리를 넷 만들면서 같은 값을 네 번 넣는 것은 실수가 나기 쉽고, 한 번 어긋나면
        /// 걸음이 미묘하게 절뚝이는데 눈으로는 어느 다리가 다른지 알기 어렵습니다.
        /// 그래서 <b>하나만 고치면 나머지가 따라오게</b> 합니다.
        ///
        /// <b>누가 본인지는 달라진 것으로 알아냅니다.</b> 지난 그리기 때의 모양을 기억해 두었다가
        /// 이번에 달라진 다리를 찾습니다. 그 다리가 방금 손댄 다리입니다. 어느 다리를 고쳐도
        /// 되므로 "0번 다리만 고치세요" 같은 규칙을 외울 필요가 없습니다.
        ///
        /// <b>여러 개가 한꺼번에 달라졌으면 손대지 않습니다.</b> 씬을 막 열었거나 프리팹을 다시
        /// 구운 직후가 그렇습니다. 그때 옮기면 일부러 만든 비대칭을 뭉개 버립니다.
        /// 기억만 새로 해 두고 넘어갑니다.
        /// </summary>
        private void SyncRig()
        {
            if (rigSync == WalkerRigSync.Off) return;

            int count = legs.Length;

            if (syncUpper == null || syncUpper.Length != count)
            {
                syncUpper = new float[count];
                syncLower = new float[count];
                syncHip = new Vector3[count];
                syncHome = new Vector3[count];
                syncReady = false;
            }

            if (!syncReady)
            {
                CacheRig();
                syncReady = true;
                return;
            }

            int master = -1;
            int changed = 0;

            for (int i = 0; i < count; i++)
            {
                if (!LegChanged(i)) continue;

                master = i;
                changed++;
            }

            if (changed != 1)
            {
                if (changed > 1) CacheRig();
                return;
            }

            if (rigSync == WalkerRigSync.AllLegs)
            {
                for (int i = 0; i < count; i++)
                {
                    if (i == master) continue;

                    legs[i].CopyShapeFrom(legs[master]);
                    MarkEdited(legs[i]);
                }
            }

            int partner = MirrorPartner(master);

            if (partner >= 0)
            {
                legs[partner].CopyShapeFrom(legs[master]);
                MirrorInto(master, partner);
            }

            CacheRig();
        }

        /// <summary>이 다리가 지난 그리기 때와 달라졌는지입니다.</summary>
        /// <param name="leg">다리 번호</param>
        /// <returns>달라졌으면 true</returns>
        private bool LegChanged(int leg)
        {
            if (Mathf.Abs(legs[leg].upperLength - syncUpper[leg]) > RigEpsilon) return true;
            if (Mathf.Abs(legs[leg].lowerLength - syncLower[leg]) > RigEpsilon) return true;
            if ((legs[leg].transform.localPosition - syncHip[leg]).sqrMagnitude > RigEpsilon * RigEpsilon) return true;

            return (legs[leg].homeOffset - syncHome[leg]).sqrMagnitude > RigEpsilon * RigEpsilon;
        }

        /// <summary>지금 모양을 기억해 둡니다.</summary>
        private void CacheRig()
        {
            for (int i = 0; i < legs.Length; i++)
            {
                syncUpper[i] = legs[i].upperLength;
                syncLower[i] = legs[i].lowerLength;
                syncHip[i] = legs[i].transform.localPosition;
                syncHome[i] = legs[i].homeOffset;
            }
        }

        /// <summary>
        /// 이 다리의 <b>좌우 짝</b>을 찾습니다. 발자리가 Z 는 같고 X 는 부호만 다른 다리입니다.
        ///
        /// <b>바뀌기 전</b>의 발자리로 찾습니다. 본은 방금 옮겨졌으므로 지금 값으로 찾으면
        /// 짝을 놓칩니다. 가운데에 있는 다리(스트라이더의 뒷다리)는 짝이 없습니다.
        /// </summary>
        /// <param name="master">본이 되는 다리 번호</param>
        /// <returns>짝의 번호. 없으면 -1</returns>
        private int MirrorPartner(int master)
        {
            Vector3 was = syncHome[master];

            if (Mathf.Abs(was.x) < MirrorTolerance) return -1;

            for (int i = 0; i < legs.Length; i++)
            {
                if (i == master) continue;
                if (Mathf.Abs(syncHome[i].x + was.x) > MirrorTolerance) continue;
                if (Mathf.Abs(syncHome[i].z - was.z) > MirrorTolerance) continue;

                return i;
            }

            return -1;
        }

        /// <summary>본이 되는 다리의 자리를 짝에게 X 대칭으로 옮깁니다.</summary>
        /// <param name="master">본이 되는 다리 번호</param>
        /// <param name="partner">짝의 번호</param>
        private void MirrorInto(int master, int partner)
        {
            Vector3 hip = legs[master].transform.localPosition;
            Vector3 home = legs[master].homeOffset;
            Vector3 pole = legs[master].kneePole;

            legs[partner].transform.localPosition = new Vector3(-hip.x, hip.y, hip.z);
            legs[partner].homeOffset = new Vector3(-home.x, home.y, home.z);
            legs[partner].kneePole = new Vector3(-pole.x, pole.y, pole.z);

            MarkEdited(legs[partner]);
        }

        /// <summary>편집 중에 고친 값이 저장되도록 표시합니다.</summary>
        /// <param name="leg">표시할 다리</param>
        private static void MarkEdited(WalkerLeg leg)
        {
            if (leg != null) UnityEditor.EditorUtility.SetDirty(leg);
        }

        // --- Private Methods : 기즈모 ---

        /// <summary>발 자리 · 작업 반경 · 지지 다각형을 그립니다.</summary>
        private void OnDrawGizmos()
        {
            if (!drawGizmos || legs == null) return;

            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;

                Vector3 home = transform.TransformPoint(legs[i].homeOffset);

                // 발이 서 있고 싶은 자리 — 노랑
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
                Gizmos.DrawWireSphere(home, 0.06f);

                if (!ready || !Application.isPlaying) continue;

                // 발이 놓일 수 있는 범위 — 주황 원. 이 밖으로 나가면 다리가 뻗은 채 끌립니다.
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
                DrawCircle(home, legs[i].StrideRadius);

                // 발이 지금 있는 자리 — 딛고 있으면 초록, 떼고 있으면 빨강
                Gizmos.color = legs[i].IsStepping ? new Color(1f, 0.3f, 0.2f) : new Color(0.3f, 1f, 0.4f);
                Gizmos.DrawSphere(legs[i].FootPosition, 0.05f);
                Gizmos.DrawLine(legs[i].HipPosition, legs[i].FootPosition);

                Gizmos.color = Color.white;
                Gizmos.DrawLine(legs[i].FootPosition, stepTargets[i]);
            }

            if (!ready || !Application.isPlaying || legs.Length < 3) return;

            // 지지 다각형 — 발을 둘레 순서로 이은 도형
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);

            for (int i = 0; i < ringOrder.Length; i++)
            {
                Vector3 a = legs[ringOrder[i]].FootPosition;
                Vector3 b = legs[ringOrder[(i + 1) % ringOrder.Length]].FootPosition;

                Gizmos.DrawLine(a, b);
            }
        }

        /// <summary>수평 원을 그립니다. <see cref="Gizmos"/> 에는 원이 없어 선분으로 잇습니다.</summary>
        /// <param name="center">원의 중심</param>
        /// <param name="radius">반지름</param>
        private static void DrawCircle(Vector3 center, float radius)
        {
            const int Segments = 24;

            Vector3 previous = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= Segments; i++)
            {
                float angle = i * (2f * Mathf.PI / Segments);
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}

#endif
