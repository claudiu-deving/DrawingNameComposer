using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Tekla.Structures.Model;

namespace DrawingNameComposer.Services;

public class MetadataService : IMetadataService
{
	public IEnumerable<string> Get()
	{
		var modelPath = new Model().GetInfo().ModelPath;
		var docManagerFile = Directory.GetFiles(modelPath, $"DocumentManagerDataGridSettings_{Environment.UserName}.xml").FirstOrDefault();
		docManagerFile ??= Directory.GetFiles(modelPath, $"DocumentManagerDataGridSettings.xml").FirstOrDefault();
		if (!File.Exists(docManagerFile))
		{
			throw new Exception("No Document Manager Data Grid Setttings found, please open the Document Manager for the file 'DocumentManagerDataGridSettings.xml' to appear in model folder");
		}
		var content = File.ReadAllText(docManagerFile);
		return ExtractColumnNames(content);
	}

	static List<string> ExtractColumnNames(string xmlString)
	{
		List<string> columnNames = new List<string>();

		XDocument doc = XDocument.Parse(xmlString);
		var columns = doc.Descendants("DataGridColumn");

		foreach (var column in columns)
		{
			var nameElement = column.Element("Name");
			if (nameElement != null)
			{
				columnNames.Add(nameElement.Value);
			}
		}

		return columnNames;
	}
}
