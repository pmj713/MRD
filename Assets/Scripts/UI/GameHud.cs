using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MRD.Data;
using MRD.Gacha;
using MRD.Game;
using MRD.Battle;
using MRD.Control;
using Selectable = MRD.Control.Selectable; // UnityEngine.UI에도 같은 이름의 클래스(Selectable)가 있어 명시적으로 구분

namespace MRD.UI
{
    /// <summary>
    /// 골드/보석/웨이브 상태를 보여주고 소환/조합을 직접 눌러볼 수 있는 최소 HUD.
    /// 전부 코드로 생성한다 (정식 UI가 생기기 전까지의 임시 화면).
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        private GameManager _game;

        private Text _goldText;
        private Text _gemsText;
        private Text _roundText;
        private Text _monsterCountText;
        private Text _resultText;
        private float _resultTextTimer;

        private GameObject _gameOverPanel;
        private Text _gameOverText;

        private GameObject _unitInfoPanel;
        private Text _unitInfoText;
        private Transform _unitActionRow;
        private Selectable _displayedUnit; // 단일 선택일 때만 채워짐 (다중 선택/선택 없음이면 null)

        private CharacterDatabase _database;

        public void Initialize(GameManager game, SelectionController selectionController, CharacterDatabase database,
            GachaTable goldSummon, GachaTable gemBasicSummon, GachaTable gemMidSummon, GachaTable gemAdvancedSummon,
            CharacterData fusionTestTarget)
        {
            _game = game;
            _database = database;

            EnsureEventSystem();
            var canvas = CreateCanvas();

            BuildTopBar(canvas.transform);
            BuildBottomBar(canvas.transform, goldSummon, gemBasicSummon, gemMidSummon, gemAdvancedSummon, fusionTestTarget);
            BuildUnitInfoPanel(canvas.transform);
            BuildGameOverPanel(canvas.transform);

            _game.OnGameEnded += HandleGameEnded;
            if (selectionController != null)
                selectionController.OnSelectionChanged += HandleSelectionChanged;
        }

        private void Update()
        {
            if (_game == null) return;

            _goldText.text = $"골드: {_game.Gold}";
            _gemsText.text = $"보석: {_game.Gems}";
            _roundText.text = $"라운드: {_game.WaveSpawner.CurrentRound} / {_game.WaveSpawner.TotalRounds}";
            _monsterCountText.text = $"몬스터: {_game.WaveSpawner.AliveMonsterCount} / {_game.WaveSpawner.MaxAliveMonsters}";

            if (_resultTextTimer > 0f)
            {
                _resultTextTimer -= Time.deltaTime;
                if (_resultTextTimer <= 0f) _resultText.text = "";
            }

            RefreshUnitInfoPanel();
        }

        private void HandleGameEnded(bool victory, string reason)
        {
            _gameOverPanel.SetActive(true);
            _gameOverText.text = victory ? $"승리!\n{reason}" : $"패배...\n{reason}";
        }

        private void HandleSelectionChanged(IReadOnlyList<Selectable> selection)
        {
            if (selection.Count == 1)
            {
                _displayedUnit = selection[0];
                _unitInfoPanel.SetActive(true);
                RebuildUnitActionButtons(_displayedUnit.Unit);
            }
            else if (selection.Count > 1)
            {
                _displayedUnit = null;
                _unitInfoPanel.SetActive(true);
                _unitInfoText.text = $"{selection.Count}개 유닛 선택됨";
                RebuildUnitActionButtons(null);
            }
            else
            {
                _displayedUnit = null;
                _unitInfoPanel.SetActive(false);
                RebuildUnitActionButtons(null);
            }
        }

        // 선택된 유닛에 대해 "판매"와 "조합: <다음 유닛>" 버튼을 다시 만든다.
        // 선택이 바뀔 때만 호출하면 되므로(체력 등과 달리 매 프레임 갱신할 필요 없음) HandleSelectionChanged에서만 부른다.
        private void RebuildUnitActionButtons(BattleUnit unit)
        {
            foreach (Transform child in _unitActionRow)
                Destroy(child.gameObject);

            if (unit == null || unit.Source == null) return;
            var data = unit.Source;

            int sellValue = _game.GetSellValue(data);
            CreateSmallButton(_unitActionRow, $"판매 (+{sellValue}G)", 0, () => OnSellClicked(unit, data));

            if (_database != null)
            {
                var targets = _database.FindFusionTargetsUsing(data);
                for (int i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    CreateSmallButton(_unitActionRow, $"조합: {target.characterName}", i + 1, () => OnUnitFuseClicked(unit, target));
                }
            }
        }

