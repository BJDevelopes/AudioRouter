using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AudioRouter.Helpers
{
    public static class IconExtractor
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static ImageSource? GetProcessIcon(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                string? exePath = null;

                try
                {
                    exePath = process.MainModule?.FileName;
                }
                catch
                {
                    // Access denied - try alternative method
                    return GetDefaultIcon();
                }

                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    return GetDefaultIcon();
                }

                return GetIconFromFile(exePath);
            }
            catch
            {
                return GetDefaultIcon();
            }
        }

        public static ImageSource? GetIconFromFile(string filePath)
        {
            try
            {
                IntPtr hIcon = ExtractIcon(IntPtr.Zero, filePath, 0);
                if (hIcon == IntPtr.Zero)
                {
                    return GetDefaultIcon();
                }

                try
                {
                    using (Icon icon = Icon.FromHandle(hIcon))
                    {
                        var bitmap = icon.ToBitmap();
                        var hBitmap = bitmap.GetHbitmap();

                        try
                        {
                            var imageSource = Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());

                            imageSource.Freeze();
                            return imageSource;
                        }
                        finally
                        {
                            DeleteObject(hBitmap);
                        }
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
            catch
            {
                return GetDefaultIcon();
            }
        }

        private static ImageSource? GetDefaultIcon()
        {
            // Return a default music note icon (you could create a simple geometry here)
            return null;
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
