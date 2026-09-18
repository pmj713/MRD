using UnityEngine;

namespace MRD.Control
{
    /// <summary>지정한 위치로 직선 이동한다. 장애물 회피나 경로탐색은 없다 (평지 이동 전제).</summary>
    public class UnitMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3f;

        private Vector3? _destination;

        public bool IsMoving => _destination.HasValue;

        public void MoveTo(Vector3 destination) => _destination = destination;

        public void Stop() => _destination = null;

        private void Update() => Tick(Time.deltaTime);

        /// <summary>실제 이동 계산. Update()가 매 프레임 호출하며, 테스트에서 직접 호출해 시간을 앞당길 수도 있다.</summary>
        public void Tick(float deltaTime)
        {
            if (!_destination.HasValue) return;

            var next = Vector3.MoveTowards(transform.position, _destination.Value, moveSpeed * deltaTime);
            transform.position = next;

            if ((next - _destination.Value).sqrMagnitude < 0.0001f)
                _destination = null;
        }
    }
}
