using UnityEngine;

namespace MRD.Battle
{
    /// <summary>BattleUnit, EnemyUnit 등 여러 곳에서 공유하는 전투 계산 공식 모음.</summary>
    public static class CombatMath
    {
        /// <summary>
        /// 100 / (100 + 방어스탯) 형태의 감쇄 배율. 방어스탯이 음수(방깎)면 오히려 데미지가 증폭된다.
        /// 분모가 0 이하로 내려가 계산이 깨지지 않도록 최소 1로 clamp한다.
        /// </summary>
        public static float CalculateMitigation(float defenseStat)
        {
            float denominator = Mathf.Max(1f, 100f + defenseStat);
            return 100f / denominator;
        }
    }
}
