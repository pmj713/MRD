using System.Collections.Generic;
using UnityEngine;
using MRD.Data;

namespace MRD.Battle
{
    /// <summary>
    /// 아군 BattleUnit을 등록해두고, OnAttack 이벤트를 받아 실제 평타 판정
    /// (데미지, 치명타, 마나 획득, 확률형 패시브 발동)을 처리한다.
    /// 공격 대상은 IDamageable로만 다루기 때문에 CombatManager가 MRD.Wave에 직접 의존하지 않는다.
    /// WaveSpawner가 스폰한 EnemyUnit을 RegisterEnemyTarget으로 등록해주면 실제 웨이브 몬스터를 공격하게 된다.
    /// 타겟팅은 attacker의 attackRange(사거리) 안에 있는 대상 중 가장 먼저 등장한(=가장 오래 살아있는) 쪽을 고른다.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        [SerializeField] private float baseCritChancePercent = 10f;
        [SerializeField] private float manaPerHit = 10f;

        private readonly List<BattleUnit> _allies = new List<BattleUnit>();
        private readonly List<IDamageable> _enemyTargets = new List<IDamageable>();

        public void RegisterAlly(BattleUnit unit)
        {
            _allies.Add(unit);
            unit.OnAttack += ProcessAttack;
        }

        public void UnregisterAlly(BattleUnit unit)
        {
            _allies.Remove(unit);
            unit.OnAttack -= ProcessAttack;
        }

        public void RegisterEnemyTarget(IDamageable target) => _enemyTargets.Add(target);

        public void UnregisterEnemyTarget(IDamageable target) => _enemyTargets.Remove(target);

        /// <summary>
        /// attacker의 평타 한 번을 판정한다. BattleUnit.OnAttack에 자동으로 연결되며,
        /// 테스트나 다른 시스템에서 직접 호출해도 된다.
        /// </summary>
        public void ProcessAttack(BattleUnit attacker)
        {
            var target = FindTarget(attacker);
            if (target == null) return;

            var stats = attacker.EffectiveStats;
            bool isCrit = Random.Range(0f, 100f) < baseCritChancePercent;
            float critMultiplier = isCrit ? Mathf.Max(1f, stats.criticalMultiplier) : 1f;

            target.TakePhysicalDamage(stats.attackPower * critMultiplier);

            attacker.AddMana(manaPerHit);
            TryRollTriggerSkill(attacker, target);
        }

        // 트리거 패시브(평타 시 % 확률 발동)는 방어력을 무시하는 추가 타격으로 통일 처리한다.
        private static void TryRollTriggerSkill(BattleUnit attacker, IDamageable target)
        {
            var skill = attacker.Source.passiveSkill;
            if (skill == null || skill.skillType != SkillType.Trigger) return;
            if (Random.Range(0f, 100f) >= skill.triggerChancePercent) return;

            target.TakeTrueDamage(attacker.EffectiveStats.attackPower * 0.5f);
        }

        // 사거리(attacker의 EffectiveStats.attackRange) 안에 있는 대상 중 SpawnOrder가 가장 작은(=가장
        // 먼저 등장해서 가장 오래 살아있는) 쪽을 고른다. 사거리가 0 이하로 설정된(아직 값을 안 채운) 유닛은
        // 예전처럼 사거리 무제한으로 취급해서 조용히 공격 불능이 되는 것을 막는다.
        private IDamageable FindTarget(BattleUnit attacker)
        {
            float range = attacker.EffectiveStats.attackRange;
            bool unlimitedRange = range <= 0f;
            float rangeSqr = range * range;

            IDamageable oldest = null;
            int oldestSpawnOrder = int.MaxValue;

            foreach (var candidate in _enemyTargets)
            {
                if (candidate == null || !candidate.IsTargetable) continue;

                float sqrDistance = (candidate.Position - attacker.Position).sqrMagnitude;
                if (!unlimitedRange && sqrDistance > rangeSqr) continue;

                if (candidate.SpawnOrder < oldestSpawnOrder)
                {
                    oldestSpawnOrder = candidate.SpawnOrder;
                    oldest = candidate;
                }
            }

            return oldest;
        }
    }
}
