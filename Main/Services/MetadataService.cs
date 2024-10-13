using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawingNameComposer.Services;

public class MetadataService : IMetadataService
{
	public async Task<IEnumerable<string>?> Get()
	{
		return [];
	}
}
