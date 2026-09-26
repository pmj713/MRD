using System;
using System.Collections.Generic;
using UnityEngine;
using MRD.Data;
using MRD.Game;

namespace MRD.Building
{
    /// <summary>
    /// 클릭해서 골드로 강화할 수 있는 건물. 등급(노말~히든)마다 강화 단계를 독립적으로 가지고 있고,
    /// 한 등급을 강화하면 그 등급 유닛 전체의 실제 공격력이 오른다 (BattleUnit.EffectiveStats에서 반영).
    /// 등급마다 강화 비용이 다르며, 한 등급당 최대 MaxUpgradeLevel번까지만 강화할 수 있다.
    /// </summary>
    public class Building : MonoBehaviour
    {
        public const int MaxUpgradeLevel = 10;

        private const float AttackBonusPerLevel = 0.15f; // 강화 1회당 해당 등급 유닛 공격력 15% 증가
        private const float CostGrowthPerLevel = 0.35f; // 강화 1회당 다음 강화 비용 35% 증가

        // FusionBookUI의 등급 테이블과 같은 등급 범위(노말~히든)를 사용한다.
        public static readonly Rarity[] DisplayedGrades =
        {
            Rarity.Normal, Rarity.Magic, Rarity.Rare, Rarity.Unique, Rarity.Legend, Rarity.Hidden,
        };

        private static readonly Dictionary<Rarity, int> BaseUpgradeCostByGrade = new Dictionary<Rarity, int>
        {
            { Rarity.Normal, 50 },
            { Rarity.Magic, 120 },
            { Rarity.Rare, 260 },
            { Rarity.Unique, 520 },
            { Rarity.Legend, 1000 },
            { Rarity.Hidden, 2000 },
        };

        /// <summary>씬에 하나만 존재하는 건물 인스턴스. BattleUnit이 등급별 공격력 보너스를 조회할 때 사용한다.</summary>
        public static Building Instance { get; private set; }

        private readonly Dictionary<Rarity, int> _levelByGrade = new Dictionary<Rarity, int>();

        /// <summary>강화에 성공할 때마다 발생 (어떤 등급이 강화됐는지와 함께 - UI 갱신용).</summary>
        public event Action<Rarity> OnUpgraded;

        private void Awake() => Instance = this;
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int GetLevel(Rarity grade) => _levelByGrade.TryGetValue(grade, out var level) ? level : 0;

        public bool IsMaxLevel(Rarity grade) => GetLevel(grade) >= MaxUpgradeLevel;

        /// <summary>해당 등급 유닛의 공격력에 곱해지는 배율 (강화 안 했으면 1배).</summary>
        public float GetAttackBonusMultiplier(Rarity grade) => 1f + GetLevel(grade) * AttackBonusPerLevel;

        /// <summary>다음 강화에 필요한 골드. 이미 최대 단계면 0을 반환한다.</summary>
        public int GetNextUpgradeCost(Rarity grade)
        {
            if (IsMaxLevel(grade)) return 0;
            int baseCost = BaseUpgradeCostByGrade.TryGetValue(grade, out var cost) ? cost : BaseUpgradeCostByGrade[Rarity.Normal];
            return Mathf.RoundToInt(baseCost * Mathf.Pow(1f + CostGrowthPerLevel, GetLevel(grade)));
        }

        /// <summary>골드가 충분하면 소모하고 해당 등급을 한 단계 강화한다. 최대 단계거나 골드가 부족하면 실패한다.</summary>
        public bool TryUpgrade(Rarity grade, GameManager game)
        {
            if (IsMaxLevel(grade) || game == null) return false;

            int cost = GetNextUpgradeCost(grade);
            if (!game.TrySpendGold(cost)) return false;

            _levelByGrade[grade] = GetLevel(grade) + 1;
            OnUpgraded?.Invoke(grade);
            return true;
        }
    }
}
