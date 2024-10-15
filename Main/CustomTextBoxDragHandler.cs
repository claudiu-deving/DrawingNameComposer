using GongSolutions.Wpf.DragDrop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace DrawingNameComposer;

public class CustomTextBoxDragHandler : IDropTarget
{
	private readonly TextBox _textBox;
	private AdornerLayer? _adornerLayer;
	private InsertionAdorner? _insertionAdorner;

	public CustomTextBoxDragHandler(TextBox textBox)
	{
		_textBox = textBox;
		_textBox.Loaded += (s, e) => InitializeAdornerLayer();

		// If the TextBox is already loaded, initialize immediately
		if (_textBox.IsLoaded)
		{
			InitializeAdornerLayer();
		}
	}

	private void InitializeAdornerLayer()
	{
		_adornerLayer = AdornerLayer.GetAdornerLayer(_textBox);
		if (_adornerLayer == null)
		{
			Console.WriteLine("Warning: AdornerLayer is still null after TextBox is loaded.");
		}
	}
	private int GetInsertionIndex(Point dropPosition)
	{
		if (string.IsNullOrEmpty(_textBox.Text))
		{
			return 0;
		}

		int charIndex = _textBox.GetCharacterIndexFromPoint(dropPosition, true);

		if (charIndex >= _textBox.Text.Length - 1)
		{
			Rect lastCharRect = _textBox.GetRectFromCharacterIndex(_textBox.Text.Length - 1);
			if (dropPosition.X > lastCharRect.Right)
			{
				return _textBox.Text.Length;
			}
		}

		return charIndex;
	}
	private void UpdateInsertionAdorner(Point dropPosition)
	{
		if (_adornerLayer is null) return;
		int charIndex = GetInsertionIndex(dropPosition);

		if (_insertionAdorner == null)
		{
			_insertionAdorner = new InsertionAdorner(_textBox, charIndex);
			_adornerLayer.Add(_insertionAdorner);
		}
		else
		{
			_insertionAdorner.UpdatePosition(charIndex);
		}
	}

	public void DragOver(IDropInfo dropInfo)
	{
		dropInfo.Effects = DragDropEffects.Copy;
		UpdateInsertionAdorner(dropInfo.DropPosition);
	}

	public void Drop(IDropInfo dropInfo)
	{
		if (dropInfo.Data is string droppedText)
		{
			int insertionIndex = GetInsertionIndex(dropInfo.DropPosition);
			_textBox.Text = _textBox.Text.Insert(insertionIndex == -1 ? 0 : insertionIndex, $"%{droppedText}%");
			_textBox.SelectionStart = insertionIndex + droppedText.Length;
			_textBox.CaretIndex = insertionIndex + droppedText.Length + 2;
			_textBox.Focus();
		}

		RemoveAdorner();
	}

	public void DragEnter(IDropInfo dropInfo)
	{
		UpdateInsertionAdorner(dropInfo.DropPosition);
	}

	public void DragLeave(IDropInfo dropInfo)
	{
		RemoveAdorner();
	}

	private void RemoveAdorner()
	{
		if (_adornerLayer is null) return;
		if (_insertionAdorner != null)
		{
			_adornerLayer.Remove(_insertionAdorner);
			_insertionAdorner = null;
		}
	}
}
