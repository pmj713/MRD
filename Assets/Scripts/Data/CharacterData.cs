using UnityEngine;

namespace MRD.Data
{
    /// <summary>
    /// 유닛 하나에 대응하는 데이터 자산.
    /// Rarity는 소환 확률, 조합 검증 등 여러 시스템이 공통으로 참조하는 값이다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "MRD/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("기본 정보")]
        public string characterName;

        [TextArea]
        public string flavorText;

        public Sprite portrait;

        [Header("모델")]
        [Tooltip("비워두면 임시 큐브로 대체 표시된다.")]
        public GameObject visualPrefab;

        [Header("분류")]
        public Rarity rarity;

        [Header("스탯")]
        public CharacterStats stats;

        [Header("스킬")]
        public SkillData activeSkill;
        public SkillData passiveSkill;

        [Header("조합 레시피 (해당 등급부터 조합으로 획득하는 경우)")]
        public FusionRecipe fusionRecipe;
    }
}
