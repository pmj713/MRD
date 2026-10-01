using UnityEngine;

namespace MRD.Control
{
    /// <summary>지정한 위치로 이동한다. 장애물 회피나 경로탐색은 없다 (평지 이동 전제).</summary>
    public class UnitMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3f;

        private Vector3? _destination;
        private float _snakeTurnSpeed;
        private float _snakeAcceleration;
        private float _currentSpeed;

        public bool IsMoving => _destination.HasValue;

        public void MoveTo(Vector3 destination) => _destination = destination;

        public void Stop()
        {
            _destination = null;
            _currentSpeed = 0f;
        }

        /// <summary>뱀 모델만 완만한 선회와 가감속을 사용한다. 다른 유닛의 이동은 그대로다.</summary>
        public void EnableSnakeMovement(float turnSpeed = 220f, float acceleration = 12f)
        {
            _snakeTurnSpeed = Mathf.Max(1f, turnSpeed);
            _snakeAcceleration = Mathf.Max(0.1f, acceleration);
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>Update()와 검증 코드에서 같은 이동 계산을 사용한다.</summary>
        public void Tick(float deltaTime)
        {
            if (!_destination.HasValue || deltaTime <= 0f) return;

            if (_snakeTurnSpeed > 0f)
            {
                TickSnake(deltaTime);
                return;
            }

            var next = Vector3.MoveTowards(transform.position, _destination.Value, moveSpeed * deltaTime);
            FaceDirection(next - transform.position);
            transform.position = next;

            if ((next - _destination.Value).sqrMagnitude < 0.0001f)
                _destination = null;
        }

        private void TickSnake(float deltaTime)
        {
            var offset = _destination.Value - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < 0.01f)
            {
                transform.position = _destination.Value;
                Stop();
                return;
            }

            var direction = offset / distance;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), _snakeTurnSpeed * deltaTime);

            float alignment = Vector3.Dot(transform.forward, direction);
            float brakingSpeed = Mathf.Sqrt(2f * _snakeAcceleration * distance);
            float targetSpeed = Mathf.Min(moveSpeed, brakingSpeed);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, _snakeAcceleration * deltaTime);

            // U턴 때도 앞으로 전진하여 몸을 꺾지 않는다. 가까운 목표는 회전 속도보다
            // 느리게 돌아가도록 이동량을 줄여 목표 주변을 계속 도는 현상을 막는다.
            float turnLimit = alignment < 0.95f
                ? distance * Mathf.Min(1f, _snakeTurnSpeed * Mathf.Deg2Rad * deltaTime * 0.5f)
                : distance;
            float step = Mathf.Min(_currentSpeed * deltaTime, turnLimit);
            var next = transform.position + transform.forward * step;
            next.y = Mathf.MoveTowards(next.y, _destination.Value.y, moveSpeed * deltaTime);
            transform.position = next;
        }

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
