using System;
using System.Collections.Generic;
using UnityEngine;
using MRD.Data;

namespace MRD.Battle
{
    /// <summary>
    /// 아군 유닛을 배치/해제하는 시스템. 슬롯은 (x, y) 정수 좌표로만 식별하고, (0, 0)을
    /// 스폰 지점(대개 화면상 순찰 경로 한가운데)으로 취급한다 - 실제 화면 좌표로의 매핑은
    /// 슬롯 좌표에 셀 크기를 곱하는 식으로 별도 테이블에서 처리한다.
    /// 격자 크기에 상한이 없어서(음수 좌표 포함, 자유 평면) 유닛 수 제한이 없고,
    /// 슬롯 좌표는 항상 유닛마다 고유하므로 화면에서 서로 겹치는 일도 없다.
    /// </summary>
    public class PlacementGrid : MonoBehaviour
    {
        [SerializeField] private CombatManager combatManager;

        private readonly Dictionary<(int x, int y), BattleUnit> _slots = new Dictionary<(int x, int y), BattleUnit>();

        public event Action<int, int, BattleUnit> OnUnitPlaced;
        public event Action<int, int, BattleUnit> OnUnitRemoved;

        /// <summary>gridWidth/gridHeight는 더 이상 배치 가능 범위를 제한하지 않는다 (하위 호환을 위해 인자만 남겨둠).</summary>
        public void Configure(int gridWidth, int gridHeight, CombatManager manager)
        {
            combatManager = manager;
        }

        public bool IsValidSlot(int x, int y) => true;

        public bool IsSlotOccupied(int x, int y) => _slots.ContainsKey((x, y));

        public BattleUnit GetUnitAt(int x, int y) => _slots.TryGetValue((x, y), out var unit) ? unit : null;

        /// <summary>
        /// 스폰 지점(0, 0)에서 가장 가까운 빈 슬롯을 찾는다 (소환/조합으로 얻은 유닛을 자동 배치할 때 사용).
        /// 스폰 지점에 이미 유닛이 있으면 그 다음으로 가까운 빈 자리를 반경을 넓혀가며 찾으므로,
        /// 유닛 수에 상한이 없고 서로 좌표가 겹치는 일도 없다.
        /// </summary>
        public bool TryFindEmptySlot(out int x, out int y)
        {
            for (int radius = 0; ; radius++)
            {
                bool foundAny = false;
                int bestX = 0, bestY = 0;
                float bestDistSqr = float.MaxValue;

                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue; // 이번 반경의 테두리만 (안쪽은 이전 반경에서 이미 확인함)
                        if (IsSlotOccupied(dx, dy)) continue;

                        float distSqr = dx * dx + dy * dy;
                        if (!foundAny || distSqr < bestDistSqr)
                        {
                            foundAny = true;
                            bestDistSqr = distSqr;
                            bestX = dx;
                            bestY = dy;
                        }
                    }
                }

                if (foundAny)
                {
                    x = bestX;
                    y = bestY;
                    return true;
                }
            }
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
    }
}
