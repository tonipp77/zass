namespace Zass.Core.Annotations;

public static class PixelationEffect
{
    /// <summary>Replaces each block with its mean BGRA color, including partial edge blocks.</summary>
    public static void Apply(byte[] pixels, int width, int height, int stride, int blockSize)
    {
        if (width <= 0 || height <= 0 || stride < (long)width * 4 ||
            (long)stride * height > pixels.Length || blockSize < 2 || blockSize > 128)
            throw new ArgumentOutOfRangeException(nameof(blockSize));
        for (int y = 0; y < height; y += blockSize)
        for (int x = 0; x < width; x += blockSize)
        {
            int right = Math.Min(width, x + blockSize), bottom = Math.Min(height, y + blockSize);
            long b = 0, g = 0, r = 0, a = 0;
            for (int row = y; row < bottom; row++)
            for (int col = x; col < right; col++)
            {
                int i = row * stride + col * 4;
                b += pixels[i]; g += pixels[i+1]; r += pixels[i+2]; a += pixels[i+3];
            }
            int count = (right-x) * (bottom-y);
            for (int row = y; row < bottom; row++)
            for (int col = x; col < right; col++)
            {
                int i = row * stride + col * 4;
                pixels[i] = (byte)(b/count); pixels[i+1] = (byte)(g/count);
                pixels[i+2] = (byte)(r/count); pixels[i+3] = (byte)(a/count);
            }
        }
    }
}
