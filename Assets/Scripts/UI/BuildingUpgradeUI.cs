using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MRD.Data;
using MRD.Game;

namespace MRD.UI
{
    /// <summary>
    /// 건물을 클릭했을 때 뜨는 강화 패널. 등급(노말~히든)별로 한 줄씩 보여주고, 각 줄의 버튼으로
    /// 그 등급 유닛 전체의 공격력을 골드로 강화한다 (한 등급당 최대 MRD.Building.Building.MaxUpgradeLevel번).
    /// 전부 코드로 생성한다 (정식 UI가 생기기 전까지의 임시 화면 - GameHud와 같은 방식).
    /// MRD.Building은 네임스페이스 이름과 클래스 이름이 같아서(MRD.Building.Building),
    /// using으로 가져오면 상위 네임스페이스 MRD의 멤버인 네임스페이스 쪽으로 먼저 해석되어 버린다.
    /// 그래서 이 파일에서는 항상 완전한 이름(MRD.Building.Building)으로만 참조한다.
    /// </summary>
    public class BuildingUpgradeUI : MonoBehaviour
    {
        private static readonly Dictionary<Rarity, string> GradeNames = new Dictionary<Rarity, string>
        {
            { Rarity.Normal, "노말" },
            { Rarity.Magic, "매직" },
            { Rarity.Rare, "레어" },
            { Rarity.Unique, "유니크" },
            { Rarity.Legend, "전설" },
            { Rarity.Hidden, "히든" },
        };

        private class GradeRow
        {
            public Rarity Grade;
            public Text InfoText;
            public Button UpgradeButton;
            public Text UpgradeButtonLabel;
        }

        private GameManager _game;
        private MRD.Building.Building _target;

        private GameObject _panel;
        private Text _messageText;
        private readonly List<GradeRow> _rows = new List<GradeRow>();

        public void Initialize(GameManager game)
        {
            _game = game;

            EnsureEventSystem();
            var canvas = CreateCanvas();
            BuildPanel(canvas.transform);
        }

        public void Show(MRD.Building.Building building)
        {
            if (_target != null) _target.OnUpgraded -= HandleUpgraded;

            _target = building;
            _target.OnUpgraded += HandleUpgraded;

            _messageText.text = "";
            _panel.SetActive(true);
            RefreshAll();
        }

        public void Hide()
        {
            if (_target != null) _target.OnUpgraded -= HandleUpgraded;
            _target = null;
            _panel.SetActive(false);
        }

        private void HandleUpgraded(Rarity grade) => RefreshAll();

        private void OnUpgradeClicked(Rarity grade)
        {
            if (_target == null) return;

            bool wasMax = _target.IsMaxLevel(grade);
            int cost = _target.GetNextUpgradeCost(grade);

            if (_target.TryUpgrade(grade, _game))
            {
                _messageText.text = $"{GradeName(grade)} 등급 강화 성공! (-{cost}G)";
            }
            else
            {
                _messageText.text = wasMax ? $"{GradeName(grade)} 등급은 이미 최대 강화 단계입니다" : "골드가 부족합니다";
            }
        }

        private void RefreshAll()
        {
            if (_target == null) return;
            foreach (var row in _rows) RefreshRow(row);
        }

        private void RefreshRow(GradeRow row)
        {
            int level = _target.GetLevel(row.Grade);
            float bonusPercent = (_target.GetAttackBonusMultiplier(row.Grade) - 1f) * 100f;

            row.InfoText.text = $"{GradeName(row.Grade)} 등급   강화 {level}/{MRD.Building.Building.MaxUpgradeLevel}   공격력 +{bonusPercent:0}%";

            if (_target.IsMaxLevel(row.Grade))
            {
                row.UpgradeButtonLabel.text = "최대";
                row.UpgradeButton.interactable = false;
            }
            else
            {
                row.UpgradeButtonLabel.text = $"{_target.GetNextUpgradeCost(row.Grade)}G";
                row.UpgradeButton.interactable = true;
            }
        }

        private static string GradeName(Rarity grade) => GradeNames.TryGetValue(grade, out var name) ? name : grade.ToString();

        // ---- UI 구성 (전부 코드로 생성) ----

        private void BuildPanel(Transform parent)
        {
            var grades = MRD.Building.Building.DisplayedGrades;

            const float rowHeight = 50f;
            float panelHeight = 130f + grades.Length * rowHeight;

            var panelGo = new GameObject("BuildingUpgradePanel");
            panelGo.transform.SetParent(parent, false);

            var rect = panelGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(460f, panelHeight);

            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

            var titleText = CreateText(panelGo.transform, "건물 강화", new Vector2(0.5f, 1f), new Vector2(0f, -16f));
            titleText.alignment = TextAnchor.UpperCenter;
            titleText.fontSize = 22;

            for (int i = 0; i < grades.Length; i++)
            {
                float rowY = -56f - i * rowHeight;
                _rows.Add(BuildGradeRow(panelGo.transform, grades[i], rowY));
            }

            _messageText = CreateText(panelGo.transform, "", new Vector2(0.5f, 0f), new Vector2(0f, 62f));
            _messageText.alignment = TextAnchor.MiddleCenter;
            _messageText.fontSize = 15;
            _messageText.color = new Color(1f, 0.9f, 0.3f);

            CreateButton(panelGo.transform, "닫기", new Vector2(0.5f, 0f), new Vector2(0f, 16f), 120f, Hide, out _);

            _panel = panelGo;
            _panel.SetActive(false);
        }

        private GradeRow BuildGradeRow(Transform parent, Rarity grade, float rowY)
        {
            var row = new GradeRow { Grade = grade };

            row.InfoText = CreateText(parent, "", new Vector2(0f, 1f), new Vector2(16f, rowY));
            row.InfoText.alignment = TextAnchor.MiddleLeft;
            row.InfoText.fontSize = 15;
            row.InfoText.rectTransform.sizeDelta = new Vector2(300f, 40f);

            row.UpgradeButton = CreateButtonTopRight(parent, new Vector2(-16f, rowY), 110f, 36f,
                () => OnUpgradeClicked(grade), out row.UpgradeButtonLabel);

            return row;
        }

        private static Canvas CreateCanvas()
        {
            var canvasGo = new GameObject("BuildingUpgrade_Canvas");
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

        private static Text CreateText(Transform parent, string text, Vector2 anchor, Vector2 anchoredPos)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(320f, 36f);

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 20;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            return txt;
        }

        // 패널 하단 중앙에 정렬되는 버튼 (anchoredPos.y는 패널 아래쪽 끝에서부터의 높이).
        private static Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 anchoredPos, float width,
            UnityEngine.Events.UnityAction onClick, out Text labelText)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(width, 44f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.28f, 0.95f);

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            labelText = CreateButtonLabel(go.transform, label);
            return button;
        }

        // 등급 줄 오른쪽 끝에 붙는 강화 버튼 (패널 위쪽 끝에서부터의 높이 기준, 우측 정렬).
        private static Button CreateButtonTopRight(Transform parent, Vector2 anchoredPos, float width, float height,
            UnityEngine.Events.UnityAction onClick, out Text labelText)
        {
            var go = new GameObject("Button_GradeUpgrade");
            go.transform.SetParent(parent, false);

            var anchor = new Vector2(1f, 1f);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(width, height);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.28f, 0.95f);

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            labelText = CreateButtonLabel(go.transform, "");
            return button;
        }

        private static Text CreateButtonLabel(Transform buttonTransform, string label)
        {
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(buttonTransform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var txt = textGo.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 15;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            return txt;
        }
    }
}
