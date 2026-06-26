using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static RegionToShareEx.NativeMethods;
using Image = System.Windows.Controls.Image;

namespace RegionToShareEx;

/// <summary>
/// Interaction logic for RecordingWindow.xaml
/// </summary>
public partial class RecordingWindow
{
    public static readonly Thickness BorderSize = new(4);

    private readonly MainWindow _mainWindow;
    private readonly Image _renderTarget;
    private readonly POINT _debugOffset;
    private readonly ScreenCapture _capture;
    private readonly DispatcherThrottle _present;

    private HwndTarget? _compositionTarget;
    private RECT _nativeMainWindowRect;
    private IntPtr _windowHandle;

    // Frame hand-off from the capture thread to the UI thread.
    private readonly object _frameLock = new();
    private byte[] _frameBuffer = [];
    private int _frameWidth;
    private int _frameHeight;
    private int _frameStride;
    private bool _frameDirty;
    private WriteableBitmap? _bitmap;

    public RecordingWindow(Image renderTarget, bool drawShadowCursor, int framesPerSecond, POINT debugOffset)
    {
        InitializeComponent();

        _mainWindow = (MainWindow)GetWindow(renderTarget)!;
        _renderTarget = renderTarget;
        _debugOffset = debugOffset;

        _present = new DispatcherThrottle(DispatcherPriority.Render, PresentFrame);
        _capture = new ScreenCapture(OnFrame, drawShadowCursor, framesPerSecond);
    }

    public void UpdateSizeAndPos(RECT mainWindowRect)
    {
        _nativeMainWindowRect = mainWindowRect;
        NativeWindowRect = _nativeMainWindowRect + NativeBorderSize;
        _capture.SourceRect = _nativeMainWindowRect - _debugOffset;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        var hwndSource = (HwndSource?)PresentationSource.FromDependencyObject(this);
        if (hwndSource == null)
            return;

        hwndSource.AddHook(WindowProc);

        _compositionTarget = hwndSource.CompositionTarget;
        _windowHandle = hwndSource.Handle;

        var rect = _mainWindow.NativeWindowRect + NativeBorderSize;

        NativeWindowRect = rect;
        _nativeMainWindowRect = _mainWindow.NativeWindowRect;
        _capture.SourceRect = _nativeMainWindowRect - _debugOffset;

        this.BeginInvoke(OnSizeOrPositionChanged);

        base.OnSourceInitialized(e);
    }

    private Transformations DeviceTransformations => _compositionTarget.GetDeviceTransformations();

    private RECT NativeWindowRect
    {
        get
        {
            GetWindowRect(_windowHandle, out var rect);
            return rect;
        }
        set
        {
            if (_windowHandle == IntPtr.Zero)
                return;

            SetWindowPos(_windowHandle, IntPtr.Zero, value.Left, value.Top, value.Width, value.Height, SWP_NOACTIVATE | SWP_NOZORDER);
        }
    }

    private Thickness NativeBorderSize => DeviceTransformations.ToDevice.Transform(BorderSize);

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == WindowStateProperty)
        {
            this.BeginInvoke(DispatcherPriority.Background, () =>
            {
                WindowState = WindowState.Normal;
                OnSizeOrPositionChanged();
            });

            return;
        }

        if (e.Property != LeftProperty
            && e.Property != TopProperty
            && e.Property != ActualWidthProperty
            && e.Property != ActualHeightProperty)
            return;

        this.BeginInvoke(DispatcherPriority.Background, OnSizeOrPositionChanged);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _capture.Dispose();
    }

    private void OnSizeOrPositionChanged()
    {
        if (!IsLoaded)
            return;

        _mainWindow.NativeWindowRect = _nativeMainWindowRect = NativeWindowRect - NativeBorderSize;
        _capture.SourceRect = _nativeMainWindowRect - _debugOffset;
    }

    /// <summary>Called on a capture thread; the data pointer is only valid for the duration of the call.</summary>
    private void OnFrame(IntPtr data, int stride, int width, int height)
    {
        lock (_frameLock)
        {
            var size = stride * height;
            if (_frameBuffer.Length < size)
                _frameBuffer = new byte[size];

            Marshal.Copy(data, _frameBuffer, 0, size);
            _frameWidth = width;
            _frameHeight = height;
            _frameStride = stride;
            _frameDirty = true;
        }

        _present.Tick();
    }

    private void PresentFrame()
    {
        lock (_frameLock)
        {
            if (!_frameDirty)
                return;

            _frameDirty = false;

            if (_bitmap == null || _bitmap.PixelWidth != _frameWidth || _bitmap.PixelHeight != _frameHeight)
            {
                _bitmap = new WriteableBitmap(_frameWidth, _frameHeight, 96.0, 96.0, PixelFormats.Bgra32, null);
                _renderTarget.Source = _bitmap;
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, _frameWidth, _frameHeight), _frameBuffer, _frameStride, 0);
        }
    }

    private IntPtr WindowProc(IntPtr windowHandle, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                handled = true;
                return (IntPtr)NcHitTest(windowHandle, lParam);
        }

        return IntPtr.Zero;
    }

    private HitTest NcHitTest(IntPtr windowHandle, IntPtr lParam)
    {
        if (WindowState.Normal != WindowState)
            return HitTest.Client;

        if ((ResizeMode != ResizeMode.CanResize) && ResizeMode != ResizeMode.CanResizeWithGrip)
            return HitTest.Client;

        // Arguments are absolute native coordinates
        var hitPoint = new POINT((short)lParam, (short)((uint)lParam >> 16));

        GetWindowRect(windowHandle, out var windowRect);

        var topLeft = windowRect.TopLeft;
        var bottomRight = windowRect.BottomRight;

        var transformations = DeviceTransformations;

        var borderSize = transformations.ToDevice.Transform(BorderSize);

        var clientPoint = transformations.FromDevice.Transform(hitPoint - topLeft);

        if (InputHitTest(clientPoint) is FrameworkElement element)
        {
            if (element.AncestorsAndSelf().OfType<ButtonBase>().Any())
            {
                return HitTest.Client;
            }
        }

        var left = topLeft.X;
        var top = topLeft.Y;
        var right = bottomRight.X;
        var bottom = bottomRight.Y;

        if ((hitPoint.Y < top) || (hitPoint.Y > bottom) || (hitPoint.X < left) || (hitPoint.X > right))
            return HitTest.Transparent;

        if ((hitPoint.Y < (top + borderSize.Top)) && (hitPoint.X < (left + borderSize.Left)))
            return HitTest.TopLeft;
        if ((hitPoint.Y < (top + borderSize.Top)) && (hitPoint.X > (right - borderSize.Right)))
            return HitTest.TopRight;
        if ((hitPoint.Y > (bottom - borderSize.Bottom)) && (hitPoint.X < (left + borderSize.Left)))
            return HitTest.BottomLeft;
        if ((hitPoint.Y > (bottom - borderSize.Bottom)) && (hitPoint.X > (right - borderSize.Right)))
            return HitTest.BottomRight;
        if (hitPoint.Y < (top + borderSize.Top))
            return HitTest.Caption;
        if (hitPoint.Y > (bottom - borderSize.Bottom))
            return HitTest.Bottom;
        if (hitPoint.X < (left + borderSize.Left))
            return HitTest.Left;
        if (hitPoint.X > (right - borderSize.Right))
            return HitTest.Right;

        return HitTest.Client;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
