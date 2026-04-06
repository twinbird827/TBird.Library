using SQLite;

namespace LanobeReader.Models;

[Table("app_settings")]
public class AppSetting
{
    [PrimaryKey]
    [Column("key")]
    public string Key { get; set; } = string.Empty;

    [Column("value")]
    public string Value { get; set; } = string.Empty;
}
