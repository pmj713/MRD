using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MRD.Data;
using MRD.Gacha;

namespace MRD.UI
{
    /// <summary>
    /// 지정한 키(기본 R)로 여닫는 조합서 패널. 왼쪽에서 진영을 누르면 그 아래로 레어/유니크/전설
    /// 등급 버튼이 펼쳐지고, 등급을 누르면 오른쪽에 "재료 + 재료 = 결과" 형태로 조합식을 보여준다.
    /// 레어는 조합이 아니라 소환 전용이라 안내 문구만 나온다.
    /// </summary>
    public class FusionBookUI : MonoBehaviour
    {
        private static readonly Faction[] ShownFactions =
        {
            Faction.Asgard, Faction.ThroneOfRa, Faction.NineRealms, Faction.AbyssalArchive, Faction.Covenant,
        };

        private static readonly Rarity[] ShownTiers = { Rarity.Rare, Rarity.Unique, Rarity.Legend };

        private static readonly Dictionary<Faction, string> FactionNames = new Dictionary<Faction, string>
        {
            { Faction.Olympus, "올림포스" },
            { Faction.Asgard, "아스가르드" },
            { Faction.ThroneOfRa, "라의 왕좌" },
            { Faction.NineRealms, "구주" },
            { Faction.AbyssalArchive, "심연의 서고" },
            { Faction.Covenant, "숲의 맹약" },
        };

        private static readonly Dictionary<Rarity, string> TierNames = new Dictionary<Rarity, string>
        {
            { Rarity.Rare, "레어" },
            { Rarity.Unique, "유니크" },
            { Rarity.Legend, "전설" },
        };

        [SerializeField] private KeyCode toggleKey = KeyCode.R;

        private CharacterDatabase _database;
        private GameObject _panelRoot;
        private Transform _recipeListParent;
        private Text _recipeHeaderText;

        private readonly Dictionary<Faction, List<GameObject>> _tierButtons = new Dictionary<Faction, List<GameObject>>();

        public void Initialize(CharacterDatabase database)
        {
            _database = database;
            BuildUI();
            _panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                _panelRoot.SetActive(!_panelRoot.activeSelf);
        }

        private void ToggleFactionGroup(Faction faction)
        {
            var buttons = _tierButtons[faction];
            bool show = !buttons[0].activeSelf;
            foreach (var b in buttons) b.SetActive(show);
        }

        private void ShowTierRecipes(Faction faction, Rarity tier)
        {
            foreach (Transform child in _recipeListParent)
                Destroy(child.gameObject);

            _recipeHeaderText.text = $"{FactionNames[faction]} - {TierNames[tier]}";

            var units = GetUnits(faction, tier);
            if (units.Count == 0)
            {
                CreateRecipeRow("해당 등급 유닛이 아직 없습니다.");
                return;
            }

            if (tier == Rarity.Rare)
            {
                CreateRecipeRow("레어 등급은 조합이 아니라 소환으로만 얻습니다.");
                return;
            }

            foreach (var unit in units)
            {
                var recipe = unit.fusionRecipe;
                if (recipe?.requiredCharacters == null || recipe.requiredCharacters.Length == 0)
                {
                    CreateRecipeRow($"{unit.characterName} : 조합식 없음");
                    continue;
                }

                var materialNames = new List<string>();
                foreach (var material in recipe.requiredCharacters)
                    materialNames.Add(material != null ? material.characterName : "???");

                string cost = recipe.goldCost > 0 ? $"  (골드 {recipe.goldCost})"
                    : recipe.gemCost > 0 ? $"  (보석 {recipe.gemCost})" : "";

                CreateRecipeRow($"{string.Join("  +  ", materialNames)}  =  {unit.characterName}{cost}");
            }
        }

        private List<CharacterData> GetUnits(Faction faction, Rarity rarity)
        {
            var result = new List<CharacterData>();
            if (_database == null) return result;

            foreach (var character in _database.allCharacters)
            {
                if (character != null && character.faction == faction && character.rarity == rarity)
                    result.Add(character);
            }
            return result;
        }

        // ---- UI 구성 (전부 코드로 생성) ----

        private void BuildUI()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("FusionBook_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // GameHud보다 위에 뜨도록

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasGo.AddComponent<GraphicRaycaster>();

            // 화면 전체를 살짝 어둡게 깔아서 모달처럼 보이게 하고, 이 오브젝트를 패널 on/off 스위치로 쓴다.
            _panelRoot = CreateFullScreenPanel(canvas.transform, new Color(0f, 0f, 0f, 0.5f));

            var mainPanel = CreateFixedPanel(_panelRoot.transform, new Color(0.1f, 0.1f, 0.13f, 0.97f),
                new Vector2(900f, 600f), Vector2.zero);

            var title = CreateText(mainPanel.transform, "조합서 (R로 닫기)", new Vector2(0.5f, 1f), new Vector2(0f, -16f), 22);
            title.alignment = TextAnchor.UpperCenter;

            var leftList = CreateVerticalList(mainPanel.transform, new Vector2(16f, -56f), new Vector2(240f, 520f));
            BuildFactionList(leftList);

            _recipeHeaderText = CreateText(mainPanel.transform, "왼쪽에서 진영과 등급을 선택하세요", new Vector2(0f, 1f), new Vector2(280f, -56f), 20);
            _recipeHeaderText.rectTransform.sizeDelta = new Vector2(580f, 30f);

            _recipeListParent = CreateVerticalList(mainPanel.transform, new Vector2(280f, -96f), new Vector2(580f, 480f));
        }

        private void BuildFactionList(Transform parent)
        {
            foreach (var faction in ShownFactions)
            {
                var factionButton = faction; // 로컬 캡처
                CreateListButton(parent, FactionNames[faction], 30f, () => ToggleFactionGroup(factionButton));

                var tierButtons = new List<GameObject>();
                foreach (var tier in ShownTiers)
                {
                    var tierValue = tier; // 로컬 캡처
                    var btn = CreateListButton(parent, "    " + TierNames[tier], 26f, () => ShowTierRecipes(factionButton, tierValue));
                    btn.SetActive(false);
                    tierButtons.Add(btn);
                }
                _tierButtons[faction] = tierButtons;
            }
        }

        private void CreateRecipeRow(string text)
        {
            var row = CreateText(_recipeListParent, text, new Vector2(0f, 1f), Vector2.zero, 18);
            row.rectTransform.sizeDelta = new Vector2(560f, 30f);
        }

        private static GameObject CreateFullScreenPanel(Transform parent, Color color)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static GameObject CreateFixedPanel(Transform parent, Color color, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            go.AddComponent<Image>().color = color;
            return go;
        }

        // 세로로 쌓이는 목록 컨테이너. 자식을 SetActive로 껐다 켜면 VerticalLayoutGroup이 자동으로 다시 배치해준다.
        private static Transform CreateVerticalList(Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject("List");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            return go.transform;
        }

        private static GameObject CreateListButton(Transform parent, string label, float height, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + label.Trim());
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, height);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.19f, 0.25f, 0.95f);

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var txt = textGo.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 15;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = Color.white;

            return go;
        }

        private static Text CreateText(Transform parent, string text, Vector2 anchor, Vector2 anchoredPos, int fontSize)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(400f, 30f);

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.UpperLeft;
            txt.color = Color.white;
            return txt;
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