        private void OnSellClicked(BattleUnit unit, CharacterData data)
        {
            if (!_game.SellUnit(unit)) return;

            ShowResult($"{data.characterName} 판매 완료 (+{_game.GetSellValue(data)}G)");
            _displayedUnit = null;
            _unitInfoPanel.SetActive(false);
            RebuildUnitActionButtons(null);
        }

        // 조합서 버튼과 달리, 재료/재화가 부족하면 아무 반응도 하지 않는다 (요청 사양).
        // 이 버튼은 항상 선택 중인 unit 자신을 재료로 쓰는 조합이므로, 성공하면 그 유닛은 화면에서도 사라진다.
        private void OnUnitFuseClicked(BattleUnit sourceUnit, CharacterData target)
        {
            if (!_game.TryFuseCharacter(target, sourceUnit)) return;

            ShowResult($"{target.characterName} 조합 성공!");
            _displayedUnit = null;
            _unitInfoPanel.SetActive(false);
            RebuildUnitActionButtons(null);
        }

        // 단일 선택 중인 유닛의 체력 등은 매 프레임 바뀌므로 여기서 계속 갱신한다.
        private void RefreshUnitInfoPanel()
        {
            if (_displayedUnit == null) return;

            var unit = _displayedUnit.Unit;
            if (unit == null || unit.Source == null)
            {
                _unitInfoPanel.SetActive(false);
                _displayedUnit = null;
                return;
            }

            var s = unit.EffectiveStats;
            _unitInfoText.text =
                $"{unit.Source.characterName}\n" +
                $"등급: {unit.Source.rarity}\n" +
                $"체력: {Mathf.CeilToInt(unit.CurrentHealth)} / {Mathf.CeilToInt(s.health)}\n" +
                $"공격력: {s.physicalAttack:0} (물리) / {s.magicAttack:0} (마법)\n" +
                $"공격속도: {s.attackSpeed:0.00}   방어력: {s.armor:0}   마법저항: {s.magicResist:0}";
        }

        private void OnSummonClicked(GachaTable table)
        {
            if (table == null) { ShowResult("소환 테이블이 설정되지 않았습니다"); return; }

            ShowResult(_game.TrySummon(table, out var result)
                ? $"{result.characterName} 획득! ({result.rarity})"
                : "소환 실패 (재화 부족)");
        }

        private void OnFuseClicked(CharacterData target)
        {
            if (target == null) { ShowResult("조합 대상이 설정되지 않았습니다"); return; }

            ShowResult(_game.TryFuseCharacter(target)
                ? $"{target.characterName} 조합 성공!"
                : "조합 실패 (재료/재화 부족)");
        }

        private void ShowResult(string message)
        {
            _resultText.text = message;
            _resultTextTimer = 3f;
        }

        // ---- UI 구성 (전부 코드로 생성) ----

        private void BuildTopBar(Transform parent)
        {
            _goldText = CreateText(parent, "골드: 0", new Vector2(0f, 1f), new Vector2(20f, -20f));
            _gemsText = CreateText(parent, "보석: 0", new Vector2(0f, 1f), new Vector2(20f, -50f));
            _roundText = CreateText(parent, "라운드: 0 / 0", new Vector2(0f, 1f), new Vector2(20f, -80f));
            _monsterCountText = CreateText(parent, "몬스터: 0 / 0", new Vector2(0f, 1f), new Vector2(20f, -110f));

            _resultText = CreateText(parent, "", new Vector2(0.5f, 1f), new Vector2(0f, -20f));
            _resultText.alignment = TextAnchor.UpperCenter;
            _resultText.fontSize = 20;
            _resultText.color = new Color(1f, 0.9f, 0.3f);
        }

