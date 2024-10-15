namespace DrawingNameComposer.Services;

public class Preset
{
	public string Name { get; set; } = string.Empty;
	public List<string> AvailableProperties { get; set; } = [];
	public string PrintSetting { get; set; } = string.Empty;
	public string Template { get; set; } = string.Empty;
}
