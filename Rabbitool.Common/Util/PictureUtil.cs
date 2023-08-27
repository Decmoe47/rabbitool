using SkiaSharp;

namespace Rabbitool.Common.Util;

/// <summary>
/// <see cref="https://www.leavescn.com/Articles/Content/1299"/>
/// </summary>
public static class ImageUtil
{
    /// <summary>
    /// Byte
    /// </summary>
    private static readonly int _allowedMaxImageSize = 2000000;

    /// <summary>
    /// 压缩图片
    /// </summary>
    /// <param name="source">原文件位置</param>
    /// <param name="target">生成目标文件位置</param>
    /// <param name="maxWidth">最大宽度，根据此宽度计算是否需要缩放，计算新高度</param>
    /// <param name="quality">图片质量，范围0-100</param>
    public static void Compress(string source, string target, int quality, decimal? maxWidth = null)
    {
        FileStream file = File.OpenRead(source);
        SKManagedStream fileStream = new(file);
        SKBitmap bitmap = SKBitmap.Decode(fileStream);

        decimal width = bitmap.Width;
        decimal height = bitmap.Height;
        decimal newWidth = width;
        decimal newHeight = height;
        if (maxWidth is decimal v && width > maxWidth)
        {
            newWidth = v;
            newHeight = height / width * v;
        }
        SKBitmap resized = bitmap.Resize(new SKImageInfo((int)newWidth, (int)newHeight), SKFilterQuality.Medium);

        if (resized != null)
        {
            SKImage image = SKImage.FromBitmap(resized);
            FileStream writeStream = File.OpenWrite(target);

            image.Encode(SKEncodedImageFormat.Jpeg, quality).SaveTo(writeStream);
        }
    }

    /// <summary>
    /// 压缩图片
    /// </summary>
    /// <param name="image">图片字节</param>
    /// <param name="quality">图片质量，范围0-100，默认70</param>
    /// <returns>压缩后的图片字节</returns>
    public static byte[] Compress(byte[] image, int quality = 70)
    {
        using MemoryStream file = new(image);
        using SKManagedStream fileStream = new(file);
        using SKBitmap bitmap = SKBitmap.Decode(fileStream);

        decimal width = bitmap.Width;
        decimal height = bitmap.Height;
        decimal newWidth = width;
        decimal newHeight = height;
        using SKBitmap resized = bitmap.Resize(new SKImageInfo((int)newWidth, (int)newHeight), SKFilterQuality.Medium)
            ?? throw new CompressPictureException();

        using SKImage img = SKImage.FromBitmap(resized);
        using MemoryStream compressedImg = new();
        img.Encode(SKEncodedImageFormat.Jpeg, quality).SaveTo(compressedImg);

        byte[] result = compressedImg.ToArray();
        while (IsLargerThanAllowed(result))
        {
            newWidth = width * (decimal)0.85;
            newHeight = height / width * newWidth;
            using SKBitmap resized2 = bitmap.Resize(new SKImageInfo((int)newWidth, (int)newHeight), SKFilterQuality.Medium)
                ?? throw new CompressPictureException();

            using SKImage img2 = SKImage.FromBitmap(resized);
            using MemoryStream compressedImg2 = new();
            img.Encode(SKEncodedImageFormat.Jpeg, quality).SaveTo(compressedImg2);

            result = compressedImg2.ToArray();
        }

        return result;
    }

    public static bool IsLargerThanAllowed(byte[] image)
    {
        string fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";

        if (!Directory.Exists("./tmp"))
            Directory.CreateDirectory("./tmp");
        File.WriteAllBytes("./tmp/" + fileName, image);

        FileInfo fileInfo = new("./tmp/" + fileName);
        return fileInfo.Length > _allowedMaxImageSize;
    }
}

public class CompressPictureException : System.Exception
{
    public CompressPictureException()
    {
    }
}