using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MRD.Data;
using MRD.Battle;
using MRD.Synergy;
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
            var spartan = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Olympus/SpartanShieldman.asset");
            var heracles = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Olympus/Heracles.asset");
            var zeus = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Olympus/Zeus.asset");

            bool ok = true;
            ok &= Check(spartan, "스파르타 방패병");
            ok &= Check(heracles, "헤라클레스");
            ok &= Check(zeus, "제우스 (봉인 해제)");

            if (zeus != null && zeus.fusionRecipe.requiredCharacters != null && zeus.fusionRecipe.requiredCharacters.Length > 0)
            {
                var material = zeus.fusionRecipe.requiredCharacters[0];
                ok &= LogAndCheck("제우스 조합 재료 참조", material != null, $"{material?.characterName}");
            }
            else
            {
                Debug.LogError("[SmokeTest] 제우스 fusionRecipe.requiredCharacters 참조 실패");
                ok = false;
            }

            // BattleUnit이 시너지 보너스를 실제로 반영해서 계산하는지 검증
            var go = new GameObject("SmokeTest_BattleUnit");
            try
            {
                var unit = go.AddComponent<BattleUnit>();
                var bonus = new StatModifier { physicalAttackPercent = 15f, attackSpeedPercent = 15f };
                unit.Initialize(heracles, bonus);

                float expectedAttack = heracles.stats.physicalAttack * 1.15f;
                float expectedSpeed = heracles.stats.attackSpeed * 1.15f;

                ok &= ApproxLog("EffectiveStats.physicalAttack", unit.EffectiveStats.physicalAttack, expectedAttack);
                ok &= ApproxLog("EffectiveStats.attackSpeed", unit.EffectiveStats.attackSpeed, expectedSpeed);
                ok &= ApproxLog("CurrentHealth(초기값 == 기본 체력)", unit.CurrentHealth, heracles.stats.health);

                bool usedSkill = unit.TryUseActiveSkill();
                ok &= LogAndCheck("마나 부족 상태에서 스킬 사용 실패해야 함", !usedSkill, usedSkill.ToString());

                unit.AddMana(heracles.activeSkill.manaCost);
                usedSkill = unit.TryUseActiveSkill();
                ok &= LogAndCheck("마나 충전 후 스킬 사용 성공해야 함", usedSkill, usedSkill.ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            ok &= CheckSynergy(spartan, heracles, zeus);
            ok &= CheckFullRoster();
            ok &= CheckCombat(spartan, heracles, zeus);
            ok &= CheckWaveSpawner();
            ok &= CheckBattleUnitAttacksEnemyUnit(heracles);
            ok &= CheckPlacementGrid(spartan, heracles, zeus);
            ok &= CheckGameManager(spartan, heracles, zeus);
            ok &= CheckGachaAndFusion(heracles, zeus);
            ok &= CheckFusionChains();
            ok &= CheckFuseSameMaterialTriple();
            ok &= CheckSummonAutoPlaceAndSell();
            ok &= CheckFuseRemovesFieldMaterials();
            ok &= CheckSelectionMath();
            ok &= CheckUnitMover();
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

            Debug.Log($"[SmokeTest] 로드됨: {data.characterName} (진영={data.faction}, 등급={data.rarity}, 체력={data.stats.health})");
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

        // SynergyManager가 배치 인원수에 따라 올바른 진영 시너지 단계를 계산하는지 엔드투엔드로 검증한다.
        private static bool CheckSynergy(CharacterData a, CharacterData b, CharacterData c)
        {
            var synergyData = AssetDatabase.LoadAssetAtPath<FactionSynergyData>("Assets/Data/Synergy/OlympusSynergy.asset");
            if (synergyData == null)
            {
                Debug.LogError("[SmokeTest] OlympusSynergy 에셋 로드 실패");
                return false;
            }

            var go = new GameObject("SmokeTest_SynergyManager");
            bool ok;
            try
            {
                var manager = go.AddComponent<SynergyManager>();
                manager.SetFactionSynergies(new List<FactionSynergyData> { synergyData });

                // 2체: 어떤 단계도 달성하지 못해야 한다.
                var twoUnits = new List<CharacterData> { a, b };
                var resultTwo = manager.Evaluate(twoUnits);
                ok = LogAndCheck("2체 배치 시 시너지 없음", !resultTwo.ContainsKey(Faction.Olympus), resultTwo.ContainsKey(Faction.Olympus).ToString());

                // 3체: 1단계(공격속도 +15%)만 달성해야 한다.
                var threeUnits = new List<CharacterData> { a, b, c };
                var resultThree = manager.Evaluate(threeUnits);
                bool hasThreeTier = resultThree.TryGetValue(Faction.Olympus, out var bonusThree);
                ok &= LogAndCheck("3체 배치 시 시너지 발동", hasThreeTier, hasThreeTier.ToString());
                ok &= ApproxLog("3체 시너지 attackSpeedPercent", bonusThree.attackSpeedPercent, 15f);
                ok &= ApproxLog("3체 시너지 physicalAttackPercent(미달성, 0이어야 함)", bonusThree.physicalAttackPercent, 0f);

                // 6체: 2단계(공격력 +25%)로 갱신되어야 한다 (1단계와 중첩되지 않음).
                var sixUnits = new List<CharacterData> { a, a, b, b, c, c };
                var resultSix = manager.Evaluate(sixUnits);
                bool hasSixTier = resultSix.TryGetValue(Faction.Olympus, out var bonusSix);
                ok &= LogAndCheck("6체 배치 시 상위 시너지로 갱신", hasSixTier, hasSixTier.ToString());
                ok &= ApproxLog("6체 시너지 physicalAttackPercent", bonusSix.physicalAttackPercent, 25f);
                ok &= ApproxLog("6체 시너지 attackSpeedPercent(1단계와 중첩되지 않아야 함)", bonusSix.attackSpeedPercent, 0f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            return ok;
        }

        // 프로젝트에 있는 모든 CharacterData 에셋이 깨짐 없이 로드되고, 진영별 개수가 기대치와 맞는지 검증한다.
        private static bool CheckFullRoster()
        {
            bool ok = true;
            var guids = AssetDatabase.FindAssets("t:CharacterData");
            var countByFaction = new Dictionary<Faction, int>();

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

                countByFaction.TryGetValue(data.faction, out var current);
                countByFaction[data.faction] = current + 1;
            }

            Debug.Log($"[SmokeTest] 전체 CharacterData 에셋 수: {guids.Length}");
            foreach (var kv in countByFaction)
                Debug.Log($"[SmokeTest]  - {kv.Key}: {kv.Value}개");

            ok &= LogAndCheck("전체 에셋 40개(기존 3개 + 신규 40개 예상)", guids.Length == 43, guids.Length.ToString());

            // 올림포스는 아직 테스트용 3종(노말/레어/히든)만 있고, 나머지 5개 진영은 8종 풀 로스터여야 한다.
            foreach (Faction faction in System.Enum.GetValues(typeof(Faction)))
            {
                int expected = faction == Faction.Olympus ? 3 : 8;
                int actual = countByFaction.GetValueOrDefault(faction);
                ok &= LogAndCheck($"{faction} 진영 유닛 수 ({expected}개 기대)", actual == expected, actual.ToString());
            }

            return ok;
        }

        // CombatManager가 평타 데미지/마나 획득/사망 처리/트리거 패시브를 실제로 처리하는지 검증한다.
        private static bool CheckCombat(CharacterData spartanData, CharacterData heraclesData, CharacterData zeusData)
        {
            bool ok = true;
            var managerGo = new GameObject("SmokeTest_CombatManager");
            var allyGo = new GameObject("SmokeTest_Ally_Heracles");
            var enemyGo = new GameObject("SmokeTest_Enemy_Spartan");
            var zeusGo = new GameObject("SmokeTest_Ally_Zeus");
            var dummyGo = new GameObject("SmokeTest_Enemy_Dummy");

            try
            {
                var manager = managerGo.AddComponent<CombatManager>();

                var heraclesUnit = allyGo.AddComponent<BattleUnit>();
                heraclesUnit.Initialize(heraclesData);
                var spartanUnit = enemyGo.AddComponent<BattleUnit>();
                spartanUnit.Initialize(spartanData);

                bool deathFired = false;
                spartanUnit.OnDeath += _ => deathFired = true;

                manager.RegisterAlly(heraclesUnit);
                manager.RegisterEnemyTarget(spartanUnit);

                // 평타 1회: 스파르타 방패병은 방어력 0이라 데미지가 [무크리, 크리] 범위 안에 들어야 한다.
                float healthBefore = spartanUnit.CurrentHealth;
                manager.ProcessAttack(heraclesUnit);
                float actualDamage = healthBefore - spartanUnit.CurrentHealth;
                float minDamage = heraclesData.stats.physicalAttack;
                float maxDamage = heraclesData.stats.physicalAttack * heraclesData.stats.criticalMultiplier;
                ok &= LogAndCheck("평타 데미지가 기대 범위 내(무크리~크리)",
                    actualDamage >= minDamage - 0.01f && actualDamage <= maxDamage + 0.01f, actualDamage.ToString());
                ok &= ApproxLog("평타 1회 후 마나 증가", heraclesUnit.CurrentMana, 10f);

                // 죽을 때까지 반복 공격 -> OnDeath 발생 확인 (최소 데미지로도 3회면 충분히 죽는 체력차)
                for (int i = 0; i < 10 && !spartanUnit.IsDead; i++)
                    manager.ProcessAttack(heraclesUnit);

                ok &= LogAndCheck("적 유닛이 사망 처리됨", spartanUnit.IsDead, spartanUnit.IsDead.ToString());
                ok &= LogAndCheck("OnDeath 이벤트 발생", deathFired, deathFired.ToString());

                // 대상이 사라진 뒤에도 예외 없이 처리되어야 한다 (자동 등록 해제 확인)
                try
                {
                    manager.ProcessAttack(heraclesUnit);
                    ok &= LogAndCheck("대상 없을 때 ProcessAttack 예외 없이 처리", true, "OK");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SmokeTest] 대상 없을 때 예외 발생: {e}");
                    ok = false;
                }

                // 제우스의 트리거 패시브(15%)가 통계적으로 실제 발동하는지 확인.
                // 대상은 체력을 사실상 무한으로 부풀려 도중에 죽지 않게 한다.
                var zeusUnit = zeusGo.AddComponent<BattleUnit>();
                zeusUnit.Initialize(zeusData);
                var dummyUnit = dummyGo.AddComponent<BattleUnit>();
                dummyUnit.Initialize(zeusData, new StatModifier { healthPercent = 1000000f });

                manager.RegisterAlly(zeusUnit);
                manager.RegisterEnemyTarget(dummyUnit);

                float mitPhys = 100f / (100f + dummyUnit.EffectiveStats.armor);
                float mitMagic = 100f / (100f + dummyUnit.EffectiveStats.magicResist);
                float maxNoTriggerDamage = (zeusUnit.EffectiveStats.physicalAttack * mitPhys
                    + zeusUnit.EffectiveStats.magicAttack * mitMagic) * zeusUnit.EffectiveStats.criticalMultiplier;

                bool triggerObserved = false;
                for (int i = 0; i < 200; i++)
                {
                    float before = dummyUnit.CurrentHealth;
                    manager.ProcessAttack(zeusUnit);
                    float damage = before - dummyUnit.CurrentHealth;
                    if (damage > maxNoTriggerDamage + 1f)
                    {
                        triggerObserved = true;
                        break;
                    }
                }

                ok &= LogAndCheck("제우스 트리거 패시브가 200회 평타 중 최소 1회 발동", triggerObserved, triggerObserved.ToString());
            }
            finally
            {
                Object.DestroyImmediate(managerGo);
                Object.DestroyImmediate(allyGo);
                Object.DestroyImmediate(enemyGo);
                Object.DestroyImmediate(zeusGo);
                Object.DestroyImmediate(dummyGo);
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
        private static bool CheckBattleUnitAttacksEnemyUnit(CharacterData heraclesData)
        {
            bool ok = true;
            var lineMonster = AssetDatabase.LoadAssetAtPath<MonsterData>("Assets/Data/Monsters/LineMonster_Basic.asset");

            var managerGo = new GameObject("SmokeTest_BvE_CombatManager");
            var allyGo = new GameObject("SmokeTest_BvE_Ally");
            var enemyGo = new GameObject("SmokeTest_BvE_Enemy");

            try
            {
                var manager = managerGo.AddComponent<CombatManager>();

                var heraclesUnit = allyGo.AddComponent<BattleUnit>();
                heraclesUnit.Initialize(heraclesData);
                manager.RegisterAlly(heraclesUnit);

                var enemy = enemyGo.AddComponent<EnemyUnit>();
                enemy.Initialize(lineMonster, round: 1);
                manager.RegisterEnemyTarget(enemy);

                // 헤라클레스가 평타로 실제 EnemyUnit의 체력을 깎아야 한다 (몬스터 방어력 0 -> 감쇄 없음).
                float healthBefore = enemy.CurrentHealth;
                manager.ProcessAttack(heraclesUnit);
                float damage = healthBefore - enemy.CurrentHealth;
                float minDamage = heraclesData.stats.physicalAttack;
                float maxDamage = heraclesData.stats.physicalAttack * heraclesData.stats.criticalMultiplier;
                ok &= LogAndCheck("BattleUnit -> EnemyUnit 평타 데미지 범위 내",
                    damage >= minDamage - 0.01f && damage <= maxDamage + 0.01f, damage.ToString());

                // 몬스터가 죽으면 더 이상 유효 타겟이 아니어야 한다.
                enemy.TakeTrueDamage(9999f);
                ok &= LogAndCheck("사망한 몬스터는 IsTargetable == false", !enemy.IsTargetable, enemy.IsTargetable.ToString());

                float healthBeforeSecond = enemy.CurrentHealth;
                manager.ProcessAttack(heraclesUnit);
                ok &= ApproxLog("사망한 몬스터는 더 이상 공격받지 않음", enemy.CurrentHealth, healthBeforeSecond);
            }
            finally
            {
                Object.DestroyImmediate(managerGo);
                Object.DestroyImmediate(allyGo);
                Object.DestroyImmediate(enemyGo);
            }

            ok &= CheckWaveSpawnerAutoRegistersWithCombatManager(heraclesData, lineMonster);

            return ok;
        }

        // WaveSpawner가 스폰한 몬스터를 CombatManager에 자동 등록해서, 실제로 공격 가능한지 확인한다.
        private static bool CheckWaveSpawnerAutoRegistersWithCombatManager(CharacterData heraclesData, MonsterData lineMonster)
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
                var heraclesUnit = allyGo.AddComponent<BattleUnit>();
                heraclesUnit.Initialize(heraclesData);
                manager.RegisterAlly(heraclesUnit);

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
                    manager.ProcessAttack(heraclesUnit);
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

        // PlacementGrid가 워크래프트3식 격자 배치(배치/이동/해제/사망 시 자동 해제)와
        // 배치 인원 기준 시너지 재계산을 올바르게 처리하는지 검증한다.
        private static bool CheckPlacementGrid(CharacterData spartanData, CharacterData heraclesData, CharacterData zeusData)
        {
            bool ok = true;
            var synergyData = AssetDatabase.LoadAssetAtPath<FactionSynergyData>("Assets/Data/Synergy/OlympusSynergy.asset");

            var managerGo = new GameObject("SmokeTest_Placement_CombatManager");
            var synergyGo = new GameObject("SmokeTest_Placement_SynergyManager");
            var gridGo = new GameObject("SmokeTest_Placement_Grid");

            try
            {
                var combatManager = managerGo.AddComponent<CombatManager>();
                var synergyManager = synergyGo.AddComponent<SynergyManager>();
                synergyManager.SetFactionSynergies(new List<FactionSynergyData> { synergyData });

                var grid = gridGo.AddComponent<PlacementGrid>();
                grid.Configure(3, 3, combatManager);

                // 배치 / 중복 배치 방지 / 범위 밖 배치 방지
                ok &= LogAndCheck("(0,0)에 스파르타 배치 성공", grid.TryPlaceUnit(0, 0, spartanData, out var spartanUnit), "true");
                ok &= LogAndCheck("이미 찬 슬롯에는 배치 실패", !grid.TryPlaceUnit(0, 0, heraclesData, out _), "true");
                ok &= LogAndCheck("(1,0)에 헤라클레스 배치 성공", grid.TryPlaceUnit(1, 0, heraclesData, out var heraclesUnit), "true");
                ok &= LogAndCheck("격자 범위 밖 배치 실패", !grid.TryPlaceUnit(5, 5, zeusData, out _), "true");
                ok &= LogAndCheck("GetUnitAt(0,0)이 스파르타 유닛 반환", grid.GetUnitAt(0, 0) == spartanUnit, "true");

                // 이동
                ok &= LogAndCheck("(1,0)->(2,0) 이동 성공", grid.TryMoveUnit(1, 0, 2, 0), "true");
                ok &= LogAndCheck("이동 후 원래 슬롯은 비어있음", !grid.IsSlotOccupied(1, 0), "true");
                ok &= LogAndCheck("찬 슬롯으로는 이동 실패", !grid.TryMoveUnit(2, 0, 0, 0), "true");

                // 세 번째 유닛 배치 후 시너지 재계산 (올림포스 3체 = 공격속도 +15%)
                ok &= LogAndCheck("(1,0)에 제우스 배치 성공", grid.TryPlaceUnit(1, 0, zeusData, out var zeusUnit), "true");
                grid.RecomputeSynergies(synergyManager);

                ok &= ApproxLog("배치 3체 시너지 반영 - 스파르타 공격속도", spartanUnit.EffectiveStats.attackSpeed, spartanData.stats.attackSpeed * 1.15f);
                ok &= ApproxLog("배치 3체 시너지 반영 - 헤라클레스 공격속도", heraclesUnit.EffectiveStats.attackSpeed, heraclesData.stats.attackSpeed * 1.15f);
                ok &= ApproxLog("배치 3체 시너지 반영 - 제우스 공격속도", zeusUnit.EffectiveStats.attackSpeed, zeusData.stats.attackSpeed * 1.15f);

                // 명시적 해제 (판매 등)
                ok &= LogAndCheck("(2,0) 유닛 해제 성공", grid.TryRemoveUnit(2, 0), "true");
                ok &= LogAndCheck("해제 후 슬롯이 비어있음", !grid.IsSlotOccupied(2, 0), "true");

                // 전투 중 사망 시 슬롯 자동 해제
                bool removedEventFired = false;
                grid.OnUnitRemoved += (_, _, _) => removedEventFired = true;
                for (int i = 0; i < 20 && !spartanUnit.IsDead; i++)
                    spartanUnit.TakePhysicalDamage(50f);

                ok &= LogAndCheck("스파르타 유닛 사망 처리됨", spartanUnit.IsDead, spartanUnit.IsDead.ToString());
                ok &= LogAndCheck("사망 시 슬롯(0,0) 자동 해제", !grid.IsSlotOccupied(0, 0), (!grid.IsSlotOccupied(0, 0)).ToString());
                ok &= LogAndCheck("사망 시 OnUnitRemoved 이벤트 발생", removedEventFired, removedEventFired.ToString());
            }
            finally
            {
                Object.DestroyImmediate(managerGo);
                Object.DestroyImmediate(synergyGo);
                Object.DestroyImmediate(gridGo);
            }

            return ok;
        }

        // GameManager가 배치/시너지/전투/웨이브/골드/승패 판정을 실제로 하나로 엮어 돌리는지 검증한다.
        private static bool CheckGameManager(CharacterData spartanData, CharacterData heraclesData, CharacterData zeusData)
        {
            var synergyData = AssetDatabase.LoadAssetAtPath<FactionSynergyData>("Assets/Data/Synergy/OlympusSynergy.asset");
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

            bool ok = CheckGameManagerPlacementAndKillReward(spartanData, heraclesData, zeusData, synergyData, lineMonster, fastConfig);
            ok &= CheckGameManagerVictory(lineMonster, synergyData);
            ok &= CheckGameManagerDefeat(lineMonster, synergyData);

            Object.DestroyImmediate(fastConfig);
            return ok;
        }

        private static bool CheckGameManagerPlacementAndKillReward(CharacterData spartanData, CharacterData heraclesData,
            CharacterData zeusData, FactionSynergyData synergyData, MonsterData lineMonster, WaveConfig fastConfig)
        {
            var go = new GameObject("SmokeTest_GameManager_Main");
            bool ok;
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(3, 3, fastConfig, lineMonster, null, null, new List<FactionSynergyData> { synergyData });

                // 웨이브 시작 전에도 배치가 가능해야 하고, 시너지가 바로 반영되어야 한다.
                game.PlaceUnit(0, 0, spartanData, out var spartanUnit);
                game.PlaceUnit(1, 0, heraclesData, out var heraclesUnit);
                game.PlaceUnit(2, 0, zeusData, out var zeusUnit);

                ok = ApproxLog("웨이브 시작 전 배치만으로도 3체 시너지 반영", spartanUnit.EffectiveStats.attackSpeed, spartanData.stats.attackSpeed * 1.15f);

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
                        game.CombatManager.ProcessAttack(heraclesUnit);

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

        private static bool CheckGameManagerVictory(MonsterData lineMonster, FactionSynergyData synergyData)
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
                game.Configure(3, 3, config, fastMonster, null, null, new List<FactionSynergyData> { synergyData });

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

        private static bool CheckGameManagerDefeat(MonsterData lineMonster, FactionSynergyData synergyData)
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
                game.Configure(3, 3, config, fastMonster, null, null, new List<FactionSynergyData> { synergyData });

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

        // 유니크/전설 등급 유닛들의 조합식(레어 2개 -> 유니크, 유니크 -> 전설 2종)이 올바르게
        // 채워졌는지 확인한다. 히든 조합식(전설 -> 히든)은 로스터 생성 시점부터 이미 있었다.
        private static bool CheckFusionChains()
        {
            bool ok = true;

            ok &= CheckFusionRecipe("Assets/Data/Characters/Asgard/Sif.asset", "시프",
                new[] { ("Assets/Data/Characters/Asgard/Tyr.asset", "티르"), ("Assets/Data/Characters/Asgard/Freyr.asset", "프레이") }, 200);
            ok &= CheckFusionRecipe("Assets/Data/Characters/Asgard/Thor.asset", "토르",
                new[] { ("Assets/Data/Characters/Asgard/Sif.asset", "시프") }, 400);
            ok &= CheckFusionRecipe("Assets/Data/Characters/Asgard/Loki.asset", "로키",
                new[] { ("Assets/Data/Characters/Asgard/Sif.asset", "시프") }, 400);

            ok &= CheckFusionRecipe("Assets/Data/Characters/AbyssalArchive/Ishtar.asset", "이슈타르",
                new[] { ("Assets/Data/Characters/AbyssalArchive/Gilgamesh.asset", "길가메시"), ("Assets/Data/Characters/AbyssalArchive/Enkidu.asset", "엔키두") }, 200);
            ok &= CheckFusionRecipe("Assets/Data/Characters/AbyssalArchive/Enlil.asset", "엔릴",
                new[] { ("Assets/Data/Characters/AbyssalArchive/Ishtar.asset", "이슈타르") }, 400);

            // 노멀 -> 매직 -> 레어까지 이어지는 하위 등급 조합 체인 검증 (같은 재료 3개를 요구).
            ok &= CheckFusionRecipe("Assets/Data/Characters/Asgard/Valkyrie.asset", "발키리 시종",
                new[] { ("Assets/Data/Characters/Asgard/Einherjar.asset", "아인헤리안 전사"),
                        ("Assets/Data/Characters/Asgard/Einherjar.asset", "아인헤리안 전사"),
                        ("Assets/Data/Characters/Asgard/Einherjar.asset", "아인헤리안 전사") }, 50);
            ok &= CheckFusionRecipe("Assets/Data/Characters/Asgard/Tyr.asset", "티르",
                new[] { ("Assets/Data/Characters/Asgard/Valkyrie.asset", "발키리 시종"),
                        ("Assets/Data/Characters/Asgard/Valkyrie.asset", "발키리 시종"),
                        ("Assets/Data/Characters/Asgard/Valkyrie.asset", "발키리 시종") }, 100);

            return ok;
        }

        // 재료로 "같은 유닛 3마리"를 요구하는 조합(노멀 3개 -> 매직)이 GameManager를 통해 실제로 동작하는지 확인한다.
        private static bool CheckFuseSameMaterialTriple()
        {
            var einherjar = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Asgard/Einherjar.asset");
            var valkyrie = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Asgard/Valkyrie.asset");

            var go = new GameObject("SmokeTest_FuseSameMaterial");
            bool ok;
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(3, 3, null, null, null, null, new List<FactionSynergyData>());

                bool fusedWithTwo = false;
                if (einherjar != null && valkyrie != null)
                {
                    game.Inventory.Add(einherjar, 2); // 2마리뿐이면 부족해야 한다
                    fusedWithTwo = game.TryFuseCharacter(valkyrie);
                }
                ok = LogAndCheck("같은 재료 2마리로는 조합 실패", !fusedWithTwo, fusedWithTwo.ToString());

                bool fusedWithThree = false;
                if (einherjar != null && valkyrie != null)
                {
                    game.Inventory.Add(einherjar, 1); // 총 3마리로 채움
                    game.GrantGold(50);
                    fusedWithThree = game.TryFuseCharacter(valkyrie);
                }
                ok &= LogAndCheck("같은 재료 3마리 + 골드로 조합 성공", fusedWithThree, fusedWithThree.ToString());
                ok &= LogAndCheck("조합 후 아인헤리안 전사 재료 전부 소모", einherjar == null || game.Inventory.GetCount(einherjar) == 0,
                    einherjar == null ? "asset missing" : game.Inventory.GetCount(einherjar).ToString());
                ok &= LogAndCheck("조합 결과 발키리 시종이 보유 목록에 추가됨", valkyrie == null || game.Inventory.GetCount(valkyrie) == 1,
                    valkyrie == null ? "asset missing" : game.Inventory.GetCount(valkyrie).ToString());
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
        private static bool CheckGachaAndFusion(CharacterData heraclesData, CharacterData zeusData)
        {
            bool ok = true;

            var database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/Data/CharacterDatabase.asset");
            var goldSummon = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Gacha/GoldSummon.asset");
            var gemMidSummon = AssetDatabase.LoadAssetAtPath<GachaTable>("Assets/Data/Gacha/GemMidSummon.asset");

            ok &= LogAndCheck("CharacterDatabase 로드", database != null, (database != null).ToString());
            ok &= LogAndCheck("CharacterDatabase 전체 43개 등록", database != null && database.allCharacters.Count == 43,
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

            ok &= CheckGameManagerSummonAndFusion(database, heraclesData, zeusData, goldSummon, gemMidSummon);

            return ok;
        }

        private static bool CheckGameManagerSummonAndFusion(CharacterDatabase database, CharacterData heraclesData,
            CharacterData zeusData, GachaTable goldSummon, GachaTable gemMidSummon)
        {
            var go = new GameObject("SmokeTest_GameManager_Gacha");
            bool ok;
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(3, 3, null, null, null, null, new List<FactionSynergyData>());
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

                // 조합: 제우스는 헤라클레스 1 + 보석 300이 필요하다.
                bool fusedWithoutMaterial = game.TryFuseCharacter(zeusData);
                ok &= LogAndCheck("재료 없이 조합 시도하면 실패", !fusedWithoutMaterial, fusedWithoutMaterial.ToString());

                game.Inventory.Add(heraclesData); // 헤라클레스를 보유하고 있다고 가정
                bool fusedWithoutGems = game.TryFuseCharacter(zeusData);
                ok &= LogAndCheck("보석 없이 조합 시도하면 실패(재료는 소모 안 됨)", !fusedWithoutGems, fusedWithoutGems.ToString());
                ok &= LogAndCheck("실패한 조합은 재료를 소모하지 않음", game.Inventory.GetCount(heraclesData) == 1, game.Inventory.GetCount(heraclesData).ToString());

                game.GrantGems(300);
                bool fused = game.TryFuseCharacter(zeusData);
                ok &= LogAndCheck("재료+재화 충분하면 조합 성공", fused, fused.ToString());
                ok &= LogAndCheck("조합 후 보석 300 소모", game.Gems == 0, game.Gems.ToString());
                ok &= LogAndCheck("조합 후 헤라클레스 재료 소모됨", game.Inventory.GetCount(heraclesData) == 0, game.Inventory.GetCount(heraclesData).ToString());
                ok &= LogAndCheck("조합 결과 제우스가 보유 목록에 추가됨", game.Inventory.GetCount(zeusData) == 1, game.Inventory.GetCount(zeusData).ToString());
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
            var heraclesData = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Olympus/Heracles.asset");
            var zeusData = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Olympus/Zeus.asset");

            bool ok = true;

            // 헤라클레스는 제우스 조합 재료이므로, 역방향 조회 시 제우스가 나와야 한다.
            var targets = database.FindFusionTargetsUsing(heraclesData);
            ok &= LogAndCheck("헤라클레스를 재료로 쓰는 조합 대상에 제우스 포함", targets.Contains(zeusData),
                string.Join(", ", targets.ConvertAll(t => t.characterName)));

            var go = new GameObject("SmokeTest_GameManager_AutoPlaceSell");
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(3, 3, null, null, null, null, new List<FactionSynergyData>());
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
            var einherjar = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Asgard/Einherjar.asset");
            var valkyrie = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Asgard/Valkyrie.asset");

            var go = new GameObject("SmokeTest_FuseFieldRemoval");
            bool ok;
            try
            {
                var game = go.AddComponent<GameManager>();
                game.Configure(4, 1, null, null, null, null, new List<FactionSynergyData>());

                game.PlaceUnit(0, 0, einherjar, out var unitA);
                game.PlaceUnit(1, 0, einherjar, out _);
                game.PlaceUnit(2, 0, einherjar, out _);
                game.PlaceUnit(3, 0, einherjar, out _);
                game.Inventory.Add(einherjar, 4);
                game.GrantGold(50);

                bool fused = game.TryFuseCharacter(valkyrie, unitA);
                ok = LogAndCheck("지정 유닛을 재료로 조합 성공", fused, fused.ToString());
                ok &= LogAndCheck("지정했던 유닛(unitA)이 화면(필드)에서 파괴됨", unitA == null, (unitA == null).ToString());

                int remainingEinherjar = 0;
                for (int x = 0; x < 4; x++)
                {
                    var u = game.PlacementGrid.GetUnitAt(x, 0);
                    if (u != null && u.Source == einherjar) remainingEinherjar++;
                }
                ok &= LogAndCheck("필드에 아인헤리안 전사가 1마리만 남음(4마리 중 3마리 소모)",
                    remainingEinherjar == 1, remainingEinherjar.ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            return ok;
        }
    }
}
