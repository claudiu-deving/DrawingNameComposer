using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingNameComposer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

using System.Windows;

using TSD = Tekla.Structures.Drawing;

[assembly: InternalsVisibleTo("ServiceTests")]
namespace DrawingNameComposer;

public partial class MainViewModel(
	IMetadataService metadataService,
	IPrintingSettingsService printingSettingsService,
	IPresetsService presetsService,
	PrintingService printingService) : ObservableObject
{
	internal void Initialize()
	{
		AvailableProperties.AddRange(metadataService.Get());
		PrintSettings.AddRange(printingSettingsService.GetNames());

		if (presetsService.FileExists())
		{
			var loaded = presetsService.LoadFromFile();
			if (loaded != null && loaded.Any())
			{
				Presets = [.. loaded];
				SelectedPreset = Presets[0];
				SelectedPrintSetting = PrintSettings.First(ps => ps == SelectedPreset.PrintSetting);
			}
		}
		else
		{
			presetsService.SaveToFile(Presets);
		}


		this.PropertyChanged += OnPropertyChanged;
		var drawingEvents = new TSD.UI.Events();
		drawingEvents.DrawingListSelectionChanged += DrawingEvents_SelectionChange;
		drawingEvents.Register();
	}

	private void DrawingEvents_SelectionChange()
	{
		ResultForDrawing = Helpers.ComposeResult(Template);
	}

	private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "Template")
		{
			ResultForDrawing = Helpers.ComposeResult(Template);
			CanPrint = !string.IsNullOrEmpty(Template);
			PrintCommand.NotifyCanExecuteChanged();
		}
	}


	public List<string> PrintSettings { get; } = [];
	public ObservableCollection<Preset> Presets { get; private set; } = [];
	public ExtendedObservableCollection<string> AvailableProperties { get; } = [];

	[ObservableProperty]
	private string _template = string.Empty;

	[ObservableProperty]
	private string _resultForDrawing = string.Empty;

	[ObservableProperty]
	private string _saveAsInput = string.Empty;

	[ObservableProperty]
	private Preset? _selectedPreset;

	[ObservableProperty]
	private string _selectedPrintSetting = Environment.UserName;

	[ObservableProperty]
	private string _statusMessage = string.Empty;

	[ObservableProperty]
	private int _progressValue;

	[ObservableProperty]
	private bool _canPrint = false;


	private readonly PrintingService _printingService = printingService;

	/// <summary>
	/// Replaces in the presets the currently selected preset and the properties chosen
	/// </summary>
	[RelayCommand]
	private void OnSave()
	{
		if (SelectedPreset is null) return;
		var existingPresetInList = Presets.FirstOrDefault(x => x.Name.Equals(SelectedPreset.Name));
		if (existingPresetInList is not null)
		{
			existingPresetInList.AvailableProperties = [.. AvailableProperties];
			existingPresetInList.PrintSetting = SelectedPrintSetting;
			existingPresetInList.Template = Template;
		}
		presetsService.SaveToFile(Presets);
	}


	/// <summary>
	/// Saves a new preset with the given name
	/// </summary>
	[RelayCommand]
	private void OnSaveAs()
	{
		if (string.IsNullOrEmpty(SaveAsInput)) return;
		SaveAsInput = SaveAsInput.Trim();
		Presets.Add(new Preset()
		{
			Name = SaveAsInput,
			AvailableProperties = [.. AvailableProperties],
			PrintSetting = SelectedPrintSetting,
			Template = Template
		});
		presetsService.SaveToFile(Presets);
	}

	[RelayCommand]
	private void OnLoad()
	{
		if (SelectedPreset is null) return;
		var existingPresetInList = Presets.FirstOrDefault(x => x.Name.Equals(SelectedPreset.Name));
		if (existingPresetInList is not null)
		{
			AvailableProperties.Clear();
			AvailableProperties.AddRange(existingPresetInList.AvailableProperties);
			SelectedPrintSetting = PrintSettings.FirstOrDefault(x => x == existingPresetInList.PrintSetting) ?? "";
			Template = existingPresetInList.Template;
		}
	}

	[RelayCommand(CanExecute = "CanPrint")]
	private async Task OnPrint()
	{
		StatusMessage = "Printing started";
		await Task.Run(() => _printingService.PrintSelectedDrawings(Template, SelectedPrintSetting));
		StatusMessage = "Finished printing";
	}
}


