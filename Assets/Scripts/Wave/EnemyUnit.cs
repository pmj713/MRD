using System;
using UnityEngine;
using MRD.Battle;

namespace MRD.Wave
{
    /// <summary>
    /// 웨이브에서 스폰된 몬스터 하나의 런타임 상태.
    /// 아직 씬에 실제 이동 경로가 없어서, 이동은 0(도착)~100(스폰 지점) 진행도 값으로 추상화했다.
    /// 진행도가 0이 되면 OnReachedEnd(라인 통과 실패), 체력이 0이 되면 OnDeath가 발생한다.
    /// </summary>
    public class EnemyUnit : MonoBehaviour, IDamageable
    {
        private const float FullProgress = 100f;

        public MonsterData Source { get; private set; }
        public int SpawnRound { get; private set; }
        public float CurrentHealth { get; private set; }
        public float RemainingProgress { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;
        public bool HasReachedEnd { get; private set; }
        public bool IsTargetable => !IsDead && !HasReachedEnd;

        public event Action<EnemyUnit> OnDeath;
        public event Action<EnemyUnit> OnReachedEnd;

        public void Initialize(MonsterData source, int round)
        {
            Source = source;
            SpawnRound = round;

            float growth = Mathf.Pow(source.healthGrowthPerRound, Mathf.Max(0, round - 1));
            CurrentHealth = source.baseHealth * growth;
            RemainingProgress = FullProgress;
            HasReachedEnd = false;
        }

        public void Tick(float deltaTime)
        {
            if (IsDead || HasReachedEnd) return;

            RemainingProgress -= Source.moveSpeed * deltaTime;
            if (RemainingProgress <= 0f)
            {
                RemainingProgress = 0f;
                HasReachedEnd = true;
                OnReachedEnd?.Invoke(this);
            }
        }

        /// <summary>물리 데미지. 몬스터의 방어력에 따라 감쇄된다.</summary>
        public void TakePhysicalDamage(float rawDamage) => ApplyDamage(rawDamage * CombatMath.CalculateMitigation(Source.baseArmor));

        /// <summary>마법 데미지. 몬스터의 마법저항에 따라 감쇄된다.</summary>
        public void TakeMagicDamage(float rawDamage) => ApplyDamage(rawDamage * CombatMath.CalculateMitigation(Source.baseMagicResist));

        /// <summary>방어력/마법저항을 무시하는 데미지.</summary>
        public void TakeTrueDamage(float rawDamage) => ApplyDamage(rawDamage);

        private void ApplyDamage(float amount)
        {
            if (IsDead || HasReachedEnd) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (IsDead)
                OnDeath?.Invoke(this);
        }
    }
}
