using System.Collections.Generic;
using UnityEngine;
using MRD.Data;

namespace MRD.Synergy
{
    /// <summary>
    /// 배치된 캐릭터 목록을 받아 진영별로 몇 단계 시너지가 발동하는지 계산한다.
    /// 실제 스탯 반영은 전투 시스템(추후 만들 BattleUnit)이 이 결과를 받아서 처리한다.
    /// </summary>
    public class SynergyManager : MonoBehaviour
    {
        [SerializeField]
        private List<FactionSynergyData> factionSynergies = new List<FactionSynergyData>();

        /// <summary>
        /// 인스펙터 대신 코드로(부트스트랩, 테스트 등) 진영 시너지 표를 등록할 때 사용한다.
        /// </summary>
        public void SetFactionSynergies(IEnumerable<FactionSynergyData> data)
        {
            factionSynergies.Clear();
            factionSynergies.AddRange(data);
        }

        /// <summary>
        /// 진영별로 달성한 가장 높은 시너지 단계의 보너스만 반환한다. (단계는 누적되지 않음)
        /// </summary>
        public Dictionary<Faction, StatModifier> Evaluate(IReadOnlyList<CharacterData> deployedCharacters)
        {
            var countByFaction = CountByFaction(deployedCharacters);
            var result = new Dictionary<Faction, StatModifier>();

            foreach (var synergyData in factionSynergies)
            {
                if (!countByFaction.TryGetValue(synergyData.faction, out var count))
                    continue;

                SynergyTier achievedTier = null;
                foreach (var tier in synergyData.tiers)
                {
                    // tiers가 오름차순이라, 조건을 만족하는 것들 중 마지막이 가장 높은 단계다.
                    if (count >= tier.requiredCount)
                        achievedTier = tier;
                }

                if (achievedTier != null)
                    result[synergyData.faction] = achievedTier.bonus;
            }

            return result;
        }

        private static Dictionary<Faction, int> CountByFaction(IReadOnlyList<CharacterData> deployedCharacters)
        {
            var countByFaction = new Dictionary<Faction, int>();
            foreach (var character in deployedCharacters)
            {
                countByFaction.TryGetValue(character.faction, out var current);
                countByFaction[character.faction] = current + 1;
            }
            return countByFaction;
        }
    }
}
