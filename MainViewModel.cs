using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawingNameComposer
{
	public class MainViewModel : ObservableObject
	{
		public List<string> PrintSettings { get; } = ["CCS", "III", "AA"];
	}
}
