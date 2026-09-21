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

        public event Action<EnemyUnit> OnDeath;

        public void Initialize(MonsterData source, int round)
        {
            Source = source;
            SpawnRound = round;
            SpawnOrder = SpawnOrderCounter.Next();

            float growth = Mathf.Pow(source.healthGrowthPerRound, Mathf.Max(0, round - 1));
            CurrentHealth = source.baseHealth * growth;
        }

        /// <summary>물리 데미지. 몬스터의 방어력에 따라 감쇄된다.</summary>
        public void TakePhysicalDamage(float rawDamage) => ApplyDamage(rawDamage * CombatMath.CalculateMitigation(Source.baseArmor));

        /// <summary>마법 데미지. 몬스터의 마법저항에 따라 감쇄된다.</summary>
        public void TakeMagicDamage(float rawDamage) => ApplyDamage(rawDamage * CombatMath.CalculateMitigation(Source.baseMagicResist));

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
