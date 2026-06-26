using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace RegionToShareEx;

/// <summary>
/// Small self-contained replacements for the handful of TomsToolbox helpers the app relied on.
/// </summary>
internal static class WpfExtensions
{
    public static void BeginInvoke(this DispatcherObject self, Action action)
        => self.Dispatcher.BeginInvoke(DispatcherPriority.Normal, action);

    public static void BeginInvoke(this DispatcherObject self, DispatcherPriority priority, Action action)
        => self.Dispatcher.BeginInvoke(priority, action);

    public static IntPtr GetWindowHandle(this Window window)
        => new WindowInteropHelper(window).Handle;

    public static IEnumerable<DependencyObject> AncestorsAndSelf(this DependencyObject? self)
    {
        for (var node = self; node != null; node = VisualTreeHelper.GetParent(node))
        {
            yield return node;
        }
    }

    public static IEnumerable<Exception> ExceptionChain(this Exception? exception)
    {
        for (var ex = exception; ex != null; ex = ex.InnerException)
        {
            yield return ex;
        }
    }
}

/// <summary>
/// Coalesces repeated <see cref="Tick"/> calls into a single deferred invocation of the target
/// at the given dispatcher priority. Safe to tick from any thread.
/// </summary>
internal sealed class DispatcherThrottle
{
    private readonly DispatcherPriority _priority;
    private readonly Action _target;
    private readonly Dispatcher _dispatcher;
    private int _pending;

    public DispatcherThrottle(Action target)
        : this(DispatcherPriority.Normal, target)
    {
    }

    public DispatcherThrottle(DispatcherPriority priority, Action target)
    {
        _priority = priority;
        _target = target;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public void Tick()
    {
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
            return;

        _dispatcher.BeginInvoke(_priority, new Action(() =>
        {
            Interlocked.Exchange(ref _pending, 0);
            _target();
        }));
    }
}
