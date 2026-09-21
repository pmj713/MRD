namespace MRD.Battle
{
    /// <summary>
    /// BattleUnit/EnemyUnit이 공유하는 전역 등장 순서 카운터.
    /// "제일 먼저 나온(=가장 오래 살아있는) 대상"을 우선 타겟팅하는 데 쓴다 - 값이 작을수록 먼저 등장했다는 뜻.
    /// </summary>
    internal static class SpawnOrderCounter
    {
        private static int _next;
        public static int Next() => _next++;
    }
}
