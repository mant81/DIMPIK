using System.Drawing.Imaging;
using DIMPIK.UI;

// Rebuild the Windows icon directly from the same original vector mark as the header.
string output = args.Length == 1 ? args[0] : "src/DIMPIK/Assets/DIMPIK.ico";
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
var frames = sizes.Select(size =>
{
    using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var graphics = Graphics.FromImage(bitmap)) BrandMark.Draw(graphics, new Size(size, size));
    using var stream = new MemoryStream();
    if (size == 256)
    {
        bitmap.Save(stream, ImageFormat.Png);
    }
    else
    {
        // Small ICO frames use a classic 32-bit DIB: Windows and System.Drawing
        // can then select the exact tray size without misinterpreting PNG bytes.
        using var dib = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        int maskStride = ((size + 31) / 32) * 4;
        dib.Write(40); dib.Write(size); dib.Write(size * 2); dib.Write((ushort)1); dib.Write((ushort)32);
        dib.Write(0); dib.Write(size * size * 4); dib.Write(0); dib.Write(0); dib.Write(0); dib.Write(0);
        for (int y = size - 1; y >= 0; y--)
            for (int x = 0; x < size; x++) dib.Write(bitmap.GetPixel(x, y).ToArgb());
        for (int y = size - 1; y >= 0; y--)
        {
            var mask = new byte[maskStride];
            for (int x = 0; x < size; x++)
                if (bitmap.GetPixel(x, y).A == 0) mask[x / 8] |= (byte)(0x80 >> (x % 8));
            dib.Write(mask);
        }
    }
    return stream.ToArray();
}).ToArray();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
using var writer = new BinaryWriter(File.Create(output));
writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
int offset = 6 + sizes.Length * 16;
for (int i = 0; i < sizes.Length; i++)
{
    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
    writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
    writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length;
}
foreach (var frame in frames) writer.Write(frame);
Console.WriteLine($"Created {output} ({sizes.Length} sizes, 16–256 px)");
