using UnityEngine;
using MRD.Battle;
using MRD.Wave;

namespace MRD.Game
{
    /// <summary>
    /// 보스 레이드 진행을 관리한다: 입구 포탈로 들어가면 레이드 장소로 이동해서 가만히 있는
    /// 보스만 때리고, 귀환 포탈로 웨이브 지역으로 돌아온다. 보스를 처치하면 보석을 주고
    /// 잠시 후 새 보스가 다시 등장한다(반복 파밍 가능).
    /// </summary>
    public class RaidManager : MonoBehaviour
    {
        [SerializeField] private float bossRespawnDelay = 5f;
        [SerializeField] private int gemReward = 50;

        private GameManager _game;
        private MonsterData _bossTemplate;
        private Vector3 _bossPosition;
        private RaidPortal _entrancePortal;
        private RaidPortal _returnPortal;

        private EnemyUnit _currentBoss;
        private GameObject _currentBossGo;
        private bool _waitingRespawn;
        private float _respawnTimer;

        public EnemyUnit CurrentBoss => _currentBoss;

        public void Setup(GameManager game, MonsterData bossTemplate, Vector3 bossPosition,
            RaidPortal entrancePortal, RaidPortal returnPortal)
        {
            _game = game;
            _bossTemplate = bossTemplate;
            _bossPosition = bossPosition;
            _entrancePortal = entrancePortal;
            _returnPortal = returnPortal;

            SpawnBoss();
        }

        private void Update()
        {
            var units = FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
            _entrancePortal.Tick(units);
            _returnPortal.Tick(units);

            if (!_waitingRespawn) return;

            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
            {
                _waitingRespawn = false;
                SpawnBoss();
            }
        }

        private void SpawnBoss()
        {
            if (_bossTemplate == null)
            {
                Debug.LogError("[MRD] RaidManager: 레이드 보스 MonsterData가 비어있어 보스를 등장시키지 못했다.");
                return;
            }

            _currentBossGo = new GameObject("RaidBoss");
            _currentBossGo.transform.position = _bossPosition;

            _currentBoss = _currentBossGo.AddComponent<EnemyUnit>();
            _currentBoss.Initialize(_bossTemplate, round: 1);
            _currentBoss.OnDeath += HandleBossDeath;

            UnitVisual.AttachCube(_currentBossGo.transform, new Color(0.5f, 0.05f, 0.6f), 2f); // 보스답게 큼직한 보라색

            _game.CombatManager.RegisterEnemyTarget(_currentBoss);
        }

        private void HandleBossDeath(EnemyUnit boss)
        {
            _game.GrantGems(gemReward);
            _game.CombatManager.UnregisterEnemyTarget(boss);

            if (Application.isPlaying)
                Destroy(_currentBossGo);
            else
                DestroyImmediate(_currentBossGo); // 헤드리스 테스트(에디터, 플레이 모드 아님)에서 호출되는 경우 대비

            _waitingRespawn = true;
            _respawnTimer = bossRespawnDelay;
        }
    }
}
