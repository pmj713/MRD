using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MRD.Data;
using MRD.Battle;
using MRD.Wave;
using MRD.Game;
using MRD.Gacha;
using MRD.Control;
using UnityEditor.SceneManagement;

namespace MRD.EditorTools
{
    /// <summary>
    /// CharacterData 에셋과 BattleUnit 스탯 계산이 정상 동작하는지 확인하는 헤드리스 점검 스크립트.
    /// 유니티 에디터를 열지 않고도 `-executeMethod`로 배치 모드에서 실행할 수 있다.
    /// </summary>
    public static class SmokeTestRunner
    {
        [MenuItem("MRD/Run Character Smoke Test")]
        public static void RunSmokeTest()
        {
            var puppy = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Canine/Puppy.asset");
            var werewolf = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Canine/Werewolf.asset");
            var whiteTiger = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Feline/WhiteTiger.asset");

            bool ok = true;
            ok &= Check(puppy, "강아지");
            ok &= Check(werewolf, "웨어울프");
            ok &= Check(whiteTiger, "백호");

            if (whiteTiger != null && whiteTiger.fusionRecipe.requiredCharacters != null && whiteTiger.fusionRecipe.requiredCharacters.Length > 0)
            {
                var material = whiteTiger.fusionRecipe.requiredCharacters[0];
                ok &= LogAndCheck("백호 조합 재료 참조", material != null, $"{material?.characterName}");
            }
            else
            {
                Debug.LogError("[SmokeTest] 백호 fusionRecipe.requiredCharacters 참조 실패");
                ok = false;
            }

            // BattleUnit이 CharacterData의 스탯을 그대로 EffectiveStats로 반영하는지, 스킬 마나/쿨타임 판정이 맞는지 검증
            var go = new GameObject("SmokeTest_BattleUnit");
            try
            {
                var unit = go.AddComponent<BattleUnit>();
                unit.Initialize(whiteTiger);

                ok &= ApproxLog("EffectiveStats.attackPower == 기본 스탯", unit.EffectiveStats.attackPower, whiteTiger.stats.attackPower);
                ok &= ApproxLog("EffectiveStats.attackSpeed == 기본 스탯", unit.EffectiveStats.attackSpeed, whiteTiger.stats.attackSpeed);

                bool usedSkill = unit.TryUseActiveSkill();
                ok &= LogAndCheck("마나 부족 상태에서 스킬 사용 실패해야 함", !usedSkill, usedSkill.ToString());

                unit.AddMana(whiteTiger.activeSkill.manaCost);
                usedSkill = unit.TryUseActiveSkill();
                ok &= LogAndCheck("마나 충전 후 스킬 사용 성공해야 함", usedSkill, usedSkill.ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            ok &= CheckFullRoster();
            ok &= CheckCombat(werewolf, whiteTiger);
            ok &= CheckAttackRangeTargeting();
            ok &= CheckWaveSpawner();
            ok &= CheckBattleUnitAttacksEnemyUnit(werewolf);
            ok &= CheckPlacementGrid(puppy, werewolf, whiteTiger);
            ok &= CheckGameManager(puppy, werewolf, whiteTiger);
            ok &= CheckGachaAndFusion();
            ok &= CheckFusionChains();
            ok &= CheckFuseSameMaterialTriple();
            ok &= CheckSummonAutoPlaceAndSell();
            ok &= CheckFuseRemovesFieldMaterials();
            ok &= CheckSelectionMath();
            ok &= CheckUnitMover();
            ok &= CheckRaidPortalTeleport();
            ok &= CheckRaidBossDeathGrantsGemsAndRespawns();
            // 씬을 다시 로드하면 그 전에 로드해둔 CharacterData 참조가 무효화될 수 있으니 항상 마지막에 실행한다.
            ok &= CheckSceneSetup();

            if (ok)
                Debug.Log("[SmokeTest] 모든 검증 통과");
            else
                Debug.LogError("[SmokeTest] 일부 검증 실패 - 위 로그 확인");
        }

        private static bool Check(CharacterData data, string expectedName)
        {
            if (data == null)
            {
                Debug.LogError($"[SmokeTest] 에셋 로드 실패: {expectedName}");
                return false;
            }

            Debug.Log($"[SmokeTest] 로드됨: {data.characterName} (등급={data.rarity}, 공격력={data.stats.attackPower})");
            return true;
        }

        private static bool ApproxLog(string label, float actual, float expected)
        {
            bool pass = Mathf.Abs(actual - expected) < 0.01f;
            Debug.Log($"[SmokeTest] {label}: actual={actual}, expected={expected}, pass={pass}");
            return pass;
        }

        private static bool LogAndCheck(string label, bool pass, string detail)
        {
            Debug.Log($"[SmokeTest] {label}: {detail}, pass={pass}");
            return pass;
        }

        // 프로젝트에 있는 모든 CharacterData 에셋이 깨짐 없이 로드되고, 등급별 개수가 기대치와 맞는지 검증한다.
        private static bool CheckFullRoster()
        {
            bool ok = true;
            var guids = AssetDatabase.FindAssets("t:CharacterData");
            var countByRarity = new Dictionary<Rarity, int>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                if (data == null)
                {
                    Debug.LogError($"[SmokeTest] 로드 실패: {path}");
                    ok = false;
                    continue;
                }

                if (string.IsNullOrEmpty(data.characterName))
                {
                    Debug.LogError($"[SmokeTest] characterName 비어있음: {path}");
                    ok = false;
                }

                countByRarity.TryGetValue(data.rarity, out var current);
                countByRarity[data.rarity] = current + 1;
            }

            Debug.Log($"[SmokeTest] 전체 CharacterData 에셋 수: {guids.Length}");
            foreach (var kv in countByRarity)
                Debug.Log($"[SmokeTest]  - {kv.Key}: {kv.Value}개");

            ok &= LogAndCheck("전체 에셋 40개", guids.Length == 40, guids.Length.ToString());

            // 동물 로스터: 노말 10 / 매직 7 / 레어 7 / 유니크 7 / 전설 5 / 히든 4
            var expectedByRarity = new Dictionary<Rarity, int>
            {
                { Rarity.Normal, 10 },
                { Rarity.Magic, 7 },
                { Rarity.Rare, 7 },
                { Rarity.Unique, 7 },
                { Rarity.Legend, 5 },
                { Rarity.Hidden, 4 },
            };
            foreach (var kv in expectedByRarity)
            {
                int actual = countByRarity.GetValueOrDefault(kv.Key);
                ok &= LogAndCheck($"{kv.Key} 등급 유닛 수 ({kv.Value}개 기대)", actual == kv.Value, actual.ToString());
            }

            return ok;
        }

        // CombatManager가 평타 데미지/마나 획득/사망 처리/트리거 패시브를 실제로 처리하는지 검증한다.
        private static bool CheckCombat(CharacterData werewolfData, CharacterData whiteTigerData)
        {
            bool ok = true;
            var managerGo = new GameObject("SmokeTest_CombatManager");
            var allyGo = new GameObject("SmokeTest_Ally_Werewolf");
            var enemyGo = new GameObject("SmokeTest_Enemy_Target");
            var tigerGo = new GameObject("SmokeTest_Ally_WhiteTiger");
            var dummyGo = new GameObject("SmokeTest_Enemy_Dummy");
            MonsterData enemyMonster = null;
            MonsterData dummyMonster = null;

            try
            {
                var manager = managerGo.AddComponent<CombatManager>();

                var werewolfUnit = allyGo.AddComponent<BattleUnit>();
                werewolfUnit.Initialize(werewolfData);

                // 아군(BattleUnit)은 몬스터에게 공격받지 않으므로, 대상 역할은 항상 실제 EnemyUnit(합성 MonsterData)으로 만든다.
                enemyMonster = ScriptableObject.CreateInstance<MonsterData>();
                enemyMonster.monsterName = "전투 테스트용 몬스터";
                enemyMonster.baseHealth = 500f;
                enemyMonster.baseArmor = 0f;
                enemyMonster.healthGrowthPerRound = 1f;
                var enemyUnit = enemyGo.AddComponent<EnemyUnit>();
                enemyUnit.Initialize(enemyMonster, round: 1);

                bool deathFired = false;
                enemyUnit.OnDeath += _ => deathFired = true;

                manager.RegisterAlly(werewolfUnit);
                manager.RegisterEnemyTarget(enemyUnit);

                // 평타 1회: 몬스터는 방어력 0이라 데미지가 [무크리, 크리] 범위 안에 들어야 한다.
                float healthBefore = enemyUnit.CurrentHealth;
                manager.ProcessAttack(werewolfUnit);
                float actualDamage = healthBefore - enemyUnit.CurrentHealth;
                float minDamage = werewolfData.stats.attackPower;
                float maxDamage = werewolfData.stats.attackPower * werewolfData.stats.criticalMultiplier;
                ok &= LogAndCheck("평타 데미지가 기대 범위 내(무크리~크리)",
                    actualDamage >= minDamage - 0.01f && actualDamage <= maxDamage + 0.01f, actualDamage.ToString());
                ok &= ApproxLog("평타 1회 후 마나 증가", werewolfUnit.CurrentMana, 10f);

                // 죽을 때까지 반복 공격 -> OnDeath 발생 확인
                for (int i = 0; i < 20 && !enemyUnit.IsDead; i++)
                    manager.ProcessAttack(werewolfUnit);

                ok &= LogAndCheck("적 유닛이 사망 처리됨", enemyUnit.IsDead, enemyUnit.IsDead.ToString());
                ok &= LogAndCheck("OnDeath 이벤트 발생", deathFired, deathFired.ToString());

                // 대상이 사라진 뒤에도 예외 없이 처리되어야 한다 (자동 등록 해제 확인)
                try
                {
                    manager.ProcessAttack(werewolfUnit);
                    ok &= LogAndCheck("대상 없을 때 ProcessAttack 예외 없이 처리", true, "OK");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SmokeTest] 대상 없을 때 예외 발생: {e}");
                    ok = false;
                }

                // 백호의 트리거 패시브(15%)가 통계적으로 실제 발동하는지 확인.
                // 대상은 체력을 사실상 무한으로 부풀린 더미 몬스터로 만들어 도중에 죽지 않게 한다.
                var tigerUnit = tigerGo.AddComponent<BattleUnit>();
                tigerUnit.Initialize(whiteTigerData);

                dummyMonster = ScriptableObject.CreateInstance<MonsterData>();
                dummyMonster.monsterName = "트리거 테스트용 더미";
                dummyMonster.baseHealth = 100000000f;
                dummyMonster.baseArmor = 0f;
                dummyMonster.healthGrowthPerRound = 1f;
                var dummyUnit = dummyGo.AddComponent<EnemyUnit>();
                dummyUnit.Initialize(dummyMonster, round: 1);

                manager.RegisterAlly(tigerUnit);
                manager.RegisterEnemyTarget(dummyUnit);

                float mitigation = 100f / (100f + dummyMonster.baseArmor);
                float maxNoTriggerDamage = tigerUnit.EffectiveStats.attackPower * mitigation * tigerUnit.EffectiveStats.criticalMultiplier;

                bool triggerObserved = false;
                for (int i = 0; i < 200; i++)
                {
                    float before = dummyUnit.CurrentHealth;
                    manager.ProcessAttack(tigerUnit);
                    float damage = before - dummyUnit.CurrentHealth;
                    if (damage > maxNoTriggerDamage + 1f)
                    {
                        triggerObserved = true;
                        break;
                    }
                }

                ok &= LogAndCheck("백호 트리거 패시브가 200회 평타 중 최소 1회 발동", triggerObserved, triggerObserved.ToString());
            }
            finally
            {
                Object.DestroyImmediate(managerGo);
                Object.DestroyImmediate(allyGo);
                Object.DestroyImmediate(enemyGo);
                Object.DestroyImmediate(tigerGo);
                Object.DestroyImmediate(dummyGo);
                if (enemyMonster != null) Object.DestroyImmediate(enemyMonster);
                if (dummyMonster != null) Object.DestroyImmediate(dummyMonster);
            }

            return ok;
        }

