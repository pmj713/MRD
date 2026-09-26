using System;
using UnityEngine;

namespace MRD.Data
{
    /// <summary>
    /// 액티브(쿨타임) / 패시브(상시) / 트리거(평타 시 확률 발동) 세 종류로 구분한다.
    /// </summary>
    public enum SkillType
    {
        Active,
        Passive,
        Trigger,
    }

    /// <summary>
    /// 트리거/액티브 스킬이 일으키는 효과 종류 (둘이 공유). TrueDamage가 0번(기본값)이라, 이 필드가
    /// 추가되기 전부터 있던 스킬 데이터(예: 청룡의 "용의 가호")는 그대로 기존 동작을 유지한다.
    /// </summary>
    public enum TriggerEffectType
    {
        TrueDamage, // 방어력을 무시하는 피해 (공격력 × 배율값 - 트리거는 고정 50%, 액티브는 activeEffectValue가 그 배율)
        ArmorShred, // 대상의 방어력을 값만큼 깎는다 (누적)
        Stun,       // 대상을 값만큼(초) 이동 불가로 만든다
    }

    [Serializable]
    public class SkillData
    {
        public string skillName;

        [TextArea]
        public string description;

        public SkillType skillType;

        [Header("액티브 스킬 전용 - 쿨타임이 돌 때마다 자동 발동해서 사거리 안의 모든 대상에게 적용된다")]
        public float cooldown;
        public TriggerEffectType activeEffectType;
        public float activeEffectValue; // ArmorShred=방어력 감소량, Stun=지속시간(초), TrueDamage=공격력 배율(예: 1.5=150%)

        [Header("트리거 스킬 전용")]
        [Range(0f, 100f)]
        public float triggerChancePercent; // 평타 적중 시 발동 확률 (%)
        public TriggerEffectType triggerEffectType;
        public float triggerEffectValue; // ArmorShred=방어력 감소량, Stun=지속시간(초)
    }
}
