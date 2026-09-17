using System;
using System.Collections.Generic;
using UnityEngine;
using MRD.Battle;
using MRD.Data;
using MRD.Synergy;
using MRD.Wave;

namespace MRD.Game
{
    /// <summary>
    /// 배치 격자(PlacementGrid), 전투 판정(CombatManager), 진영 시너지(SynergyManager),
    /// 웨이브 진행(WaveSpawner)을 한 곳에서 엮어서 굴리는 최상위 진행 관리자.
    /// 인스펙터로 값만 채워 넣고 StartGame()을 호출하면 바로 플레이 가능한 상태가 되는 걸 목표로 한다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("컴포넌트 참조 (비워두면 같은 오브젝트에 자동으로 추가)")]
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private SynergyManager synergyManager;
        [SerializeField] private PlacementGrid placementGrid;
        [SerializeField] private WaveSpawner waveSpawner;

        [Header("배치 격자 크기")]
        [SerializeField] private int gridWidth = 5;
        [SerializeField] private int gridHeight = 5;

        [Header("웨이브 설정")]
        [SerializeField] private WaveConfig waveConfig;
        [SerializeField] private MonsterData lineMonsterTemplate;
        [SerializeField] private MonsterData leftBossTemplate;
        [SerializeField] private MonsterData rightBossTemplate;

        [Header("등록할 진영 시너지 표")]
        [SerializeField] private List<FactionSynergyData> factionSynergies = new List<FactionSynergyData>();

        public CombatManager CombatManager => combatManager;
        public SynergyManager SynergyManager => synergyManager;
        public PlacementGrid PlacementGrid => placementGrid;
        public WaveSpawner WaveSpawner => waveSpawner;

        public int Gold { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsVictory { get; private set; }

        public event Action<int> OnGoldChanged;
        public event Action<bool, string> OnGameEnded; // (승리 여부, 사유)

        private bool _initialized;

        private void Awake()
        {
            EnsureComponents();
        }

        /// <summary>인스펙터 대신 코드로 설정할 때 사용한다 (부트스트랩, 테스트 등).</summary>
        public void Configure(int width, int height, WaveConfig config, MonsterData lineMonster,
            MonsterData leftBoss, MonsterData rightBoss, List<FactionSynergyData> synergies)
        {
            gridWidth = width;
            gridHeight = height;
            waveConfig = config;
            lineMonsterTemplate = lineMonster;
            leftBossTemplate = leftBoss;
            rightBossTemplate = rightBoss;
            factionSynergies = synergies ?? new List<FactionSynergyData>();
        }

        /// <summary>배치는 웨이브 시작 전에도 가능해야 하므로, 필요해지는 시점에 한 번만 배선한다.</summary>
        private void EnsureInitialized()
        {
            if (_initialized) return;

            EnsureComponents(); // Awake가 아직 실행되지 않았을 수 있는 상황(에디터 스크립트 등)에 대비한 방어적 호출

            synergyManager.SetFactionSynergies(factionSynergies);
            placementGrid.Configure(gridWidth, gridHeight, combatManager);
            waveSpawner.Configure(waveConfig, lineMonsterTemplate, leftBossTemplate, rightBossTemplate);
            waveSpawner.SetCombatManager(combatManager);

            waveSpawner.OnMonsterKilled += HandleMonsterKilled;
            waveSpawner.OnGameOver += reason => EndGame(false, reason);
            waveSpawner.OnAllRoundsCleared += () => EndGame(true, "모든 라운드 클리어");

            _initialized = true;
        }

        private void EnsureComponents()
        {
            if (combatManager == null) combatManager = gameObject.AddComponent<CombatManager>();
            if (synergyManager == null) synergyManager = gameObject.AddComponent<SynergyManager>();
            if (placementGrid == null) placementGrid = gameObject.AddComponent<PlacementGrid>();
            if (waveSpawner == null) waveSpawner = gameObject.AddComponent<WaveSpawner>();
        }

        public void StartGame()
        {
            EnsureInitialized();

            Gold = 0;
            IsGameOver = false;
            IsVictory = false;

            waveSpawner.StartRun();
        }

        /// <summary>지정 슬롯에 유닛을 배치하고, 배치 결과에 맞춰 진영 시너지를 다시 계산한다.</summary>
        public bool PlaceUnit(int x, int y, CharacterData data, out BattleUnit placedUnit)
        {
            EnsureInitialized();

            bool success = placementGrid.TryPlaceUnit(x, y, data, out placedUnit);
            if (success)
                placementGrid.RecomputeSynergies(synergyManager);
            return success;
        }

        /// <summary>지정 슬롯의 유닛을 빼내고, 배치 결과에 맞춰 진영 시너지를 다시 계산한다.</summary>
        public bool RemoveUnit(int x, int y)
        {
            EnsureInitialized();

            bool success = placementGrid.TryRemoveUnit(x, y);
            if (success)
                placementGrid.RecomputeSynergies(synergyManager);
            return success;
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

        private void EndGame(bool victory, string reason)
        {
            if (IsGameOver) return; // 승리/패배 이벤트가 동시에 여러 번 들어와도 한 번만 처리한다.

            IsGameOver = true;
            IsVictory = victory;
            OnGameEnded?.Invoke(victory, reason);
        }
    }
}
