using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DrawingNameComposer;

public class InsertionAdorner(TextBox textBox, int insertionIndex) : Adorner(textBox)
{
	public void UpdatePosition(int newIndex)
	{
		if (insertionIndex != newIndex)
		{
			insertionIndex = newIndex;
			InvalidateVisual();
		}
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);

		Rect characterRect = textBox.GetRectFromCharacterIndex(insertionIndex == -1 ? 0 : insertionIndex);
		Pen pen = new(Brushes.Black, 2);
		drawingContext.DrawLine(pen, new Point(characterRect.Left, characterRect.Top), new Point(characterRect.Left, characterRect.Bottom));
	}
}
