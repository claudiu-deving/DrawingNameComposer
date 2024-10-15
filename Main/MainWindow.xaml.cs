using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DrawingNameComposer
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow(MainViewModel mainViewModel)
		{
			InitializeComponent();
			DataContext = mainViewModel;
			GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(template, new CustomTextBoxDragHandler(template));
		}


		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Environment.Exit(0);
		}

		private void Window_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.F1)
			{
				new Process() { StartInfo = new ProcessStartInfo() { FileName = "https://www.bitluz.com", UseShellExecute = true } }.Start();
			}
		}
	}
}