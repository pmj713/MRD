using UnityEngine;

namespace MRD.Wave
{
    /// <summary>
    /// 웨이브에 등장하는 몬스터 하나의 기준 데이터. 라운드가 올라갈수록 체력이
    /// healthGrowthPerRound 배율만큼 누적 성장한다 (1라운드 기준값 = baseHealth).
    /// </summary>
    [CreateAssetMenu(fileName = "NewMonster", menuName = "MRD/Monster Data")]
    public class MonsterData : ScriptableObject
    {
        public string monsterName;

        [Header("1라운드 기준 스탯")]
        public float baseHealth;
        public float baseArmor;
        public float baseMagicResist;

        [Header("라운드당 성장")]
        public float healthGrowthPerRound = 1.05f;

        [Header("이동/보상")]
        public float moveSpeed = 10f; // 초당 진행도(0~100) 감소량
        public int goldReward = 5;

        public bool isBoss;
    }
}
