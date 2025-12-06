namespace VisualSearch.Api.Data.Entities;

/// <summary>
/// Represents a configurable application setting stored in the database.
/// Settings are cached in memory and can be invalidated via SSE broadcast.
/// </summary>
public sealed class Setting
{
    /// <summary>
    /// Gets or sets the unique key for the setting (e.g., "search.maxImageSize", "ui.siteName").
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the value of the setting as a string.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Gets or sets the data type of the setting for proper parsing.
    /// </summary>
    public SettingType Type { get; set; } = SettingType.String;

    /// <summary>
    /// Gets or sets a description of what this setting controls.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category/group for organizing settings in the admin panel.
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Gets or sets when this setting was last modified.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Defines the supported data types for settings.
/// </summary>
public enum SettingType
{
    /// <summary>String value.</summary>
    String = 0,

    /// <summary>Integer value.</summary>
    Integer = 1,

    /// <summary>Boolean value.</summary>
    Boolean = 2,

    /// <summary>Decimal/floating-point value.</summary>
    Decimal = 3
}
