using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MRD.Data;
using MRD.Battle;
using MRD.Synergy;
using MRD.Wave;

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

                manager.RegisterUnit(heraclesUnit, Team.Ally);
                manager.RegisterUnit(spartanUnit, Team.Enemy);

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

                manager.RegisterUnit(zeusUnit, Team.Ally);
                manager.RegisterUnit(dummyUnit, Team.Enemy);

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

        // WaveSpawner가 라운드 진행/보스 등장 규칙/데스카운트/필수클리어 게임오버를 올바르게 처리하는지 검증한다.
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
            ok &= CheckWaveDeathCountGameOver(fastMonster);
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
            config.startingDeathCount = 9999;
            config.deathCountDecreaseEveryRounds = 0;

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

        // 시나리오 B: 몬스터를 죽이지 않고 계속 통과시키면 데스카운트가 줄어들다 게임오버가 되어야 한다.
        private static bool CheckWaveDeathCountGameOver(MonsterData fastMonster)
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
            config.startingDeathCount = 3;
            config.deathCountDecreaseEveryRounds = 0;

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

                ok = LogAndCheck("데스카운트 소진으로 게임오버 발생", spawner.IsGameOver, spawner.IsGameOver.ToString());
                ok &= LogAndCheck("데스카운트 0", spawner.RemainingDeathCount == 0, spawner.RemainingDeathCount.ToString());
                ok &= LogAndCheck("게임오버 사유에 '데스카운트' 포함", gameOverReason != null && gameOverReason.Contains("데스카운트"), gameOverReason ?? "null");
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
            config.startingDeathCount = 9999;
            config.deathCountDecreaseEveryRounds = 0;

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
    }
}