        private void BuildBottomBar(Transform parent, GachaTable goldSummon, GachaTable gemBasicSummon,
            GachaTable gemMidSummon, GachaTable gemAdvancedSummon, CharacterData fusionTestTarget)
        {
            float y = 60f;
            float width = 160f;
            float gap = 10f;
            float startX = -((width + gap) * 2f);

            CreateButton(parent, "골드 소환 (100G)", startX, y, width, () => OnSummonClicked(goldSummon));
            CreateButton(parent, "보석 소환 하급 (1)", startX + (width + gap), y, width, () => OnSummonClicked(gemBasicSummon));
            CreateButton(parent, "보석 소환 중급 (3)", startX + (width + gap) * 2f, y, width, () => OnSummonClicked(gemMidSummon));
            CreateButton(parent, "보석 소환 고급 (5)", startX + (width + gap) * 3f, y, width, () => OnSummonClicked(gemAdvancedSummon));
            CreateButton(parent, "제우스 조합", startX + (width + gap) * 4f, y, width, () => OnFuseClicked(fusionTestTarget));

            // 랭크 미션 등 정식 재화 획득 수단이 아직 없어서, 테스트용으로 재화를 바로 지급하는 버튼.
            CreateButton(parent, "[테스트] 골드 +1000", startX, y - 60f, width, () => _game.GrantGold(1000));
            CreateButton(parent, "[테스트] 보석 +50", startX + (width + gap), y - 60f, width, () => _game.GrantGems(50));
        }

        // 워크래프트3처럼 화면 하단에 선택한 유닛 정보를 보여주는 패널 (기본은 숨김).
        private void BuildUnitInfoPanel(Transform parent)
        {
            var panelGo = new GameObject("UnitInfoPanel");
            panelGo.transform.SetParent(parent, false);

            var rect = panelGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(20f, 120f);
            rect.sizeDelta = new Vector2(340f, 240f);

            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);

            _unitInfoText = CreateText(panelGo.transform, "", new Vector2(0f, 1f), new Vector2(12f, -12f));
            _unitInfoText.fontSize = 18;
            _unitInfoText.rectTransform.sizeDelta = new Vector2(316f, 130f); // 부모 패널 안쪽 여백만큼 줄인 크기 (CreateText 기본값 덮어씀)

            // 판매/조합 버튼을 세로로 쌓는 영역 (0~3개 정도, 대상 유닛에 따라 개수가 바뀐다).
            var actionRowGo = new GameObject("ActionButtons");
            actionRowGo.transform.SetParent(panelGo.transform, false);
            var actionRowRect = actionRowGo.AddComponent<RectTransform>();
            actionRowRect.anchorMin = new Vector2(0f, 0f);
            actionRowRect.anchorMax = new Vector2(0f, 0f);
            actionRowRect.pivot = new Vector2(0f, 0f);
            actionRowRect.anchoredPosition = new Vector2(12f, 12f);
            actionRowRect.sizeDelta = new Vector2(316f, 86f);
            _unitActionRow = actionRowGo.transform;

            _unitInfoPanel = panelGo;
            _unitInfoPanel.SetActive(false);
        }

        private void BuildGameOverPanel(Transform parent)
        {
            var panelGo = new GameObject("GameOverPanel");
            panelGo.transform.SetParent(parent, false);
            var rect = panelGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.75f);

            _gameOverText = CreateText(panelGo.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero);
            _gameOverText.alignment = TextAnchor.MiddleCenter;
            _gameOverText.fontSize = 40;
            _gameOverText.color = Color.white;
            _gameOverText.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 200f);

            _gameOverPanel = panelGo;
            _gameOverPanel.SetActive(false);
        }

        private static Canvas CreateCanvas()
        {
            var canvasGo = new GameObject("HUD_Canvas");
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
            rect.sizeDelta = new Vector2(400f, 36f);

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 22;
            txt.alignment = TextAnchor.UpperLeft;
            txt.color = Color.white;
            return txt;
        }

        private static void CreateButton(Transform parent, string label, float x, float y, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, 44f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.15f, 0.16f, 0.22f, 0.95f);

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
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }

        // 유닛 정보창의 판매/조합 버튼처럼, 부모 영역(_unitActionRow) 안에서 index번째 줄에 놓이는 작은 버튼.
        private static void CreateSmallButton(Transform parent, string label, int index, UnityEngine.Events.UnityAction onClick)
        {
            const float height = 24f;
            const float spacing = 4f;

            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(height + spacing) * index);
            rect.sizeDelta = new Vector2(316f, height);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.22f, 0.3f, 0.95f);

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
            txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }
    }
}
