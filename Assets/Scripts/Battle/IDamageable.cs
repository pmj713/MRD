using UnityEngine;

namespace MRD.Battle
{
    /// <summary>
    /// 피해를 입고 공격 대상이 될 수 있는 존재의 공통 인터페이스. 현재는 EnemyUnit(MRD.Wave)만 구현한다 -
    /// 아군(BattleUnit)은 몬스터에게 공격받지 않으므로 데미지를 받을 필요가 없다. CombatManager가
    /// MRD.Wave에 직접 의존하지 않도록 이 인터페이스로 대상을 다룬다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>지금 공격 대상으로 선택해도 되는 상태인지. 죽었거나(체력 0) 전장을 이탈했으면 false.</summary>
        bool IsTargetable { get; }

        /// <summary>사거리 판정에 쓰는 현재 월드 좌표.</summary>
        Vector3 Position { get; }

        /// <summary>등장(초기화) 순서. 값이 작을수록 먼저 등장한(=더 오래 살아있는) 대상이다.</summary>
        int SpawnOrder { get; }

        void TakePhysicalDamage(float rawDamage);
        void TakeTrueDamage(float rawDamage);
    }
}
