using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Blasting_Layout.Models;

public sealed class BlastHole : INotifyPropertyChanged
{
    public const double MarkerDiameter = 14;

    private string _holeId;
    private double _xPosition;
    private double _yPosition;
    private string _rowName;
    private int _holeNumber;
    private double _depth;
    private double _burden;
    private double _spacing;
    private bool _isSelected;

    public BlastHole(
        string holeId,
        string rowName,
        int holeNumber,
        double xPosition,
        double yPosition,
        bool isAutoNamedRow = false)
    {
        _holeId = holeId;
        _rowName = rowName;
        _holeNumber = holeNumber;
        _xPosition = xPosition;
        _yPosition = yPosition;
        IsAutoNamedRow = isAutoNamedRow;
    }

    public bool IsAutoNamedRow { get; }

    public string HoleId
    {
        get => _holeId;
        set => SetField(ref _holeId, value);
    }

    public double XPosition
    {
        get => _xPosition;
        set
        {
            if (SetField(ref _xPosition, value))
            {
                OnPropertyChanged(nameof(CenterX));
            }
        }
    }

    public double YPosition
    {
        get => _yPosition;
        set
        {
            if (SetField(ref _yPosition, value))
            {
                OnPropertyChanged(nameof(CenterY));
            }
        }
    }

    public string RowName
    {
        get => _rowName;
        set => SetField(ref _rowName, value);
    }

    public int HoleNumber
    {
        get => _holeNumber;
        set => SetField(ref _holeNumber, value);
    }

    public double Depth
    {
        get => _depth;
        set => SetField(ref _depth, value);
    }

    public double Burden
    {
        get => _burden;
        set => SetField(ref _burden, value);
    }

    public double Spacing
    {
        get => _spacing;
        set => SetField(ref _spacing, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public double CenterX => XPosition + MarkerDiameter / 2;

    public double CenterY => YPosition + MarkerDiameter / 2;

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
}
