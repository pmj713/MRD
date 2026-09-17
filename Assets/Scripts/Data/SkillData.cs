using System;
using UnityEngine;

namespace MRD.Data
{
    /// <summary>
    /// 액티브(마나+쿨타임) / 패시브(상시) / 트리거(평타 시 확률 발동) 세 종류로 구분한다.
    /// </summary>
    public enum SkillType
    {
        Active,
        Passive,
        Trigger,
    }

    [Serializable]
    public class SkillData
    {
        public string skillName;

        [TextArea]
        public string description;

        public SkillType skillType;

        [Header("액티브 스킬 전용")]
        public float manaCost;
        public float cooldown;

        [Header("트리거 스킬 전용")]
        [Range(0f, 100f)]
        public float triggerChancePercent; // 평타 적중 시 발동 확률 (%)
    }
}
