using System.Collections.ObjectModel;

namespace DrawingNameComposer.Services
{
	public interface IPresetsService
	{
		bool FileExists();
		IEnumerable<Preset>? LoadFromFile();
		void SaveToFile(ObservableCollection<Preset> presets);
	}
}