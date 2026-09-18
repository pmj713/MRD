using System;
using System.Collections.Generic;
using UnityEngine;
using MRD.Data;

namespace MRD.Gacha
{
    public enum GachaCurrency
    {
        Gold,
        Gem,
    }

    [Serializable]
    public class RarityWeight
    {
        public Rarity rarity;
        public float weight;
    }

    /// <summary>
    /// 소환 한 번의 규칙: 어떤 재화를 얼마나 소모하고, 등급별로 어떤 확률로 뽑히는지.
    /// 원작의 "골드=노말/매직", "보석 1개=하급, 3개=중급, 5개=고급" 구조를 테이블로 표현한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGachaTable", menuName = "MRD/Gacha Table")]
    public class GachaTable : ScriptableObject
    {
        public string tableName;
        public GachaCurrency currency;
        public int cost;
        public List<RarityWeight> weights = new List<RarityWeight>();
    }
}
