using System.Collections.Generic;
using UnityEngine;
using MRD.Data;
using MRD.Battle;
using MRD.Synergy;
using MRD.Wave;
using MRD.Control;
using MRD.Gacha;
using MRD.UI;
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

        [Header("소환/조합 (비워두면 자동 로드)")]
        [SerializeField] private CharacterDatabase characterDatabase;
        [SerializeField] private GachaTable goldSummonTable;
        [SerializeField] private GachaTable gemBasicSummonTable;
        [SerializeField] private GachaTable gemMidSummonTable;
        [SerializeField] private GachaTable gemAdvancedSummonTable;
        [SerializeField] private CharacterData fusionTestTarget;

        [Header("배치 격자 (몬스터 순찰 경로 한가운데에 놓인다 - 몬스터가 격자를 둘러싸고 돈다)")]
        [SerializeField] private int gridWidth = 10;
        [SerializeField] private int gridHeight = 6;
        [SerializeField] private float gridCellSize = 1.5f;

        [Header("몬스터 순찰 경로 (바닥 위 직사각형 루프, 죽을 때까지 계속 돈다 - 폭/깊이/중심을 각각 조절 가능)")]
        [SerializeField] private float patrolHalfWidth = 14f;
        [SerializeField] private float patrolHalfDepth = 12f;
        [SerializeField] private float patrolCenterX = 0f;
        [SerializeField] private float patrolCenterZ = 3f;

        private GameManager _game;

        private void Start()
        {
#if UNITY_EDITOR
            AutoLoadMissingReferences();
#endif
            SetupCamera();
            var selectionController = gameObject.AddComponent<SelectionController>();
            gameObject.AddComponent<CameraEdgePan>();
            gameObject.AddComponent<CameraZoom>();

            var gameGo = new GameObject("GameManager");
            _game = gameGo.AddComponent<GameManager>();
            _game.Configure(gridWidth, gridHeight, waveConfig, lineMonsterTemplate, bossTemplate, bossTemplate, factionSynergies);
            _game.SetCharacterDatabase(characterDatabase);

            _game.PlacementGrid.OnUnitPlaced += HandleUnitPlaced;
            _game.WaveSpawner.OnMonsterSpawned += HandleMonsterSpawned;
            _game.WaveSpawner.OnRoundStarted += HandleRoundStarted;
            _game.OnGoldChanged += gold => Debug.Log($"[MRD] 골드: {gold}");
            _game.OnGameEnded += (victory, reason) => Debug.Log(victory ? $"[MRD] 승리! {reason}" : $"[MRD] 패배: {reason}");

            gameObject.AddComponent<GameHud>().Initialize(_game, selectionController, characterDatabase, goldSummonTable,
                gemBasicSummonTable, gemMidSummonTable, gemAdvancedSummonTable, fusionTestTarget);
            gameObject.AddComponent<FusionBookUI>().Initialize(characterDatabase);

            PlaceStarterRoster();

            _game.StartGame();
            _game.GrantGold(500); // 소환/조합 버튼을 바로 눌러볼 수 있도록 지급하는 테스트용 시작 재화
            _game.GrantGems(350); // 제우스 조합(보석 300)까지 바로 시도해볼 수 있는 넉넉한 값
            Debug.Log("[MRD] 게임 시작");
        }

        private void PlaceStarterRoster()
        {
            int x = 0;
            foreach (var data in starterRoster)
            {
                if (data == null) continue;
                _game.PlaceUnit(x, 0, data, out _);
                _game.Inventory.Add(data); // 배치한 유닛은 보유 중인 것으로 취급 (조합 재료 등으로 바로 쓸 수 있게)
                x++;
            }
        }

        private void HandleUnitPlaced(int x, int y, BattleUnit unit)
        {
            // 격자의 x/y 인덱스를 바닥(X-Z 평면) 좌표로 매핑한다. Y(높이)는 항상 0.
            // 격자 전체가 순찰 경로의 중심(patrolCenterX/Z)에 오도록, 격자 크기의 절반만큼 빼서 중심을 맞춘다.
            float originX = patrolCenterX - (gridWidth - 1) * gridCellSize / 2f;
            float originZ = patrolCenterZ - (gridHeight - 1) * gridCellSize / 2f;
            var pos = new Vector3(originX + x * gridCellSize, 0f, originZ + y * gridCellSize);
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

        // 첫 꼭짓점(왼쪽 위)이 스폰 지점이 된다 (EnemyUnitView는 항상 corners[0]에서 출발한다).
        // 왼쪽 위 -> 왼쪽 아래 -> 오른쪽 아래 -> 오른쪽 위 순서라 화면상 시계 반대 방향으로 돈다.
        private Vector3[] BuildPatrolPath()
        {
            float xMin = patrolCenterX - patrolHalfWidth;
            float xMax = patrolCenterX + patrolHalfWidth;
            float zMin = patrolCenterZ - patrolHalfDepth;
            float zMax = patrolCenterZ + patrolHalfDepth;
            return new[]
            {
                new Vector3(xMin, 0f, zMax), // 왼쪽 위 (스폰 지점)
                new Vector3(xMin, 0f, zMin), // 왼쪽 아래
                new Vector3(xMax, 0f, zMin), // 오른쪽 아래
                new Vector3(xMax, 0f, zMax), // 오른쪽 위
            };
        }

        private static void HandleRoundStarted(int round, bool isLeftBoss, bool isRightBoss)
        {
            string suffix = isLeftBoss ? " (좌측 보스)" : isRightBoss ? " (우측 보스)" : "";
            Debug.Log($"[MRD] {round}라운드 시작{suffix}");
        }

        // 순찰 경로(=배치 격자를 둘러싼 플레이 영역) 전체가 화면에 넉넉히 들어오도록,
        // 경로의 중심(patrolCenterX/Z)을 바라보되 폭/깊이 중 큰 쪽에 비례해서 카메라를 뒤로 뺀다.
        // 순찰 경로 크기를 Inspector에서 바꿔도 카메라가 항상 알아서 다시 맞춰진다.
        private void SetupCamera()
        {
            CreateGround();

            var cam = Camera.main;
            if (cam == null) return;

            float span = Mathf.Max(patrolHalfWidth, patrolHalfDepth) * 2f;
            var lookAt = new Vector3(patrolCenterX, 0f, patrolCenterZ);

            cam.orthographic = false;
            cam.fieldOfView = 50f;
            cam.transform.position = lookAt + new Vector3(0f, span * 0.7f, -span * 1.1f);
            cam.transform.LookAt(lookAt);
        }

        // 3D 공간에서 오브젝트들이 허공에 떠 있는 것처럼 보이지 않도록 넣어두는 임시 바닥.
        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f); // 기본 10x10 평면 -> 50x50 (순찰 경로+배치 격자를 더 넉넉하게 늘리기 위해 확장)

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

            if (characterDatabase == null)
                characterDatabase = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/Data/CharacterDatabase.asset");
            if (goldSummonTable == null)
                goldSummonTable = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Gacha/GoldSummon.asset");
            if (gemBasicSummonTable == null)
                gemBasicSummonTable = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Gacha/GemBasicSummon.asset");
            if (gemMidSummonTable == null)
                gemMidSummonTable = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Gacha/GemMidSummon.asset");
            if (gemAdvancedSummonTable == null)
                gemAdvancedSummonTable = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Gacha/GemAdvancedSummon.asset");
            if (fusionTestTarget == null)
                fusionTestTarget = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Olympus/Zeus.asset");
        }
#endif
    }
}
