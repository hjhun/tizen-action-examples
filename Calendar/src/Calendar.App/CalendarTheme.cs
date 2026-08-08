namespace Calendar.App;

public sealed record CalendarTheme(
    string RootSurface,
    string SecondarySurface,
    string CellSurface,
    string CellOutOfMonthSurface,
    string CellSelectedSurface,
    string TextPrimary,
    string TextSecondary,
    string TextDisabled,
    string SundayAccent,
    string TodayPillSurface,
    string TodayPillText,
    string FocusOutline,
    IReadOnlyList<string> EventColors,
    float MonthPaneRatio,
    float AgendaPaneRatio,
    int SafeInsetHorizontal,
    int SafeInsetVertical,
    int FocusOutlineWidth,
    float FocusScale)
{
    public static CalendarTheme Light { get; } = new(
        RootSurface: "#F8F9FC",
        SecondarySurface: "#F1F3F7",
        CellSurface: "#F6F7F9",
        CellOutOfMonthSurface: "#FAFAFB",
        CellSelectedSurface: "#E5EDFC",
        TextPrimary: "#27282C",
        TextSecondary: "#656870",
        TextDisabled: "#BEC1C8",
        SundayAccent: "#DD403A",
        TodayPillSurface: "#36383C",
        TodayPillText: "#FFFFFF",
        FocusOutline: "#15161A",
        EventColors:
        [
            "#BEE9AD",
            "#BDD7FF",
            "#FFD39A",
            "#DEB8F4",
        ],
        MonthPaneRatio: 0.68f,
        AgendaPaneRatio: 0.32f,
        SafeInsetHorizontal: 64,
        SafeInsetVertical: 44,
        FocusOutlineWidth: 4,
        FocusScale: 1.03f);
}
