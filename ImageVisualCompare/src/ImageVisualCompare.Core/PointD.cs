namespace ImageVisualCompare.Core;

/// <summary>
/// A point in either screen space or content space. Declared here rather than reusing
/// <c>System.Windows.Point</c> so the zoom/pan maths carries no UI framework dependency
/// and stays testable without a WPF host.
/// </summary>
public readonly record struct PointD(double X, double Y);
