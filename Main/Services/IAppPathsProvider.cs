namespace DrawingNameComposer.Services
{
	public interface IAppPathsProvider
	{
		string AppFolderPath { get; }
		string LocalAppDataDirectory { get; }
	}
}