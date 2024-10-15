using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;

namespace DrawingNameComposer.Services
{
	public class PresetsService : IPresetsService
	{
		public PresetsService(IAppPathsProvider appPathsProvider)
		{
			_appPathsProvider = appPathsProvider;
			if (!Directory.Exists(_appPathsProvider.AppFolderPath))
			{
				Directory.CreateDirectory(_appPathsProvider.AppFolderPath);
			}
			_presetsFilePath = Path.Combine(_appPathsProvider.AppFolderPath, "presets.json");
		}

		private readonly string _presetsFilePath = string.Empty;
		private readonly IAppPathsProvider _appPathsProvider;

		public void SaveToFile(ObservableCollection<Preset> presets)
		{
			var json = JsonConvert.SerializeObject(presets);
			File.WriteAllText(_presetsFilePath, json);
		}

		public IEnumerable<Preset>? LoadFromFile()
		{
			var content = File.ReadAllText(_presetsFilePath);
			return JsonConvert.DeserializeObject<IEnumerable<Preset>>(content);
		}

		public bool FileExists()
		{
			return File.Exists(_presetsFilePath);
		}
	}
}
