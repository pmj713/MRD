using System;
using UnityEngine;
using MRD.Battle;

namespace MRD.Wave
{
    /// <summary>
    /// 웨이브에서 스폰된 몬스터 하나의 런타임 상태.
    /// 몬스터는 정해진 경로를 계속 순찰하며(라인 이탈이라는 개념이 없음), 죽어야만 전장에서 사라진다.
    /// 실제 순찰 이동은 시각 표현(EnemyUnitView)이 담당하고, 여기서는 전투 관련 상태만 다룬다.
    /// </summary>
    public class EnemyUnit : MonoBehaviour, IDamageable
    {
        public MonsterData Source { get; private set; }
        public int SpawnRound { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;
        public bool IsTargetable => !IsDead;
        public Vector3 Position => transform.position;
        public int SpawnOrder { get; private set; }

        /// <summary>누적된 방어력 감소를 반영한 실제 방어력. 음수가 되면 CombatMath 공식상 오히려
        /// 데미지가 증폭되는데, 이는 의도된 동작이다 (CombatMath.CalculateMitigation 주석 참고).</summary>
        public float EffectiveArmor => Source.baseArmor - _armorReduction;

        /// <summary>지금 스턴 상태라 이동할 수 없는지 (EnemyUnitView가 이동 처리 전에 확인한다).</summary>
        public bool IsStunned => _stunRemaining > 0f;

        public event Action<EnemyUnit> OnDeath;

        private float _armorReduction;
        private float _stunRemaining;

        public void Initialize(MonsterData source, int round)
        {
            Source = source;
            SpawnRound = round;
            SpawnOrder = SpawnOrderCounter.Next();

            float growth = Mathf.Pow(source.healthGrowthPerRound, Mathf.Max(0, round - 1));
            CurrentHealth = source.baseHealth * growth;

            _armorReduction = 0f;
            _stunRemaining = 0f;
        }

        private void Update()
        {
            if (_stunRemaining > 0f)
                _stunRemaining -= Time.deltaTime;
        }

        /// <summary>물리 데미지. 몬스터의 방어력에 따라 감쇄된다.</summary>
        public void TakePhysicalDamage(float rawDamage) => ApplyDamage(rawDamage * CombatMath.CalculateMitigation(EffectiveArmor));

        /// <summary>방어력을 무시하는 데미지.</summary>
        public void TakeTrueDamage(float rawDamage) => ApplyDamage(rawDamage);

        /// <summary>방어력을 amount만큼 깎는다. 여러 유닛/여러 번 맞을수록 계속 누적된다.</summary>
        public void ReduceArmor(float amount)
        {
            if (amount <= 0f) return;
            _armorReduction += amount;
        }

        /// <summary>duration초 동안 이동을 멈춘다. 이미 스턴 중이면 더 긴 쪽으로 갱신한다(중첩 누적 아님).</summary>
        public void ApplyStun(float duration)
        {
            if (duration <= 0f) return;
            _stunRemaining = Mathf.Max(_stunRemaining, duration);
        }

        private void ApplyDamage(float amount)
        {
            if (IsDead) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (IsDead)
                OnDeath?.Invoke(this);
        }
    }
}
