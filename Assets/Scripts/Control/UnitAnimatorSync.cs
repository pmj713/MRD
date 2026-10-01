using UnityEngine;

namespace MRD.Control
{
    /// <summary>이동 중일 때만 걷기 애니메이션이 재생되도록 Animator의 IsMoving 파라미터를 매 프레임 동기화한다.
    /// visualPrefab에 Animator가 없는 유닛(아직 리깅 안 된 정적 모델)에는 아무 효과가 없다.</summary>
    public class UnitAnimatorSync : MonoBehaviour
    {
        private Animator _animator;
        private UnitMover _mover;
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        public void Initialize(Animator animator, UnitMover mover)
        {
            _animator = animator;
            _mover = mover;
        }

        private void Update()
        {
            if (_animator == null || _mover == null) return;
            _animator.SetBool(IsMovingHash, _mover.IsMoving);
        }
    }
}
