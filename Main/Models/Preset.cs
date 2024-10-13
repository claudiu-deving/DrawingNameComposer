using System.Collections.ObjectModel;

namespace DrawingNameComposer.Services
{
	public class Preset
	{
		public string Name { get; set; } = string.Empty;
		public ObservableCollection<string> ChosenProperties { get; set; } = [];
		public ObservableCollection<string> AvailableProperties { get; set; } = [];
		public string PrintSetting { get; set; } = string.Empty;
	}
}
