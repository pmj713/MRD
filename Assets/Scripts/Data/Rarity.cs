namespace MRD.Data
{
    /// <summary>
    /// 유닛 등급. 숫자가 높을수록 상위 등급이며, 상위 등급은 하위 등급을 재료로 조합해서 얻는다.
    /// 엘리트 이상은 아직 실제 유닛 콘텐츠는 없지만, 추후 확장을 위해 미리 정의해 둔다.
    /// </summary>
    public enum Rarity
    {
        Normal = 0,
        Magic = 1,
        Rare = 2,
        Unique = 3,
        Legend = 4,
        Hidden = 5,

        // 아래는 확장용 (현재 유닛 콘텐츠 없음)
        Elite = 6,
        Limit = 7,
        Epic = 8,
        Infinity = 9,
        Creation = 10,
        Special = 11,
    }
}
