
namespace DrawingNameComposer.Services
{
	public interface IPrintingSettingsService
	{
		IEnumerable<string> GetNames();
		Dictionary<string, string> GetSettings();
	}
}