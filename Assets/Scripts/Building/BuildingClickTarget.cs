using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MRD.Building
{
    /// <summary>
    /// 건물 큐브의 콜라이더 클릭을 감지해서 전달한다. 강화 패널이 열려 있을 때 그 위의 UI 버튼을
    /// 누르는 클릭까지 건물 클릭으로 잡히지 않도록, 포인터가 UI 위에 있으면 무시한다.
    /// </summary>
    public class BuildingClickTarget : MonoBehaviour
    {
        public event Action OnClicked;

        private void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            OnClicked?.Invoke();
        }
    }
}
