using UnityEngine;

namespace MRD.Control
{
    /// <summary>
    /// 워크래프트3처럼 마우스 커서가 화면 가장자리에 닿으면 그 방향으로 카메라를 이동시킨다.
    /// 카메라의 현재 각도를 유지한 채 바닥(X-Z 평면) 위를 미끄러지듯 움직인다.
    /// </summary>
    public class CameraEdgePan : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float edgeThicknessPixels = 20f;
        [SerializeField] private float panSpeed = 12f;
        [SerializeField] private Vector2 xLimits = new Vector2(-20f, 20f);
        [SerializeField] private Vector2 zLimits = new Vector2(-23f, 15f);

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void Update()
        {
            if (targetCamera == null) return;

            Vector2 mouse = Input.mousePosition;
            if (!IsInsideScreen(mouse)) return; // 에디터에서 커서가 게임 뷰 밖(인스펙터 등)에 있을 때는 무시

            Vector3 forward = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(targetCamera.transform.right, Vector3.up).normalized;

            Vector3 move = Vector3.zero;
            if (mouse.x <= edgeThicknessPixels) move -= right;
            else if (mouse.x >= Screen.width - edgeThicknessPixels) move += right;

            if (mouse.y <= edgeThicknessPixels) move -= forward;
            else if (mouse.y >= Screen.height - edgeThicknessPixels) move += forward;

            if (move.sqrMagnitude <= 0f) return;

            var newPos = targetCamera.transform.position + move.normalized * panSpeed * Time.deltaTime;
            newPos.x = Mathf.Clamp(newPos.x, xLimits.x, xLimits.y);
            newPos.z = Mathf.Clamp(newPos.z, zLimits.x, zLimits.y);
            targetCamera.transform.position = newPos;
        }

        private static bool IsInsideScreen(Vector2 mouse) =>
            mouse.x >= 0f && mouse.x <= Screen.width && mouse.y >= 0f && mouse.y <= Screen.height;
    }
}
