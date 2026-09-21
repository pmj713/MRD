using UnityEngine;

namespace MRD.Control
{
    /// <summary>
    /// 마우스 휠을 올리면 줌인(카메라가 정면 방향으로 전진), 내리면 줌아웃(후진)한다.
    /// 카메라의 현재 기울기는 그대로 유지한 채 정면 축을 따라서만 미끄러지듯 움직인다.
    /// </summary>
    public class CameraZoom : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float zoomSpeed = 6f;
        [SerializeField] private float minHeight = 4f;
        [SerializeField] private float maxHeight = 22f;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void Update()
        {
            if (targetCamera == null) return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f)) return;

            float currentHeight = targetCamera.transform.position.y;
            if (scroll > 0f && currentHeight <= minHeight) return; // 이미 최대로 줌인됨
            if (scroll < 0f && currentHeight >= maxHeight) return; // 이미 최대로 줌아웃됨

            Vector3 newPos = targetCamera.transform.position + targetCamera.transform.forward * (scroll * zoomSpeed);
            newPos.y = Mathf.Clamp(newPos.y, minHeight, maxHeight);
            targetCamera.transform.position = newPos;
        }
    }
}
