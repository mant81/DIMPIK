namespace DIMPIK.Models;

public sealed class CaptureDocument : IDisposable
{
    public Bitmap Image { get; private set; }
    public string Name { get; private set; }
    public string? SavePath { get; set; }
    public bool Dirty { get; private set; } = true;
    public float Zoom { get; set; } = 1;
    public List<Annotation> Annotations { get; private set; } = [];
    public Guid? Selection { get; set; }
    public int Revision { get; private set; }
    // Only pixel operations retain an old bitmap; ordinary annotation edits do not clone it.
    private sealed record UndoState(List<Annotation> Annotations, Bitmap? Image = null);
    private readonly Stack<UndoState> undo = new();
    private readonly Stack<UndoState> redo = new();
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public CaptureDocument(Bitmap image, string name) { Image = image; Name = name; }
    public void Rename(string name) { if (!string.IsNullOrWhiteSpace(name)) Name = name; }
    public void Checkpoint()
    {
        undo.Push(new(Annotations.Select(a => a.Copy()).ToList()));
        ClearStack(redo);
    }
    public void Changed() { Dirty = true; Revision++; }
    public void Saved() => Dirty = false;
    public void Undo()
    {
        if (undo.TryPop(out var previous))
        {
            redo.Push(new(Annotations.Select(a => a.Copy()).ToList(), previous.Image == null ? null : Image));
            if (previous.Image != null) Image = previous.Image;
            Annotations = previous.Annotations; Selection = null; Changed();
        }
    }
    public void Redo()
    {
        if (redo.TryPop(out var next))
        {
            undo.Push(new(Annotations.Select(a => a.Copy()).ToList(), next.Image == null ? null : Image));
            if (next.Image != null) Image = next.Image;
            Annotations = next.Annotations; Selection = null; Changed();
        }
    }
    public bool Crop(Rectangle region)
    {
        region = Rectangle.Intersect(new Rectangle(Point.Empty, Image.Size), region);
        if (region.Width < 1 || region.Height < 1 || region == new Rectangle(Point.Empty, Image.Size)) return false;
        var annotations = Annotations.Select(a => a.Copy()).ToList();
        // Keep object geometry intact, including partially clipped objects, so they remain editable.
        foreach (var a in annotations)
            a.Bounds = new(a.Bounds.X - region.X, a.Bounds.Y - region.Y, a.Bounds.Width, a.Bounds.Height);
        var cropped = Image.Clone(region, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        undo.Push(new(Annotations, Image)); ClearStack(redo);
        Image = cropped; Annotations = annotations; Selection = null; Changed(); return true;
    }
    public bool ResizeImage(Size size)
    {
        Services.ImageSafety.CheckSize(size);
        if (size == Image.Size) return false;
        float sx = size.Width / (float)Image.Width, sy = size.Height / (float)Image.Height;
        var annotations = Annotations.Select(a => a.Copy()).ToList();
        foreach (var a in annotations)
        {
            a.Bounds = new RectangleF(a.Bounds.X * sx, a.Bounds.Y * sy, a.Bounds.Width * sx, a.Bounds.Height * sy);
            float scale = Math.Min(sx, sy);
            a.Width = Math.Clamp(a.Width * scale, .01f, 4096); a.FontSize = Math.Clamp(a.FontSize * scale, 1, 4096);
        }
        var resized = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(resized);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(Image, new Rectangle(Point.Empty, size));
        }
        catch { resized.Dispose(); throw; }
        undo.Push(new(Annotations, Image)); ClearStack(redo);
        Image = resized; Annotations = annotations; Selection = null; Changed(); return true;
    }
    public bool RotateClockwise()
    {
        Services.ImageSafety.CheckSize(new Size(Image.Height, Image.Width));
        var rotated = (Bitmap)Image.Clone();
        try { rotated.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipNone); }
        catch { rotated.Dispose(); throw; }

        var annotations = Annotations.Select(a => a.Copy()).ToList();
        foreach (var annotation in annotations)
        {
            var bounds = annotation.Bounds;
            annotation.Bounds = new(Image.Height - bounds.Bottom, bounds.X, bounds.Height, bounds.Width);
            annotation.Rotation = (annotation.Rotation + 90) % 360;
        }

