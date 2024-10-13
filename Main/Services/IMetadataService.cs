namespace DrawingNameComposer.Services;

public interface IMetadataService
{
	public Task<IEnumerable<string>?> Get();
}