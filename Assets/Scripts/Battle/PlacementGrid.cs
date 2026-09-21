using System;
using System.Collections.Generic;
using UnityEngine;
using MRD.Data;
using MRD.Synergy;

namespace MRD.Battle
{
    /// <summary>
    /// 워크래프트3 커스텀 디펜스 맵처럼, 고정된 격자 슬롯에 아군 유닛을 배치/해제하는 시스템.
    /// 슬롯은 (x, y) 정수 좌표로만 식별한다 - 실제 화면 좌표로의 매핑은 씬/아트가 준비되면
    /// 별도 테이블(슬롯 좌표 -> 배치 지점 Transform)로 추가할 자리다.
    /// </summary>
    public class PlacementGrid : MonoBehaviour
    {
        [SerializeField] private int width = 5;
        [SerializeField] private int height = 5;
        [SerializeField] private CombatManager combatManager;

        private readonly Dictionary<(int x, int y), BattleUnit> _slots = new Dictionary<(int x, int y), BattleUnit>();

        public int Width => width;
        public int Height => height;

        public event Action<int, int, BattleUnit> OnUnitPlaced;
        public event Action<int, int, BattleUnit> OnUnitRemoved;

        public void Configure(int gridWidth, int gridHeight, CombatManager manager)
        {
            width = gridWidth;
            height = gridHeight;
            combatManager = manager;
        }

        public bool IsValidSlot(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

        public bool IsSlotOccupied(int x, int y) => _slots.ContainsKey((x, y));

        public BattleUnit GetUnitAt(int x, int y) => _slots.TryGetValue((x, y), out var unit) ? unit : null;

        /// <summary>비어 있는 슬롯을 하나 찾는다 (소환 등으로 얻은 유닛을 자동 배치할 때 사용).</summary>
        public bool TryFindEmptySlot(out int x, out int y)
        {
            for (int yy = 0; yy < height; yy++)
            {
                for (int xx = 0; xx < width; xx++)
                {
                    if (!IsSlotOccupied(xx, yy))
                    {
                        x = xx;
                        y = yy;
                        return true;
                    }
                }
            }
            x = 0;
            y = 0;
            return false;
        }

        /// <summary>주어진 유닛이 배치돼 있는 슬롯 좌표를 찾는다 (판매 등 유닛 참조로부터 슬롯을 역추적할 때 사용).</summary>
        public bool TryFindSlotOf(BattleUnit unit, out int x, out int y)
        {
            foreach (var kv in _slots)
            {
                if (kv.Value == unit)
                {
                    x = kv.Key.x;
                    y = kv.Key.y;
                    return true;
                }
            }
            x = 0;
            y = 0;
            return false;
        }

        /// <summary>주어진 CharacterData와 일치하는 배치된 유닛의 슬롯을 하나 찾는다 (조합 재료 소모 시 필드에서도 제거할 대상을 찾을 때 사용).</summary>
        public bool TryFindSlotOfData(CharacterData data, out int x, out int y)
        {
            foreach (var kv in _slots)
            {
                if (kv.Value.Source == data)
                {
                    x = kv.Key.x;
                    y = kv.Key.y;
                    return true;
                }
            }
            x = 0;
            y = 0;
            return false;
        }

        /// <summary>빈 슬롯에 새 유닛을 배치한다. 성공하면 CombatManager에 아군으로 자동 등록된다.</summary>
        public bool TryPlaceUnit(int x, int y, CharacterData data, out BattleUnit placedUnit)
        {
            placedUnit = null;
            if (data == null || !IsValidSlot(x, y) || IsSlotOccupied(x, y))
                return false;

            var go = new GameObject($"BattleUnit_{data.characterName}_{x}_{y}");
            go.transform.SetParent(transform);
            var unit = go.AddComponent<BattleUnit>();
            unit.Initialize(data);
            unit.OnDeath += HandleAnyUnitDeath;

            _slots[(x, y)] = unit;
            combatManager?.RegisterAlly(unit);

            placedUnit = unit;
            OnUnitPlaced?.Invoke(x, y, unit);
            return true;
        }

        /// <summary>슬롯에서 유닛을 빼낸다 (판매, 재배치 준비 등). 유닛 GameObject는 파괴된다.</summary>
        public bool TryRemoveUnit(int x, int y)
        {
            if (!_slots.TryGetValue((x, y), out var unit))
                return false;

            _slots.Remove((x, y));
            unit.OnDeath -= HandleAnyUnitDeath;
            combatManager?.UnregisterAlly(unit);
            OnUnitRemoved?.Invoke(x, y, unit);

            if (Application.isPlaying)
                Destroy(unit.gameObject);
            else
                DestroyImmediate(unit.gameObject); // 에디터(플레이 모드 아님)에서 호출되는 경우 대비

            return true;
        }

        /// <summary>이미 배치된 유닛을 다른 빈 슬롯으로 옮긴다.</summary>
        public bool TryMoveUnit(int fromX, int fromY, int toX, int toY)
        {
            if (!IsValidSlot(toX, toY) || IsSlotOccupied(toX, toY))
                return false;
            if (!_slots.TryGetValue((fromX, fromY), out var unit))
                return false;

            _slots.Remove((fromX, fromY));
            _slots[(toX, toY)] = unit;
            return true;
        }

        /// <summary>
        /// 현재 배치된 유닛 전체를 기준으로 진영 시너지를 다시 계산해서 각 유닛에 반영한다.
        /// 배치/해제가 있을 때마다 호출해줘야 한다 (자동 호출하지 않음 - 여러 번 바꾼 뒤 한 번만 계산하고 싶을 수 있어서).
        /// </summary>
        public void RecomputeSynergies(SynergyManager synergyManager)
        {
            if (synergyManager == null) return;

            var deployedCharacters = new List<CharacterData>();
            foreach (var unit in _slots.Values)
                deployedCharacters.Add(unit.Source);

            var bonuses = synergyManager.Evaluate(deployedCharacters);

            foreach (var unit in _slots.Values)
            {
                var bonus = bonuses.TryGetValue(unit.Source.faction, out var b) ? b : default;
                unit.ApplySynergyBonus(bonus);
            }
        }

        // 배치된 유닛이 전투 중 사망하면 슬롯을 비워서 다시 배치할 수 있게 한다.
        // (CombatManager 등록 해제는 BattleUnit.OnDeath를 통해 CombatManager 스스로 처리한다.)
        private void HandleAnyUnitDeath(BattleUnit unit)
        {
            (int x, int y)? foundKey = null;
            foreach (var kvp in _slots)
            {
                if (kvp.Value == unit)
                {
                    foundKey = kvp.Key;
                    break;
                }
            }

            if (!foundKey.HasValue) return;

            _slots.Remove(foundKey.Value);
            OnUnitRemoved?.Invoke(foundKey.Value.x, foundKey.Value.y, unit);
        }
    }
}