        undo.Push(new(Annotations, Image)); ClearStack(redo);
        Image = rotated; Annotations = annotations; Selection = null; Changed(); return true;
    }
    public Bitmap Compose()
    {
        var result = new Bitmap(Image);
        using var g = Graphics.FromImage(result);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        foreach (var annotation in Annotations) annotation.Draw(g);
        return result;
    }
    public bool ApplyPixelEffect(Rectangle region, Services.PixelEffectKind effect, int strength)
    {
        region.Intersect(new Rectangle(Point.Empty, Image.Size));
        if (region.Width < 1 || region.Height < 1) return false;
        using var patch = PreviewPixelEffect(region, effect, strength);
        // A committed raster edit covers the composited region while keeping other objects editable.
        var edit = new Annotation { Kind = AnnotationKind.PixelPatch, Bounds = region, ImageData = Encode(patch) };
        Checkpoint(); Annotations.Add(edit); Selection = null; Changed(); return true;
    }
    public Bitmap PreviewPixelEffect(Rectangle region, Services.PixelEffectKind effect, int strength)
    {
        region.Intersect(new Rectangle(Point.Empty, Image.Size));
        if (region.Width < 1 || region.Height < 1) throw new ArgumentException("효과를 적용할 영역이 없습니다.");
        using var composed = new Bitmap(region.Width, region.Height);
        using (var g = Graphics.FromImage(composed))
        {
            g.TranslateTransform(-region.X, -region.Y); g.DrawImageUnscaled(Image, Point.Empty);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            foreach (var annotation in Annotations) annotation.Draw(g);
        }
        return Services.PixelEffectService.Create(composed, new Rectangle(Point.Empty, region.Size), effect, strength);
    }
    internal SessionDocument ExportSession()
    {
        if (undo.Count > 256 || redo.Count > 256 || Annotations.Count > 2048) throw new InvalidDataException("세션의 편집 이력 또는 주석 수가 제한을 초과합니다.");
        Services.ImageSafety.CheckSize(Image.Size);
        return new(Name, SavePath, Dirty, Zoom, Selection, Encode(Image), Annotations,
            undo.Select(state => new SessionUndo(state.Annotations, state.Image == null ? null : Encode(state.Image))).ToList(),
            redo.Select(state => new SessionUndo(state.Annotations, state.Image == null ? null : Encode(state.Image))).ToList());
    }
    internal static CaptureDocument ImportSession(SessionDocument state)
    {
        var doc = new CaptureDocument(Decode(state.Image), state.Name) { SavePath = state.SavePath, Dirty = state.Dirty, Zoom = state.Zoom, Selection = state.Selection, Annotations = state.Annotations };
        try
        {
            foreach (var entry in state.Undo.AsEnumerable().Reverse()) doc.undo.Push(new(entry.Annotations, entry.Image == null ? null : Decode(entry.Image)));
            foreach (var entry in (state.Redo ?? []).AsEnumerable().Reverse()) doc.redo.Push(new(entry.Annotations, entry.Image == null ? null : Decode(entry.Image)));
            return doc;
        }
        catch { doc.Dispose(); throw; }
    }
    private static string Encode(Bitmap image) { using var stream = new MemoryStream(); image.Save(stream, System.Drawing.Imaging.ImageFormat.Png); return Convert.ToBase64String(stream.ToArray()); }
    private static Bitmap Decode(string image) => Services.ImageSafety.LoadBase64(image);
    private static void ClearStack(Stack<UndoState> stack)
    {
        foreach (var state in stack) state.Image?.Dispose();
        stack.Clear();
    }
    public void Dispose() { Image.Dispose(); ClearStack(undo); ClearStack(redo); Annotations.Clear(); }
}

internal sealed record SessionUndo(List<Annotation> Annotations, string? Image);
internal sealed record SessionDocument(string Name, string? SavePath, bool Dirty, float Zoom, Guid? Selection, string Image, List<Annotation> Annotations, List<SessionUndo> Undo, List<SessionUndo>? Redo = null);
