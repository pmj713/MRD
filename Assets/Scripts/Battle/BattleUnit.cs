using System;
using UnityEngine;
using MRD.Data;

namespace MRD.Battle
{
    /// <summary>
    /// 전투에 배치된 캐릭터 하나의 런타임 상태.
    /// 몬스터에게 공격받지 않으므로(항상 아군만 공격을 가함) 체력/피격 개념 없이 공격 관련 상태만 관리한다.
    /// </summary>
    public class BattleUnit : MonoBehaviour
    {
        public CharacterData Source { get; private set; }
        public CharacterStats EffectiveStats { get; private set; }
        public Vector3 Position => transform.position;
        public float CurrentMana { get; private set; }

        /// <summary>
        /// 공격 타이머가 한 번 채워질 때마다 발생. 실제 데미지 판정/투사체 생성은
        /// 이 이벤트를 구독하는 전투 시스템(추후 제작)이 담당한다.
        /// </summary>
        public event Action<BattleUnit> OnAttack;

        private float _activeSkillCooldownRemaining;
        private float _attackTimer;

        public void Initialize(CharacterData source)
        {
            Source = source;
            EffectiveStats = source.stats;

            CurrentMana = 0f;
            _activeSkillCooldownRemaining = 0f;
            _attackTimer = 0f;
        }

        private void Update()
        {
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
    }
}
