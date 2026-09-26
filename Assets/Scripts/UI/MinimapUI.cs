using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MRD.UI
{
    /// <summary>
    /// 화면 오른쪽 아래에 뜨는 미니맵. 실제 월드와 같은 바닥색(웨이브 지역 초록/레이드 장소 보라)과
    /// 순찰 경로 테두리를 실제 비율 그대로(가로/세로 같은 배율) 보여주고, 지금 화면에 실제로 보이는
    /// 범위를 흰색 선 사각형으로 표시한다. 미니맵을 클릭하면 그 지점이 화면 가운데로 오도록 카메라를
    /// 이동시킨다. 전부 코드로 생성한다 (GameHud와 같은 방식).
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        private const float PanelWidth = 300f;
        private const float PanelHeight = 100f;
        private const float Margin = 16f;
        private const float WorldMargin = 6f; // 순찰 경로 바깥 여백(강화 건물 등)까지 넉넉히 보여주기 위한 값

        private static readonly Color GroundColor = new Color(0.22f, 0.28f, 0.22f); // GameBootstrap.CreateGround와 동일
        private static readonly Color RaidGroundColor = new Color(0.16f, 0.12f, 0.2f); // GameBootstrap.CreateFlatGround와 동일
        private static readonly Color PathColor = new Color(1f, 0.9f, 0.15f); // DrawPatrolPathVisual과 동일

        private Camera _targetCamera;
        private Vector2 _worldMin; // (x, z)
        private Vector2 _worldMax;
        private Vector2 _worldCenter;
        private float _scale; // 가로/세로 동일하게 적용하는 배율 (모양이 찌그러지지 않도록)

        private RectTransform _panelRect;
        private readonly RectTransform[] _viewBoxLines = new RectTransform[4];

        /// <summary>raidOffsetX만큼 떨어진 곳에 웨이브 지역과 같은 크기의 레이드 장소가 있다는 전제로,
        /// 그 레이드 장소까지 전부 포함하도록 미니맵이 보여줄 월드 범위를 잡는다.</summary>
        public void Initialize(Camera targetCamera, float centerX, float centerZ, float halfWidth, float halfDepth, float raidOffsetX)
        {
            _targetCamera = targetCamera;
            _worldMin = new Vector2(centerX - halfWidth - WorldMargin, centerZ - halfDepth - WorldMargin);
            _worldMax = new Vector2(centerX + raidOffsetX + halfWidth + WorldMargin, centerZ + halfDepth + WorldMargin);
            _worldCenter = (_worldMin + _worldMax) * 0.5f;

            float worldWidth = _worldMax.x - _worldMin.x;
            float worldHeight = _worldMax.y - _worldMin.y;
            _scale = Mathf.Min(PanelWidth / worldWidth, PanelHeight / worldHeight);

            EnsureEventSystem();
            var canvas = CreateCanvas();
            BuildPanel(canvas.transform, centerX, centerZ, halfWidth, halfDepth, raidOffsetX);
        }

        private void Update()
        {
            if (_targetCamera == null) return;
            UpdateViewBox();
        }

        // ---- 좌표 변환 (가로/세로 같은 배율만 사용해서 실제 비율을 그대로 유지한다) ----

        private Vector2 WorldToMinimapLocal(Vector3 world) =>
            new Vector2((world.x - _worldCenter.x) * _scale, (world.z - _worldCenter.y) * _scale);

        private Vector2 MinimapLocalToWorld(Vector2 local)
        {
            var world = new Vector2(_worldCenter.x + local.x / _scale, _worldCenter.y + local.y / _scale);
            world.x = Mathf.Clamp(world.x, _worldMin.x, _worldMax.x);
            world.y = Mathf.Clamp(world.y, _worldMin.y, _worldMax.y);
            return world;
        }

        // 카메라가 바라보는 방향과 바닥(Y=0 평면)의 교점 - 카메라가 지금 가운데에 두고 있는 지점.
        private Vector3 GetCameraFocusPoint()
        {
            var ray = new Ray(_targetCamera.transform.position, _targetCamera.transform.forward);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            return groundPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.GetPoint(10000f);
        }

        // 화면 네 귀퉁이에서 쏜 레이가 바닥과 만나는 지점 - 지금 화면에 실제로 보이는 범위의 꼭짓점.
        private Vector3 GetGroundPointForViewport(float viewportX, float viewportY)
        {
            var ray = _targetCamera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            return groundPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.GetPoint(10000f);
        }

        // 카메라가 기울어져 있으면 네 귀퉁이의 바닥 교점은 원근 때문에 사다리꼴(심하면 교차된 모양)이 된다.
        // 미니맵에는 보기 쉽게 그 점들을 감싸는 축(X/Z)에 나란한 사각형으로 대신 그린다.
        private void UpdateViewBox()
        {
            var corners = new[]
            {
                GetGroundPointForViewport(0f, 0f),
                GetGroundPointForViewport(0f, 1f),
                GetGroundPointForViewport(1f, 1f),
                GetGroundPointForViewport(1f, 0f),
            };

            float minX = corners[0].x, maxX = corners[0].x;
            float minZ = corners[0].z, maxZ = corners[0].z;
            for (int i = 1; i < corners.Length; i++)
            {
                minX = Mathf.Min(minX, corners[i].x);
                maxX = Mathf.Max(maxX, corners[i].x);
                minZ = Mathf.Min(minZ, corners[i].z);
                maxZ = Mathf.Max(maxZ, corners[i].z);
            }

            var rectCorners = new[]
            {
                WorldToMinimapLocal(new Vector3(minX, 0f, minZ)),
                WorldToMinimapLocal(new Vector3(minX, 0f, maxZ)),
                WorldToMinimapLocal(new Vector3(maxX, 0f, maxZ)),
                WorldToMinimapLocal(new Vector3(maxX, 0f, minZ)),
            };

            for (int i = 0; i < 4; i++)
                PositionLineSegment(_viewBoxLines[i], rectCorners[i], rectCorners[(i + 1) % 4]);
        }

        private void HandleMinimapClicked(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_panelRect, eventData.position, eventData.pressEventCamera, out var localPoint))
                return;

            var targetWorldXZ = MinimapLocalToWorld(localPoint);
            var focusPoint = GetCameraFocusPoint();
            var delta = new Vector3(targetWorldXZ.x - focusPoint.x, 0f, targetWorldXZ.y - focusPoint.z);
            _targetCamera.transform.position += delta;
        }

        // ---- UI 구성 (전부 코드로 생성) ----

        private void BuildPanel(Transform parent, float centerX, float centerZ, float halfWidth, float halfDepth, float raidOffsetX)
        {
            var panelGo = new GameObject("MinimapPanel");
            panelGo.transform.SetParent(parent, false);

            _panelRect = panelGo.AddComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(1f, 0f);
            _panelRect.anchorMax = new Vector2(1f, 0f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.anchoredPosition = new Vector2(-Margin - PanelWidth / 2f, Margin + PanelHeight / 2f);
            _panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0.02f, 0.02f, 0.03f, 0.75f); // 두 지역 바깥의 빈 공간

            var clickArea = panelGo.AddComponent<MinimapClickArea>();
            clickArea.OnClicked += HandleMinimapClicked;

            // 웨이브 지역 바닥 (실제 CreateGround와 같은 초록색) + 그 위에 순찰 경로 테두리(노란색).
            var groundSize = new Vector2(halfWidth * 2f * _scale, halfDepth * 2f * _scale);
            CreateFilledRect(panelGo.transform, GroundColor, WorldToMinimapLocal(new Vector3(centerX, 0f, centerZ)), groundSize);

            var pathCorners = new[]
            {
                WorldToMinimapLocal(new Vector3(centerX - halfWidth, 0f, centerZ - halfDepth)),
                WorldToMinimapLocal(new Vector3(centerX - halfWidth, 0f, centerZ + halfDepth)),
                WorldToMinimapLocal(new Vector3(centerX + halfWidth, 0f, centerZ + halfDepth)),
                WorldToMinimapLocal(new Vector3(centerX + halfWidth, 0f, centerZ - halfDepth)),
            };
            for (int i = 0; i < 4; i++)
                PositionLineSegment(CreateLine(panelGo.transform, PathColor, 1.5f), pathCorners[i], pathCorners[(i + 1) % 4]);

            // 레이드 장소 바닥 (실제 CreateFlatGround와 같은 보라색, 웨이브 지역에서 +raidOffsetX만큼 떨어진 곳, 같은 크기).
            float raidCenterX = centerX + raidOffsetX;
            CreateFilledRect(panelGo.transform, RaidGroundColor, WorldToMinimapLocal(new Vector3(raidCenterX, 0f, centerZ)), groundSize);

            // 지금 화면에 보이는 범위를 나타내는 흰색 선 네 개 (매 프레임 위치를 다시 계산해서 갱신한다).
            for (int i = 0; i < 4; i++)
                _viewBoxLines[i] = CreateLine(panelGo.transform, Color.white, 2f);
        }

        private static RectTransform CreateFilledRect(Transform parent, Color color, Vector2 center, Vector2 size)
        {
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return rect;
        }

        private static RectTransform CreateLine(Transform parent, Color color, float thickness)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, thickness);

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false; // 클릭 판정을 가리지 않도록

            return rect;
        }

        // 패널 로컬 좌표계에서 a->b를 잇는 얇고 회전된 사각형으로 선을 표현한다
        // (LineRenderer는 카메라 각도에 따라 끊겨 보이는 문제가 있어 프로젝트 전반에서 피하는 방식과 동일).
        private static void PositionLineSegment(RectTransform line, Vector2 a, Vector2 b)
        {
            Vector2 diff = b - a;
            float length = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            line.anchoredPosition = a + diff * 0.5f;
            line.sizeDelta = new Vector2(length, line.sizeDelta.y);
            line.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static Canvas CreateCanvas()
        {
            var canvasGo = new GameObject("Minimap_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            canvasGo.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
