using System.Windows;
using System.Windows.Media;

namespace AmongUsModManager.Services;

public record ThemeDefinition(string Id, string Name, string Description, string AccentHex);

public static class ThemeManager
{
    public static readonly List<ThemeDefinition> AvailableThemes = new()
    {
        new("SteelBlue", "Stalowy Błękit", "Klasyczny ciemny motyw inżynieryjny z neutralnym błękitem.", "#2563EB"),
        new("Emerald", "Szmaragdowy Grafit", "Chłodny ciemny grafit z tonem butelkowej zieleni.", "#059669"),
        new("Amber", "Bursztynowy Węgiel", "Ciepły węgiel drzewny z akcentem szlachetnego bursztynu.", "#D97706"),
        new("Titanium", "Tytanowy Karbon", "Minimalistyczny grafit z jasnoniebieskim stalowym akcentem.", "#0EA5E9")
    };

    public static void ApplyTheme(string themeId)
    {
        var app = Application.Current;
        if (app == null) return;

        void SetBrush(string key, string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();

                app.Resources[key] = brush;

                if (app.MainWindow != null)
                {
                    app.MainWindow.Resources[key] = brush;
                }
            }
            catch { }
        }

        switch (themeId)
        {
            case "Emerald":
                SetBrush("WindowBackground", "#0B1410");
                SetBrush("CardBackground", "#12211A");
                SetBrush("CardBorder", "#1E3B2F");
                SetBrush("AccentColor", "#059669");
                SetBrush("AccentHover", "#047857");
                SetBrush("SuccessColor", "#10B981");
                SetBrush("WarningColor", "#F59E0B");
                SetBrush("DangerColor", "#EF4444");
                SetBrush("SubTextColor", "#86EFAC");
                SetBrush("InputBackground", "#070E0B");
                SetBrush("HeaderBadgeBg", "#132D22");
                SetBrush("HeaderBadgeFg", "#6EE7B7");
                break;

            case "Amber":
                SetBrush("WindowBackground", "#14120E");
                SetBrush("CardBackground", "#201B14");
                SetBrush("CardBorder", "#3D3222");
                SetBrush("AccentColor", "#D97706");
                SetBrush("AccentHover", "#B45309");
                SetBrush("SuccessColor", "#10B981");
                SetBrush("WarningColor", "#F59E0B");
                SetBrush("DangerColor", "#EF4444");
                SetBrush("SubTextColor", "#D4C7B8");
                SetBrush("InputBackground", "#0D0C09");
                SetBrush("HeaderBadgeBg", "#2E2416");
                SetBrush("HeaderBadgeFg", "#FCD34D");
                break;

            case "Titanium":
                SetBrush("WindowBackground", "#101114");
                SetBrush("CardBackground", "#191B21");
                SetBrush("CardBorder", "#2B2E38");
                SetBrush("AccentColor", "#0EA5E9");
                SetBrush("AccentHover", "#0284C7");
                SetBrush("SuccessColor", "#10B981");
                SetBrush("WarningColor", "#F59E0B");
                SetBrush("DangerColor", "#EF4444");
                SetBrush("SubTextColor", "#94A3B8");
                SetBrush("InputBackground", "#0B0C0E");
                SetBrush("HeaderBadgeBg", "#1E2633");
                SetBrush("HeaderBadgeFg", "#7DD3FC");
                break;

            case "SteelBlue":
            default:
                SetBrush("WindowBackground", "#0F141C");
                SetBrush("CardBackground", "#161E2E");
                SetBrush("CardBorder", "#263449");
                SetBrush("AccentColor", "#2563EB");
                SetBrush("AccentHover", "#1D4ED8");
                SetBrush("SuccessColor", "#10B981");
                SetBrush("WarningColor", "#F59E0B");
                SetBrush("DangerColor", "#EF4444");
                SetBrush("SubTextColor", "#94A3B8");
                SetBrush("InputBackground", "#0B111A");
                SetBrush("HeaderBadgeBg", "#1E293B");
                SetBrush("HeaderBadgeFg", "#93C5FD");
                break;
        }
    }
}
