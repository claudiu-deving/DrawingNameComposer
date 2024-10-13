using DrawingNameComposer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DrawingNameComposerTests
{
	/*
	public class PresetsService
{
	public PresetsService(AppPathsProvider appPathsProvider)
	{
		_appPathsProvider = appPathsProvider;
		_presetsFilePath = Path.Combine(_appPathsProvider.AppFolderPath, "presets.json");
		if (!Directory.Exists(_appPathsProvider.AppFolderPath))
		{
			Directory.CreateDirectory(_appPathsProvider.AppFolderPath);
		}
	}

	private readonly string _presetsFilePath = string.Empty;
	private readonly AppPathsProvider _appPathsProvider;

	internal void SaveToFile(ObservableCollection<Preset> presets)
	{
		var json = JsonConvert.SerializeObject(presets);
		File.WriteAllText(_presetsFilePath, json);
	}

	internal IEnumerable<Preset>? LoadFromFile()
	{
		var content = File.ReadAllText(_presetsFilePath);
		return JsonConvert.DeserializeObject<IEnumerable<Preset>>(content);
	}

	internal bool FileExists()
	{
		return File.Exists(_presetsFilePath);
	}
}
	*/
	[TestClass]
	public class PresetsServiceTests
	{
		private readonly string _presetsPath;
		private PresetsService _sut;

		public PresetsServiceTests()
		{
			var mockedPathsProvider = new Mock<IAppPathsProvider>();
			mockedPathsProvider.Setup(p => p.AppFolderPath).Returns(Path.Combine(Environment.CurrentDirectory, "local"));
			_presetsPath = Path.Combine(Environment.CurrentDirectory, "local", "presets.json");
			_sut = new PresetsService(mockedPathsProvider.Object);
		}
		[TestMethod]
		public void TestSaveToFile()
		{
			var presets = new ObservableCollection<Preset>()
			{
				new()
				{
					Name = "Test",
					AvailableProperties = ["X"],
					ChosenProperties = ["Y"]
				}
			};
			_sut.SaveToFile(presets);
			Assert.IsTrue(_sut.FileExists());
			File.Delete(_presetsPath);
		}

		[TestMethod]
		public void TestLoadFromFile()
		{
			var presets = new ObservableCollection<Preset>()
			{
				new()
				{
					Name = "Test",
					AvailableProperties = ["X"],
					ChosenProperties = ["Y"]
				}
			};
			_sut.SaveToFile(presets);
			var result = _sut.LoadFromFile();
			Assert.IsTrue(result.Any());
			File.Delete(_presetsPath);
		}
	}
}
