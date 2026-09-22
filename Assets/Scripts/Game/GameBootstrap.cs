using System.Collections.Generic;
using UnityEngine;
using MRD.Data;
using MRD.Battle;
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
        [SerializeField] private MonsterData raidBossTemplate;
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
            DrawPatrolPathVisual();
            var selectionController = gameObject.AddComponent<SelectionController>();
            gameObject.AddComponent<CameraEdgePan>();
            gameObject.AddComponent<CameraZoom>();

            var gameGo = new GameObject("GameManager");
            _game = gameGo.AddComponent<GameManager>();
            _game.Configure(gridWidth, gridHeight, waveConfig, lineMonsterTemplate, bossTemplate, bossTemplate);
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
            SetupRaid();

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

        // 보스 레이드: 순찰 경로 안쪽 한 구석에 입구 포탈을 두고, 웨이브 지역과 완전히 떨어진 곳에
        // 순찰 경로와 같은 크기의 레이드 장소를 만든다. 레이드 장소 반대쪽 구석엔 귀환 포탈을 둔다.
        private void SetupRaid()
        {
            const float margin = 2f;
            var raidCenter = new Vector3(patrolCenterX + 200f, 0f, patrolCenterZ); // 웨이브 지역과 겹치지 않도록 멀리 떨어뜨린다

            CreateFlatGround(raidCenter, patrolHalfWidth, patrolHalfDepth, new Color(0.16f, 0.12f, 0.2f));

            // 순찰 경로 안쪽, 오른쪽 위 구석에 입구를 둔다 (경계선에 딱 붙지 않도록 margin만큼 안쪽으로).
            var entrancePos = new Vector3(patrolCenterX + patrolHalfWidth - margin, 0f, patrolCenterZ + patrolHalfDepth - margin);
            var entranceArrival = raidCenter + new Vector3(-patrolHalfWidth + margin, 0f, -patrolHalfDepth + margin);

            // 귀환 포탈은 레이드 장소 반대쪽(오른쪽 위) 구석에 둬서 도착 지점과 겹치지 않게 한다.
            var returnPos = raidCenter + new Vector3(patrolHalfWidth - margin, 0f, patrolHalfDepth - margin);
            var returnArrival = entrancePos + new Vector3(-3f, 0f, 0f); // 입구 포탈에 바로 다시 안 걸리도록 살짝 떨어뜨림

            var cam = Camera.main;

            var entranceGo = new GameObject("RaidEntrancePortal");
            entranceGo.transform.position = entrancePos;
            UnitVisual.AttachCube(entranceGo.transform, new Color(0.6f, 0.2f, 0.9f), 1.2f);
            var entrancePortal = entranceGo.AddComponent<RaidPortal>();
            entrancePortal.Setup(entranceArrival, cam);

            var returnGo = new GameObject("RaidReturnPortal");
            returnGo.transform.position = returnPos;
            UnitVisual.AttachCube(returnGo.transform, new Color(0.2f, 0.8f, 0.9f), 1.2f);
            var returnPortal = returnGo.AddComponent<RaidPortal>();
            returnPortal.Setup(returnArrival, cam);

            var raidManagerGo = new GameObject("RaidManager");
            raidManagerGo.AddComponent<RaidManager>().Setup(_game, raidBossTemplate, raidCenter, entrancePortal, returnPortal);
        }

        // CreateGround와 같은 방식이지만, 위치/크기/색을 지정할 수 있는 버전 (레이드 장소용).
        private static void CreateFlatGround(Vector3 center, float halfWidth, float halfDepth, Color color)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "RaidGround";
            ground.transform.position = center;
            ground.transform.localScale = new Vector3(halfWidth * 2f / 10f, 1f, halfDepth * 2f / 10f); // 기본 10x10 평면 기준 스케일 환산

            var renderer = ground.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            renderer.material = new Material(shader) { color = color };
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

        // 몬스터가 순찰하는 경로를 바닥 위에 밝은 색 테두리로 그려서 한눈에 보이게 한다.
        // LineRenderer는 카메라 각도에 따라 이어지는 부분이 끊겨 보이는 문제가 반복돼서,
        // 대신 이미 검증된 큐브 프리미티브 4개(네 변)를 바닥에 눕혀서 테두리를 만든다.
        private void DrawPatrolPathVisual()
        {
            float xMin = patrolCenterX - patrolHalfWidth;
            float xMax = patrolCenterX + patrolHalfWidth;
            float zMin = patrolCenterZ - patrolHalfDepth;
            float zMax = patrolCenterZ + patrolHalfDepth;

            const float thickness = 0.8f; // 일반 몬스터 큐브 크기와 맞춘 값
            const float height = 0.1f; // 바닥에 거의 붙어있는 얇은 두께
            var color = new Color(1f, 0.9f, 0.15f); // 바닥/유닛 색과 구분되는 밝은 노란색

            // 네 변의 길이에 두께만큼을 더해서 모서리에서 서로 겹치게 만들어, 이음매에 빈틈이 생기지 않게 한다.
            CreatePathSegment(new Vector3((xMin + xMax) / 2f, height / 2f, zMin), new Vector3(xMax - xMin + thickness, height, thickness), color);
            CreatePathSegment(new Vector3((xMin + xMax) / 2f, height / 2f, zMax), new Vector3(xMax - xMin + thickness, height, thickness), color);
            CreatePathSegment(new Vector3(xMin, height / 2f, (zMin + zMax) / 2f), new Vector3(thickness, height, zMax - zMin + thickness), color);
            CreatePathSegment(new Vector3(xMax, height / 2f, (zMin + zMax) / 2f), new Vector3(thickness, height, zMax - zMin + thickness), color);
        }

        private static void CreatePathSegment(Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PatrolPathSegment";
            go.transform.position = position;
            go.transform.localScale = scale;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider); // 선택 판정은 화면좌표 기반이라 콜라이더가 필요 없다

            var renderer = go.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            renderer.material = new Material(shader) { color = color };
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
            if (raidBossTemplate == null)
                raidBossTemplate = AssetDatabase.LoadAssetAtPath<MonsterData>("Assets/Data/Monsters/RaidBoss_AncientColossus.asset");

            if (starterRoster.Count == 0)
            {
                string[] paths =
                {
                    "Assets/Data/Characters/Canine/Puppy.asset",
                    "Assets/Data/Characters/Canine/Werewolf.asset",
                    "Assets/Data/Characters/Feline/WhiteTiger.asset",
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
                fusionTestTarget = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Feline/WhiteTiger.asset");
        }
#endif
    }
}
