using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRD.Control
{
    /// <summary>
    /// 워크래프트3식 조작: 좌클릭=단일 선택, 좌클릭 드래그=범위 선택, 우클릭=선택된 유닛 전체 이동 명령.
    /// 이동은 전투 중에도 언제든 가능하고, 슬롯에 묶이지 않는 자유 이동이다.
    /// </summary>
    public class SelectionController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float clickMaxPixelRadius = 24f;
        [SerializeField] private float dragThresholdPixels = 6f;
        [SerializeField] private float moveFormationSpacing = 0.7f;

        private static readonly List<Selectable> AllSelectables = new List<Selectable>();
        private readonly List<Selectable> _selected = new List<Selectable>();

        private Vector2 _dragStart;
        private bool _isMouseDown;
        private bool _isDragging;

        /// <summary>선택 내용이 바뀔 때마다 발생 (UI가 선택된 유닛 정보를 보여주는 용도 등).</summary>
        public event Action<IReadOnlyList<Selectable>> OnSelectionChanged;

        public static void Register(Selectable selectable) => AllSelectables.Add(selectable);
        public static void Unregister(Selectable selectable) => AllSelectables.Remove(selectable);

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void Update()
        {
            HandleLeftButton();
            HandleRightButton();
        }

        private void HandleLeftButton()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _dragStart = Input.mousePosition;
                _isMouseDown = true;
                _isDragging = false;
            }
            else if (_isMouseDown && Input.GetMouseButton(0))
            {
                if (!_isDragging && Vector2.Distance(_dragStart, Input.mousePosition) >= dragThresholdPixels)
                    _isDragging = true;
            }
            else if (_isMouseDown && Input.GetMouseButtonUp(0))
            {
                Vector2 end = Input.mousePosition;

                if (_isDragging)
                {
                    var rect = BuildRect(_dragStart, end);
                    SetSelection(SelectionMath.FindInRect(AllSelectables, targetCamera, rect));
                }
                else
                {
                    var nearest = SelectionMath.FindNearest(AllSelectables, targetCamera, end, clickMaxPixelRadius);
                    SetSelection(nearest != null ? new List<Selectable> { nearest } : new List<Selectable>());
                }

                _isMouseDown = false;
                _isDragging = false;
            }
        }

        private void HandleRightButton()
        {
            if (!Input.GetMouseButtonDown(1) || _selected.Count == 0) return;

            Vector3 worldPoint = ScreenToWorldPoint(Input.mousePosition);
            IssueMoveCommand(worldPoint);
        }

        // 선택된 유닛들이 한 점에 완전히 겹치지 않도록 목표 지점 주변 바닥(X-Z 평면)에 격자 형태로 펼쳐서 보낸다.
        private void IssueMoveCommand(Vector3 center)
        {
            int count = _selected.Count;
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / columns));

            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                float offsetX = (col - (columns - 1) / 2f) * moveFormationSpacing;
                float offsetZ = (row - (rows - 1) / 2f) * moveFormationSpacing;

                var mover = _selected[i].GetComponent<UnitMover>();
                mover?.MoveTo(center + new Vector3(offsetX, 0f, offsetZ));
            }
        }

        // 카메라 각도와 상관없이 항상 바닥(Y=0 평면)과의 교점을 이동 목적지로 삼는다.
        private Vector3 ScreenToWorldPoint(Vector3 screenPos)
        {
            var ray = targetCamera.ScreenPointToRay(screenPos);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
                return ray.GetPoint(distance);

            return targetCamera.transform.position + targetCamera.transform.forward * 10f;
        }

        private void SetSelection(List<Selectable> newSelection)
        {
            foreach (var s in _selected) s.SetSelected(false);
            _selected.Clear();
            _selected.AddRange(newSelection);
            foreach (var s in _selected) s.SetSelected(true);

            OnSelectionChanged?.Invoke(_selected);
        }

        private static Rect BuildRect(Vector2 a, Vector2 b)
        {
            float xMin = Mathf.Min(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            return new Rect(xMin, yMin, Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        // 드래그 중인 선택 사각형을 화면에 표시한다 (정식 UI가 생기기 전까지의 임시 표현).
        private void OnGUI()
        {
            if (!_isDragging) return;

            Vector2 start = ToGuiSpace(_dragStart);
            Vector2 end = ToGuiSpace(Input.mousePosition);
            var rect = new Rect(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y),
                Mathf.Abs(start.x - end.x), Mathf.Abs(start.y - end.y));

            var prevColor = GUI.color;
            GUI.color = new Color(0.3f, 0.9f, 0.3f, 0.25f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.9f, 0.3f, 0.9f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = prevColor;
        }

        // OnGUI는 화면 좌상단이 원점이라, 좌하단이 원점인 마우스 좌표를 변환해줘야 한다.
        private static Vector2 ToGuiSpace(Vector2 screenPos) => new Vector2(screenPos.x, Screen.height - screenPos.y);
    }
}
