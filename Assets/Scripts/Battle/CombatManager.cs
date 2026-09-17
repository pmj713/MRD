using System.Collections.Generic;
using UnityEngine;
using MRD.Data;

namespace MRD.Battle
{
    /// <summary>
    /// 아군 BattleUnit을 등록해두고, OnAttack 이벤트를 받아 실제 평타 판정
    /// (데미지, 치명타, 마나 획득, 확률형 패시브 발동)을 처리한다.
    /// 공격 대상은 IDamageable로만 다루기 때문에, 실제 몬스터(MRD.Wave.EnemyUnit)든
    /// 테스트용 BattleUnit이든 상관없이 동일하게 동작한다. WaveSpawner가 스폰한
    /// EnemyUnit을 RegisterEnemyTarget으로 등록해주면 실제 웨이브 몬스터를 공격하게 된다.
    /// 타겟팅은 아직 좌표/경로 시스템이 없어 "등록된 대상 중 첫 번째 유효 대상"으로 단순화했다.
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
            unit.OnDeath += HandleAllyDeath;
        }

        public void UnregisterAlly(BattleUnit unit)
        {
            _allies.Remove(unit);
            unit.OnAttack -= ProcessAttack;
            unit.OnDeath -= HandleAllyDeath;
        }

        public void RegisterEnemyTarget(IDamageable target) => _enemyTargets.Add(target);

        public void UnregisterEnemyTarget(IDamageable target) => _enemyTargets.Remove(target);

        /// <summary>
        /// attacker의 평타 한 번을 판정한다. BattleUnit.OnAttack에 자동으로 연결되며,
        /// 테스트나 다른 시스템에서 직접 호출해도 된다.
        /// </summary>
        public void ProcessAttack(BattleUnit attacker)
        {
            var target = FindTarget();
            if (target == null) return;

            var stats = attacker.EffectiveStats;
            bool isCrit = Random.Range(0f, 100f) < baseCritChancePercent;
            float critMultiplier = isCrit ? Mathf.Max(1f, stats.criticalMultiplier) : 1f;

            if (stats.physicalAttack > 0f)
                target.TakePhysicalDamage(stats.physicalAttack * critMultiplier);

            if (stats.magicAttack > 0f)
                target.TakeMagicDamage(stats.magicAttack * critMultiplier);

            attacker.AddMana(manaPerHit);
            TryRollTriggerSkill(attacker, target);
        }

        // 트리거 패시브(평타 시 % 확률 발동)는 방어력/마법저항을 무시하는 추가 타격으로 통일 처리한다.
        private static void TryRollTriggerSkill(BattleUnit attacker, IDamageable target)
        {
            var skill = attacker.Source.passiveSkill;
            if (skill == null || skill.skillType != SkillType.Trigger) return;
            if (Random.Range(0f, 100f) >= skill.triggerChancePercent) return;

            float baseDamage = Mathf.Max(attacker.EffectiveStats.physicalAttack, attacker.EffectiveStats.magicAttack);
            target.TakeTrueDamage(baseDamage * 0.5f);
        }

        private void HandleAllyDeath(BattleUnit unit) => UnregisterAlly(unit);

        private IDamageable FindTarget()
        {
            foreach (var candidate in _enemyTargets)
            {
                if (candidate != null && candidate.IsTargetable)
                    return candidate;
            }
            return null;
        }
    }
}
