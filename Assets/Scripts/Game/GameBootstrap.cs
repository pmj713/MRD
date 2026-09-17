using System.Collections.Generic;
using UnityEngine;
using MRD.Data;
using MRD.Battle;
using MRD.Synergy;
using MRD.Wave;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MRD.Game
{
    /// <summary>
    /// 씬에 하나만 놓고 Play를 누르면 GameManager를 자동으로 구성해서 바로 눈으로 확인할 수 있게
    /// 해주는 임시 부트스트랩. 필드를 비워두면 에디터에서는 알려진 경로의 기본 에셋을 자동으로
    /// 불러온다 (플레이스홀더 단계에서 빠르게 확인하기 위한 용도 - 정식 시작 화면이 생기면 대체될 자리).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("비워두면 에디터에서 기본 에셋을 자동으로 불러온다")]
        [SerializeField] private WaveConfig waveConfig;
        [SerializeField] private MonsterData lineMonsterTemplate;
        [SerializeField] private MonsterData bossTemplate;
        [SerializeField] private List<FactionSynergyData> factionSynergies = new List<FactionSynergyData>();
        [SerializeField] private List<CharacterData> starterRoster = new List<CharacterData>();

        [Header("배치 격자")]
        [SerializeField] private int gridWidth = 5;
        [SerializeField] private int gridHeight = 3;
        [SerializeField] private float gridCellSize = 1.2f;

        [Header("적 이동 라인 (오른쪽에서 등장해 왼쪽으로 이동)")]
        [SerializeField] private float enemyLaneStartX = 7f;
        [SerializeField] private float enemyLaneEndX = -7f;
        [SerializeField] private float enemyLaneY = -2.5f;

        private GameManager _game;

        private void Start()
        {
#if UNITY_EDITOR
            AutoLoadMissingReferences();
#endif
            SetupCamera();

            var gameGo = new GameObject("GameManager");
            _game = gameGo.AddComponent<GameManager>();
            _game.Configure(gridWidth, gridHeight, waveConfig, lineMonsterTemplate, bossTemplate, bossTemplate, factionSynergies);

            _game.PlacementGrid.OnUnitPlaced += HandleUnitPlaced;
            _game.WaveSpawner.OnMonsterSpawned += HandleMonsterSpawned;
            _game.WaveSpawner.OnRoundStarted += HandleRoundStarted;
            _game.OnGoldChanged += gold => Debug.Log($"[MRD] 골드: {gold}");
            _game.OnGameEnded += (victory, reason) => Debug.Log(victory ? $"[MRD] 승리! {reason}" : $"[MRD] 패배: {reason}");

            PlaceStarterRoster();

            _game.StartGame();
            Debug.Log("[MRD] 게임 시작");
        }

        private void PlaceStarterRoster()
        {
            int x = 0;
            foreach (var data in starterRoster)
            {
                if (data == null) continue;
                _game.PlaceUnit(x, 0, data, out _);
                x++;
            }
        }

        private void HandleUnitPlaced(int x, int y, BattleUnit unit)
        {
            var pos = new Vector3((x - (gridWidth - 1) / 2f) * gridCellSize, y * gridCellSize, 0f);
            unit.transform.position = pos;
            UnitVisual.AttachSquare(unit.transform, new Color(0.3f, 0.5f, 1f), 0.9f);
        }

        private void HandleMonsterSpawned(EnemyUnit enemy)
        {
            bool isBoss = enemy.Source.isBoss;
            var color = isBoss ? new Color(0.8f, 0.1f, 0.1f) : new Color(0.9f, 0.5f, 0.1f);
            float size = isBoss ? 1.4f : 0.8f;

            UnitVisual.AttachSquare(enemy.transform, color, size);
            enemy.gameObject.AddComponent<EnemyUnitView>().Setup(enemy, enemyLaneStartX, enemyLaneEndX, enemyLaneY);
        }

        private static void HandleRoundStarted(int round, bool isLeftBoss, bool isRightBoss)
        {
            string suffix = isLeftBoss ? " (좌측 보스)" : isRightBoss ? " (우측 보스)" : "";
            Debug.Log($"[MRD] {round}라운드 시작{suffix}");
        }

        private static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(0f, -1f, cam.transform.position.z);
        }

#if UNITY_EDITOR
        private void AutoLoadMissingReferences()
        {
            if (waveConfig == null)
                waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/Data/Wave/PlaytestWaveConfig.asset");
            if (lineMonsterTemplate == null)
                lineMonsterTemplate = AssetDatabase.LoadAssetAtPath<MonsterData>("Assets/Data/Monsters/LineMonster_Basic.asset");
            if (bossTemplate == null)
                bossTemplate = AssetDatabase.LoadAssetAtPath<MonsterData>("Assets/Data/Monsters/Boss_ChaosGuardian.asset");

            if (factionSynergies.Count == 0)
            {
                var synergy = AssetDatabase.LoadAssetAtPath<FactionSynergyData>("Assets/Data/Synergy/OlympusSynergy.asset");
                if (synergy != null) factionSynergies.Add(synergy);
            }

            if (starterRoster.Count == 0)
            {
                string[] paths =
                {
                    "Assets/Data/Characters/Olympus/SpartanShieldman.asset",
                    "Assets/Data/Characters/Olympus/Heracles.asset",
                    "Assets/Data/Characters/Olympus/Zeus.asset",
                };
                foreach (var path in paths)
                {
                    var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                    if (data != null) starterRoster.Add(data);
                }
            }
        }
#endif
    }
}
