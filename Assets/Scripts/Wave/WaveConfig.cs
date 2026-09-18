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

        [Header("몬스터는 정해진 경로를 계속 순찰하며 죽어야만 사라진다 - 처치가 밀려 생존 몬스터 수가 이 값에 도달하면 게임 종료")]
        public int maxAliveMonsters = 100;
    }
}
