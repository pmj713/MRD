using System;
using System.Collections.Generic;
using UnityEngine;
using MRD.Battle;
using MRD.Data;
using MRD.Wave;
using MRD.Gacha;

namespace MRD.Game
{
    /// <summary>
    /// 배치 격자(PlacementGrid), 전투 판정(CombatManager),
    /// 웨이브 진행(WaveSpawner)을 한 곳에서 엮어서 굴리는 최상위 진행 관리자.
    /// 인스펙터로 값만 채워 넣고 StartGame()을 호출하면 바로 플레이 가능한 상태가 되는 걸 목표로 한다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("컴포넌트 참조 (비워두면 같은 오브젝트에 자동으로 추가)")]
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private PlacementGrid placementGrid;
        [SerializeField] private WaveSpawner waveSpawner;
        [SerializeField] private GachaManager gachaManager;

        [Header("소환 관련")]
        [SerializeField] private CharacterDatabase characterDatabase;

        [Header("배치 격자 크기")]
        [SerializeField] private int gridWidth = 5;
        [SerializeField] private int gridHeight = 5;

        [Header("웨이브 설정")]
        [SerializeField] private WaveConfig waveConfig;
        [SerializeField] private MonsterData lineMonsterTemplate;
        [SerializeField] private MonsterData leftBossTemplate;
        [SerializeField] private MonsterData rightBossTemplate;

        // 등급별 기본 가치 - 판매가는 이 값의 SellRefundRatio 비율만큼 골드로 돌려준다.
        private static readonly Dictionary<Rarity, int> BaseValueByRarity = new Dictionary<Rarity, int>
        {
            { Rarity.Normal, 50 },
            { Rarity.Magic, 100 },
            { Rarity.Rare, 200 },
            { Rarity.Unique, 400 },
            { Rarity.Legend, 800 },
            { Rarity.Hidden, 1600 },
            { Rarity.Special, 3200 },
        };

        private const float SellRefundRatio = 0.5f;

        public CombatManager CombatManager => combatManager;
        public PlacementGrid PlacementGrid => placementGrid;
        public WaveSpawner WaveSpawner => waveSpawner;
        public GachaManager GachaManager => gachaManager;
        public PlayerInventory Inventory { get; } = new PlayerInventory();

