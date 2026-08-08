namespace Calendar.App;

internal static class CalendarLayoutMetrics
{
    internal const float CommandBarHeight = 82.0f;
    internal const float CommandBarGap = 18.0f;

    internal static float CalculateMainHeight(CalendarTheme theme)
    {
        var reservedDesignHeight =
            theme.SafeInsetVertical +
            theme.SafeInsetBottom +
            CommandBarHeight +
            CommandBarGap;
        return ProportionalViewport.ReferenceHeight - reservedDesignHeight;
    }
}
