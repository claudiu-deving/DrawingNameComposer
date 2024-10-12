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
		public MainWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// TitleBar_MouseDown - Drag if single-click, resize if double-click
		/// </summary>
		private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
				if (e.ClickCount == 2)
				{
					AdjustWindowSize();
				}
				else
				{
					Application.Current.MainWindow.DragMove();
				}
		}

		/// <summary>
		/// CloseButton_Clicked
		/// </summary>
		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}

		/// <summary>
		/// MaximizedButton_Clicked
		/// </summary>
		private void MaximizeButton_Click(object sender, RoutedEventArgs e)
		{
			AdjustWindowSize();
		}

		/// <summary>
		/// Minimized Button_Clicked
		/// </summary>
		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			this.WindowState = WindowState.Minimized;
		}

		/// <summary>
		/// Adjusts the WindowSize to correct parameters when Maximize button is clicked
		/// </summary>
		private void AdjustWindowSize()
		{
			if (this.WindowState == WindowState.Maximized)
			{
				this.WindowState = WindowState.Normal;
				MaxButton.Content = "🗖";
				MaxButton.ToolTip = "Maximize";
				caption_wrap.Margin = new Thickness(0, -8, 2, 0);
			}
			else
			{
				this.WindowState = WindowState.Maximized;
				MaxButton.Content = "";
				MaxButton.ToolTip = "Restore";
				caption_wrap.Margin = new Thickness(0, -3, 5, 0);
				caption_wrap.UpdateLayout();
				RootWindow.Margin = new Thickness(10);
			}

		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
			e.Handled = true;
		}
	}
}