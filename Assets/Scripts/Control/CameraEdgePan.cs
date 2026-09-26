using UnityEngine;

namespace MRD.Control
{
    /// <summary>
    /// 워크래프트3처럼 마우스 커서가 화면 가장자리에 닿거나 키보드 방향키를 누르면 그 방향으로
    /// 카메라를 이동시킨다. 카메라의 현재 각도를 유지한 채 바닥(X-Z 평면) 위를 미끄러지듯 움직이며,
    /// 이동 범위에는 제한을 두지 않는다.
    /// </summary>
    public class CameraEdgePan : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float edgeThicknessPixels = 20f;
        [SerializeField] private float panSpeed = 30f;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void Update()
        {
            if (targetCamera == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(targetCamera.transform.right, Vector3.up).normalized;

            Vector3 move = Vector3.zero;

            Vector2 mouse = Input.mousePosition;
            if (IsInsideScreen(mouse)) // 에디터에서 커서가 게임 뷰 밖(인스펙터 등)에 있을 때는 무시
            {
                if (mouse.x <= edgeThicknessPixels) move -= right;
                else if (mouse.x >= Screen.width - edgeThicknessPixels) move += right;

                if (mouse.y <= edgeThicknessPixels) move -= forward;
                else if (mouse.y >= Screen.height - edgeThicknessPixels) move += forward;
            }

            if (Input.GetKey(KeyCode.LeftArrow)) move -= right;
            if (Input.GetKey(KeyCode.RightArrow)) move += right;
            if (Input.GetKey(KeyCode.DownArrow)) move -= forward;
            if (Input.GetKey(KeyCode.UpArrow)) move += forward;

            if (move.sqrMagnitude <= 0f) return;

            targetCamera.transform.position += move.normalized * panSpeed * Time.deltaTime;
        }

        private static bool IsInsideScreen(Vector2 mouse) =>
            mouse.x >= 0f && mouse.x <= Screen.width && mouse.y >= 0f && mouse.y <= Screen.height;
    }
}
