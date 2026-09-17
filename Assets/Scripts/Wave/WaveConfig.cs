using UnityEngine;

namespace MRD.Wave
{
    /// <summary>원작(나랜디/원랜디)의 라운드제 진행 규칙을 담는 설정값 모음.</summary>
    [CreateAssetMenu(fileName = "NewWaveConfig", menuName = "MRD/Wave Config")]
    public class WaveConfig : ScriptableObject
    {
        [Header("라운드 진행")]
        public int totalRounds = 85;
        public int monstersPerRound = 10;

        [Header("라운드 길이 (초)")]
        public int earlyRoundThreshold = 9; // 이 라운드까지는 짧은 길이를 쓴다
        public float earlyRoundDuration = 30f;
        public float lateRoundDuration = 42f;

        [Header("보스 등장 규칙")]
        public int leftBossInterval = 10; // 10의 배수 라운드
        public int rightBossOffset = 3;   // 끝자리 3 라운드 (13, 23, ...)

        [Header("필수 클리어 라운드 (해당 라운드의 몬스터를 전부 처치 못하면 게임 종료)")]
        public int[] mandatoryClearRounds = { 80, 85 };

        [Header("데스카운트 (몬스터가 끝까지 도달해도 되는 허용 횟수)")]
        public int startingDeathCount = 10;
        public int deathCountDecreaseEveryRounds = 20; // 이 라운드 간격마다 데스카운트 1 감소
    }
}
