using System.Diagnostics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using static RegionToShareEx.NativeMethods;

namespace RegionToShareEx;

/// <summary>
/// GPU-accelerated screen region capture based on Windows.Graphics.Capture (WGC).
///
/// WGC can only capture a whole monitor or window, so we capture the monitor that contains
/// the requested region and copy just the region sub-rectangle into a small staging texture,
/// which is then read back to system memory and handed to the consumer.
/// </summary>
internal sealed class ScreenCapture : IDisposable
{
    /// <summary>Invoked on a capture thread while the frame data pointer is valid. Copy synchronously.</summary>
    public delegate void FrameHandler(IntPtr data, int stride, int width, int height);

    private readonly FrameHandler _onFrame;
    private readonly bool _captureCursor;
    private readonly long _minFrameTicks;
    private readonly object _sync = new();

    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly IDirect3DDevice _winRtDevice;

    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;

    private ID3D11Texture2D? _staging;
    private int _stagingWidth;
    private int _stagingHeight;

    private IntPtr _monitor;
    private RECT _monitorRect;
    private RECT _sourceRect;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastFrameTicks;
    private bool _disposed;

    public ScreenCapture(FrameHandler onFrame, bool captureCursor, int framesPerSecond)
    {
        _onFrame = onFrame;
        _captureCursor = captureCursor;
        _minFrameTicks = Stopwatch.Frequency / Math.Max(1, framesPerSecond);

        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            null,
            out _device!).CheckError();

        _context = _device.ImmediateContext;

        using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
        _winRtDevice = Direct3D11Interop.CreateDirect3DDevice(dxgiDevice.NativePointer);
    }

    /// <summary>The region to capture, in physical screen pixels. Setting it (re)targets the monitor if needed.</summary>
    public RECT SourceRect
    {
        set
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _sourceRect = value;
                EnsureMonitor(value);
            }
        }
    }

    private void EnsureMonitor(RECT rect)
    {
        var monitor = MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);
        if (monitor == _monitor && _framePool != null)
            return;

        _monitor = monitor;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref info);
        _monitorRect = info.rcMonitor;

        StartCapture();
    }

    private void StartCapture()
    {
        DisposeSession();

        _item = Direct3D11Interop.CreateItemForMonitor(_monitor);

        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _winRtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            _item.Size);

        _session = _framePool.CreateCaptureSession(_item);

        // IsCursorCaptureEnabled is available since Windows 10 2004 (our minimum).
        Try(() => _session.IsCursorCaptureEnabled = _captureCursor);

        // Hiding the yellow capture border is only possible on Windows 11+.
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            Try(() => _session.IsBorderRequired = false);

        _framePool.FrameArrived += OnFrameArrived;
        _session.StartCapture();
    }

    private static void Try(Action action)
    {
        try { action(); } catch { /* property not supported on this OS build */ }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            using var frame = sender.TryGetNextFrame();
            if (frame is null)
                return;

            var now = _clock.ElapsedTicks;
            if (now - _lastFrameTicks < _minFrameTicks)
                return; // frame drained but skipped to honor the target frame rate

            _lastFrameTicks = now;
            ProcessFrame(frame);
        }
    }

    private void ProcessFrame(Direct3D11CaptureFrame frame)
    {
        var contentSize = frame.ContentSize;

        var left = _sourceRect.Left - _monitorRect.Left;
        var top = _sourceRect.Top - _monitorRect.Top;
        var width = _sourceRect.Width;
        var height = _sourceRect.Height;

        // Clamp the region to the actual captured monitor surface.
        if (left < 0) { width += left; left = 0; }
        if (top < 0) { height += top; top = 0; }
        if (left + width > contentSize.Width) width = contentSize.Width - left;
        if (top + height > contentSize.Height) height = contentSize.Height - top;
        if (width <= 0 || height <= 0)
            return;

        EnsureStaging(width, height);

        using var sourceTexture = Direct3D11Interop.GetTexture2D(frame.Surface);
        var box = new Box(left, top, 0, left + width, top + height, 1);
        _context.CopySubresourceRegion(_staging!, 0, 0, 0, 0, sourceTexture, 0, box);

        var map = _context.Map(_staging!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            _onFrame(map.DataPointer, (int)map.RowPitch, width, height);
        }
        finally
        {
            _context.Unmap(_staging!, 0);
        }
    }

    private void EnsureStaging(int width, int height)
    {
        if (_staging != null && _stagingWidth == width && _stagingHeight == height)
            return;

        _staging?.Dispose();
        _staging = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None
        });
        _stagingWidth = width;
        _stagingHeight = height;
    }

    private void DisposeSession()
    {
        if (_framePool != null)
            _framePool.FrameArrived -= OnFrameArrived;

        _session?.Dispose();
        _session = null;
        _framePool?.Dispose();
        _framePool = null;
        _item = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            DisposeSession();
            _staging?.Dispose();
            _winRtDevice.Dispose();
            _context.Dispose();
            _device.Dispose();
        }
    }
}