        // CombatManager가 attackRange(사거리) 안의 대상 중 "가장 먼저 등장한(=가장 오래 살아있는)" 쪽을
        // 고르는지 검증한다. 일부러 먼저 등장한 쪽을 더 멀리(그래도 사거리 안에) 두고, 나중에 등장한 쪽을
        // 더 가깝게 둬서 - "거리"가 아니라 "등장 순서"로 고른다는 걸 구분해서 확인한다.
        // 사거리 밖 대상은 등록돼 있어도(가장 먼저 등장했어도) 무시돼야 한다.
        private static bool CheckAttackRangeTargeting()
        {
            bool ok;
            var managerGo = new GameObject("SmokeTest_RangeCombatManager");
            var attackerGo = new GameObject("SmokeTest_RangeAttacker");
            var oldGo = new GameObject("SmokeTest_RangeOld");
            var newGo = new GameObject("SmokeTest_RangeNew");
            var outOfRangeGo = new GameObject("SmokeTest_RangeOutOfRange");
            CharacterData attackerData = null;
            MonsterData targetMonster = null;

            try
            {
                var manager = managerGo.AddComponent<CombatManager>();

                attackerData = ScriptableObject.CreateInstance<CharacterData>();
                attackerData.characterName = "사거리 테스트용 유닛";
                attackerData.stats = new CharacterStats
                {
                    attackPower = 10f,
                    attackSpeed = 1f,
                    criticalMultiplier = 1f,
                    attackRange = 5f,
                };

                var attacker = attackerGo.AddComponent<BattleUnit>();
                attacker.Initialize(attackerData);
                attackerGo.transform.position = Vector3.zero;

                targetMonster = ScriptableObject.CreateInstance<MonsterData>();
                targetMonster.monsterName = "사거리 테스트용 몬스터";
                targetMonster.baseHealth = 1000f;
                targetMonster.baseArmor = 0f;
                targetMonster.healthGrowthPerRound = 1f;

                // outOfRange를 old/new보다 먼저 등장시켜서, "가장 먼저 등장" 규칙이 사거리 필터보다
                // 우선하지 않는지(=사거리 밖이면 아무리 먼저 나왔어도 제외되는지)까지 같이 확인한다.
                var outOfRange = outOfRangeGo.AddComponent<EnemyUnit>();
                outOfRange.Initialize(targetMonster, round: 1);
                outOfRangeGo.transform.position = new Vector3(10f, 0f, 0f); // 거리 10 (사거리 밖)

                var old = oldGo.AddComponent<EnemyUnit>();
                old.Initialize(targetMonster, round: 1); // outOfRange보다 나중, new보다 먼저 등장 (SpawnOrder가 더 작음)
                oldGo.transform.position = new Vector3(4.5f, 0f, 0f); // 거리 4.5 (사거리 이내, new보다 멂)

                var newer = newGo.AddComponent<EnemyUnit>();
                newer.Initialize(targetMonster, round: 1); // 가장 나중에 등장
                newGo.transform.position = new Vector3(3f, 0f, 0f); // 거리 3 (사거리 이내, old보다 가까움)

                manager.RegisterAlly(attacker);
                manager.RegisterEnemyTarget(newer); // 등록 순서도 일부러 섞어서, 등록 순서가 아니라 SpawnOrder로 고르는지 확인
                manager.RegisterEnemyTarget(outOfRange);
                manager.RegisterEnemyTarget(old);

                float oldHealthBefore = old.CurrentHealth;
                float newHealthBefore = newer.CurrentHealth;
                float outHealthBefore = outOfRange.CurrentHealth;

                manager.ProcessAttack(attacker);

                ok = LogAndCheck("사거리 안에서 더 멀지만 먼저 등장한 대상이 피격됨",
                    old.CurrentHealth < oldHealthBefore, old.CurrentHealth.ToString());
                ok &= LogAndCheck("더 가깝지만 나중에 등장한 대상은 안 맞음",
                    Mathf.Approximately(newer.CurrentHealth, newHealthBefore), newer.CurrentHealth.ToString());
                ok &= LogAndCheck("가장 먼저 등장했어도 사거리 밖이면 안 맞음",
                    Mathf.Approximately(outOfRange.CurrentHealth, outHealthBefore), outOfRange.CurrentHealth.ToString());

                // old가 빠지면 사거리 안에 남은 newer가 다음으로 오래된 대상으로서 맞아야 한다.
                manager.UnregisterEnemyTarget(old);
                float newHealthBefore2 = newer.CurrentHealth;
                manager.ProcessAttack(attacker);
                ok &= LogAndCheck("가장 오래된 대상이 빠지면 다음으로 오래된(사거리 안) 대상이 피격됨",
                    newer.CurrentHealth < newHealthBefore2, newer.CurrentHealth.ToString());

                // 사거리 안에 아무도 안 남으면 아예 공격하지 않아야 한다 (사거리 무제한으로 새지 않는지 확인)
                manager.UnregisterEnemyTarget(newer);
                float outHealthBefore2 = outOfRange.CurrentHealth;
                manager.ProcessAttack(attacker);
                ok &= LogAndCheck("사거리 안에 대상이 없으면 공격하지 않음",
                    Mathf.Approximately(outOfRange.CurrentHealth, outHealthBefore2), outOfRange.CurrentHealth.ToString());
            }
            finally
            {
                Object.DestroyImmediate(managerGo);
                Object.DestroyImmediate(attackerGo);
                Object.DestroyImmediate(oldGo);
                Object.DestroyImmediate(newGo);
                Object.DestroyImmediate(outOfRangeGo);
                if (attackerData != null) Object.DestroyImmediate(attackerData);
                if (targetMonster != null) Object.DestroyImmediate(targetMonster);
            }

            return ok;
        }

