using UnityEngine;
using MRD.Battle;

namespace MRD.Control
{
    /// <summary>
    /// 클릭/드래그로 선택 가능한 아군 유닛 표시자. 선택 판정(SelectionMath)과 입력 처리(SelectionController)는
    /// 이 컴포넌트를 활성화된 동안 자동으로 등록/해제되는 후보 목록을 대상으로 동작한다.
    /// </summary>
    public class Selectable : MonoBehaviour
    {
        public BattleUnit Unit { get; private set; }
        public bool IsSelected { get; private set; }

        private SpriteRenderer _visualRenderer;
        private Color _baseColor;

        public void Initialize(BattleUnit unit, SpriteRenderer visualRenderer)
        {
            Unit = unit;
            _visualRenderer = visualRenderer;
            _baseColor = _visualRenderer != null ? _visualRenderer.color : Color.white;
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (_visualRenderer == null) return;

            _visualRenderer.color = selected ? Color.Lerp(_baseColor, Color.white, 0.6f) : _baseColor;
        }

        private void OnEnable() => SelectionController.Register(this);
        private void OnDisable() => SelectionController.Unregister(this);
    }
}
