using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingNameComposer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TSD = Tekla.Structures.Drawing;
using DragDrop = System.Windows.DragDrop;
using Tekla.Structures.Drawing;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Tekla.Structures.ModelInternal;

[assembly: InternalsVisibleTo("ServiceTests")]
namespace DrawingNameComposer;

public partial class MainViewModel(
	IMetadataService metadataService,
	IPrintingService printingService,
	IPresetsService presetsService) : ObservableObject
{
	internal void Initialize()
	{
		if (presetsService.FileExists())
		{
			var loaded = presetsService.LoadFromFile();
			if (loaded != null && loaded.Any())
			{
				Presets = [.. loaded];
				SelectedPreset = Presets[0];
			}
		}
		else
		{
			presetsService.SaveToFile(Presets);
		}
		AvailableProperties.AddRange(metadataService.Get());
		if (SelectedPreset != null)
		{
			ChosenProperties.AddRange(SelectedPreset.ChosenProperties);
		}
		PrintSettings.AddRange(printingService.Get());
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
		}
	}


	public List<string> PrintSettings { get; } = [];
	public ObservableCollection<Preset> Presets { get; private set; } = [];
	public ExtendedObservableCollection<string> AvailableProperties { get; } = [];
	public ExtendedObservableCollection<string> ChosenProperties { get; } = [];

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
			existingPresetInList.ChosenProperties = ChosenProperties;
			existingPresetInList.AvailableProperties = AvailableProperties;
			existingPresetInList.PrintSetting = SelectedPrintSetting;
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
			ChosenProperties = ChosenProperties,
			AvailableProperties = AvailableProperties,
			PrintSetting = SelectedPrintSetting
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
			ChosenProperties.Clear();
			ChosenProperties.AddRange(existingPresetInList.ChosenProperties);
			AvailableProperties.Clear();
			AvailableProperties.AddRange(AvailableProperties);
			SelectedPrintSetting = PrintSettings.FirstOrDefault(x => x == existingPresetInList.PrintSetting) ?? "";
		}
	}

	[RelayCommand]
	private void OnPrint()
	{
	}
}
