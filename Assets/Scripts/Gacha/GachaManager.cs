using System.Collections.Generic;
using UnityEngine;
using MRD.Data;

namespace MRD.Gacha
{
    /// <summary>
    /// GachaTable의 등급별 가중치로 등급을 하나 뽑고, CharacterDatabase에서 그 등급에 해당하는
    /// 캐릭터 하나를 무작위로 골라 반환한다. 재화 차감/보유 등록은 GameManager가 담당한다.
    /// </summary>
    public class GachaManager : MonoBehaviour
    {
        [SerializeField] private CharacterDatabase database;

        public void SetDatabase(CharacterDatabase db) => database = db;

        /// <summary>뽑기에 성공하면 캐릭터를 반환하고, 해당 등급의 실제 유닛이 없으면 null을 반환한다.</summary>
        public CharacterData Roll(GachaTable table)
        {
            if (table == null || table.weights == null || table.weights.Count == 0)
                return null;
            if (database == null)
            {
                Debug.LogWarning("[MRD] GachaManager.Roll: database가 연결되어 있지 않다 (GameManager.SetCharacterDatabase 확인 필요).");
                return null;
            }

            var rarity = RollRarity(table.weights);
            var candidates = database.GetByRarity(rarity);
            if (candidates.Count == 0)
            {
                Debug.LogWarning($"[MRD] GachaManager.Roll: '{rarity}' 등급 후보 0마리 " +
                    $"(데이터베이스 총 {database.allCharacters.Count}마리 등록됨). " +
                    "CharacterDatabase.asset이 최신 상태로 반영됐는지 확인 필요.");
                return null;
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        private static Rarity RollRarity(List<RarityWeight> weights)
        {
            float total = 0f;
            foreach (var w in weights) total += w.weight;

            float roll = Random.Range(0f, total);
            float cumulative = 0f;
            foreach (var w in weights)
            {
                cumulative += w.weight;
                if (roll <= cumulative) return w.rarity;
            }

            return weights[weights.Count - 1].rarity; // 부동소수 오차 대비 fallback
        }
    }
}