        // WaveSpawner가 라운드 진행/보스 등장 규칙/생존 몬스터 상한/필수클리어 게임오버를 올바르게 처리하는지 검증한다.
        private static bool CheckWaveSpawner()
        {
            bool ok = true;

            var defaultConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/Data/Wave/DefaultWaveConfig.asset");
            var lineMonster = AssetDatabase.LoadAssetAtPath<MonsterData>("Assets/Data/Monsters/LineMonster_Basic.asset");
            var boss = AssetDatabase.LoadAssetAtPath<MonsterData>("Assets/Data/Monsters/Boss_ChaosGuardian.asset");

            ok &= LogAndCheck("DefaultWaveConfig 로드", defaultConfig != null, (defaultConfig != null).ToString());
            ok &= LogAndCheck("LineMonster_Basic 로드", lineMonster != null, (lineMonster != null).ToString());
            ok &= LogAndCheck("Boss_ChaosGuardian 로드", boss != null, (boss != null).ToString());
            if (defaultConfig != null)
                ok &= LogAndCheck("DefaultWaveConfig.totalRounds == 85", defaultConfig.totalRounds == 85, defaultConfig.totalRounds.ToString());

            // 빠른 시뮬레이션을 위한 합성 몬스터 템플릿 (진행속도를 크게 올려 라운드 시간 내에 도착하게 함)
            var fastMonster = ScriptableObject.CreateInstance<MonsterData>();
            fastMonster.monsterName = "테스트용 마수";
            fastMonster.baseHealth = 50;
            fastMonster.baseArmor = 0;
            fastMonster.healthGrowthPerRound = 1f;
            fastMonster.moveSpeed = 200f; // 0.5초면 도착
            fastMonster.goldReward = 1;

            var fastBoss = ScriptableObject.CreateInstance<MonsterData>();
            fastBoss.monsterName = "테스트용 보스";
            fastBoss.baseHealth = 50;
            fastBoss.baseArmor = 0;
            fastBoss.healthGrowthPerRound = 1f;
            fastBoss.moveSpeed = 200f;
            fastBoss.goldReward = 10;
            fastBoss.isBoss = true;

            ok &= CheckWaveProgressionAndBossRounds(fastMonster);
            ok &= CheckWaveMaxAliveMonstersGameOver(fastMonster);
            ok &= CheckWaveMandatoryClearFailure(fastMonster);

            Object.DestroyImmediate(fastMonster);
            Object.DestroyImmediate(fastBoss);

            return ok;
        }

