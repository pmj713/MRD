namespace MRD.Data
{
    /// <summary>
    /// 캐릭터가 소속된 신화 진영.
    /// 전투에 같은 진영을 일정 수 이상 배치하면 시너지 버프가 발동한다.
    /// </summary>
    public enum Faction
    {
        Olympus,        // 올림포스 (그리스 신화)
        Asgard,         // 아스가르드 (북유럽 신화)
        ThroneOfRa,     // 라의 왕좌 (이집트 신화)
        NineRealms,     // 구주 (동양 신화)
        AbyssalArchive, // 심연의 서고 (메소포타미아 신화)
        Covenant,       // 숲의 맹약 (켈트 신화)
    }
}
