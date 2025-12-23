namespace AmmoClarity.Models;

public class ModConfig
{
    public required bool LeadingCaliberName { get; set; }
    public required bool LogAllAmmos { get; set; }
    public required Dictionary<string, string> Calibers { get; set; }
    public required Dictionary<string, string> NameUpdates { get; set; }
    public required string Language { get; set; }
    public required bool STFU { get; set; }
}
