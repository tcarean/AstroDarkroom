using System.Windows;
using System.Windows.Media;
using MindfulnessReminder.Core;
using Key = System.Windows.Input.Key;
using Point = System.Windows.Point;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Screen = System.Windows.Forms.Screen;
using WinFormsControl = System.Windows.Forms.Control;

namespace MindfulnessReminder.App;

public partial class BreakWindow : Window
{
    public BreakWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        MoveToActiveScreen();

        // Windows only grants foreground activation to a process with recent input, and a
        // freshly unlocked desktop has none. Re-asserting Topmost after the handle exists is
        // what reliably puts the window in front of whatever was left open before the lock.
        Topmost = true;
        Activate();
        CloseButton.Focus();
    }

    /// <summary>
    /// Puts the reminder on the monitor holding the mouse pointer -- the best available guess
    /// at where the user is actually looking after unlocking.
    /// </summary>
    private void MoveToActiveScreen()
    {
        if (PresentationSource.FromVisual(this) is not { } source)
        {
            return;
        }

        // Screen reports physical pixels; WPF positions in device-independent units.
        var toDip = source.CompositionTarget.TransformFromDevice;
        var bounds = Screen.FromPoint(WinFormsControl.MousePosition).WorkingArea;

        var origin = toDip.Transform(new Point(bounds.Left, bounds.Top));
        var size = toDip.Transform(new Point(bounds.Width, bounds.Height));

        var (left, top) = WindowPlacement.CenterIn(
            new WorkArea(origin.X, origin.Y, size.X, size.Y),
            Width,
            Height);

        Left = left;
        Top = top;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
