using System;
using UnityEngine;
using MRD.Data;
using MRD.Control;

namespace MRD.Battle
{
    /// <summary>
    /// 전투에 배치된 캐릭터 하나의 런타임 상태.
    /// 몬스터에게 공격받지 않으므로(항상 아군만 공격을 가함) 체력/피격 개념 없이 공격 관련 상태만 관리한다.
    /// </summary>
    public class BattleUnit : MonoBehaviour
    {
        public CharacterData Source { get; private set; }

        /// <summary>
        /// 기본 스탯에 강화 건물의 등급별 공격력 보너스를 곱해서 매번 새로 계산한다.
        /// 건물에서 강화할 때마다 즉시 반영되도록 캐시하지 않는다.
        /// </summary>
        public CharacterStats EffectiveStats
        {
            get
            {
                var stats = Source.stats;
                var building = MRD.Building.Building.Instance;
                if (building != null)
                    stats.attackPower *= building.GetAttackBonusMultiplier(Source.rarity);
                return stats;
            }
        }

        public Vector3 Position => transform.position;

        /// <summary>
        /// 공격 타이머가 한 번 채워질 때마다 발생. 실제 데미지 판정/투사체 생성은
        /// 이 이벤트를 구독하는 전투 시스템(추후 제작)이 담당한다.
        /// </summary>
        public event Action<BattleUnit> OnAttack;

        /// <summary>
        /// 액티브 스킬이 자동으로 발동할 때마다 발생 (마나/쿨타임이 다 찼을 때 - 별도 조작 없이 자동 시전).
        /// 실제 효과 적용은 이 이벤트를 구독하는 전투 시스템이 담당한다.
        /// </summary>
        public event Action<BattleUnit> OnActiveSkillCast;

        private float _activeSkillCooldownRemaining;
        private float _attackTimer;
        private UnitMover _mover; // 이동 중인 동안은 공격하지 않으려고 참조한다 (없으면 항상 공격 가능한 유닛으로 취급).

        public void Initialize(CharacterData source)
        {
            Source = source;

            _activeSkillCooldownRemaining = 0f;
            _attackTimer = 0f;
        }

        private void Update()
        {
            if (_activeSkillCooldownRemaining > 0f)
                _activeSkillCooldownRemaining -= Time.deltaTime;

            TickAttack(Time.deltaTime);

            // 별도 조작 없이, 쿨타임이 돌 때마다 액티브 스킬을 자동으로 시전한다.
            if (TryUseActiveSkill())
                OnActiveSkillCast?.Invoke(this);
        }

        private void TickAttack(float deltaTime)
        {
            if (EffectiveStats.attackSpeed <= 0f) return;

            // UnitMover는 배치 시점 이후에 붙는 컴포넌트라 Initialize 시점엔 아직 없을 수 있어 여기서 지연 조회한다.
            if (_mover == null) _mover = GetComponent<UnitMover>();
            if (_mover != null && _mover.IsMoving) return; // 이동 중에는 공격하지 않는다 (타이머도 멈춰서, 멈추자마자 밀린 시간만큼 즉시 때리지 않는다)

            _attackTimer += deltaTime;
            float attackInterval = 1f / EffectiveStats.attackSpeed;
            if (_attackTimer < attackInterval) return;

            _attackTimer -= attackInterval;
            OnAttack?.Invoke(this);
        }

        /// <summary>
        /// 액티브 스킬 사용을 시도한다. 마나 없이 쿨타임 조건만 만족하면 성공한다.
        /// </summary>
        public bool TryUseActiveSkill()
        {
            var skill = Source.activeSkill;
            // 이름이 없으면 아직 내용이 채워지지 않은 플레이스홀더 스킬로 취급해 시전하지 않는다.
            if (skill == null || skill.skillType != SkillType.Active || string.IsNullOrEmpty(skill.skillName)) return false;
            if (_activeSkillCooldownRemaining > 0f) return false;

            _activeSkillCooldownRemaining = skill.cooldown;
            return true;
        }
    }
}
