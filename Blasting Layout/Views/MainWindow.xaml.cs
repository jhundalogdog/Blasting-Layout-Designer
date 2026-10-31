using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using Blasting_Layout.Models;
using Blasting_Layout.Services;
using Blasting_Layout.ViewModels;

namespace Blasting_Layout.Views;

public partial class MainWindow : Window
{
    private IReadOnlyList<(BlastHole Hole, double X, double Y)>? _draggedHoles;
    private Point _dragStartPointer;
    private Point _lastPanPoint;
    private bool _isPanning;
    private bool _initialPlotAreaCentered;
    private bool _isSelecting;
    private bool _addToSelection;
    private bool _canSetPasteLocation;
    private bool _focusHoleIdAfterClick;
    private bool _holeWasDragged;
    private Point _selectionStart;
    private Point? _pasteLocation;
    private readonly Rectangle _selectionBox = new()
    {
        Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(48, 36, 114, 232)),
        Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 114, 232)),
        StrokeThickness = 1,
        StrokeDashArray = [4, 2],
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed
    };

    public MainWindow()
    {
        InitializeComponent();
        Canvas.SetZIndex(_selectionBox, int.MaxValue);
        BlastCanvas.Children.Add(_selectionBox);
    }

    private void Hole_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: BlastHole hole } border ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        _focusHoleIdAfterClick = false;
        _holeWasDragged = false;
        var toggleSelection = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (viewModel.IsDrawingConnector ||
            toggleSelection ||
            !viewModel.SelectedHoles.Contains(hole))
        {
            if (viewModel.HandleHoleClick(hole, toggleSelection))
            {
                e.Handled = true;
                return;
            }
        }

        if (!viewModel.SelectedHoles.Contains(hole))
        {
            e.Handled = true;
            return;
        }

        viewModel.BeginMoveHole();
        _draggedHoles = viewModel.SelectedHoles
            .Select(selected => (selected, selected.XPosition, selected.YPosition))
            .ToArray();
        _dragStartPointer = e.GetPosition(BlastCanvas);
        _focusHoleIdAfterClick = true;
        border.CaptureMouse();
        e.Handled = true;
    }

    private void Hole_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedHoles is null ||
            e.LeftButton != MouseButtonState.Pressed ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var pointer = e.GetPosition(BlastCanvas);
        var zoomScale = DataContext is MainViewModel vm ? vm.ZoomScale : 1;
        if (Math.Abs(pointer.X - _dragStartPointer.X) >
                SystemParameters.MinimumHorizontalDragDistance / zoomScale ||
            Math.Abs(pointer.Y - _dragStartPointer.Y) >
                SystemParameters.MinimumVerticalDragDistance / zoomScale)
        {
            _holeWasDragged = true;
        }

        viewModel.MoveHoles(_draggedHoles, pointer.X - _dragStartPointer.X, pointer.Y - _dragStartPointer.Y);
        e.Handled = true;
    }

    private void Hole_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        border.ReleaseMouseCapture();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CompleteMoveHole();
        }

        _draggedHoles = null;
        if (_focusHoleIdAfterClick && !_holeWasDragged)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                HoleIdTextBox.Focus();
                HoleIdTextBox.SelectAll();
            }));
        }

        _focusHoleIdAfterClick = false;
        _holeWasDragged = false;
        e.Handled = true;
    }

    private void Connector_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: BlastConnector connector } ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.SelectConnector(
            connector,
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
        Keyboard.Focus(BlastCanvas);
        e.Handled = true;
    }

    private void PropertyEditor_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.BeginPropertyEdit();
        }
    }

    private void PropertyEditor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CompletePropertyEdit();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
        {
            if (viewModel.UndoCommand.CanExecute(null))
            {
                viewModel.UndoCommand.Execute(null);
                e.Handled = true;
            }

            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control &&
            Keyboard.FocusedElement is not (TextBoxBase or ComboBox))
        {
            if (e.Key == Key.C && viewModel.CopySelectedHoles())
            {
                e.Handled = true;
            }
            else if (e.Key == Key.V)
            {
                var pasteLocation = _pasteLocation;
                if (viewModel.PasteCopiedHoles(
                        pasteLocation is { } location ? (location.X, location.Y) : null))
                {
                    _pasteLocation = null;
                    e.Handled = true;
                }
            }

            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
        {
            viewModel.CancelConnectorDrawing();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None &&
            e.Key == Key.C &&
            Keyboard.FocusedElement is not (TextBoxBase or ComboBox))
        {
            viewModel.DrawConnectorCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Canvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var pointer = e.GetPosition(CanvasScrollViewer);
        var oldScale = viewModel.ZoomScale;
        viewModel.ZoomBy(e.Delta > 0 ? 0.1 : -0.1);
        var newScale = viewModel.ZoomScale;

        if (Math.Abs(newScale - oldScale) > double.Epsilon)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                var scaleRatio = newScale / oldScale;
                CanvasScrollViewer.ScrollToHorizontalOffset(
                    (CanvasScrollViewer.HorizontalOffset + pointer.X) * scaleRatio - pointer.X);
                CanvasScrollViewer.ScrollToVerticalOffset(
                    (CanvasScrollViewer.VerticalOffset + pointer.Y) * scaleRatio - pointer.Y);
            }));
        }

        e.Handled = true;
    }

    private void CanvasScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialPlotAreaCentered)
        {
            return;
        }

        _initialPlotAreaCentered = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var scale = DataContext is MainViewModel viewModel ? viewModel.ZoomScale : 1;
            CanvasScrollViewer.ScrollToHorizontalOffset(
                BlastLayoutService.InitialPlotCenter * scale - CanvasScrollViewer.ViewportWidth / 2);
            CanvasScrollViewer.ScrollToVerticalOffset(
                BlastLayoutService.InitialPlotCenter * scale - CanvasScrollViewer.ViewportHeight / 2);
        }));
    }

    private void RecenterCanvas_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyCollection<BlastHole> holes = DataContext is MainViewModel viewModel
            ? viewModel.Holes
            : Array.Empty<BlastHole>();
        var centerX = holes.Count > 0
            ? (holes.Min(hole => hole.CenterX) + holes.Max(hole => hole.CenterX)) / 2
            : BlastLayoutService.InitialPlotCenter;
        var centerY = holes.Count > 0
            ? (holes.Min(hole => hole.CenterY) + holes.Max(hole => hole.CenterY)) / 2
            : BlastLayoutService.InitialPlotCenter;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var scale = DataContext is MainViewModel vm ? vm.ZoomScale : 1;
            CanvasScrollViewer.ScrollToHorizontalOffset(
                centerX * scale - CanvasScrollViewer.ViewportWidth / 2);
            CanvasScrollViewer.ScrollToVerticalOffset(
                centerY * scale - CanvasScrollViewer.ViewportHeight / 2);
        }));
    }

    private void Canvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left &&
            !IsScrollBarElement(e.OriginalSource as DependencyObject))
        {
            Keyboard.Focus(BlastCanvas);
        }

        if (e.ChangedButton == MouseButton.Left &&
            DataContext is MainViewModel &&
            !IsHoleElement(e.OriginalSource as DependencyObject) &&
            !IsConnectorElement(e.OriginalSource as DependencyObject) &&
            !IsScrollBarElement(e.OriginalSource as DependencyObject))
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ClearConnectorSelection();
            }

            _canSetPasteLocation = true;
            _pasteLocation = null;
            _isSelecting = true;
            _addToSelection = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            _selectionStart = e.GetPosition(BlastCanvas);
            UpdateSelectionBox(_selectionStart);
            CanvasScrollViewer.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = true;
        _lastPanPoint = e.GetPosition(CanvasScrollViewer);
        CanvasScrollViewer.Cursor = Cursors.SizeAll;
        CanvasScrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void Canvas_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isSelecting)
        {
            var selectionPointer = e.GetPosition(BlastCanvas);
            if (Math.Abs(selectionPointer.X - _selectionStart.X) >
                    SystemParameters.MinimumHorizontalDragDistance / (DataContext is MainViewModel viewModel ? viewModel.ZoomScale : 1) ||
                Math.Abs(selectionPointer.Y - _selectionStart.Y) >
                    SystemParameters.MinimumVerticalDragDistance / (DataContext is MainViewModel vm ? vm.ZoomScale : 1))
            {
                _canSetPasteLocation = false;
            }

            UpdateSelectionBox(selectionPointer);
            e.Handled = true;
            return;
        }

        if (!_isPanning)
        {
            return;
        }

        var current = e.GetPosition(CanvasScrollViewer);
        var delta = current - _lastPanPoint;
        CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset - delta.X);
        CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset - delta.Y);
        _lastPanPoint = current;
        e.Handled = true;
    }

    private void Canvas_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelecting && e.ChangedButton == MouseButton.Left)
        {
            var selectionEnd = e.GetPosition(BlastCanvas);
            if (_canSetPasteLocation)
            {
                _pasteLocation = selectionEnd;
            }

            _isSelecting = false;
            _canSetPasteLocation = false;
            CanvasScrollViewer.ReleaseMouseCapture();
            _selectionBox.Visibility = Visibility.Collapsed;

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.SelectHolesInRectangle(
                    Math.Min(_selectionStart.X, selectionEnd.X),
                    Math.Min(_selectionStart.Y, selectionEnd.Y),
                    Math.Max(_selectionStart.X, selectionEnd.X),
                    Math.Max(_selectionStart.Y, selectionEnd.Y),
                    _addToSelection);
            }

            e.Handled = true;
            return;
        }

        if (!_isPanning || e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = false;
        CanvasScrollViewer.ReleaseMouseCapture();
        CanvasScrollViewer.Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    private void UpdateSelectionBox(Point current)
    {
        Canvas.SetLeft(_selectionBox, Math.Min(_selectionStart.X, current.X));
        Canvas.SetTop(_selectionBox, Math.Min(_selectionStart.Y, current.Y));
        _selectionBox.Width = Math.Abs(current.X - _selectionStart.X);
        _selectionBox.Height = Math.Abs(current.Y - _selectionStart.Y);
        _selectionBox.Visibility = Visibility.Visible;
    }

    private static bool IsHoleElement(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is FrameworkElement { DataContext: BlastHole })
            {
                return true;
            }

            element = GetParent(element);
        }

        return false;
    }

    private static bool IsConnectorElement(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is FrameworkElement { DataContext: BlastConnector })
            {
                return true;
            }

            element = GetParent(element);
        }

        return false;
    }

    private static bool IsScrollBarElement(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is ScrollBar)
            {
                return true;
            }

            element = GetParent(element);
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject element) =>
        element is Visual or Visual3D
            ? VisualTreeHelper.GetParent(element)
            : LogicalTreeHelper.GetParent(element);
}
