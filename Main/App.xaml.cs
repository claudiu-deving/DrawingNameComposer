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
		protected override async void OnStartup(StartupEventArgs e)
		{
			var mainViewModel = new MainViewModel(new MetadataService(), new PrintingService(), new PresetsService(new AppPathsProvider()));
			await mainViewModel.Initialize();
			var mainWindow = new MainWindow(mainViewModel);
			mainWindow.Show();
		}
	}

}
