using System.Configuration;

namespace RegionToShareEx.Properties;

/// <summary>
/// User settings, persisted to user.config via the standard local file settings provider.
/// Hand-written replacement for the former designer-generated settings class.
/// </summary>
internal sealed class Settings : ApplicationSettingsBase
{
    public static Settings Default { get; } = (Settings)Synchronized(new Settings());

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string WindowPlacement
    {
        get => (string)this[nameof(WindowPlacement)];
        set => this[nameof(WindowPlacement)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("15")]
    public int FramesPerSecond
    {
        get => (int)this[nameof(FramesPerSecond)];
        set => this[nameof(FramesPerSecond)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("False")]
    public bool DrawShadowCursor
    {
        get => (bool)this[nameof(DrawShadowCursor)];
        set => this[nameof(DrawShadowCursor)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("SteelBlue")]
    public string ThemeColor
    {
        get => (string)this[nameof(ThemeColor)];
        set => this[nameof(ThemeColor)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("False")]
    public bool StartActivated
    {
        get => (bool)this[nameof(StartActivated)];
        set => this[nameof(StartActivated)] = value;
    }
}
