using System.Runtime.InteropServices;

namespace LynxLab;

internal sealed record DirectXDiagnosticsResult(
    bool Direct2DDll,
    bool Direct2DFactory,
    bool DirectCompositionDll,
    bool DirectCompositionExport,
    bool DxgiDll,
    bool D3D11Dll,
    bool D3D11Hardware,
    bool D3D11Warp,
    string FeatureLevel,
    string HardwareStatus,
    string Summary,
    string? Error)
{
    public static DirectXDiagnosticsResult Probe()
    {
        var d2dDll = HasLibrary("d2d1.dll");
        var dcompDll = HasLibrary("dcomp.dll", "DCompositionCreateDevice");
        var dxgiDll = HasLibrary("dxgi.dll");
        var d3d11Dll = HasLibrary("d3d11.dll");

        var d2dFactory = false;
        var dcompExport = false;
        var hardware = false;
        var warp = false;
        var featureLevel = "n/a";
        string? error = null;

        if (dcompDll)
            dcompExport = HasExport("dcomp.dll", "DCompositionCreateDevice");

        if (d2dDll)
        {
            try
            {
                d2dFactory = TryCreateDirect2DFactory(out var d2dError);
                if (!d2dFactory && !string.IsNullOrWhiteSpace(d2dError))
                    error = "Direct2D: " + d2dError;
            }
            catch (Exception ex)
            {
                error = "Direct2D: " + ex.Message;
            }
        }

        if (d3d11Dll)
        {
            try
            {
                hardware = TryCreateD3D11Device(
                    D3DDriverType.Hardware,
                    out var hardwareFeature,
                    out var hardwareError);

                if (hardware)
                {
                    featureLevel = FeatureLevelName(hardwareFeature);
                }
                else
                {
                    warp = TryCreateD3D11Device(
                        D3DDriverType.Warp,
                        out var warpFeature,
                        out var warpError);

                    if (warp)
                        featureLevel = FeatureLevelName(warpFeature);

                    if (!hardware &&
                        !warp &&
                        string.IsNullOrWhiteSpace(error))
                    {
                        error =
                            "D3D11 hardware: " + hardwareError +
                            " · WARP: " + warpError;
                    }
                }
            }
            catch (Exception ex)
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = "D3D11: " + ex.Message;
            }
        }

        var hardwareStatus = hardware
            ? "HARDWARE"
            : warp
                ? "WARP / SOFTWARE"
                : "UNAVAILABLE";

        var summary =
            d2dFactory && dcompExport && (hardware || warp)
                ? hardware
                    ? "DirectX foundation ready · hardware path"
                    : "DirectX foundation ready · WARP fallback"
                : "DirectX foundation incomplete · GDI+ fallback remains active";

        return new DirectXDiagnosticsResult(
            Direct2DDll: d2dDll,
            Direct2DFactory: d2dFactory,
            DirectCompositionDll: dcompDll,
            DirectCompositionExport: dcompExport,
            DxgiDll: dxgiDll,
            D3D11Dll: d3d11Dll,
            D3D11Hardware: hardware,
            D3D11Warp: warp,
            FeatureLevel: featureLevel,
            HardwareStatus: hardwareStatus,
            Summary: summary,
            Error: error);
    }

    private static bool HasLibrary(string name, string? requiredExport = null)
    {
        if (!NativeLibrary.TryLoad(name, out var handle))
            return false;

        try
        {
            return requiredExport is null ||
                NativeLibrary.TryGetExport(handle, requiredExport, out _);
        }
        finally
        {
            NativeLibrary.Free(handle);
        }
    }

    private static bool HasExport(string library, string export)
    {
        if (!NativeLibrary.TryLoad(library, out var handle))
            return false;

        try
        {
            return NativeLibrary.TryGetExport(handle, export, out _);
        }
        finally
        {
            NativeLibrary.Free(handle);
        }
    }

    private static bool TryCreateDirect2DFactory(out string? error)
    {
        error = null;
        var iid = new Guid("06152247-6F50-465A-9245-118BFD3B6007");
        IntPtr factory = IntPtr.Zero;

        try
        {
            var hr = D2D1CreateFactory(
                D2DFactoryType.SingleThreaded,
                ref iid,
                IntPtr.Zero,
                out factory);

            if (hr < 0 || factory == IntPtr.Zero)
            {
                error = HResultText(hr);
                return false;
            }

            return true;
        }
        finally
        {
            if (factory != IntPtr.Zero)
                Marshal.Release(factory);
        }
    }

    private static bool TryCreateD3D11Device(
        D3DDriverType driverType,
        out int featureLevel,
        out string? error)
    {
        featureLevel = 0;
        error = null;

        IntPtr device = IntPtr.Zero;
        IntPtr context = IntPtr.Zero;

        try
        {
            var hr = D3D11CreateDevice(
                IntPtr.Zero,
                driverType,
                IntPtr.Zero,
                0,
                IntPtr.Zero,
                0,
                D3D11SdkVersion,
                out device,
                out featureLevel,
                out context);

            if (hr < 0 || device == IntPtr.Zero)
            {
                error = HResultText(hr);
                return false;
            }

            return true;
        }
        finally
        {
            if (context != IntPtr.Zero)
                Marshal.Release(context);

            if (device != IntPtr.Zero)
                Marshal.Release(device);
        }
    }

    private static string FeatureLevelName(int value) =>
        value switch
        {
            0xC200 => "12.2",
            0xC100 => "12.1",
            0xC000 => "12.0",
            0xB100 => "11.1",
            0xB000 => "11.0",
            0xA100 => "10.1",
            0xA000 => "10.0",
            0x9300 => "9.3",
            0x9200 => "9.2",
            0x9100 => "9.1",
            _ => value == 0 ? "n/a" : $"0x{value:X}"
        };

    private static string HResultText(int hr) =>
        $"HRESULT 0x{unchecked((uint)hr):X8}";

    private const uint D3D11SdkVersion = 7;

    private enum D2DFactoryType : uint
    {
        SingleThreaded = 0,
        MultiThreaded = 1
    }

    private enum D3DDriverType : uint
    {
        Unknown = 0,
        Hardware = 1,
        Reference = 2,
        Null = 3,
        Software = 4,
        Warp = 5
    }

    [DllImport(
        "d2d1.dll",
        CallingConvention = CallingConvention.StdCall,
        ExactSpelling = true)]
    private static extern int D2D1CreateFactory(
        D2DFactoryType factoryType,
        ref Guid riid,
        IntPtr factoryOptions,
        out IntPtr factory);

    [DllImport(
        "d3d11.dll",
        CallingConvention = CallingConvention.StdCall,
        ExactSpelling = true)]
    private static extern int D3D11CreateDevice(
        IntPtr adapter,
        D3DDriverType driverType,
        IntPtr software,
        uint flags,
        IntPtr featureLevels,
        uint featureLevelsCount,
        uint sdkVersion,
        out IntPtr device,
        out int featureLevel,
        out IntPtr immediateContext);
}
