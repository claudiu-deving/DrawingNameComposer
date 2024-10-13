using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tekla.Structures.Drawing;

namespace DrawingNameComposer
{
	internal static class Helpers
	{
		internal static string ComposeResult(string initial)
		{
			var selectedDrawings = new DrawingHandler().GetDrawingSelector().GetSelected();
			selectedDrawings.MoveNext();
			var currentDrawing = selectedDrawings.Current;
			return ComposeResult(currentDrawing, initial);
		}
		private static Regex _regex = new("(?<=%)([a-zA-Z0-9]+(?:_[a-zA-Z0-9]+)*)(?=%)");

		internal static string ComposeResult(Drawing currentDrawing, string initial)
		{
			var result = initial;

			if (currentDrawing != null)
			{
				var matches = _regex.Matches(initial);
				foreach (Match match in matches)
				{
					if (!match.Success) continue;
					var propertyName = match.Value;
					if (propertyName.StartsWith("UDA_"))
					{
						propertyName = propertyName.Replace("UDA_", "");
					}
					var placeholder = $"%{match.Value}%";
					string replacementValue = GetPropertyValue(currentDrawing, propertyName);
					if (replacementValue is string strValue &&
						DateTime.TryParseExact(strValue, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
					{
						replacementValue = parsedDate.ToString("dd/MM/yyyy");
					}

					result = result.Replace(placeholder, replacementValue);
				}
			}
			else
			{
				result = "Select at least one drawing to see how it will look like.";
			}

			return result;
		}
		internal static string GetPropertyValue(object obj, string propertyName)
		{
			PropertyInfo propertyInfo = obj.GetType().GetProperty(propertyName);

			if (propertyInfo != null)
			{
				// Regular property
				return propertyInfo.GetValue(obj)?.ToString() ?? string.Empty;
			}
			else
			{
				// Try to get user property
				string value = string.Empty;
				if (obj is Drawing drawing)
				{
					drawing.GetUserProperty(propertyName, ref value);
				}
				return value;
			}
		}

	}
}
