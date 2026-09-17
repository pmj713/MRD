using System;
using UnityEngine;
using MRD.Data;

namespace MRD.Battle
{
    /// <summary>
    /// 전투에 배치된 캐릭터 하나의 런타임 상태.
    /// CharacterData(고정 수치)에 진영 시너지 보너스를 반영한 실제 전투 스탯과
    /// 현재 체력/마나/쿨타임을 관리한다.
    /// </summary>
    public class BattleUnit : MonoBehaviour
    {
        public CharacterData Source { get; private set; }
        public CharacterStats EffectiveStats { get; private set; }

        public float CurrentHealth { get; private set; }
        public float CurrentMana { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        /// <summary>
        /// 공격 타이머가 한 번 채워질 때마다 발생. 실제 데미지 판정/투사체 생성은
        /// 이 이벤트를 구독하는 전투 시스템(추후 제작)이 담당한다.
        /// </summary>
        public event Action<BattleUnit> OnAttack;

        /// <summary>체력이 0이 되는 순간 한 번만 발생. 전투 매니저가 이 이벤트로 유닛을 전장에서 제거한다.</summary>
        public event Action<BattleUnit> OnDeath;

        private float _activeSkillCooldownRemaining;
        private float _attackTimer;

        public void Initialize(CharacterData source, StatModifier synergyBonus = default)
        {
            Source = source;
            RecomputeEffectiveStats(synergyBonus);

            CurrentHealth = EffectiveStats.health;
            CurrentMana = 0f;
            _activeSkillCooldownRemaining = 0f;
            _attackTimer = 0f;
        }

        /// <summary>
        /// 배치 상태가 바뀌어 진영 시너지 단계가 달라졌을 때 호출한다.
        /// 최대 체력이 줄어드는 경우를 대비해 현재 체력은 새 최대치로 clamp한다.
        /// </summary>
        public void ApplySynergyBonus(StatModifier synergyBonus)
        {
            RecomputeEffectiveStats(synergyBonus);
            CurrentHealth = Mathf.Min(CurrentHealth, EffectiveStats.health);
        }

        private void RecomputeEffectiveStats(StatModifier modifier)
        {
            var baseStats = Source.stats;
            EffectiveStats = new CharacterStats
            {
                physicalAttack = baseStats.physicalAttack * (1f + modifier.physicalAttackPercent / 100f),
                magicAttack = baseStats.magicAttack * (1f + modifier.magicAttackPercent / 100f),
                attackSpeed = baseStats.attackSpeed * (1f + modifier.attackSpeedPercent / 100f),
                criticalMultiplier = baseStats.criticalMultiplier,
                health = baseStats.health * (1f + modifier.healthPercent / 100f),
                armor = baseStats.armor * (1f + modifier.armorPercent / 100f),
                magicResist = baseStats.magicResist * (1f + modifier.magicResistPercent / 100f),
            };
        }

        private void Update()
        {
            if (IsDead) return;

            if (_activeSkillCooldownRemaining > 0f)
                _activeSkillCooldownRemaining -= Time.deltaTime;

            TickAttack(Time.deltaTime);
        }

        private void TickAttack(float deltaTime)
        {
            if (EffectiveStats.attackSpeed <= 0f) return;

            _attackTimer += deltaTime;
            float attackInterval = 1f / EffectiveStats.attackSpeed;
            if (_attackTimer < attackInterval) return;

            _attackTimer -= attackInterval;
            OnAttack?.Invoke(this);
        }

        /// <summary>
        /// 액티브 스킬 사용을 시도한다. 마나/쿨타임 조건을 만족해야 성공한다.
        /// </summary>
        public bool TryUseActiveSkill()
        {
            var skill = Source.activeSkill;
            if (skill == null || skill.skillType != SkillType.Active) return false;
            if (_activeSkillCooldownRemaining > 0f) return false;
            if (CurrentMana < skill.manaCost) return false;

            CurrentMana -= skill.manaCost;
            _activeSkillCooldownRemaining = skill.cooldown;
            return true;
        }

        /// <summary>
        /// 평타 적중 등으로 마나를 얻는다. 액티브 스킬 소모 마나치를 최대치로 취급한다.
        /// </summary>
        public void AddMana(float amount)
        {
            var skill = Source.activeSkill;
            if (skill == null) return;

            CurrentMana = Mathf.Min(CurrentMana + amount, skill.manaCost);
        }

        /// <summary>물리 데미지. 방어력에 따라 감쇄된다.</summary>
        public void TakePhysicalDamage(float rawDamage) => ApplyDamage(rawDamage * CombatMath.CalculateMitigation(EffectiveStats.armor));

        /// <summary>마법 데미지. 마법저항에 따라 감쇄된다.</summary>
        public void TakeMagicDamage(float rawDamage) => ApplyDamage(rawDamage * CombatMath.CalculateMitigation(EffectiveStats.magicResist));

        /// <summary>방어력/마법저항을 무시하는 데미지.</summary>
        public void TakeTrueDamage(float rawDamage) => ApplyDamage(rawDamage);

        private void ApplyDamage(float amount)
        {
            if (IsDead) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (IsDead)
                OnDeath?.Invoke(this);
        }
    }
}
