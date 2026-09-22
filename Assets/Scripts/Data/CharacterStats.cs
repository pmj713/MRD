using System;

namespace MRD.Data
{
    /// <summary>
    /// 캐릭터(아군)의 전투 수치. 아군은 몬스터에게 공격받지 않으므로 공격 관련 수치만 가진다.
    /// </summary>
    [Serializable]
    public struct CharacterStats
    {
        public float attackPower;
        public float attackSpeed;
        public float criticalMultiplier;
        public float attackRange;
    }
}
