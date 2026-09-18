using System.Collections.Generic;
using UnityEngine;
using MRD.Data;
using MRD.Battle;
using MRD.Synergy;
using MRD.Wave;
using MRD.Control;
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

        [Header("몬스터 순찰 경로 (바닥 위 정사각형 루프, 죽을 때까지 계속 돈다)")]
        [SerializeField] private float patrolHalfSize = 5f;
        [SerializeField] private float patrolCenterZ = 6f;

        private GameManager _game;

        private void Start()
        {
#if UNITY_EDITOR
            AutoLoadMissingReferences();
#endif
            SetupCamera();
            gameObject.AddComponent<SelectionController>();

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
            // 격자의 x/y 인덱스를 바닥(X-Z 평면) 좌표로 매핑한다. Y(높이)는 항상 0.
            var pos = new Vector3((x - (gridWidth - 1) / 2f) * gridCellSize, 0f, y * gridCellSize);
            unit.transform.position = pos;
            var renderer = UnitVisual.AttachCube(unit.transform, new Color(0.3f, 0.5f, 1f), 0.9f);

            unit.gameObject.AddComponent<UnitMover>();
            var selectable = unit.gameObject.AddComponent<Selectable>();
            selectable.Initialize(unit, renderer);
        }

        private void HandleMonsterSpawned(EnemyUnit enemy)
        {
            bool isBoss = enemy.Source.isBoss;
            var color = isBoss ? new Color(0.8f, 0.1f, 0.1f) : new Color(0.9f, 0.5f, 0.1f);
            float size = isBoss ? 1.4f : 0.8f;

            UnitVisual.AttachCube(enemy.transform, color, size);
            enemy.gameObject.AddComponent<EnemyUnitView>().Setup(enemy, BuildPatrolPath(), enemy.Source.moveSpeed);
        }

        private Vector3[] BuildPatrolPath()
        {
            float h = patrolHalfSize;
            float z = patrolCenterZ;
            return new[]
            {
                new Vector3(-h, 0f, z - h),
                new Vector3(h, 0f, z - h),
                new Vector3(h, 0f, z + h),
                new Vector3(-h, 0f, z + h),
            };
        }

        private static void HandleRoundStarted(int round, bool isLeftBoss, bool isRightBoss)
        {
            string suffix = isLeftBoss ? " (좌측 보스)" : isRightBoss ? " (우측 보스)" : "";
            Debug.Log($"[MRD] {round}라운드 시작{suffix}");
        }

        private static void SetupCamera()
        {
            CreateGround();

            var cam = Camera.main;
            if (cam == null) return;

            cam.orthographic = false;
            cam.fieldOfView = 50f;
            cam.transform.position = new Vector3(0f, 9f, -5f);
            cam.transform.LookAt(new Vector3(0f, 0f, 4f));
        }

        // 3D 공간에서 오브젝트들이 허공에 떠 있는 것처럼 보이지 않도록 넣어두는 임시 바닥.
        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(3f, 1f, 3f); // 기본 10x10 평면 -> 30x30

            var renderer = ground.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            renderer.material = new Material(shader) { color = new Color(0.22f, 0.28f, 0.22f) };
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