        // 시나리오 A: 보스 없는 15라운드를 전부 통과하면서, 좌/우 보스 라운드 판정이 맞는지 확인한다.
        private static bool CheckWaveProgressionAndBossRounds(MonsterData fastMonster)
        {
            var config = ScriptableObject.CreateInstance<WaveConfig>();
            config.totalRounds = 15;
            config.monstersPerRound = 2;
            config.earlyRoundThreshold = 0; // 전 라운드 동일한 길이 사용
            config.earlyRoundDuration = 2f;
            config.lateRoundDuration = 2f;
            config.leftBossInterval = 10;
            config.rightBossOffset = 3;
            config.mandatoryClearRounds = new int[0];

            var go = new GameObject("SmokeTest_WaveSpawner_A");
            bool ok;
            try
            {
                var spawner = go.AddComponent<WaveSpawner>();
                spawner.Configure(config, fastMonster, fastMonster, fastMonster);

                var bossRoundsSeen = new Dictionary<int, (bool left, bool right)>();
                int spawnCount = 0;
                spawner.OnRoundStarted += (round, left, right) => bossRoundsSeen[round] = (left, right);
                spawner.OnMonsterSpawned += _ => spawnCount++;

                spawner.StartRun();
                for (int i = 0; i < 2000 && !spawner.IsAllRoundsCleared && !spawner.IsGameOver; i++)
                    spawner.Tick(0.1f);

                ok = LogAndCheck("15라운드 전부 클리어(보스 없음)", spawner.IsAllRoundsCleared, spawner.IsAllRoundsCleared.ToString());
                ok &= LogAndCheck("게임오버 발생 안 함", !spawner.IsGameOver, spawner.IsGameOver.ToString());
                ok &= LogAndCheck("10라운드 = 좌측 보스", bossRoundsSeen.TryGetValue(10, out var r10) && r10.left, bossRoundsSeen.GetValueOrDefault(10).ToString());
                ok &= LogAndCheck("3라운드 = 우측 보스", bossRoundsSeen.TryGetValue(3, out var r3) && r3.right, bossRoundsSeen.GetValueOrDefault(3).ToString());
                ok &= LogAndCheck("13라운드 = 우측 보스", bossRoundsSeen.TryGetValue(13, out var r13) && r13.right, bossRoundsSeen.GetValueOrDefault(13).ToString());
                // 15라운드 * 2마리 + 보스 3회(3,10,13라운드) = 33마리
                ok &= LogAndCheck("총 스폰 수 33마리", spawnCount == 33, spawnCount.ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
            }

            return ok;
        }

        // 시나리오 B: 몬스터는 죽지 않는 한 계속 순찰하며 쌓이므로, 생존 마릿수가 상한에 도달하면 게임오버가 되어야 한다.
        private static bool CheckWaveMaxAliveMonstersGameOver(MonsterData fastMonster)
        {
            var config = ScriptableObject.CreateInstance<WaveConfig>();
            config.totalRounds = 20;
            config.monstersPerRound = 1;
            config.earlyRoundThreshold = 0;
            config.earlyRoundDuration = 1f;
            config.lateRoundDuration = 1f;
            config.leftBossInterval = 0;
            config.rightBossOffset = 0;
            config.mandatoryClearRounds = new int[0];
            config.maxAliveMonsters = 3;

            var go = new GameObject("SmokeTest_WaveSpawner_B");
            bool ok;
            try
            {
                var spawner = go.AddComponent<WaveSpawner>();
                spawner.Configure(config, fastMonster, null, null);

                string gameOverReason = null;
                spawner.OnGameOver += reason => gameOverReason = reason;

                spawner.StartRun();
                for (int i = 0; i < 2000 && !spawner.IsAllRoundsCleared && !spawner.IsGameOver; i++)
                    spawner.Tick(0.1f);

                ok = LogAndCheck("생존 몬스터 상한 도달로 게임오버 발생", spawner.IsGameOver, spawner.IsGameOver.ToString());
                ok &= LogAndCheck("생존 몬스터 수가 상한과 일치", spawner.AliveMonsterCount == 3, spawner.AliveMonsterCount.ToString());
                ok &= LogAndCheck("게임오버 사유에 '생존 몬스터' 포함", gameOverReason != null && gameOverReason.Contains("생존 몬스터"), gameOverReason ?? "null");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
            }

            return ok;
        }

        // 시나리오 C: 필수 클리어 라운드의 몬스터를 처치하지 못하고 통과시키면 즉시 게임오버가 되어야 한다.
        private static bool CheckWaveMandatoryClearFailure(MonsterData fastMonster)
        {
            var config = ScriptableObject.CreateInstance<WaveConfig>();
            config.totalRounds = 5;
            config.monstersPerRound = 1;
            config.earlyRoundThreshold = 0;
            config.earlyRoundDuration = 1f;
            config.lateRoundDuration = 1f;
            config.leftBossInterval = 0;
            config.rightBossOffset = 0;
            config.mandatoryClearRounds = new[] { 2 };

            var go = new GameObject("SmokeTest_WaveSpawner_C");
            bool ok;
            try
            {
                var spawner = go.AddComponent<WaveSpawner>();
                spawner.Configure(config, fastMonster, null, null);

                string gameOverReason = null;
                spawner.OnGameOver += reason => gameOverReason = reason;

                spawner.StartRun();
                for (int i = 0; i < 200 && !spawner.IsAllRoundsCleared && !spawner.IsGameOver; i++)
                    spawner.Tick(0.1f);

                ok = LogAndCheck("필수 클리어 실패로 게임오버 발생", spawner.IsGameOver, spawner.IsGameOver.ToString());
                ok &= LogAndCheck("게임오버 사유에 '필수 클리어 실패' 포함", gameOverReason != null && gameOverReason.Contains("필수 클리어 실패"), gameOverReason ?? "null");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
            }

            return ok;
        }

        // BattleUnit(아군)이 CombatManager를 통해 실제 EnemyUnit(웨이브 몬스터)을 공격하는지 검증한다.
        private static bool CheckBattleUnitAttacksEnemyUnit(CharacterData werewolfData)
        {
            bool ok = true;
            var lineMonster = AssetDatabase.LoadAssetAtPath<MonsterData>("Assets/Data/Monsters/LineMonster_Basic.asset");

            var managerGo = new GameObject("SmokeTest_BvE_CombatManager");
            var allyGo = new GameObject("SmokeTest_BvE_Ally");
            var enemyGo = new GameObject("SmokeTest_BvE_Enemy");

            try
            {
                var manager = managerGo.AddComponent<CombatManager>();

                var werewolfUnit = allyGo.AddComponent<BattleUnit>();
                werewolfUnit.Initialize(werewolfData);
                manager.RegisterAlly(werewolfUnit);

                var enemy = enemyGo.AddComponent<EnemyUnit>();
                enemy.Initialize(lineMonster, round: 1);
                manager.RegisterEnemyTarget(enemy);

                // 웨어울프가 평타로 실제 EnemyUnit의 체력을 깎아야 한다 (몬스터 방어력 0 -> 감쇄 없음).
                float healthBefore = enemy.CurrentHealth;
                manager.ProcessAttack(werewolfUnit);
                float damage = healthBefore - enemy.CurrentHealth;
                float minDamage = werewolfData.stats.attackPower;
                float maxDamage = werewolfData.stats.attackPower * werewolfData.stats.criticalMultiplier;
                ok &= LogAndCheck("BattleUnit -> EnemyUnit 평타 데미지 범위 내",
                    damage >= minDamage - 0.01f && damage <= maxDamage + 0.01f, damage.ToString());

                // 몬스터가 죽으면 더 이상 유효 타겟이 아니어야 한다.
                enemy.TakeTrueDamage(9999f);
                ok &= LogAndCheck("사망한 몬스터는 IsTargetable == false", !enemy.IsTargetable, enemy.IsTargetable.ToString());

                float healthBeforeSecond = enemy.CurrentHealth;
                manager.ProcessAttack(werewolfUnit);
                ok &= ApproxLog("사망한 몬스터는 더 이상 공격받지 않음", enemy.CurrentHealth, healthBeforeSecond);
            }
            finally
            {
                Object.DestroyImmediate(managerGo);
                Object.DestroyImmediate(allyGo);
                Object.DestroyImmediate(enemyGo);
            }

            ok &= CheckWaveSpawnerAutoRegistersWithCombatManager(werewolfData, lineMonster);

            return ok;
        }

        // WaveSpawner가 스폰한 몬스터를 CombatManager에 자동 등록해서, 실제로 공격 가능한지 확인한다.
        private static bool CheckWaveSpawnerAutoRegistersWithCombatManager(CharacterData werewolfData, MonsterData lineMonster)
        {
            var config = ScriptableObject.CreateInstance<WaveConfig>();
            config.totalRounds = 3;
            config.monstersPerRound = 2; // spawnInterval(=duration/개수)이 라운드 길이보다 짧아야 라운드 도중 스폰됨
            config.earlyRoundThreshold = 0;
            config.earlyRoundDuration = 100f; // 라운드가 끝나기 전에 직접 공격을 시도할 시간을 넉넉히 확보
            config.lateRoundDuration = 100f;
            config.leftBossInterval = 0;
            config.rightBossOffset = 0;
            config.mandatoryClearRounds = new int[0];

            var managerGo = new GameObject("SmokeTest_WaveIntegration_CombatManager");
            var allyGo = new GameObject("SmokeTest_WaveIntegration_Ally");
            var spawnerGo = new GameObject("SmokeTest_WaveIntegration_Spawner");

            bool ok;
            try
            {
                var manager = managerGo.AddComponent<CombatManager>();
                var werewolfUnit = allyGo.AddComponent<BattleUnit>();
                werewolfUnit.Initialize(werewolfData);
                manager.RegisterAlly(werewolfUnit);

                var spawner = spawnerGo.AddComponent<WaveSpawner>();
                spawner.Configure(config, lineMonster, null, null);
                spawner.SetCombatManager(manager);

                EnemyUnit spawnedEnemy = null;
                spawner.OnMonsterSpawned += e => spawnedEnemy = e;

                spawner.StartRun();
                spawner.Tick(60f); // spawnInterval=50초 지점에서 첫 몬스터가 스폰되도록 시간 진행 (라운드는 아직 안 끝남)

                ok = LogAndCheck("웨이브 스폰 이벤트 발생", spawnedEnemy != null, (spawnedEnemy != null).ToString());

                if (spawnedEnemy != null)
                {
                    float before = spawnedEnemy.CurrentHealth;
                    manager.ProcessAttack(werewolfUnit);
                    float damage = before - spawnedEnemy.CurrentHealth;
                    ok &= LogAndCheck("WaveSpawner가 자동 등록한 몬스터를 실제로 공격함", damage > 0f, damage.ToString());
                }
            }
            finally
            {
                Object.DestroyImmediate(managerGo);
                Object.DestroyImmediate(allyGo);
                Object.DestroyImmediate(spawnerGo);
                Object.DestroyImmediate(config);
            }

            return ok;
        }

        // PlacementGrid가 워크래프트3식 격자 배치(배치/이동/해제)를 올바르게 처리하는지 검증한다.
        private static bool CheckPlacementGrid(CharacterData puppyData, CharacterData werewolfData, CharacterData whiteTigerData)
        {
            bool ok = true;

            var managerGo = new GameObject("SmokeTest_Placement_CombatManager");
            var gridGo = new GameObject("SmokeTest_Placement_Grid");

            try
            {
                var combatManager = managerGo.AddComponent<CombatManager>();

                var grid = gridGo.AddComponent<PlacementGrid>();
                grid.Configure(3, 3, combatManager);

                // 배치 / 중복 배치 방지 (더 이상 격자 범위 제한은 없다 - 스폰 지점에서 가장 가까운 빈 자리를 찾는 방식이라
                // 유닛 수에 상한이 없어야 하므로, 아주 먼 좌표에도 배치가 성공해야 한다)
                ok &= LogAndCheck("(0,0)에 강아지 배치 성공", grid.TryPlaceUnit(0, 0, puppyData, out var puppyUnit), "true");
                ok &= LogAndCheck("이미 찬 슬롯에는 배치 실패", !grid.TryPlaceUnit(0, 0, werewolfData, out _), "true");
                ok &= LogAndCheck("(1,0)에 웨어울프 배치 성공", grid.TryPlaceUnit(1, 0, werewolfData, out var werewolfUnit), "true");
                ok &= LogAndCheck("먼 좌표(5,5)에도 배치 성공(상한 없음)", grid.TryPlaceUnit(5, 5, whiteTigerData, out var farUnit), "true");
                ok &= LogAndCheck("GetUnitAt(0,0)이 강아지 유닛 반환", grid.GetUnitAt(0, 0) == puppyUnit, "true");

                grid.TryRemoveUnit(5, 5); // 이후 (1,0) 재사용 테스트와 겹치지 않도록 바로 정리

                // 이동
                ok &= LogAndCheck("(1,0)->(2,0) 이동 성공", grid.TryMoveUnit(1, 0, 2, 0), "true");
                ok &= LogAndCheck("이동 후 원래 슬롯은 비어있음", !grid.IsSlotOccupied(1, 0), "true");
                ok &= LogAndCheck("찬 슬롯으로는 이동 실패", !grid.TryMoveUnit(2, 0, 0, 0), "true");

                // 세 번째 유닛 배치
                ok &= LogAndCheck("(1,0)에 백호 배치 성공", grid.TryPlaceUnit(1, 0, whiteTigerData, out var whiteTigerUnit), "true");
                ok &= LogAndCheck("배치된 백호의 EffectiveStats가 기본 스탯과 일치", whiteTigerUnit.EffectiveStats.attackSpeed == whiteTigerData.stats.attackSpeed,
                    whiteTigerUnit.EffectiveStats.attackSpeed.ToString());

                // 명시적 해제 (판매 등) 시 OnUnitRemoved 이벤트가 발생하는지 확인
                bool removedEventFired = false;
                grid.OnUnitRemoved += (_, _, _) => removedEventFired = true;
                ok &= LogAndCheck("(2,0) 유닛 해제 성공", grid.TryRemoveUnit(2, 0), "true");
                ok &= LogAndCheck("해제 후 슬롯이 비어있음", !grid.IsSlotOccupied(2, 0), "true");
                ok &= LogAndCheck("해제 시 OnUnitRemoved 이벤트 발생", removedEventFired, removedEventFired.ToString());
            }
            finally
            {
                Object.DestroyImmediate(managerGo);
                Object.DestroyImmediate(gridGo);
            }

            return ok;
        }

        // GameManager가 배치/전투/웨이브/골드/승패 판정을 실제로 하나로 엮어 돌리는지 검증한다.
        private static bool CheckGameManager(CharacterData puppyData, CharacterData werewolfData, CharacterData whiteTigerData)
        {
            var lineMonster = AssetDatabase.LoadAssetAtPath<MonsterData>("Assets/Data/Monsters/LineMonster_Basic.asset");

            var fastConfig = ScriptableObject.CreateInstance<WaveConfig>();
            fastConfig.totalRounds = 3;
            fastConfig.monstersPerRound = 1;
            fastConfig.earlyRoundThreshold = 0;
            fastConfig.earlyRoundDuration = 100f;
            fastConfig.lateRoundDuration = 100f;
            fastConfig.leftBossInterval = 0;
            fastConfig.rightBossOffset = 0;
            fastConfig.mandatoryClearRounds = new int[0];

            bool ok = CheckGameManagerPlacementAndKillReward(puppyData, werewolfData, whiteTigerData, lineMonster, fastConfig);
            ok &= CheckGameManagerVictory(lineMonster);
            ok &= CheckGameManagerDefeat(lineMonster);

            Object.DestroyImmediate(fastConfig);
            return ok;
        }

        private static bool CheckGameManagerPlacementAndKillReward(CharacterData puppyData, CharacterData werewolfData,
            CharacterData whiteTigerData, MonsterData lineMonster, WaveConfig fastConfig)
        {
            var go = new GameObject("SmokeTest_GameManager_Main");
            bool ok;
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(3, 3, fastConfig, lineMonster, null, null);

                // 웨이브 시작 전에도 배치가 가능해야 한다.
                game.PlaceUnit(0, 0, puppyData, out var puppyUnit);
                game.PlaceUnit(1, 0, werewolfData, out var werewolfUnit);
                game.PlaceUnit(2, 0, whiteTigerData, out var whiteTigerUnit);

                ok = LogAndCheck("웨이브 시작 전에도 3체 배치 성공",
                    puppyUnit != null && werewolfUnit != null && whiteTigerUnit != null, "true");

                int goldSeen = -1;
                game.OnGoldChanged += g => goldSeen = g;

                EnemyUnit spawnedEnemy = null;
                game.WaveSpawner.OnMonsterSpawned += e => spawnedEnemy = e;

                game.StartGame();
                game.WaveSpawner.Tick(60f); // monstersPerRound=1, earlyRoundDuration=100 -> spawnInterval=100. 넉넉히 진행만 시켜 스폰 대기

                // monstersPerRound가 1이면 spawnInterval == 라운드 길이라 라운드가 끝나야 스폰된다.
                // 그래서 라운드가 끝나는 시점까지 마저 진행시킨다.
                for (int i = 0; i < 50 && spawnedEnemy == null; i++)
                    game.WaveSpawner.Tick(1f);

                ok &= LogAndCheck("게임 시작 후 몬스터 스폰됨", spawnedEnemy != null, (spawnedEnemy != null).ToString());

                if (spawnedEnemy != null)
                {
                    for (int i = 0; i < 20 && !spawnedEnemy.IsDead; i++)
                        game.CombatManager.ProcessAttack(werewolfUnit);

                    ok &= LogAndCheck("배치된 아군이 스폰된 몬스터를 처치함", spawnedEnemy.IsDead, spawnedEnemy.IsDead.ToString());
                    ok &= LogAndCheck("처치 보상으로 골드 지급됨", game.Gold == lineMonster.goldReward, game.Gold.ToString());
                    ok &= LogAndCheck("OnGoldChanged 이벤트 발생", goldSeen == lineMonster.goldReward, goldSeen.ToString());
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            return ok;
        }

        private static bool CheckGameManagerVictory(MonsterData lineMonster)
        {
            var config = ScriptableObject.CreateInstance<WaveConfig>();
            config.totalRounds = 2;
            config.monstersPerRound = 1;
            config.earlyRoundThreshold = 0;
            config.earlyRoundDuration = 1f;
            config.lateRoundDuration = 1f;
            config.leftBossInterval = 0;
            config.rightBossOffset = 0;
            config.mandatoryClearRounds = new int[0];

            var fastMonster = ScriptableObject.CreateInstance<MonsterData>();
            fastMonster.monsterName = "빠른 마수";
            fastMonster.baseHealth = 10;
            fastMonster.moveSpeed = 200f;

            var go = new GameObject("SmokeTest_GameManager_Victory");
            bool ok;
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(3, 3, config, fastMonster, null, null);

                bool? victoryResult = null;
                string reason = null;
                game.OnGameEnded += (victory, r) => { victoryResult = victory; reason = r; };

                game.StartGame();
                for (int i = 0; i < 500 && !game.IsGameOver; i++)
                    game.WaveSpawner.Tick(0.1f);

                ok = LogAndCheck("전체 라운드 클리어 시 게임 종료", game.IsGameOver, game.IsGameOver.ToString());
                ok &= LogAndCheck("승리로 판정됨", game.IsVictory, game.IsVictory.ToString());
                ok &= LogAndCheck("OnGameEnded(true, ...)로 발생", victoryResult == true, (victoryResult ?? false).ToString());
                Debug.Log($"[SmokeTest] 승리 사유: {reason}");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(fastMonster);
            }

            return ok;
        }

        private static bool CheckGameManagerDefeat(MonsterData lineMonster)
        {
            var config = ScriptableObject.CreateInstance<WaveConfig>();
            config.totalRounds = 20;
            config.monstersPerRound = 1;
            config.earlyRoundThreshold = 0;
            config.earlyRoundDuration = 1f;
            config.lateRoundDuration = 1f;
            config.leftBossInterval = 0;
            config.rightBossOffset = 0;
            config.mandatoryClearRounds = new int[0];
            config.maxAliveMonsters = 2;

            var fastMonster = ScriptableObject.CreateInstance<MonsterData>();
            fastMonster.monsterName = "빠른 마수";
            fastMonster.baseHealth = 10;
            fastMonster.moveSpeed = 200f;

            var go = new GameObject("SmokeTest_GameManager_Defeat");
            bool ok;
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(3, 3, config, fastMonster, null, null);

                bool? victoryResult = null;
                game.OnGameEnded += (victory, _) => victoryResult = victory;

                game.StartGame(); // 아무도 배치하지 않아서 몬스터가 죽지 않고 계속 쌓임
                for (int i = 0; i < 500 && !game.IsGameOver; i++)
                    game.WaveSpawner.Tick(0.1f);

                ok = LogAndCheck("생존 몬스터 상한 도달 시 게임 종료", game.IsGameOver, game.IsGameOver.ToString());
                ok &= LogAndCheck("패배로 판정됨", !game.IsVictory, game.IsVictory.ToString());
                ok &= LogAndCheck("OnGameEnded(false, ...)로 발생", victoryResult == false, (victoryResult ?? true).ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(fastMonster);
            }

            return ok;
        }

        // SampleScene.unity에 추가한 GameBootstrap 오브젝트가 실제로 씬에 존재하고
        // 스크립트 참조가 깨지지 않았는지(= 손으로 수정한 씬 YAML이 유효한지) 확인한다.
        private static bool CheckSceneSetup()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            bool ok = LogAndCheck("SampleScene 로드 성공", scene.IsValid(), scene.IsValid().ToString());

            var bootstrapGo = GameObject.Find("GameBootstrap");
            ok &= LogAndCheck("GameBootstrap 오브젝트 존재", bootstrapGo != null, (bootstrapGo != null).ToString());

            if (bootstrapGo != null)
            {
                var bootstrap = bootstrapGo.GetComponent<GameBootstrap>();
                ok &= LogAndCheck("GameBootstrap 컴포넌트 스크립트 참조 정상", bootstrap != null, (bootstrap != null).ToString());
            }

            return ok;
        }

