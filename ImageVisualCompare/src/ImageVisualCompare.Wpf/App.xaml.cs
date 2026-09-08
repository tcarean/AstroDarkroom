using System.IO;
using System.Windows;
using ImageVisualCompare.Wpf.Services;

namespace ImageVisualCompare.Wpf;

public partial class App : Application
{
    /// <summary>
    /// A pair named on the command line, so the tool can be launched straight from a folder
    /// without walking through both dialogs. Null unless two readable paths were given.
    /// </summary>
    private static ImagePairSelection? _startupSelection;

    /// <summary>Hands over the command line pair, once. Later opens go through the dialogs.</summary>
    public static ImagePairSelection? TakeStartupSelection()
    {
        ImagePairSelection? selection = _startupSelection;
        _startupSelection = null;
        return selection;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Anything less than two existing files falls through to the dialogs rather than
        // failing: a mistyped path is not worth refusing to start over.
        if (e.Args.Length >= 2 && File.Exists(e.Args[0]) && File.Exists(e.Args[1]))
        {
            _startupSelection = new ImagePairSelection(e.Args[0], e.Args[1]);
        }
    }
}
