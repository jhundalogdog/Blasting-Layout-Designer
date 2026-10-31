using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Blasting_Layout.Models;
using Blasting_Layout.Services;

namespace Blasting_Layout.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly BlastLayoutService _layoutService = new();
    private readonly BlastConnectionPathService _connectionPathService = new();
    private BlastHole? _selectedHole;
    private BlastHole? _connectorStartHole;
    private BlastPattern _pattern = BlastPattern.Staggered;
    private StaggerDirection _staggerDirection = StaggerDirection.Right;
    private RowInsertionDirection _rowInsertionDirection = RowInsertionDirection.Below;
    private string _rowNameInput = string.Empty;
    private string _holeCountInput = "5";
    private string _statusMessage = "Ready. Add a row to begin designing the blast layout.";
    private bool _isDrawingConnector;
    private bool _isGridVisible = true;
    private bool _isToolboxVisible = true;
    private bool _isHolePropertiesVisible = true;
    private double _zoomScale = 1;
    private double _canvasWidth = BlastLayoutService.InitialCanvasExtent;
    private double _canvasHeight = BlastLayoutService.InitialCanvasExtent;
    private int _nextRowIndex;
    private string _selectedConnectorColor = "#F2C94C";
    private LayoutSnapshot? _pendingEditSnapshot;
    private IReadOnlyList<CopiedHole>? _copiedHoles;
    private int _pasteCount;
    private string? _selectedConnectionChainId;

    public MainViewModel()
    {
        Holes.CollectionChanged += OnHolesChanged;
        Connectors.CollectionChanged += OnConnectorsChanged;
        SelectedHoles.CollectionChanged += OnSelectedHolesChanged;
        SelectedConnectors.CollectionChanged += OnSelectedConnectorsChanged;

        AddRowCommand = new RelayCommand(AddRow);
        DeleteSelectedHoleCommand = new RelayCommand(DeleteSelectedHole, () => SelectedHoles.Count > 0 || SelectedHole is not null);
        ClearLayoutCommand = new RelayCommand(ClearLayout);
        ZoomInCommand = new RelayCommand(() => ZoomScale = Math.Min(3, ZoomScale + 0.1));
        ZoomOutCommand = new RelayCommand(() => ZoomScale = Math.Max(0.4, ZoomScale - 0.1));
        DrawConnectorCommand = new RelayCommand(ToggleConnectorDrawing);
        ToggleGridCommand = new RelayCommand(() => IsGridVisible = !IsGridVisible);
        ToggleToolboxCommand = new RelayCommand(() => IsToolboxVisible = !IsToolboxVisible);
        ToggleHolePropertiesCommand = new RelayCommand(() => IsHolePropertiesVisible = !IsHolePropertiesVisible);
        DeleteSelectedConnectionSegmentsCommand = new RelayCommand(DeleteSelectedConnectionSegments, () => SelectedConnectors.Count > 0);
        DeleteSelectedConnectionChainCommand = new RelayCommand(DeleteSelectedConnectionChain, () => SelectedConnectionChainId is not null);
        UndoCommand = new RelayCommand(Undo, () => _undoStack.Count > 0 || _pendingEditSnapshot is not null);
    }

    private readonly Stack<LayoutSnapshot> _undoStack = new();

    public ObservableCollection<BlastHole> Holes { get; } = [];

    public ObservableCollection<BlastConnector> Connectors { get; } = [];

    public ObservableCollection<BlastHole> SelectedHoles { get; } = [];

    public ObservableCollection<BlastConnector> SelectedConnectors { get; } = [];

    public ICommand AddRowCommand { get; }

    public ICommand DeleteSelectedHoleCommand { get; }

    public ICommand ClearLayoutCommand { get; }

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    public ICommand DrawConnectorCommand { get; }

    public ICommand ToggleGridCommand { get; }

    public ICommand ToggleToolboxCommand { get; }

    public ICommand ToggleHolePropertiesCommand { get; }

    public ICommand DeleteSelectedConnectionSegmentsCommand { get; }

    public ICommand DeleteSelectedConnectionChainCommand { get; }

    public ICommand UndoCommand { get; }

    public IReadOnlyList<ConnectorColorOption> ConnectorColors { get; } =
    [
        new("Green", "#26966F"),
        new("Blue", "#2472E8"),
        new("Red", "#D64545"),
        new("Yellow", "#F2C94C"),
        new("Orange", "#E07A2D"),
        new("Purple", "#8756C5")
    ];

    public IReadOnlyList<StaggerDirectionOption> StaggerDirections { get; } =
    [
        new("Shift Right", StaggerDirection.Right),
        new("Shift Left", StaggerDirection.Left),
        new("Shift Alternately Start on Right", StaggerDirection.AlternateRight),
        new("Shift Alternately Start on Left", StaggerDirection.AlternateLeft)
    ];

    public IReadOnlyList<RowInsertionDirectionOption> RowInsertionDirections { get; } =
    [
        new("Add Rows Below", RowInsertionDirection.Below),
        new("Add Rows Above", RowInsertionDirection.Above)
    ];

    public RowInsertionDirection SelectedRowInsertionDirection
    {
        get => _rowInsertionDirection;
        set
        {
            if (_rowInsertionDirection == value)
            {
                return;
            }

            PushUndoSnapshot();
            _rowInsertionDirection = value;
            OnPropertyChanged();
        }
    }

    public StaggerDirection SelectedStaggerDirection
    {
        get => _staggerDirection;
        set
        {
            if (_staggerDirection == value)
            {
                return;
            }

            PushUndoSnapshot();
            _staggerDirection = value;
            OnPropertyChanged();
        }
    }

    public BlastHole? SelectedHole
    {
        get => _selectedHole;
        set
        {
            if (_selectedHole == value)
            {
                return;
            }

            _selectedHole = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedHoleId));
            OnPropertyChanged(nameof(HasSelectedHole));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string SelectedHoleId
    {
        get => SelectedHole?.HoleId ?? string.Empty;
        set
        {
            if (SelectedHole is not { } hole)
            {
                return;
            }

            var holeId = value.Trim();
            if (string.IsNullOrWhiteSpace(holeId))
            {
                StatusMessage = "Hole ID cannot be empty.";
                OnPropertyChanged();
                return;
            }

            if (Holes.Any(existing =>
                    existing != hole &&
                    string.Equals(existing.HoleId, holeId, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = $"A hole named {holeId} already exists.";
                OnPropertyChanged();
                return;
            }

            if (hole.HoleId == holeId)
            {
                return;
            }

            hole.HoleId = holeId;
            StatusMessage = $"Renamed hole to {holeId}.";
            OnPropertyChanged();
        }
    }

    public bool HasSelectedHole => SelectedHole is not null;

    public bool CanUndo => _undoStack.Count > 0 || _pendingEditSnapshot is not null;

    public void SelectConnector(BlastConnector connector, bool selectOnlyThisSegment)
    {
        if (selectOnlyThisSegment)
        {
            var deselectCurrentSegment =
                SelectedConnectors.Count == 1 &&
                SelectedConnectors.Contains(connector);
            SelectedConnectors.Clear();
            if (!deselectCurrentSegment)
            {
                SelectedConnectors.Add(connector);
            }

            SelectedConnectionChainId = connector.ChainId;
        }
        else
        {
            SelectedConnectors.Clear();
            foreach (var chainSegment in Connectors.Where(item => item.ChainId == connector.ChainId))
            {
                SelectedConnectors.Add(chainSegment);
            }

            SelectedConnectionChainId = connector.ChainId;
        }

        StatusMessage = $"{SelectedConnectors.Count} connection segment(s) selected.";
    }

    public void ClearConnectorSelection()
    {
        SelectedConnectors.Clear();
        SelectedConnectionChainId = null;
    }

    public bool CopySelectedHoles()
    {
        if (SelectedHoles.Count == 0)
        {
            return false;
        }

        _copiedHoles = SelectedHoles
            .Select(hole => new CopiedHole(
                hole.HoleId,
                hole.RowName,
                hole.HoleNumber,
                hole.XPosition,
                hole.YPosition,
                hole.Depth,
                hole.Burden,
                hole.Spacing,
                hole.IsAutoNamedRow))
            .ToArray();
        _pasteCount = 0;
        StatusMessage = $"Copied {_copiedHoles.Count} hole(s).";
        return true;
    }

    public bool PasteCopiedHoles((double X, double Y)? pasteLocation = null)
    {
        if (_copiedHoles is not { Count: > 0 } copiedHoles)
        {
            return false;
        }

        PushUndoSnapshot();
        var offsetX = pasteLocation is { } location
            ? _layoutService.SnapHolePositionToGrid(location.X) - copiedHoles.Min(hole => hole.XPosition)
            : BlastLayoutService.GridSpacing * ++_pasteCount;
        var offsetY = pasteLocation is { } target
            ? _layoutService.SnapHolePositionToGrid(target.Y) - copiedHoles.Min(hole => hole.YPosition)
            : 0;
        var pastedHoles = copiedHoles.Select(copiedHole =>
        {
            var hole = new BlastHole(
                CreateUniqueHoleId(copiedHole.HoleId),
                copiedHole.RowName,
                copiedHole.HoleNumber,
                copiedHole.XPosition + offsetX,
                copiedHole.YPosition + offsetY,
                copiedHole.IsAutoNamedRow)
            {
                Depth = copiedHole.Depth,
                Burden = copiedHole.Burden,
                Spacing = copiedHole.Spacing
            };
            return hole;
        }).ToArray();

        SelectedHoles.Clear();
        foreach (var hole in pastedHoles)
        {
            Holes.Add(hole);
            SelectedHoles.Add(hole);
        }

        SelectedHole = pastedHoles[^1];
        StatusMessage = $"Pasted {pastedHoles.Length} hole(s).";
        return true;
    }

    public string SelectedConnectorColor
    {
        get => _selectedConnectorColor;
        set => SetField(ref _selectedConnectorColor, value);
    }

    public string RowNameInput
    {
        get => _rowNameInput;
        set => SetField(ref _rowNameInput, value);
    }

    public string HoleCountInput
    {
        get => _holeCountInput;
        set => SetField(ref _holeCountInput, value);
    }

    public bool IsSquarePattern
    {
        get => _pattern == BlastPattern.Square;
        set
        {
            if (value)
            {
                SetPattern(BlastPattern.Square);
            }
        }
    }

    public bool IsStaggeredPattern
    {
        get => _pattern == BlastPattern.Staggered;
        set
        {
            if (value)
            {
                SetPattern(BlastPattern.Staggered);
            }
        }
    }

    public bool IsDrawingConnector
    {
        get => _isDrawingConnector;
        private set
        {
            if (SetField(ref _isDrawingConnector, value))
            {
                OnPropertyChanged(nameof(ConnectorButtonText));
            }
        }
    }

    public string ConnectorButtonText => IsDrawingConnector ? "Stop Drawing Connector" : "Draw Connector";

    public bool IsGridVisible
    {
        get => _isGridVisible;
        private set
        {
            if (SetField(ref _isGridVisible, value))
            {
                OnPropertyChanged(nameof(GridButtonText));
            }
        }
    }

    public string GridButtonText => IsGridVisible ? "Grid: On" : "Grid: Off";

    public bool IsToolboxVisible
    {
        get => _isToolboxVisible;
        private set
        {
            if (SetField(ref _isToolboxVisible, value))
            {
                OnPropertyChanged(nameof(ToolboxToggleText));
            }
        }
    }

    public string ToolboxToggleText => IsToolboxVisible ? "Hide Toolbox" : "Show Toolbox";

    public bool IsHolePropertiesVisible
    {
        get => _isHolePropertiesVisible;
        private set
        {
            if (SetField(ref _isHolePropertiesVisible, value))
            {
                OnPropertyChanged(nameof(HolePropertiesToggleText));
            }
        }
    }

    public string HolePropertiesToggleText => IsHolePropertiesVisible ? "Hide Hole Properties" : "Show Hole Properties";

    public string? SelectedConnectionChainId
    {
        get => _selectedConnectionChainId;
        private set
        {
            if (SetField(ref _selectedConnectionChainId, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public int TotalHoles => Holes.Count;

    public int TotalConnectors => Connectors.Count;

    public double ZoomScale
    {
        get => _zoomScale;
        private set
        {
            if (SetField(ref _zoomScale, value))
            {
                StatusMessage = $"Zoom: {ZoomScale:P0}";
            }
        }
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        private set => SetField(ref _canvasWidth, value);
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        private set => SetField(ref _canvasHeight, value);
    }

    public bool HandleHoleClick(BlastHole hole, bool toggleSelection = false)
    {
        if (IsDrawingConnector)
        {
            SelectedHole = hole;
            CompleteConnectorSelection(hole);
            return true;
        }

        if (toggleSelection)
        {
            if (SelectedHoles.Contains(hole))
            {
                SelectedHoles.Remove(hole);
                if (SelectedHole == hole)
                {
                    SelectedHole = SelectedHoles.LastOrDefault();
                }
            }
            else
            {
                SelectedHoles.Add(hole);
                SelectedHole = hole;
            }
        }
        else
        {
            SelectedHoles.Clear();
            SelectedHoles.Add(hole);
            SelectedHole = hole;
        }

        return false;
    }

    public void SelectHolesInRectangle(double left, double top, double right, double bottom, bool addToSelection)
    {
        var holesInside = Holes
            .Where(hole => hole.CenterX >= left && hole.CenterX <= right &&
                           hole.CenterY >= top && hole.CenterY <= bottom)
            .ToArray();

        if (!addToSelection)
        {
            SelectedHoles.Clear();
        }

        foreach (var hole in holesInside)
        {
            if (!SelectedHoles.Contains(hole))
            {
                SelectedHoles.Add(hole);
            }
        }

        SelectedHole = holesInside.LastOrDefault() ?? SelectedHoles.LastOrDefault();
        StatusMessage = $"{SelectedHoles.Count} hole(s) selected.";
    }

    private void CompleteConnectorSelection(BlastHole hole)
    {
        if (_connectorStartHole is null)
        {
            _connectorStartHole = hole;
            StatusMessage = $"Start hole {hole.HoleId} selected. Select an end hole.";
            return;
        }

        if (_connectorStartHole == hole)
        {
            StatusMessage = "Select a different hole to complete the connector.";
            return;
        }

        if (!_connectionPathService.TryFindStraightPath(Holes, _connectorStartHole, hole, out var path))
        {
            StatusMessage = "Invalid connection path. Selected holes do not follow a valid blast layout alignment.";
            return;
        }

        PushUndoSnapshot();
        var chainId = Guid.NewGuid().ToString("N");
        for (var index = 1; index < path.Count; index++)
        {
            Connectors.Add(new BlastConnector(
                path[index - 1],
                path[index],
                color: SelectedConnectorColor,
                chainId: chainId));
        }

        SelectedConnectors.Clear();
        foreach (var connector in Connectors.Where(item => item.ChainId == chainId))
        {
            SelectedConnectors.Add(connector);
        }

        SelectedConnectionChainId = chainId;
        _connectorStartHole = null;
        StatusMessage = $"Connection chain added with {path.Count - 1} segment(s). Select a start hole for the next chain or stop drawing.";
    }

    public void MoveHole(BlastHole hole, double x, double y)
    {
        hole.XPosition = _layoutService.SnapHolePositionToGrid(x);
        hole.YPosition = _layoutService.SnapHolePositionToGrid(y);
        UpdateCanvasSize();
    }

    public void MoveHoles(IReadOnlyList<(BlastHole Hole, double X, double Y)> startingPositions, double deltaX, double deltaY)
    {
        foreach (var (hole, x, y) in startingPositions)
        {
            hole.XPosition = _layoutService.SnapHolePositionToGrid(x + deltaX);
            hole.YPosition = _layoutService.SnapHolePositionToGrid(y + deltaY);
        }

        UpdateCanvasSize();
    }

    public void BeginMoveHole() => BeginUndoableEdit();

    public void CompleteMoveHole() => CompleteUndoableEdit();

    public void BeginPropertyEdit() => BeginUndoableEdit();

    public void CompletePropertyEdit() => CompleteUndoableEdit();

    public void ZoomBy(double amount) =>
        ZoomScale = Math.Clamp(ZoomScale + amount, 0.4, 3);

    public void CancelConnectorDrawing()
    {
        if (!IsDrawingConnector)
        {
            return;
        }

        IsDrawingConnector = false;
        _connectorStartHole = null;
        StatusMessage = "Connector drawing cancelled.";
    }

    private void AddRow()
    {
        var rowName = RowNameInput.Trim();
        var isAutoNamedRow = string.IsNullOrWhiteSpace(rowName);
        if (isAutoNamedRow)
        {
            rowName = GetNextDefaultRowName();
        }

        if (!int.TryParse(HoleCountInput, out var holeCount) || holeCount <= 0)
        {
            StatusMessage = "Hole count must be a positive whole number.";
            return;
        }

        if (Holes.Any(hole => string.Equals(hole.RowName, rowName, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"A row named {rowName} already exists.";
            return;
        }

        PushUndoSnapshot();
        var existingRows = GetRowsInTopToBottomOrder();
        var rowIndex = _rowInsertionDirection == RowInsertionDirection.Above
            ? 0
            : existingRows.Count;
        var rowY = _rowInsertionDirection switch
        {
            RowInsertionDirection.Above when existingRows.Count > 0 =>
                existingRows[0].Holes.Min(hole => hole.YPosition),
            RowInsertionDirection.Below when existingRows.Count > 0 =>
                existingRows[^1].Holes.Max(hole => hole.YPosition) + BlastLayoutService.RowSpacing,
            _ => BlastLayoutService.InitialPlotCenter - BlastHole.MarkerDiameter / 2
        };

        if (_rowInsertionDirection == RowInsertionDirection.Above && existingRows.Count > 0)
        {
            for (var index = 0; index < existingRows.Count; index++)
            {
                var existingRow = existingRows[index];
                var oldOffset = _layoutService.GetStaggerOffset(index, _pattern, SelectedStaggerDirection);
                var newOffset = _layoutService.GetStaggerOffset(index + 1, _pattern, SelectedStaggerDirection);
                foreach (var hole in existingRow.Holes)
                {
                    hole.XPosition += newOffset - oldOffset;
                    hole.YPosition += BlastLayoutService.RowSpacing;
                }
            }
        }

        var addedRowHoles = new List<BlastHole>(holeCount);
        for (var index = 0; index < holeCount; index++)
        {
            var position = _layoutService.GetHolePosition(
                rowIndex,
                index,
                _pattern,
                SelectedStaggerDirection);
            var holeNumber = index + 1;
            var hole = new BlastHole(
                $"{rowName}{holeNumber}",
                rowName,
                holeNumber,
                position.X,
                rowY,
                isAutoNamedRow);
            Holes.Add(hole);
            addedRowHoles.Add(hole);
        }

        _nextRowIndex++;
        if (_rowInsertionDirection == RowInsertionDirection.Above && isAutoNamedRow)
        {
            RenumberAutoNamedRows(GetRowsInTopToBottomOrder());
        }

        rowName = addedRowHoles[0].RowName;
        var insertionDescription = _rowInsertionDirection == RowInsertionDirection.Above
            ? "above the existing rows"
            : "below the existing rows";
        StatusMessage = $"Added row {rowName} {insertionDescription} with {holeCount} holes.";
    }

    private List<HoleRow> GetRowsInTopToBottomOrder() =>
        Holes
            .GroupBy(hole => (hole.RowName, RowY: Math.Round(hole.YPosition, 3)))
            .Select(group => new HoleRow(group.Key.RowName, group.ToArray()))
            .OrderBy(row => row.Holes.Min(hole => hole.YPosition))
            .ThenBy(row => row.Holes.Min(hole => hole.XPosition))
            .ToList();

    private void RenumberAutoNamedRows(IReadOnlyList<HoleRow> rows)
    {
        var customRowNames = rows
            .Where(row => !row.IsAutoNamed)
            .Select(row => row.RowName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextLabelIndex = 0;
        var labelsByRow = new Dictionary<HoleRow, string>();

        foreach (var row in rows.Reverse().Where(row => row.IsAutoNamed))
        {
            string label;
            do
            {
                label = ToExcelColumnName(nextLabelIndex++);
            }
            while (customRowNames.Contains(label));

            labelsByRow.Add(row, label);
        }

        var changedIds = labelsByRow
            .SelectMany(pair => pair.Key.Holes
                .Where(hole => hole.HoleId == $"{pair.Key.RowName}{hole.HoleNumber}")
                .Select(hole => (Hole: hole, Label: pair.Value)))
            .ToArray();
        foreach (var (hole, _) in changedIds)
        {
            hole.HoleId = $"__row_insert_{Guid.NewGuid():N}";
        }

        foreach (var (row, label) in labelsByRow)
        {
            foreach (var hole in row.Holes)
            {
                hole.RowName = label;
            }
        }

        foreach (var (hole, label) in changedIds)
        {
            hole.HoleId = $"{label}{hole.HoleNumber}";
        }

        if (changedIds.Any(item => item.Hole == SelectedHole))
        {
            OnPropertyChanged(nameof(SelectedHoleId));
        }
    }

    private string GetNextDefaultRowName()
    {
        var existingRowNames = Holes
            .Select(hole => hole.RowName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; ; index++)
        {
            var rowName = ToExcelColumnName(index);
            if (!existingRowNames.Contains(rowName))
            {
                return rowName;
            }
        }
    }

    private static string ToExcelColumnName(int index)
    {
        var name = string.Empty;
        var value = (long)index + 1;
        while (value > 0)
        {
            value--;
            name = (char)('A' + value % 26) + name;
            value /= 26;
        }

        return name;
    }

    private void DeleteSelectedHole()
    {
        var holesToDelete = SelectedHoles.ToHashSet();
        if (holesToDelete.Count == 0 && SelectedHole is { } selectedHole)
        {
            holesToDelete.Add(selectedHole);
        }

        if (holesToDelete.Count == 0)
        {
            return;
        }

        PushUndoSnapshot();
        RemoveConnectors(Connectors
            .Where(connector => holesToDelete.Contains(connector.StartHole) || holesToDelete.Contains(connector.EndHole))
            .ToArray());

        foreach (var hole in holesToDelete)
        {
            Holes.Remove(hole);
        }

        SelectedHoles.Clear();
        SelectedHole = null;
        StatusMessage = holesToDelete.Count == 1
            ? $"Deleted hole {holesToDelete.Single().HoleId}."
            : $"Deleted {holesToDelete.Count} selected holes.";
    }

    private void DeleteSelectedConnectionSegments()
    {
        var connectorsToDelete = SelectedConnectors.ToArray();
        if (connectorsToDelete.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        RemoveConnectors(connectorsToDelete);
        StatusMessage = connectorsToDelete.Length == 1
            ? "Deleted selected connection segment."
            : $"Deleted {connectorsToDelete.Length} selected connection segments.";
    }

    private void DeleteSelectedConnectionChain()
    {
        if (SelectedConnectionChainId is not { } chainId)
        {
            return;
        }

        var connectorsToDelete = Connectors
            .Where(connector => connector.ChainId == chainId)
            .ToArray();
        if (connectorsToDelete.Length == 0)
        {
            SelectedConnectionChainId = null;
            return;
        }

        PushUndoSnapshot();
        RemoveConnectors(connectorsToDelete);
        StatusMessage = $"Deleted connection chain with {connectorsToDelete.Length} segment(s).";
    }

    private void RemoveConnectors(IEnumerable<BlastConnector> connectorsToDelete)
    {
        foreach (var connector in connectorsToDelete)
        {
            connector.Detach();
            Connectors.Remove(connector);
            SelectedConnectors.Remove(connector);
        }

        if (SelectedConnectionChainId is { } selectedChainId &&
            !Connectors.Any(connector => connector.ChainId == selectedChainId))
        {
            SelectedConnectionChainId = null;
        }
    }

    private string CreateUniqueHoleId(string originalId)
    {
        var holeIds = Holes.Select(hole => hole.HoleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copyId = $"{originalId} copy";
        if (!holeIds.Contains(copyId))
        {
            return copyId;
        }

        for (var copyNumber = 2; ; copyNumber++)
        {
            var numberedCopyId = $"{originalId} copy {copyNumber}";
            if (!holeIds.Contains(numberedCopyId))
            {
                return numberedCopyId;
            }
        }
    }

    private void ClearLayout()
    {
        if (Holes.Count == 0 && Connectors.Count == 0)
        {
            return;
        }

        PushUndoSnapshot();
        Holes.Clear();
        RemoveConnectors(Connectors.ToArray());
        SelectedHoles.Clear();
        SelectedHole = null;
        _nextRowIndex = 0;
        _connectorStartHole = null;
        IsDrawingConnector = false;
        UpdateCanvasSize();
        StatusMessage = "Layout cleared.";
    }

    private void ToggleConnectorDrawing()
    {
        IsDrawingConnector = !IsDrawingConnector;
        _connectorStartHole = null;
        StatusMessage = IsDrawingConnector
            ? "Select the first hole for the connector."
            : "Connector drawing stopped.";
    }

    private void SetPattern(BlastPattern pattern)
    {
        if (_pattern == pattern)
        {
            return;
        }

        PushUndoSnapshot();
        _pattern = pattern;
        OnPropertyChanged(nameof(IsSquarePattern));
        OnPropertyChanged(nameof(IsStaggeredPattern));
    }

    private void OnHolesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TotalHoles));
        UpdateCanvasSize();
    }

    private void OnConnectorsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(TotalConnectors));

    private void OnSelectedHolesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var hole in Holes)
        {
            hole.IsSelected = SelectedHoles.Contains(hole);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private void OnSelectedConnectorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var connector in Connectors)
        {
            connector.IsSelected = SelectedConnectors.Contains(connector);
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private void PushUndoSnapshot()
    {
        _undoStack.Push(CreateSnapshot());
        OnPropertyChanged(nameof(CanUndo));
        CommandManager.InvalidateRequerySuggested();
    }

    private void BeginUndoableEdit()
    {
        if (_pendingEditSnapshot is null)
        {
            _pendingEditSnapshot = CreateSnapshot();
            OnPropertyChanged(nameof(CanUndo));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void CompleteUndoableEdit()
    {
        if (_pendingEditSnapshot is not { } snapshot)
        {
            return;
        }

        _pendingEditSnapshot = null;
        if (!SnapshotsMatch(snapshot, CreateSnapshot()))
        {
            _undoStack.Push(snapshot);
        }

        OnPropertyChanged(nameof(CanUndo));
        CommandManager.InvalidateRequerySuggested();
    }

    private void Undo()
    {
        CompleteUndoableEdit();
        if (_undoStack.Count == 0)
        {
            return;
        }

        var snapshot = _undoStack.Pop();
        foreach (var connector in Connectors)
        {
            connector.Detach();
        }

        Holes.Clear();
        SelectedConnectors.Clear();
        Connectors.Clear();
        SelectedConnectionChainId = null;
        SelectedHoles.Clear();
        foreach (var savedHole in snapshot.Holes)
        {
            Holes.Add(new BlastHole(
                savedHole.HoleId,
                savedHole.RowName,
                savedHole.HoleNumber,
                savedHole.XPosition,
                savedHole.YPosition,
                savedHole.IsAutoNamedRow)
            {
                Depth = savedHole.Depth,
                Burden = savedHole.Burden,
                Spacing = savedHole.Spacing
            });
        }

        var holesById = Holes.ToDictionary(hole => hole.HoleId);
        foreach (var savedConnector in snapshot.Connectors)
        {
            Connectors.Add(new BlastConnector(
                holesById[savedConnector.StartHoleId],
                holesById[savedConnector.EndHoleId],
                savedConnector.DelayType,
                savedConnector.Color,
                savedConnector.ChainId,
                savedConnector.ConnectorId));
        }

        _pattern = snapshot.Pattern;
        _staggerDirection = snapshot.StaggerDirection;
        _rowInsertionDirection = snapshot.RowInsertionDirection;
        _nextRowIndex = snapshot.NextRowIndex;
        SelectedHole = snapshot.SelectedHoleId is not null
            ? Holes.FirstOrDefault(hole => hole.HoleId == snapshot.SelectedHoleId)
            : null;
        foreach (var holeId in snapshot.SelectedHoleIds)
        {
            var hole = Holes.FirstOrDefault(candidate => candidate.HoleId == holeId);
            if (hole is not null)
            {
                SelectedHoles.Add(hole);
            }
        }
        foreach (var connectorId in snapshot.SelectedConnectorIds)
        {
            var connector = Connectors.FirstOrDefault(candidate => candidate.ConnectorId == connectorId);
            if (connector is not null)
            {
                SelectedConnectors.Add(connector);
            }
        }
        SelectedConnectionChainId = snapshot.SelectedConnectionChainId;
        OnPropertyChanged(nameof(IsSquarePattern));
        OnPropertyChanged(nameof(IsStaggeredPattern));
        OnPropertyChanged(nameof(SelectedStaggerDirection));
        OnPropertyChanged(nameof(SelectedRowInsertionDirection));
        UpdateCanvasSize();
        StatusMessage = "Last layout change undone.";
        OnPropertyChanged(nameof(CanUndo));
        CommandManager.InvalidateRequerySuggested();
    }

    private LayoutSnapshot CreateSnapshot() =>
        new(
            Holes.Select(hole => new HoleSnapshot(
                hole.HoleId,
                hole.RowName,
                hole.HoleNumber,
                hole.XPosition,
                hole.YPosition,
                hole.Depth,
                hole.Burden,
                hole.Spacing,
                hole.IsAutoNamedRow)).ToList(),
            Connectors.Select(connector => new ConnectorSnapshot(
                connector.ConnectorId,
                connector.ChainId,
                connector.StartHole.HoleId,
                connector.EndHole.HoleId,
                connector.DelayType,
                connector.Color)).ToList(),
            _pattern,
            _staggerDirection,
            _nextRowIndex,
            SelectedHole?.HoleId,
            SelectedHoles.Select(hole => hole.HoleId).ToList(),
            SelectedConnectors.Select(connector => connector.ConnectorId).ToList(),
            SelectedConnectionChainId,
            _rowInsertionDirection);

    private static bool SnapshotsMatch(LayoutSnapshot first, LayoutSnapshot second) =>
        first.Pattern == second.Pattern &&
        first.StaggerDirection == second.StaggerDirection &&
        first.RowInsertionDirection == second.RowInsertionDirection &&
        first.NextRowIndex == second.NextRowIndex &&
        first.SelectedHoleId == second.SelectedHoleId &&
        first.SelectedHoleIds.SequenceEqual(second.SelectedHoleIds) &&
        first.SelectedConnectorIds.SequenceEqual(second.SelectedConnectorIds) &&
        first.SelectedConnectionChainId == second.SelectedConnectionChainId &&
        first.Holes.SequenceEqual(second.Holes) &&
        first.Connectors.SequenceEqual(second.Connectors);

    private void UpdateCanvasSize()
    {
        CanvasWidth = Math.Max(
            BlastLayoutService.InitialCanvasExtent,
            Holes.Count == 0 ? BlastLayoutService.InitialCanvasExtent : Holes.Max(hole => hole.XPosition) + 250);
        CanvasHeight = Math.Max(
            BlastLayoutService.InitialCanvasExtent,
            Holes.Count == 0 ? BlastLayoutService.InitialCanvasExtent : Holes.Max(hole => hole.YPosition) + 200);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed record HoleSnapshot(
        string HoleId,
        string RowName,
        int HoleNumber,
        double XPosition,
        double YPosition,
        double Depth,
        double Burden,
        double Spacing,
        bool IsAutoNamedRow);

    private sealed record ConnectorSnapshot(
        string ConnectorId,
        string ChainId,
        string StartHoleId,
        string EndHoleId,
        string DelayType,
        string Color);

    private sealed record LayoutSnapshot(
        List<HoleSnapshot> Holes,
        List<ConnectorSnapshot> Connectors,
        BlastPattern Pattern,
        StaggerDirection StaggerDirection,
        int NextRowIndex,
        string? SelectedHoleId,
        List<string> SelectedHoleIds,
        List<string> SelectedConnectorIds,
        string? SelectedConnectionChainId,
        RowInsertionDirection RowInsertionDirection);

    private sealed record CopiedHole(
        string HoleId,
        string RowName,
        int HoleNumber,
        double XPosition,
        double YPosition,
        double Depth,
        double Burden,
        double Spacing,
        bool IsAutoNamedRow);

    private sealed record HoleRow(string RowName, BlastHole[] Holes)
    {
        public bool IsAutoNamed => Holes.All(hole => hole.IsAutoNamedRow);
    }
}

public sealed record ConnectorColorOption(string Name, string Hex);

public sealed record StaggerDirectionOption(string Name, StaggerDirection Value);

public sealed record RowInsertionDirectionOption(string Name, RowInsertionDirection Value);
