using System.Collections.Generic;
using UnityEngine;

namespace MRD.Control
{
    /// <summary>
    /// 화면 좌표 기준 클릭/드래그 판정을 순수 계산으로 분리한 것. 실제 마우스 입력 읽기는
    /// SelectionController가 담당하고, 여기 있는 함수들은 입력 없이도 검증할 수 있다.
    /// </summary>
    public static class SelectionMath
    {
        /// <summary>screenPoint에서 maxPixelRadius 이내에 있는 후보 중 가장 가까운 것을 반환한다. 없으면 null.</summary>
        public static Selectable FindNearest(IReadOnlyList<Selectable> candidates, Camera cam, Vector2 screenPoint, float maxPixelRadius)
        {
            Selectable best = null;
            float bestDistSqr = maxPixelRadius * maxPixelRadius;

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;

                Vector2 screenPos = cam.WorldToScreenPoint(candidate.transform.position);
                float distSqr = (screenPos - screenPoint).sqrMagnitude;
                if (distSqr <= bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>screenRect 안에 화면 좌표가 들어오는 후보를 전부 반환한다.</summary>
        public static List<Selectable> FindInRect(IReadOnlyList<Selectable> candidates, Camera cam, Rect screenRect)
        {
            var result = new List<Selectable>();
            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;

                Vector2 screenPos = cam.WorldToScreenPoint(candidate.transform.position);
                if (screenRect.Contains(screenPos))
                    result.Add(candidate);
            }
            return result;
        }
    }
}
