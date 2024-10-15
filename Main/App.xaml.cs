using DrawingNameComposer.Services;
using System.Configuration;
using System.Data;
using System.Windows;

namespace DrawingNameComposer
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			var printingSettingsService = new PrintingSettingsService();
			var mainViewModel = new MainViewModel(new MetadataService(), printingSettingsService, new PresetsService(new AppPathsProvider()), new PrintingService(printingSettingsService));
			mainViewModel.Initialize();
			var mainWindow = new MainWindow(mainViewModel);
			mainWindow.Show();
		}
	}

}
