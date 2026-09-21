using System;
using System.Collections.Generic;
using UnityEngine;
using MRD.Battle;

namespace MRD.Wave
{
    /// <summary>
    /// 라운드제 웨이브 진행을 관리한다. 매 라운드 정해진 시간 동안 몬스터를 스폰하고,
    /// 10의 배수/끝자리 3 라운드에는 보스를 함께 등장시키며, 필수 클리어 라운드(80/85 등)는
    /// 그 라운드에 스폰된 몬스터를 전부 처치하지 못하면 게임을 종료한다.
    /// 몬스터는 정해진 경로를 계속 순찰하며(라인 이탈 개념 없음) 죽어야만 사라지므로,
    /// 처치가 밀려 생존 몬스터 수가 maxAliveMonsters에 도달하면 게임을 종료한다.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] private WaveConfig config;
        [SerializeField] private MonsterData lineMonsterTemplate;
        [SerializeField] private MonsterData leftBossTemplate;
        [SerializeField] private MonsterData rightBossTemplate;
        [SerializeField] private CombatManager combatManager; // 지정하면 스폰되는 몬스터가 자동으로 공격 대상으로 등록된다

        public int CurrentRound { get; private set; }
        public int AliveMonsterCount => _activeMonsters.Count;
        public int TotalRounds => config.totalRounds;
        public int MaxAliveMonsters => config.maxAliveMonsters;
        public bool IsGameOver { get; private set; }
        public bool IsAllRoundsCleared { get; private set; }

        public event Action<int, bool, bool> OnRoundStarted; // round, isLeftBoss, isRightBoss
        public event Action<EnemyUnit> OnMonsterSpawned;
        public event Action<EnemyUnit> OnMonsterKilled;
        public event Action<string> OnGameOver;
        public event Action OnAllRoundsCleared;

        private readonly List<EnemyUnit> _activeMonsters = new List<EnemyUnit>();
        private readonly List<EnemyUnit> _currentRoundMonsters = new List<EnemyUnit>();

        private float _roundTimer;
        private float _roundDuration;
        private float _spawnInterval;
        private float _spawnTimer;
        private int _spawnedThisRound;
        private bool _isRunning;

        public void Configure(WaveConfig waveConfig, MonsterData lineMonster, MonsterData leftBoss, MonsterData rightBoss)
        {
            config = waveConfig;
            lineMonsterTemplate = lineMonster;
            leftBossTemplate = leftBoss;
            rightBossTemplate = rightBoss;
        }

        public void SetCombatManager(CombatManager manager) => combatManager = manager;

        public void StartRun()
        {
            CurrentRound = 0;
            IsGameOver = false;
            IsAllRoundsCleared = false;
            _isRunning = true;

            BeginRound(1);
        }

        private void Update()
        {
            if (!_isRunning) return;
            Tick(Time.deltaTime);
        }

        /// <summary>매 프레임 진행 로직. Update()가 호출하지만, 테스트에서 직접 호출해 시간을 앞당길 수도 있다.</summary>
        public void Tick(float deltaTime)
        {
            if (!_isRunning) return;

            TickSpawn(deltaTime);

            _roundTimer += deltaTime;
            if (_roundTimer >= _roundDuration)
                AdvanceRound();
        }

        private void TickSpawn(float deltaTime)
        {
            if (_spawnedThisRound >= config.monstersPerRound) return;

            _spawnTimer += deltaTime;
            while (_spawnTimer >= _spawnInterval && _spawnedThisRound < config.monstersPerRound)
            {
                _spawnTimer -= _spawnInterval;
                SpawnMonster(lineMonsterTemplate);
                _spawnedThisRound++;
                if (!_isRunning) return; // 스폰 도중 최대 생존 마릿수 초과로 게임오버가 났으면 더 스폰하지 않는다
            }
        }

        private void SpawnMonster(MonsterData template)
        {
            if (template == null) return;

            var go = new GameObject($"Enemy_{template.monsterName}_R{CurrentRound}");
            go.transform.SetParent(transform);
            var enemy = go.AddComponent<EnemyUnit>();
            enemy.Initialize(template, CurrentRound);
            enemy.OnDeath += HandleMonsterDeath;

            _activeMonsters.Add(enemy);
            _currentRoundMonsters.Add(enemy);
            combatManager?.RegisterEnemyTarget(enemy);
            OnMonsterSpawned?.Invoke(enemy);

            if (_activeMonsters.Count >= config.maxAliveMonsters)
                TriggerGameOver($"생존 몬스터 {config.maxAliveMonsters}마리 도달");
        }

        private void AdvanceRound()
        {
            if (IsMandatoryClearRound(CurrentRound))
            {
                bool cleared = _currentRoundMonsters.TrueForAll(m => m.IsDead);
                if (!cleared)
                {
                    TriggerGameOver($"{CurrentRound}라운드 필수 클리어 실패");
                    return;
                }
            }

            int nextRound = CurrentRound + 1;
            if (nextRound > config.totalRounds)
            {
                _isRunning = false;
                IsAllRoundsCleared = true;
                OnAllRoundsCleared?.Invoke();
                return;
            }

            BeginRound(nextRound);
        }

        private void BeginRound(int round)
        {
            CurrentRound = round;
            _roundTimer = 0f;
            _spawnTimer = 0f;
            _spawnedThisRound = 0;
            _currentRoundMonsters.Clear();

            _roundDuration = round <= config.earlyRoundThreshold ? config.earlyRoundDuration : config.lateRoundDuration;
            _spawnInterval = _roundDuration / Mathf.Max(1, config.monstersPerRound);

            bool isLeftBoss = config.leftBossInterval > 0 && round % config.leftBossInterval == 0;
            bool isRightBoss = config.rightBossOffset > 0 && round % 10 == config.rightBossOffset;

            if (isLeftBoss) SpawnMonster(leftBossTemplate);
            if (isRightBoss) SpawnMonster(rightBossTemplate);

            OnRoundStarted?.Invoke(round, isLeftBoss, isRightBoss);
        }

        private bool IsMandatoryClearRound(int round)
        {
            if (config.mandatoryClearRounds == null) return false;
            foreach (var r in config.mandatoryClearRounds)
                if (r == round) return true;
            return false;
        }

        private void HandleMonsterDeath(EnemyUnit monster)
        {
            RemoveFromActive(monster);
            OnMonsterKilled?.Invoke(monster);
        }

        private void RemoveFromActive(EnemyUnit monster)
        {
            monster.OnDeath -= HandleMonsterDeath;
            _activeMonsters.Remove(monster);
            combatManager?.UnregisterEnemyTarget(monster);

            // 죽은 몬스터는 더 이상 전장에 남아있을 이유가 없으니 정리한다.
            if (monster != null)
            {
                if (Application.isPlaying)
                    Destroy(monster.gameObject);
                else
                    DestroyImmediate(monster.gameObject); // 에디터(플레이 모드 아님)에서 호출되는 경우 대비
            }
        }

        private void TriggerGameOver(string reason)
        {
            _isRunning = false;
            IsGameOver = true;
            OnGameOver?.Invoke(reason);
        }
    }
}