        // 클릭/드래그 선택 판정(SelectionMath)이 화면 좌표 기준으로 올바르게 동작하는지 검증한다.
        // 실제 마우스 입력 없이, 같은 카메라의 WorldToScreenPoint로부터 좌표를 역산해서 해상도에 무관하게 검증한다.
        private static bool CheckSelectionMath()
        {
            bool ok;
            var camGo = new GameObject("SmokeTest_SelectionCamera");
            var leftGo = new GameObject("SmokeTest_Left");
            var midGo = new GameObject("SmokeTest_Mid");
            var rightGo = new GameObject("SmokeTest_Right");

            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                camGo.transform.position = new Vector3(0f, 0f, -10f);

                leftGo.transform.position = new Vector3(-3f, 0f, 0f);
                midGo.transform.position = new Vector3(0f, 0f, 0f);
                rightGo.transform.position = new Vector3(3f, 0f, 0f);

                var left = leftGo.AddComponent<Selectable>();
                var mid = midGo.AddComponent<Selectable>();
                var right = rightGo.AddComponent<Selectable>();
                left.Initialize(null, null);
                mid.Initialize(null, null);
                right.Initialize(null, null);

                var all = new List<Selectable> { left, mid, right };

                Vector2 midScreen = cam.WorldToScreenPoint(midGo.transform.position);
                var nearest = SelectionMath.FindNearest(all, cam, midScreen, 10f);
                ok = LogAndCheck("클릭 지점과 가장 가까운 유닛(가운데) 선택", nearest == mid, (nearest == mid).ToString());

                Vector2 farPoint = midScreen + new Vector2(10000f, 10000f);
                var nothingNearby = SelectionMath.FindNearest(all, cam, farPoint, 10f);
                ok &= LogAndCheck("반경 밖 클릭은 아무것도 선택하지 않음", nothingNearby == null, (nothingNearby == null).ToString());

                Vector2 leftScreen = cam.WorldToScreenPoint(leftGo.transform.position);
                float xMin = Mathf.Min(leftScreen.x, midScreen.x) - 5f;
                float xMax = Mathf.Max(leftScreen.x, midScreen.x) + 5f;
                var dragRect = new Rect(xMin, -1e6f, xMax - xMin, 2e6f); // y는 전부 포함시키고 x만으로 왼쪽/가운데만 가려냄

                var found = SelectionMath.FindInRect(all, cam, dragRect);
                ok &= LogAndCheck("드래그 선택 범위: 왼쪽+가운데만 포함, 오른쪽 제외",
                    found.Contains(left) && found.Contains(mid) && !found.Contains(right),
                    $"count={found.Count}");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(leftGo);
                Object.DestroyImmediate(midGo);
                Object.DestroyImmediate(rightGo);
            }

