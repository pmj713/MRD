using System.Collections.Generic;
using UnityEngine;
using MRD.Battle;
using MRD.Control;

namespace MRD.Game
{
    /// <summary>
    /// 지정한 위치 근처에 유닛이 다가오면 목적지로 순간이동시키는 포탈.
    /// 입구 포탈(웨이브 지역 -> 레이드 장소)과 귀환 포탈(레이드 장소 -> 웨이브 지역) 양쪽에 똑같이 쓴다.
    /// 콜라이더 기반 트리거가 아니라(이 프로젝트 유닛엔 콜라이더가 없다) 거리 비교로 접촉을 판정한다.
    /// </summary>
    public class RaidPortal : MonoBehaviour
    {
        [SerializeField] private float triggerRadius = 1.5f;

        private Vector3 _teleportTarget;
        private Camera _followCamera;

        /// <summary>목적지와, 순간이동 시 함께 시점을 옮겨줄 카메라(선택)를 지정한다.</summary>
        public void Setup(Vector3 teleportTarget, Camera followCamera = null)
        {
            _teleportTarget = teleportTarget;
            _followCamera = followCamera;
        }

        /// <summary>주어진 유닛들 중 포탈 반경 안에 들어온 유닛을 목적지로 순간이동시킨다.</summary>
        public void Tick(IReadOnlyList<BattleUnit> units)
        {
            float radiusSqr = triggerRadius * triggerRadius;
            bool teleportedAny = false;

            foreach (var unit in units)
            {
                if (unit == null) continue;
                if ((unit.Position - transform.position).sqrMagnitude > radiusSqr) continue;

                unit.transform.position = _teleportTarget;
                unit.GetComponent<UnitMover>()?.Stop(); // 이동 명령이 남아있으면 포탈 위치로 계속 되돌아오려 하므로 멈춘다
                teleportedAny = true;
            }

            // 유닛이 이동한 만큼 카메라도 그대로 따라가서, 포탈을 넘어간 곳을 바로 볼 수 있게 한다.
            if (teleportedAny && _followCamera != null)
                _followCamera.transform.position += _teleportTarget - transform.position;
        }
    }
}
