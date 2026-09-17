namespace MRD.Battle
{
    /// <summary>
    /// 피해를 입고 공격 대상이 될 수 있는 존재의 공통 인터페이스.
    /// BattleUnit과 EnemyUnit(MRD.Wave)이 각각 구현해서, CombatManager가 아군/적 구분 없이
    /// 동일한 방식으로 데미지를 판정할 수 있게 한다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>지금 공격 대상으로 선택해도 되는 상태인지. 죽었거나(체력 0) 전장을 이탈했으면 false.</summary>
        bool IsTargetable { get; }

        void TakePhysicalDamage(float rawDamage);
        void TakeMagicDamage(float rawDamage);
        void TakeTrueDamage(float rawDamage);
    }
}
