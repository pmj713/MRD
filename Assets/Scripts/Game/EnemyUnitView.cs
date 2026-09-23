using UnityEngine;
using MRD.Wave;

namespace MRD.Game
{
    /// <summary>
    /// 몬스터가 정해진 다각형 경로(정사각형 루프)를 계속 순찰하도록 만드는 임시 시각 표현.
    /// 실제 경로/웨이포인트 시스템이 생기면 이 스크립트를 대체하면 된다.
    /// </summary>
    public class EnemyUnitView : MonoBehaviour
    {
        private EnemyUnit _enemy;
        private Vector3[] _corners;
        private float _speed;
        private float _perimeter;
        private float _distanceTraveled;

        public void Setup(EnemyUnit enemy, Vector3[] pathCorners, float speed)
        {
            _enemy = enemy;
            _corners = pathCorners;
            _speed = Mathf.Max(0.01f, speed);
            _perimeter = ComputePerimeter(pathCorners);
            _distanceTraveled = 0f; // 항상 같은 스폰 지점(경로의 첫 꼭짓점)에서 출발한다

            transform.position = GetPointAtDistance(_distanceTraveled);
        }

        private void Update()
        {
            if (_enemy == null || _enemy.IsDead) return;

            _distanceTraveled += _speed * Time.deltaTime;
            if (_perimeter > 0f)
                _distanceTraveled %= _perimeter;

            var next = GetPointAtDistance(_distanceTraveled);
            FaceDirection(next - transform.position);
            transform.position = next;
        }

        // 이동 방향(바닥 기준, Y 성분 제외)을 바라보게 회전시킨다.
        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(direction);
        }

        private Vector3 GetPointAtDistance(float distance)
        {
            if (_corners == null || _corners.Length < 2) return transform.position;

            float remaining = distance;
            for (int i = 0; i < _corners.Length; i++)
            {
                Vector3 a = _corners[i];
                Vector3 b = _corners[(i + 1) % _corners.Length];
                float segmentLength = Vector3.Distance(a, b);

                if (remaining <= segmentLength)
                    return Vector3.Lerp(a, b, segmentLength > 0f ? remaining / segmentLength : 0f);

                remaining -= segmentLength;
            }

            return _corners[0];
        }

        private static float ComputePerimeter(Vector3[] corners)
        {
            if (corners == null || corners.Length < 2) return 0f;

            float total = 0f;
            for (int i = 0; i < corners.Length; i++)
                total += Vector3.Distance(corners[i], corners[(i + 1) % corners.Length]);
            return total;
        }
    }
}
