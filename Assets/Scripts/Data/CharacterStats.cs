using System;
using UnityEngine;

namespace MRD.Data
{
    /// <summary>
    /// 캐릭터의 기본 전투 수치. 물리/마법 데미지를 분리해서 관리한다.
    /// </summary>
    [Serializable]
    public struct CharacterStats
    {
        [Header("공격")]
        public float physicalAttack;
        public float magicAttack;
        public float attackSpeed;
        public float criticalMultiplier;

        [Header("방어")]
        public float health;
        public float armor;
        public float magicResist;
    }
}
