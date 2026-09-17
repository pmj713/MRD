using System;

namespace MRD.Data
{
    /// <summary>
    /// 진영 시너지 등이 적용하는 퍼센트 증가값 모음. 0이면 해당 스탯에 영향 없음.
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        public float physicalAttackPercent;
        public float magicAttackPercent;
        public float attackSpeedPercent;
        public float healthPercent;
        public float armorPercent;
        public float magicResistPercent;
    }
}