        public int Gold { get; private set; }
        public int Gems { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsVictory { get; private set; }

        public event Action<int> OnGoldChanged;
        public event Action<int> OnGemsChanged;
        public event Action<bool, string> OnGameEnded; // (승리 여부, 사유)
        public event Action<CharacterData> OnCharacterSummoned;
        public event Action<CharacterData> OnCharacterFused;

        private bool _initialized;

        private void Awake()
        {
            EnsureComponents();
        }

        /// <summary>인스펙터 대신 코드로 설정할 때 사용한다 (부트스트랩, 테스트 등).</summary>
        public void Configure(int width, int height, WaveConfig config, MonsterData lineMonster,
            MonsterData leftBoss, MonsterData rightBoss)
        {
            gridWidth = width;
            gridHeight = height;
            waveConfig = config;
            lineMonsterTemplate = lineMonster;
            leftBossTemplate = leftBoss;
            rightBossTemplate = rightBoss;
        }

        /// <summary>소환 시스템에 쓸 캐릭터 데이터베이스를 지정한다 (부트스트랩, 테스트 등).</summary>
        public void SetCharacterDatabase(CharacterDatabase database) => characterDatabase = database;

        /// <summary>배치는 웨이브 시작 전에도 가능해야 하므로, 필요해지는 시점에 한 번만 배선한다.</summary>
        private void EnsureInitialized()
        {
            if (_initialized) return;

            EnsureComponents(); // Awake가 아직 실행되지 않았을 수 있는 상황(에디터 스크립트 등)에 대비한 방어적 호출

            placementGrid.Configure(gridWidth, gridHeight, combatManager);
            waveSpawner.Configure(waveConfig, lineMonsterTemplate, leftBossTemplate, rightBossTemplate);
            waveSpawner.SetCombatManager(combatManager);
            gachaManager.SetDatabase(characterDatabase);

            waveSpawner.OnMonsterKilled += HandleMonsterKilled;
            waveSpawner.OnGameOver += reason => EndGame(false, reason);
            waveSpawner.OnAllRoundsCleared += () => EndGame(true, "모든 라운드 클리어");

            _initialized = true;
        }

        private void EnsureComponents()
        {
            if (combatManager == null) combatManager = gameObject.AddComponent<CombatManager>();
            if (placementGrid == null) placementGrid = gameObject.AddComponent<PlacementGrid>();
            if (waveSpawner == null) waveSpawner = gameObject.AddComponent<WaveSpawner>();
            if (gachaManager == null) gachaManager = gameObject.AddComponent<GachaManager>();
        }

        public void StartGame()
        {
            EnsureInitialized();

            Gold = 0;
            IsGameOver = false;
            IsVictory = false;

            waveSpawner.StartRun();
        }

        /// <summary>지정 슬롯에 유닛을 배치한다.</summary>
        public bool PlaceUnit(int x, int y, CharacterData data, out BattleUnit placedUnit)
        {
            EnsureInitialized();
            return placementGrid.TryPlaceUnit(x, y, data, out placedUnit);
        }

        /// <summary>지정 슬롯의 유닛을 빼낸다.</summary>
        public bool RemoveUnit(int x, int y)
        {
            EnsureInitialized();
            return placementGrid.TryRemoveUnit(x, y);
        }

        /// <summary>골드를 지급한다 (보상, 테스트 등). 전투 처치 보상 외에 외부에서 지급할 때 사용.</summary>
        public void GrantGold(int amount) => AddGold(amount);

        /// <summary>보석을 지급한다 (랭크 미션 보상 등, 아직 랭크 미션 시스템은 없어 외부에서 직접 호출).</summary>
        public void GrantGems(int amount) => AddGems(amount);

        /// <summary>
        /// 지정된 소환 테이블로 한 번 뽑는다. 재화가 부족하거나 해당 등급에 실제 유닛이 없으면 실패한다.
        /// 성공하면 재화를 차감하고 결과 캐릭터를 보유 목록에 추가한다.
        /// </summary>
        public bool TrySummon(GachaTable table, out CharacterData result)
        {
            EnsureInitialized();
            result = null;

            if (table == null) return false;
            if (!TrySpend(table.currency, table.cost)) return false;

            var rolled = gachaManager.Roll(table);
            if (rolled == null)
            {
                Refund(table.currency, table.cost); // 뽑을 대상이 없었으면 재화를 돌려준다
                return false;
            }

            Inventory.Add(rolled);
            AutoPlaceIfPossible(rolled);
            result = rolled;
            OnCharacterSummoned?.Invoke(rolled);
            return true;
        }

        /// <summary>
        /// target의 조합 레시피(FusionRecipe)대로 재료와 재화가 충분한지 확인하고, 충분하면 소모한 뒤
        /// target을 보유 목록에 추가한다. 재료가 하나라도 부족하면 아무것도 소모하지 않고 실패한다.
        /// 재료로 소모된 유닛이 필드에 배치돼 있었다면 화면에서도 사라진다.
        /// preferredMaterialUnit을 지정하면(유닛 정보창의 "조합" 버튼처럼 특정 유닛을 클릭해서 조합한 경우),
        /// 그 유닛이 재료 중 하나와 일치할 때 다른 동일 유닛보다 그 유닛을 먼저 소모 대상으로 삼는다.
        /// </summary>
        public bool TryFuseCharacter(CharacterData target, BattleUnit preferredMaterialUnit = null)
        {
            EnsureInitialized();

            if (target == null) return false;
            var recipe = target.fusionRecipe;
            if (recipe == null || recipe.requiredCharacters == null || recipe.requiredCharacters.Length == 0)
                return false;

            var required = new Dictionary<CharacterData, int>();
            foreach (var material in recipe.requiredCharacters)
            {
                if (material == null) continue;
                required.TryGetValue(material, out var count);
                required[material] = count + 1;
            }

            foreach (var kv in required)
            {
                if (Inventory.GetCount(kv.Key) < kv.Value) return false;
            }
            if (Gold < recipe.goldCost || Gems < recipe.gemCost) return false;

            foreach (var kv in required)
            {
                Inventory.TryConsume(kv.Key, kv.Value);
                RemoveFieldMaterials(kv.Key, kv.Value, preferredMaterialUnit);
            }

            SpendGold(recipe.goldCost);
            SpendGems(recipe.gemCost);

            Inventory.Add(target);
            AutoPlaceIfPossible(target);
            OnCharacterFused?.Invoke(target);
            return true;
        }

        /// <summary>조합 재료로 소모된 만큼 필드에 배치된 유닛도 제거한다 (인벤토리 수량과 화면을 일치시킨다).</summary>
        private void RemoveFieldMaterials(CharacterData material, int count, BattleUnit preferredUnit)
        {
            int remaining = count;

            if (preferredUnit != null && preferredUnit.Source == material &&
                placementGrid.TryFindSlotOf(preferredUnit, out int px, out int py))
            {
                RemoveUnit(px, py);
                remaining--;
            }

            for (int i = 0; i < remaining; i++)
            {
                if (!placementGrid.TryFindSlotOfData(material, out int x, out int y)) break;
                RemoveUnit(x, y);
            }
        }

        /// <summary>빈 슬롯이 있으면 해당 유닛을 필드에 자동으로 배치한다 (소환/조합으로 얻은 유닛이 바로 눈에 보이도록).</summary>
        private void AutoPlaceIfPossible(CharacterData data)
        {
            if (!placementGrid.TryFindEmptySlot(out int x, out int y)) return;
            PlaceUnit(x, y, data, out _);
        }

        /// <summary>해당 캐릭터를 판매했을 때 돌려받는 골드(등급 기본 가치의 일정 비율)를 계산한다.</summary>
        public int GetSellValue(CharacterData data)
        {
            if (data == null) return 0;
            int baseValue = BaseValueByRarity.TryGetValue(data.rarity, out var v) ? v : 0;
            return Mathf.RoundToInt(baseValue * SellRefundRatio);
        }

        /// <summary>필드에 배치된 유닛을 판매한다. 슬롯에서 제거하고 가치의 일정 비율만큼 골드를 지급한다.</summary>
        public bool SellUnit(BattleUnit unit)
        {
            EnsureInitialized();

            if (unit == null || unit.Source == null) return false;
            if (!placementGrid.TryFindSlotOf(unit, out int x, out int y)) return false;

            int refund = GetSellValue(unit.Source);
            if (!RemoveUnit(x, y)) return false;

            AddGold(refund);
            return true;
        }

        private bool TrySpend(GachaCurrency currency, int cost)
        {
            if (currency == GachaCurrency.Gold) return SpendGold(cost);
            return SpendGems(cost);
        }

        private void Refund(GachaCurrency currency, int amount)
        {
            if (currency == GachaCurrency.Gold) AddGold(amount);
            else AddGems(amount);
        }

        private bool SpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (Gold < amount) return false;
            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }

        private bool SpendGems(int amount)
        {
            if (amount <= 0) return true;
            if (Gems < amount) return false;
            Gems -= amount;
            OnGemsChanged?.Invoke(Gems);
            return true;
        }

        private void HandleMonsterKilled(EnemyUnit monster)
        {
            AddGold(monster.Source.goldReward);
        }

        private void AddGold(int amount)
        {
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        private void AddGems(int amount)
        {
            Gems += amount;
            OnGemsChanged?.Invoke(Gems);
        }

        private void EndGame(bool victory, string reason)
        {
            if (IsGameOver) return; // 승리/패배 이벤트가 동시에 여러 번 들어와도 한 번만 처리한다.

            IsGameOver = true;
            IsVictory = victory;
            OnGameEnded?.Invoke(victory, reason);
        }
    }
}
