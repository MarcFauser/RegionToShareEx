using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;

namespace RegionToShareEx;

/// <summary>
/// Bridges the WinRT (CsWinRT projected) capture types to the native Direct3D11 objects
/// exposed by Vortice. Both worlds are COM under the hood; we only marshal raw pointers.
/// </summary>
internal static class Direct3D11Interop
{
    private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid Texture2DGuid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] ref Guid iid);
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
        IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
    }

    /// <summary>Wraps a native DXGI device as the WinRT <see cref="IDirect3DDevice"/> the capture API expects.</summary>
    public static IDirect3DDevice CreateDirect3DDevice(IntPtr dxgiDevice)
    {
        Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var graphicsDevice));
        try
        {
            return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevice);
        }
        finally
        {
            Marshal.Release(graphicsDevice);
        }
    }

    /// <summary>Creates a capture item for a monitor handle (HMONITOR).</summary>
    public static GraphicsCaptureItem CreateItemForMonitor(IntPtr monitor)
    {
        var factory = WinRT.ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var interop = factory.AsInterface<IGraphicsCaptureItemInterop>();
        var iid = GraphicsCaptureItemGuid;
        var itemPointer = interop.CreateForMonitor(monitor, ref iid);
        try
        {
            return GraphicsCaptureItem.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    /// <summary>Gets the underlying Direct3D11 texture of a captured frame surface.</summary>
    public static ID3D11Texture2D GetTexture2D(IDirect3DSurface surface)
    {
        var inspectable = WinRT.MarshalInspectable<IDirect3DSurface>.FromManaged(surface);
        try
        {
            var accessGuid = typeof(IDirect3DDxgiInterfaceAccess).GUID;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(inspectable, in accessGuid, out var accessPtr));
            try
            {
                var access = (IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(accessPtr);
                var textureGuid = Texture2DGuid;
                var texturePtr = access.GetInterface(ref textureGuid);
                return new ID3D11Texture2D(texturePtr);
            }
            finally
            {
                Marshal.Release(accessPtr);
            }
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }
}
