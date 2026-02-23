// Visualizador OZJ/OZT - MU Online
// OZJ: 24 bytes header + JPEG
// OZT: 22 bytes header + BGRA (width/height en bytes 16-19, 32bpp)

namespace OzViewer;

public static class OzImageLoader
{
    private const int OZJ_HEADER_SIZE = 24;
    private const int OZT_HEADER_SIZE = 22;
    private const int OZT_WIDTH_OFFSET = 16;
    private const int OZT_HEIGHT_OFFSET = 18;
    private const int OZT_BPP_OFFSET = 20;
    private const int OZT_PIXELS_OFFSET = 22;

    public static Image? Load(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".ozj" => LoadOzj(filePath),
            ".ozt" => LoadOzt(filePath),
            ".jpg" or ".jpeg" or ".tga" => LoadStandard(filePath),
            _ => TryLoadByContent(filePath)
        };
    }

    private static Image? LoadOzj(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length <= OZJ_HEADER_SIZE)
            return null;

        var jpegData = data.AsSpan(OZJ_HEADER_SIZE);
        using var ms = new MemoryStream(jpegData.ToArray());
        using var img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: true);
        return new Bitmap(img);
    }

    private static Image? LoadOzt(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < OZT_PIXELS_OFFSET)
            return null;

        int width = BitConverter.ToInt16(data, OZT_WIDTH_OFFSET);
        int height = BitConverter.ToInt16(data, OZT_HEIGHT_OFFSET);
        int bpp = data[OZT_BPP_OFFSET];

        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            return null;

        if (bpp != 32)
            return null;

        int pixelSize = width * height * 4;
        if (data.Length < OZT_PIXELS_OFFSET + pixelSize)
            return null;

        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var bmpData = bmp.LockBits(
            new Rectangle(0, 0, width, height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            unsafe
            {
                byte* dst = (byte*)bmpData.Scan0;
                int srcIndex = OZT_PIXELS_OFFSET;

                for (int y = height - 1; y >= 0; y--)
                {
                    byte* row = dst + y * bmpData.Stride;
                    for (int x = 0; x < width; x++)
                    {
                        row[x * 4 + 0] = data[srcIndex + 0];
                        row[x * 4 + 1] = data[srcIndex + 1];
                        row[x * 4 + 2] = data[srcIndex + 2];
                        row[x * 4 + 3] = data[srcIndex + 3];
                        srcIndex += 4;
                    }
                }
            }
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        return bmp;
    }

    private static Image? LoadStandard(string path)
    {
        try
        {
            using var img = Image.FromFile(path);
            return new Bitmap(img);
        }
        catch
        {
            return null;
        }
    }

    private static Image? TryLoadByContent(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 32)
            return null;

        if (data.Length > OZJ_HEADER_SIZE + 2 && data[OZJ_HEADER_SIZE] == 0xFF && data[OZJ_HEADER_SIZE + 1] == 0xD8)
            return LoadOzj(path);

        if (data.Length >= OZT_PIXELS_OFFSET)
        {
            int w = BitConverter.ToInt16(data, OZT_WIDTH_OFFSET);
            int h = BitConverter.ToInt16(data, OZT_HEIGHT_OFFSET);
            if (w > 0 && h > 0 && w < 4096 && h < 4096 && data[OZT_BPP_OFFSET] == 32)
                return LoadOzt(path);
        }

        return LoadStandard(path);
    }

    public static bool SaveOzj(Image image, string filePath)
    {
        try
        {
            using var ms = new MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            var jpegBytes = ms.ToArray();

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            var header = new byte[OZJ_HEADER_SIZE];
            fs.Write(header, 0, header.Length);
            fs.Write(jpegBytes, 0, jpegBytes.Length);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool SaveOzt(Image image, string filePath)
    {
        try
        {
            using var bmp = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.DrawImage(image, 0, 0, image.Width, image.Height);

            var width = (short)bmp.Width;
            var height = (short)bmp.Height;
            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                var header = new byte[OZT_PIXELS_OFFSET];
                BitConverter.GetBytes(width).CopyTo(header, OZT_WIDTH_OFFSET);
                BitConverter.GetBytes(height).CopyTo(header, OZT_HEIGHT_OFFSET);
                header[OZT_BPP_OFFSET] = 32;

                var pixelCount = width * height * 4;
                var pixels = new byte[pixelCount];
                unsafe
                {
                    byte* src = (byte*)bmpData.Scan0;
                    int dstIndex = 0;
                    for (int y = height - 1; y >= 0; y--)
                    {
                        byte* row = src + y * bmpData.Stride;
                        for (int x = 0; x < width; x++)
                        {
                            pixels[dstIndex++] = row[x * 4 + 0];
                            pixels[dstIndex++] = row[x * 4 + 1];
                            pixels[dstIndex++] = row[x * 4 + 2];
                            pixels[dstIndex++] = row[x * 4 + 3];
                        }
                    }
                }

                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                fs.Write(header, 0, header.Length);
                fs.Write(pixels, 0, pixels.Length);
                return true;
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }
        catch
        {
            return false;
        }
    }
}
