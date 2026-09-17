using System;
using System.Collections.Generic;
using UnityEngine;
using MRD.Data;

namespace MRD.Synergy
{
    /// <summary>
    /// 특정 진영을 몇 체 이상 배치했을 때 어떤 보너스를 주는지 정의하는 표.
    /// tiers는 requiredCount 오름차순으로 등록한다 (예: 3체 -> 6체).
    /// </summary>
    [Serializable]
    public class SynergyTier
    {
        public int requiredCount;

        [TextArea]
        public string description;

        public StatModifier bonus;
    }

    [CreateAssetMenu(fileName = "NewFactionSynergy", menuName = "MRD/Faction Synergy Data")]
    public class FactionSynergyData : ScriptableObject
    {
        public Faction faction;
        public List<SynergyTier> tiers = new List<SynergyTier>();
    }
}
