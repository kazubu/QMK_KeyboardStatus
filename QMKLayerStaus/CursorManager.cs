using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class CursorManager
{
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetSystemCursor(IntPtr hCursor, uint id);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CreateIconIndirect(ref ICONINFO icon);

    [DllImport("user32.dll")]
    static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hObject, int nCount, out BITMAP lpObject);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, IntPtr lpvBits);
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage,
    out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

    const uint SPI_SETCURSORS = 0x0057;


    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    readonly Dictionary<uint, Cursor> SystemCursorsX = new Dictionary<uint, Cursor>()
    {
        { 32512, Cursors.Arrow },
        { 32513, Cursors.IBeam },
        { 32514, Cursors.WaitCursor },
        { 32515, Cursors.Cross },
        { 32516, Cursors.UpArrow },
        { 32517, Cursors.SizeNWSE },
        { 32518, Cursors.SizeNESW },
        { 32519, Cursors.SizeWE },
        { 32520, Cursors.SizeNS },
        { 32521, Cursors.SizeAll },
        { 32522, Cursors.No },
        { 32524, Cursors.Hand }
    };

    readonly Dictionary<uint, Cursor> SystemCursors = new Dictionary<uint, Cursor>()
    {
        { 32512u, Cursors.Arrow    }, // OCR_NORMAL
        { 32513u, Cursors.IBeam    }, // OCR_IBEAM
        { 32514u, Cursors.WaitCursor }, // OCR_WAIT
        { 32515u, Cursors.Cross    }, // OCR_CROSS
        { 32516u, Cursors.UpArrow  }, // OCR_UP

        { 32642u, Cursors.SizeNWSE }, // OCR_SIZENWSE  
        { 32643u, Cursors.SizeNESW }, // OCR_SIZENESW  
        { 32644u, Cursors.SizeWE   }, // OCR_SIZEWE    
        { 32645u, Cursors.SizeNS   }, // OCR_SIZENS    
        { 32646u, Cursors.SizeAll  }, // OCR_SIZEALL  

        { 32648u, Cursors.No       }, // OCR_NO       
        { 32649u, Cursors.Hand     }  // OCR_HAND     
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public uint[] bmiColors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    private IntPtr CreateColorDibSection(Bitmap source)
    {
        int width = source.Width;
        int height = source.Height;

        BITMAPINFO bi = new BITMAPINFO();
        bi.bmiColors = new uint[256];
        bi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
        bi.bmiHeader.biWidth = width;
        bi.bmiHeader.biHeight = -height;
        bi.bmiHeader.biPlanes = 1;
        bi.bmiHeader.biBitCount = 32;
        bi.bmiHeader.biCompression = 0;

        IntPtr bits;
        IntPtr hBitmap = CreateDIBSection(IntPtr.Zero, ref bi, 0, out bits, IntPtr.Zero, 0);

        BitmapData data = source.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        unsafe
        {
            Buffer.MemoryCopy((void*)data.Scan0, (void*)bits, width * height * 4, width * height * 4);
        }

        source.UnlockBits(data);
        return hBitmap;
    }

    private IntPtr CreateFlatMaskBitmap(int width, int height)
    {
        int stride = ((width + 31) / 32) * 4;
        int size = stride * height;
        byte[] bits = new byte[size];
        for (int i = 0; i < size; i++) bits[i] = 0xFF;

        GCHandle handle = GCHandle.Alloc(bits, GCHandleType.Pinned);
        IntPtr hMask = CreateBitmap(width, height, 1, 1, handle.AddrOfPinnedObject());
        handle.Free();
        return hMask;
    }

    public void ChangeCursorColor(Color color)
    {
        foreach (var kvp in SystemCursors)
        {
            uint cursorId = kvp.Key;
            Cursor sysCursor = kvp.Value;
            IntPtr hOriginal = sysCursor.Handle;

            if (!GetIconInfo(hOriginal, out ICONINFO originalInfo))
            {
                Console.WriteLine($"[Cursor {cursorId}] GetIconInfo failed.");
                continue;
            }

#if DEBUG

            // Dump cursor bitmap info
            if (originalInfo.hbmColor != IntPtr.Zero)
            {
                if (GetObject(originalInfo.hbmColor, Marshal.SizeOf(typeof(BITMAP)), out BITMAP bmpColor) != 0)
                {
                    Console.WriteLine($"[Original Cursor {cursorId}] Color Bitmap: {bmpColor.bmWidth}x{bmpColor.bmHeight}, {bmpColor.bmBitsPixel}bpp");
                }
            }
            else
            {
                Console.WriteLine($"[Original Cursor {cursorId}] No hbmColor.");
            }

            if (originalInfo.hbmMask != IntPtr.Zero)
            {
                if (GetObject(originalInfo.hbmMask, Marshal.SizeOf(typeof(BITMAP)), out BITMAP bmpMask) != 0)
                {
                    Console.WriteLine($"[Original Cursor {cursorId}] Mask Bitmap: {bmpMask.bmWidth}x{bmpMask.bmHeight}, {bmpMask.bmBitsPixel}bpp");
                }
            }
            else
            {
                Console.WriteLine($"[Original Cursor {cursorId}] No hbmMask.");
            }
            Console.WriteLine($"[Original Cursor {cursorId}] Hotspot: {originalInfo.xHotspot}x{originalInfo.yHotspot}");
#endif

            try
            {
                using (var bmp = new Bitmap(sysCursor.Size.Width, sysCursor.Size.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                        sysCursor.Draw(g, new Rectangle(Point.Empty, bmp.Size));

                    Bitmap tinted = TintCursorColor(bmp, color);
                    IntPtr hbmColor;
                    IntPtr hbmMask;

                    hbmColor = CreateColorDibSection(tinted);
                    hbmMask = CreateFlatMaskBitmap(tinted.Width, tinted.Height);

                    ICONINFO iconInfo = new ICONINFO();
                    iconInfo.fIcon = false;
                    iconInfo.xHotspot = originalInfo.xHotspot;
                    iconInfo.yHotspot = originalInfo.yHotspot;
                    iconInfo.hbmColor = hbmColor;
                    iconInfo.hbmMask = hbmMask;

#if DEBUG
                    if (iconInfo.hbmColor != IntPtr.Zero)
                    {
                        if (GetObject(iconInfo.hbmColor, Marshal.SizeOf(typeof(BITMAP)), out BITMAP bmpColor) != 0)
                        {
                            Console.WriteLine($"[Changed Cursor {cursorId}] Color Bitmap: {bmpColor.bmWidth}x{bmpColor.bmHeight}, {bmpColor.bmBitsPixel}bpp");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Changed Cursor {cursorId}] No hbmColor.");
                    }

                    if (iconInfo.hbmMask != IntPtr.Zero)
                    {
                        if (GetObject(iconInfo.hbmMask, Marshal.SizeOf(typeof(BITMAP)), out BITMAP bmpMask) != 0)
                        {
                            Console.WriteLine($"[Changed Cursor {cursorId}] Mask Bitmap: {bmpMask.bmWidth}x{bmpMask.bmHeight}, {bmpMask.bmBitsPixel}bpp");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Changed Cursor {cursorId}] No hbmMask.");
                    }
#endif

                    IntPtr hCursor = CreateIconIndirect(ref iconInfo);

                    if (hCursor == IntPtr.Zero || !SetSystemCursor(hCursor, cursorId))
                    {
                        int err = Marshal.GetLastWin32Error();
                        Console.Error.WriteLine($"SetSystemCursor failed for ID {cursorId}, LastError={err}");
                    }

                    DeleteObject(hbmColor);
                    DeleteObject(hbmMask);
                    DeleteObject(originalInfo.hbmColor);
                    DeleteObject(originalInfo.hbmMask);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing cursor ID {cursorId}: {ex.Message}");
            }
        }
    }

    public void RestoreCursorColor()
    {
        SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
    }

    private Bitmap TintCursorColor(Bitmap bmp, Color tint)
    {
        Bitmap newBmp = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
        Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);

        BitmapData srcData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData dstData = newBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* srcPtr = (byte*)srcData.Scan0;
            byte* dstPtr = (byte*)dstData.Scan0;
            int pixelBytes = 4;

            for (int y = 0; y < bmp.Height; y++)
            {
                byte* srcRow = srcPtr + y * srcData.Stride;
                byte* dstRow = dstPtr + y * dstData.Stride;

                for (int x = 0; x < bmp.Width; x++)
                {
                    int idx = x * pixelBytes;
                    byte b = srcRow[idx + 0];
                    byte g = srcRow[idx + 1];
                    byte r = srcRow[idx + 2];
                    byte a = srcRow[idx + 3];

                    if (a == 0)
                    {
                        dstRow[idx + 0] = 0;
                        dstRow[idx + 1] = 0;
                        dstRow[idx + 2] = 0;
                        dstRow[idx + 3] = 0;
                        continue;
                    }

                    float luminance = (r + g + b) / 3f / 255f;
                    dstRow[idx + 0] = (byte)(tint.B * luminance);
                    dstRow[idx + 1] = (byte)(tint.G * luminance);
                    dstRow[idx + 2] = (byte)(tint.R * luminance);
                    dstRow[idx + 3] = a;
                }
            }
        }

        bmp.UnlockBits(srcData);
        newBmp.UnlockBits(dstData);

        return newBmp;
    }
}