            return ok;
        }

        // UnitMover가 실제로 목표 지점까지 이동하고, 도착하면 멈추는지 검증한다.
        private static bool CheckUnitMover()
        {
            bool ok;
            var go = new GameObject("SmokeTest_UnitMover");
            try
            {
                go.transform.position = Vector3.zero;
                var mover = go.AddComponent<UnitMover>();

                var destination = new Vector3(5f, 0f, 0f);
                mover.MoveTo(destination);
                ok = LogAndCheck("이동 명령 직후 IsMoving == true", mover.IsMoving, mover.IsMoving.ToString());

                for (int i = 0; i < 200 && mover.IsMoving; i++)
                    mover.Tick(0.1f);

                ok &= ApproxLog("충분한 시간 후 목표 지점에 도달", go.transform.position.x, destination.x);
                ok &= LogAndCheck("도착 후 IsMoving == false", !mover.IsMoving, mover.IsMoving.ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            return ok;
        }

        // RaidPortal이 반경 안에 들어온 유닛만 목적지로 순간이동시키고, 반경 밖의 유닛은 그대로 두는지 검증한다.
        private static bool CheckRaidPortalTeleport()
        {
            bool ok;
            var portalGo = new GameObject("SmokeTest_RaidPortal");
            var nearGo = new GameObject("SmokeTest_RaidPortal_Near");
            var farGo = new GameObject("SmokeTest_RaidPortal_Far");

            try
            {
                portalGo.transform.position = Vector3.zero;
                var portal = portalGo.AddComponent<RaidPortal>();
                var target = new Vector3(100f, 0f, 0f);
                portal.Setup(target);

                nearGo.transform.position = new Vector3(0.5f, 0f, 0f); // 포탈 기본 반경(1.5) 안
                var nearUnit = nearGo.AddComponent<BattleUnit>();

                farGo.transform.position = new Vector3(20f, 0f, 0f); // 포탈 반경 밖
                var farUnit = farGo.AddComponent<BattleUnit>();

                portal.Tick(new List<BattleUnit> { nearUnit, farUnit });

                ok = LogAndCheck("반경 안의 유닛이 목적지로 순간이동함", nearGo.transform.position == target, nearGo.transform.position.ToString());
                ok &= LogAndCheck("반경 밖의 유닛은 그대로 있음", farGo.transform.position != target, farGo.transform.position.ToString());
            }
            finally
            {
                Object.DestroyImmediate(portalGo);
                Object.DestroyImmediate(nearGo);
                Object.DestroyImmediate(farGo);
            }

            return ok;
        }

        // RaidManager가 시작하자마자 보스를 등장시키고, 보스를 처치하면 보석을 지급하는지 검증한다.
        // (5초 후 재등장은 RaidManager.Update가 비공개라 헤드리스 테스트에서는 직접 확인하지 않는다.)
        private static bool CheckRaidBossDeathGrantsGemsAndRespawns()
        {
            var bossTemplate = AssetDatabase.LoadAssetAtPath<MonsterData>("Assets/Data/Monsters/RaidBoss_AncientColossus.asset");

            bool ok = LogAndCheck("RaidBoss_AncientColossus 에셋 로드", bossTemplate != null, (bossTemplate != null).ToString());
            if (bossTemplate == null) return ok;

            var gameGo = new GameObject("SmokeTest_Raid_GameManager");
            var entranceGo = new GameObject("SmokeTest_Raid_Entrance");
            var returnGo = new GameObject("SmokeTest_Raid_Return");
            var managerGo = new GameObject("SmokeTest_Raid_Manager");

            try
            {
                var game = gameGo.AddComponent<GameManager>();
                game.Configure(3, 3, null, null, null, null);
                game.PlaceUnit(0, 0, null, out _); // GameManager의 지연 초기화(EnsureInitialized)를 미리 확실히 트리거해둔다 (data==null이라 실제로 배치되진 않음)

                var entrancePortal = entranceGo.AddComponent<RaidPortal>();
                entrancePortal.Setup(Vector3.zero);
                var returnPortal = returnGo.AddComponent<RaidPortal>();
                returnPortal.Setup(Vector3.zero);

                var raidManager = managerGo.AddComponent<RaidManager>();
                raidManager.Setup(game, bossTemplate, Vector3.zero, entrancePortal, returnPortal);

                ok &= LogAndCheck("레이드 보스가 시작과 함께 등장함", raidManager.CurrentBoss != null, (raidManager.CurrentBoss != null).ToString());

                int gemsBefore = game.Gems;
                if (raidManager.CurrentBoss != null)
                    raidManager.CurrentBoss.TakeTrueDamage(999999f);

                ok &= LogAndCheck("보스 처치 시 보석 지급됨", game.Gems > gemsBefore, game.Gems.ToString());
            }
            finally
            {
                Object.DestroyImmediate(gameGo);
                Object.DestroyImmediate(entranceGo);
                Object.DestroyImmediate(returnGo);
                Object.DestroyImmediate(managerGo);
            }

            return ok;
        }

        // 동물 로스터의 조합식(늑대과 체인 전체 + 다른 계열 일부)이 올바르게 채워졌는지 확인한다.
        private static bool CheckFusionChains()
        {
            bool ok = true;

            ok &= CheckFusionRecipe("Assets/Data/Characters/Canine/Wolf.asset", "늑대",
                new[] { ("Assets/Data/Characters/Canine/Puppy.asset", "강아지"),
                        ("Assets/Data/Characters/Canine/Puppy.asset", "강아지"),
                        ("Assets/Data/Characters/Canine/Puppy.asset", "강아지") }, 50);
            ok &= CheckFusionRecipe("Assets/Data/Characters/Canine/DireWolf.asset", "다이어울프",
                new[] { ("Assets/Data/Characters/Canine/Wolf.asset", "늑대"),
                        ("Assets/Data/Characters/Canine/Wolf.asset", "늑대"),
                        ("Assets/Data/Characters/Canine/Wolf.asset", "늑대") }, 100);
            ok &= CheckFusionRecipe("Assets/Data/Characters/Canine/Werewolf.asset", "웨어울프",
                new[] { ("Assets/Data/Characters/Canine/DireWolf.asset", "다이어울프"),
                        ("Assets/Data/Characters/Canine/DireWolf.asset", "다이어울프"),
                        ("Assets/Data/Characters/Canine/DireWolf.asset", "다이어울프") }, 200);
            ok &= CheckFusionRecipe("Assets/Data/Characters/Canine/NineTailedFox.asset", "구미호",
                new[] { ("Assets/Data/Characters/Canine/Werewolf.asset", "웨어울프"),
                        ("Assets/Data/Characters/Canine/Werewolf.asset", "웨어울프"),
                        ("Assets/Data/Characters/Canine/Werewolf.asset", "웨어울프") }, 0);

            ok &= CheckFusionRecipe("Assets/Data/Characters/Feline/WhiteTiger.asset", "백호",
                new[] { ("Assets/Data/Characters/Feline/Taotie.asset", "도철"),
                        ("Assets/Data/Characters/Feline/Taotie.asset", "도철"),
                        ("Assets/Data/Characters/Feline/Taotie.asset", "도철") }, 0);

            return ok;
        }

        // 재료로 "같은 유닛 3마리"를 요구하는 조합(노멀 3개 -> 매직)이 GameManager를 통해 실제로 동작하는지 확인한다.
        private static bool CheckFuseSameMaterialTriple()
        {
            var puppy = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Canine/Puppy.asset");
            var wolf = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Canine/Wolf.asset");

            var go = new GameObject("SmokeTest_FuseSameMaterial");
            bool ok;
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(3, 3, null, null, null, null);

                bool fusedWithTwo = false;
                if (puppy != null && wolf != null)
                {
                    game.Inventory.Add(puppy, 2); // 2마리뿐이면 부족해야 한다
                    fusedWithTwo = game.TryFuseCharacter(wolf);
                }
                ok = LogAndCheck("같은 재료 2마리로는 조합 실패", !fusedWithTwo, fusedWithTwo.ToString());

                bool fusedWithThree = false;
                if (puppy != null && wolf != null)
                {
                    game.Inventory.Add(puppy, 1); // 총 3마리로 채움
                    game.GrantGold(50);
                    fusedWithThree = game.TryFuseCharacter(wolf);
                }
                ok &= LogAndCheck("같은 재료 3마리 + 골드로 조합 성공", fusedWithThree, fusedWithThree.ToString());
                ok &= LogAndCheck("조합 후 강아지 재료 전부 소모", puppy == null || game.Inventory.GetCount(puppy) == 0,
                    puppy == null ? "asset missing" : game.Inventory.GetCount(puppy).ToString());
                ok &= LogAndCheck("조합 결과 늑대가 보유 목록에 추가됨", wolf == null || game.Inventory.GetCount(wolf) == 1,
                    wolf == null ? "asset missing" : game.Inventory.GetCount(wolf).ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            return ok;
        }

        private static bool CheckFusionRecipe(string targetPath, string expectedTargetName,
            (string path, string expectedName)[] expectedMaterials, int expectedGold)
        {
            bool ok = true;
            var target = AssetDatabase.LoadAssetAtPath<CharacterData>(targetPath);
            ok &= LogAndCheck($"{expectedTargetName} 에셋 로드", target != null, (target != null).ToString());
            if (target == null) return ok;

            var recipe = target.fusionRecipe;
            ok &= LogAndCheck($"{expectedTargetName} 조합 재료 개수", recipe.requiredCharacters != null && recipe.requiredCharacters.Length == expectedMaterials.Length,
                recipe.requiredCharacters?.Length.ToString() ?? "null");
            ok &= LogAndCheck($"{expectedTargetName} 조합 골드 비용", recipe.goldCost == expectedGold, recipe.goldCost.ToString());

            if (recipe.requiredCharacters != null)
            {
                for (int i = 0; i < expectedMaterials.Length && i < recipe.requiredCharacters.Length; i++)
                {
                    var actualName = recipe.requiredCharacters[i]?.characterName;
                    ok &= LogAndCheck($"{expectedTargetName} 재료[{i}] = {expectedMaterials[i].expectedName}",
                        actualName == expectedMaterials[i].expectedName, actualName ?? "null");
                }
            }

            return ok;
        }

        // 소환(가챠)과 조합이 재화 차감/보유 목록/등급 확률대로 실제로 동작하는지 검증한다.
        private static bool CheckGachaAndFusion()
        {
            bool ok = true;

            var database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/Data/CharacterDatabase.asset");
            var goldSummon = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Gacha/GoldSummon.asset");
            var gemMidSummon = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Gacha/GemMidSummon.asset");
            var werewolf = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Canine/Werewolf.asset");
            var nineTailedFox = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Canine/NineTailedFox.asset");

            ok &= LogAndCheck("CharacterDatabase 로드", database != null, (database != null).ToString());
            ok &= LogAndCheck("CharacterDatabase 전체 40개 등록", database != null && database.allCharacters.Count == 40,
                database?.allCharacters.Count.ToString() ?? "null");
            ok &= LogAndCheck("GoldSummon 테이블 로드", goldSummon != null, (goldSummon != null).ToString());
            ok &= LogAndCheck("GemMidSummon 테이블 로드", gemMidSummon != null, (gemMidSummon != null).ToString());

            // GachaManager: 100% 레어 테이블은 항상 레어만 뽑아야 한다.
            var gachaGo = new GameObject("SmokeTest_GachaManager");
            try
            {
                var gacha = gachaGo.AddComponent<GachaManager>();
                gacha.SetDatabase(database);

                bool allRare = true;
                for (int i = 0; i < 30; i++)
                {
                    var rolled = gacha.Roll(gemMidSummon);
                    if (rolled == null || rolled.rarity != Rarity.Rare) { allRare = false; break; }
                }
                ok &= LogAndCheck("100% 레어 테이블은 30회 전부 레어", allRare, allRare.ToString());
            }
            finally
            {
                Object.DestroyImmediate(gachaGo);
            }

            ok &= CheckGameManagerSummonAndFusion(database, werewolf, nineTailedFox, goldSummon);

            return ok;
        }

        // 구미호는 웨어울프 3마리 + 보석 100이 필요하다 (소환/조합 통합 검증).
        private static bool CheckGameManagerSummonAndFusion(CharacterDatabase database, CharacterData werewolfData,
            CharacterData nineTailedFoxData, GachaTable goldSummon)
        {
            var go = new GameObject("SmokeTest_GameManager_Gacha");
            bool ok;
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(3, 3, null, null, null, null);
                game.SetCharacterDatabase(database);

                // 재화 없이는 소환 실패, 아무것도 차감되지 않아야 한다.
                bool summonedWithoutGold = game.TrySummon(goldSummon, out _);
                ok = LogAndCheck("골드 없이 소환 시도하면 실패", !summonedWithoutGold, summonedWithoutGold.ToString());

                game.GrantGold(1000);
                bool summoned = game.TrySummon(goldSummon, out var summonedCharacter);
                ok &= LogAndCheck("골드 소환 성공", summoned, summoned.ToString());
                ok &= LogAndCheck("소환 후 골드 100 차감", game.Gold == 900, game.Gold.ToString());
                if (summonedCharacter != null)
                {
                    ok &= LogAndCheck("소환된 캐릭터 등급이 노말 또는 매직",
                        summonedCharacter.rarity == Rarity.Normal || summonedCharacter.rarity == Rarity.Magic,
                        summonedCharacter.rarity.ToString());
                    ok &= LogAndCheck("소환된 캐릭터가 보유 목록에 추가됨", game.Inventory.GetCount(summonedCharacter) == 1,
                        game.Inventory.GetCount(summonedCharacter).ToString());
                }

                // 조합: 구미호는 웨어울프 3마리 + 보석 100이 필요하다.
                bool fusedWithoutMaterial = game.TryFuseCharacter(nineTailedFoxData);
                ok &= LogAndCheck("재료 없이 조합 시도하면 실패", !fusedWithoutMaterial, fusedWithoutMaterial.ToString());

                game.Inventory.Add(werewolfData, 3); // 웨어울프 3마리를 보유하고 있다고 가정
                bool fusedWithoutGems = game.TryFuseCharacter(nineTailedFoxData);
                ok &= LogAndCheck("보석 없이 조합 시도하면 실패(재료는 소모 안 됨)", !fusedWithoutGems, fusedWithoutGems.ToString());
                ok &= LogAndCheck("실패한 조합은 재료를 소모하지 않음", game.Inventory.GetCount(werewolfData) == 3, game.Inventory.GetCount(werewolfData).ToString());

                game.GrantGems(100);
                bool fused = game.TryFuseCharacter(nineTailedFoxData);
                ok &= LogAndCheck("재료+재화 충분하면 조합 성공", fused, fused.ToString());
                ok &= LogAndCheck("조합 후 보석 100 소모", game.Gems == 0, game.Gems.ToString());
                ok &= LogAndCheck("조합 후 웨어울프 재료 소모됨", game.Inventory.GetCount(werewolfData) == 0, game.Inventory.GetCount(werewolfData).ToString());
                ok &= LogAndCheck("조합 결과 구미호가 보유 목록에 추가됨", game.Inventory.GetCount(nineTailedFoxData) == 1, game.Inventory.GetCount(nineTailedFoxData).ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            return ok;
        }

        // 소환/조합으로 얻은 유닛이 실제로 격자에 자동 배치되는지, 판매 시 골드가 환급되고 슬롯이 비워지는지,
        // CharacterDatabase의 조합 역방향 조회(어떤 재료가 어느 유닛으로 조합되는지)가 맞는지 검증한다.
        private static bool CheckSummonAutoPlaceAndSell()
        {
            var database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/Data/CharacterDatabase.asset");
            var goldSummon = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Gacha/GoldSummon.asset");
            var werewolfData = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Canine/Werewolf.asset");
            var nineTailedFoxData = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Canine/NineTailedFox.asset");

            bool ok = true;

            // 웨어울프는 구미호 조합 재료이므로, 역방향 조회 시 구미호가 나와야 한다.
            var targets = database.FindFusionTargetsUsing(werewolfData);
            ok &= LogAndCheck("웨어울프를 재료로 쓰는 조합 대상에 구미호 포함", targets.Contains(nineTailedFoxData),
                string.Join(", ", targets.ConvertAll(t => t.characterName)));

            var go = new GameObject("SmokeTest_GameManager_AutoPlaceSell");
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(3, 3, null, null, null, null);
                game.SetCharacterDatabase(database);

                game.GrantGold(1000);
                bool summoned = game.TrySummon(goldSummon, out var summonedCharacter);
                ok &= LogAndCheck("소환 성공", summoned, summoned.ToString());

                // 격자가 비어있는 상태에서 첫 소환이므로 (0,0) 슬롯에 자동 배치되어야 한다.
                var placedUnit = game.PlacementGrid.GetUnitAt(0, 0);
                ok &= LogAndCheck("소환된 유닛이 격자(0,0)에 자동 배치됨",
                    placedUnit != null && placedUnit.Source == summonedCharacter,
                    placedUnit != null ? placedUnit.Source.characterName : "null");

                if (placedUnit != null)
                {
                    int expectedRefund = game.GetSellValue(summonedCharacter);
                    ok &= LogAndCheck("판매 가치가 0보다 큼", expectedRefund > 0, expectedRefund.ToString());

                    int goldBeforeSell = game.Gold;
                    bool sold = game.SellUnit(placedUnit);
                    ok &= LogAndCheck("배치된 유닛 판매 성공", sold, sold.ToString());
                    ok &= LogAndCheck("판매 시 가치의 절반만큼 골드 환급",
                        game.Gold == goldBeforeSell + expectedRefund, game.Gold.ToString());
                    ok &= LogAndCheck("판매 후 슬롯에서 제거됨",
                        !game.PlacementGrid.TryFindSlotOf(placedUnit, out _, out _), "slot cleared");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            return ok;
        }

        // 조합 재료로 소모된 유닛이 필드에 배치돼 있었다면 화면(격자)에서도 사라지는지 검증한다.
        // 재료 요구량(3)보다 1마리 많은 4마리를 필드에 두고, 그중 특정 유닛을 지정해서 조합하면
        // 그 유닛이 반드시(다른 동일 유닛보다 우선해서) 사라지는지도 함께 확인한다.
        private static bool CheckFuseRemovesFieldMaterials()
        {
            var puppy = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Canine/Puppy.asset");
            var wolf = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Canine/Wolf.asset");

            var go = new GameObject("SmokeTest_FuseFieldRemoval");
            bool ok;
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(4, 1, null, null, null, null);

                game.PlaceUnit(0, 0, puppy, out var unitA);
                game.PlaceUnit(1, 0, puppy, out _);
                game.PlaceUnit(2, 0, puppy, out _);
                game.PlaceUnit(3, 0, puppy, out _);
                game.Inventory.Add(puppy, 4);
                game.GrantGold(50);

                bool fused = game.TryFuseCharacter(wolf, unitA);
                ok = LogAndCheck("지정 유닛을 재료로 조합 성공", fused, fused.ToString());
                ok &= LogAndCheck("지정했던 유닛(unitA)이 화면(필드)에서 파괴됨", unitA == null, (unitA == null).ToString());

                int remainingPuppy = 0;
                for (int x = 0; x < 4; x++)
                {
                    var u = game.PlacementGrid.GetUnitAt(x, 0);
                    if (u != null && u.Source == puppy) remainingPuppy++;
                }
                ok &= LogAndCheck("필드에 강아지가 1마리만 남음(4마리 중 3마리 소모)",
                    remainingPuppy == 1, remainingPuppy.ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            return ok;
        }
    }
}
