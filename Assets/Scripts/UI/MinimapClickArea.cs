using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MRD.UI
{
    /// <summary>미니맵 배경 클릭을 감지해서 전달하는 작은 도우미 컴포넌트 (BuildingClickTarget과 같은 역할, UI 버전).</summary>
    public class MinimapClickArea : MonoBehaviour, IPointerClickHandler
    {
        public event Action<PointerEventData> OnClicked;

        public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(eventData);
    }
}
