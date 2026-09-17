using System.Collections.Generic;
using UnityEngine;
using MRD.Data;

namespace MRD.Battle
{
    public enum Team
    {
        Ally,
        Enemy,
    }

    /// <summary>
    /// 전장의 아군/적 BattleUnit을 등록해두고, OnAttack 이벤트를 받아 실제 평타 판정
    /// (데미지, 치명타, 마나 획득, 확률형 패시브 발동)을 처리한다.
    /// 타겟팅은 아직 좌표/경로 시스템이 없어 "상대 팀의 생존한 첫 유닛"으로 단순화했고,
    /// 웨이브/스포너 시스템이 생기면 교체될 자리다.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        [SerializeField] private float baseCritChancePercent = 10f;
        [SerializeField] private float manaPerHit = 10f;

        private readonly List<BattleUnit> _allies = new List<BattleUnit>();
        private readonly List<BattleUnit> _enemies = new List<BattleUnit>();

        public void RegisterUnit(BattleUnit unit, Team team)
        {
            GetTeamList(team).Add(unit);
            unit.OnAttack += ProcessAttack;
            unit.OnDeath += HandleDeath;
        }

        public void UnregisterUnit(BattleUnit unit, Team team)
        {
            GetTeamList(team).Remove(unit);
            unit.OnAttack -= ProcessAttack;
            unit.OnDeath -= HandleDeath;
        }

        /// <summary>
        /// attacker의 평타 한 번을 판정한다. BattleUnit.OnAttack에 자동으로 연결되며,
        /// 테스트나 다른 시스템에서 직접 호출해도 된다.
        /// </summary>
        public void ProcessAttack(BattleUnit attacker)
        {
            var opponents = _allies.Contains(attacker) ? _enemies : _allies;
            var target = FindTarget(opponents);
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
        private static void TryRollTriggerSkill(BattleUnit attacker, BattleUnit target)
        {
            var skill = attacker.Source.passiveSkill;
            if (skill == null || skill.skillType != SkillType.Trigger) return;
            if (Random.Range(0f, 100f) >= skill.triggerChancePercent) return;

            float baseDamage = Mathf.Max(attacker.EffectiveStats.physicalAttack, attacker.EffectiveStats.magicAttack);
            target.TakeTrueDamage(baseDamage * 0.5f);
        }

        private void HandleDeath(BattleUnit unit)
        {
            var team = _allies.Contains(unit) ? Team.Ally : Team.Enemy;
            UnregisterUnit(unit, team);
        }

        private static BattleUnit FindTarget(List<BattleUnit> candidates)
        {
            foreach (var candidate in candidates)
            {
                if (candidate != null && !candidate.IsDead)
                    return candidate;
            }
            return null;
        }

        private List<BattleUnit> GetTeamList(Team team) => team == Team.Ally ? _allies : _enemies;
    }
}
