using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MRD.Data;

namespace MRD.Game
{
    /// <summary>플레이어가 보유한 캐릭터 수량. 소환/조합으로 늘어나고, 조합 재료로 소모된다.</summary>
    public class PlayerInventory
    {
        // UnityEngine.Object가 오버라이드하는 Equals/GetHashCode에 기대지 않고, 순수 참조 동일성으로만
        // 키를 비교한다. (Object의 해시가 항상 안정적이지 않아 기본 비교자를 쓰면 같은 에셋인데도
        // 다른 항목으로 취급되는 경우가 있었다.)
        private sealed class ReferenceComparer : IEqualityComparer<CharacterData>
        {
            public bool Equals(CharacterData a, CharacterData b) => ReferenceEquals(a, b);
            public int GetHashCode(CharacterData obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private readonly Dictionary<CharacterData, int> _counts = new Dictionary<CharacterData, int>(new ReferenceComparer());

        public int GetCount(CharacterData data)
        {
            if (data == null) return 0;
            return _counts.TryGetValue(data, out var count) ? count : 0;
        }

        public void Add(CharacterData data, int amount = 1)
        {
            if (data == null || amount <= 0) return;
            _counts[data] = GetCount(data) + amount;
        }

        /// <summary>보유량이 충분하면 차감하고 true, 부족하면 아무 변화 없이 false.</summary>
        public bool TryConsume(CharacterData data, int amount = 1)
        {
            if (GetCount(data) < amount) return false;
            _counts[data] -= amount;
            return true;
        }
    }
}
