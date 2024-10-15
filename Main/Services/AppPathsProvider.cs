using System.IO;

namespace DrawingNameComposer.Services;

public class AppPathsProvider : IAppPathsProvider
{
	public string LocalAppDataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitLuz");
	public string AppFolderPath { get; }

	public AppPathsProvider()
	{
		AppFolderPath = Path.Combine(LocalAppDataDirectory, "drawing_names_composer");
	}
}
