using UnityEngine;
using MRD.Wave;

namespace MRD.Game
{
    /// <summary>
    /// EnemyUnit.RemainingProgress(100~0)에 맞춰 좌우로 이동하는 임시 시각 표현.
    /// 실제 이동 경로(웨이포인트 등)가 생기면 이 스크립트를 대체하면 된다.
    /// </summary>
    public class EnemyUnitView : MonoBehaviour
    {
        private EnemyUnit _enemy;
        private float _startX;
        private float _endX;
        private float _laneY;

        public void Setup(EnemyUnit enemy, float startX, float endX, float laneY)
        {
            _enemy = enemy;
            _startX = startX;
            _endX = endX;
            _laneY = laneY;
        }

        private void Update()
        {
            if (_enemy == null) return;

            float t = 1f - (_enemy.RemainingProgress / 100f);
            float x = Mathf.Lerp(_startX, _endX, t);
            transform.position = new Vector3(x, _laneY, 0f);
        }
    }
}
