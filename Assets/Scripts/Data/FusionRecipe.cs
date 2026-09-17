using System;

namespace MRD.Data
{
    /// <summary>
    /// 상위 등급 유닛을 만들기 위한 조합 재료. 노말/매직처럼 소환으로만 얻는 최하위 등급은 비워둔다.
    /// </summary>
    [Serializable]
    public class FusionRecipe
    {
        public CharacterData[] requiredCharacters; // 재료로 소모되는 하위 유닛들
        public int goldCost;
        public int gemCost; // 보석 (구 목재)
    }
}
