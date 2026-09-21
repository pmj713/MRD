using System.Collections.Generic;
using UnityEngine;
using MRD.Data;

namespace MRD.Gacha
{
    /// <summary>
    /// 프로젝트에 존재하는 모든 CharacterData를 한 곳에 모아둔 마스터 목록.
    /// 가챠 풀 조회(등급별 후보 찾기)와 도감 등 다른 시스템에서도 재사용한다.
    /// 목록 채우기는 Assets/Editor/DatabaseBuilder.cs의 편집기 도구로 자동화한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "MRD/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        public List<CharacterData> allCharacters = new List<CharacterData>();

        public List<CharacterData> GetByRarity(Rarity rarity)
        {
            var result = new List<CharacterData>();
            foreach (var character in allCharacters)
            {
                if (character != null && character.rarity == rarity)
                    result.Add(character);
            }
            return result;
        }

        /// <summary>주어진 유닛을 조합 재료로 사용하는 상위 유닛들을 찾는다 (유닛 정보창의 "조합" 버튼 등에 사용).</summary>
        public List<CharacterData> FindFusionTargetsUsing(CharacterData material)
        {
            var result = new List<CharacterData>();
            if (material == null) return result;

            foreach (var candidate in allCharacters)
            {
                if (candidate == null || candidate.fusionRecipe?.requiredCharacters == null) continue;

                foreach (var required in candidate.fusionRecipe.requiredCharacters)
                {
                    if (required == material)
                    {
                        result.Add(candidate);
                        break;
                    }
                }
            }
            return result;
        }
    }
}
