namespace DIMPIK.Models;

public sealed class CaptureDocument : IDisposable
{
    public Bitmap Image { get; private set; }
    public Size CanvasSize { get; private set; }
    public RectangleF ImageBounds { get; private set; }
    public string Name { get; private set; }
    public string? SavePath { get; set; }
    public bool Dirty { get; private set; } = true;
    public float Zoom { get; set; } = 1;
    public List<Annotation> Annotations { get; private set; } = [];
    public Guid? Selection { get; set; }
    public int Revision { get; private set; }
    // Only pixel operations retain an old bitmap; ordinary annotation edits do not clone it.
    private sealed record UndoState(List<Annotation> Annotations, Bitmap? Image = null, Size? CanvasSize = null, RectangleF? ImageBounds = null);
    private readonly Stack<UndoState> undo = new();
    private readonly Stack<UndoState> redo = new();
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public CaptureDocument(Bitmap image, string name) { Image = image; Name = name; CanvasSize = image.Size; ImageBounds = new(Point.Empty, image.Size); }
    public void Rename(string name) { if (!string.IsNullOrWhiteSpace(name)) Name = name; }
    public void Checkpoint()
    {
        undo.Push(Snapshot());
        ClearStack(redo);
    }
    public void Changed() { Dirty = true; Revision++; }
    private UndoState Snapshot(Bitmap? image = null) => new(Annotations.Select(a => a.Copy()).ToList(), image, CanvasSize, ImageBounds);
    public bool SetImageBounds(RectangleF bounds, bool checkpoint = true)
    {
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) ||
            Math.Abs(bounds.X) > 1_000_000 || Math.Abs(bounds.Y) > 1_000_000 || bounds.Width is < 1 or > 1_000_000 || bounds.Height is < 1 or > 1_000_000 || bounds == ImageBounds) return false;
        if (checkpoint) { undo.Push(Snapshot()); ClearStack(redo); }
        ImageBounds = bounds;
        if (checkpoint) Changed();
        return true;
    }
    public bool ResizeCanvas(Size size)
    {
        Services.ImageSafety.CheckSize(size);
        if (size == CanvasSize) return false;
        var prior = Snapshot(); undo.Push(prior); ClearStack(redo);
        CanvasSize = size;
        Changed(); return true;
    }
    public void Saved() => Dirty = false;
    public void Undo()
    {
        if (undo.TryPop(out var previous))
        {
            redo.Push(Snapshot(previous.Image == null ? null : Image));
            if (previous.Image != null) Image = previous.Image;
            if (previous.CanvasSize is { } canvas) CanvasSize = canvas;
            if (previous.ImageBounds is { } bounds) ImageBounds = bounds;
            Annotations = previous.Annotations; Selection = null; Changed();
        }
    }
    public void Redo()
    {
        if (redo.TryPop(out var next))
        {
            undo.Push(Snapshot(next.Image == null ? null : Image));
            if (next.Image != null) Image = next.Image;
            if (next.CanvasSize is { } canvas) CanvasSize = canvas;
            if (next.ImageBounds is { } bounds) ImageBounds = bounds;
            Annotations = next.Annotations; Selection = null; Changed();
        }
    }
    public bool Crop(Rectangle region)
    {
        region = Rectangle.Intersect(new Rectangle(Point.Empty, CanvasSize), region);
        if (region.Width < 1 || region.Height < 1 || region == new Rectangle(Point.Empty, CanvasSize)) return false;
        var visibleImage = RectangleF.Intersect(ImageBounds, region);
        if (visibleImage.Width < 1 || visibleImage.Height < 1) return false;
        float sx = Image.Width / ImageBounds.Width, sy = Image.Height / ImageBounds.Height;
        var sourceRegion = Rectangle.Intersect(new Rectangle(Point.Empty, Image.Size), Rectangle.FromLTRB(
            (int)Math.Floor((visibleImage.Left - ImageBounds.Left) * sx),
            (int)Math.Floor((visibleImage.Top - ImageBounds.Top) * sy),
            (int)Math.Ceiling((visibleImage.Right - ImageBounds.Left) * sx),
            (int)Math.Ceiling((visibleImage.Bottom - ImageBounds.Top) * sy)));
        if (sourceRegion.Width < 1 || sourceRegion.Height < 1) return false;
        var annotations = Annotations.Select(a => a.Copy()).ToList();
        // Keep object geometry intact, including partially clipped objects, so they remain editable.
        foreach (var a in annotations)
            a.Bounds = new(a.Bounds.X - region.X, a.Bounds.Y - region.Y, a.Bounds.Width, a.Bounds.Height);
        var cropped = Image.Clone(sourceRegion, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        undo.Push(Snapshot(Image)); ClearStack(redo);
        Image = cropped; CanvasSize = region.Size;
        ImageBounds = new(ImageBounds.X + sourceRegion.X / sx - region.X, ImageBounds.Y + sourceRegion.Y / sy - region.Y, sourceRegion.Width / sx, sourceRegion.Height / sy);
        Annotations = annotations; Selection = null; Changed(); return true;
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
        undo.Push(Snapshot(Image)); ClearStack(redo);
        Image = resized; CanvasSize = resized.Size; ImageBounds = new(Point.Empty, resized.Size); Annotations = annotations; Selection = null; Changed(); return true;
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
            annotation.Bounds = new(CanvasSize.Height - bounds.Bottom, bounds.X, bounds.Height, bounds.Width);
            annotation.Rotation = (annotation.Rotation + 90) % 360;
        }

        undo.Push(Snapshot(Image)); ClearStack(redo);
        Image = rotated; CanvasSize = new Size(CanvasSize.Height, CanvasSize.Width);
        ImageBounds = new(CanvasSize.Width - ImageBounds.Bottom, ImageBounds.X, ImageBounds.Height, ImageBounds.Width);
        Annotations = annotations; Selection = null; Changed(); return true;
    }
    public Bitmap Compose(Annotation? exclude = null)
    {
        var result = new Bitmap(CanvasSize.Width, CanvasSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            using (var baseGraphics = Graphics.FromImage(result))
            {
                baseGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                baseGraphics.DrawImage(Image, ImageBounds);
            }
            foreach (var annotation in Annotations)
            {
                if (ReferenceEquals(annotation, exclude)) continue;
                if (annotation.Kind == AnnotationKind.PixelPatch && annotation.EffectKind is { } effect)
                {
                    var region = Rectangle.Intersect(new Rectangle(Point.Empty, result.Size), Rectangle.Round(annotation.Bounds));
                    if (region.Width < 1 || region.Height < 1) continue;
                    using var patch = Services.PixelEffectService.Create(result, region, effect, annotation.EffectStrength);
                    using var effectGraphics = Graphics.FromImage(result);
                    effectGraphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    effectGraphics.DrawImageUnscaled(patch, region.Location);
                }
                else
                {
                    using var g = Graphics.FromImage(result);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    annotation.Draw(g);
                }
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    public bool ApplyPixelEffect(Rectangle region, Services.PixelEffectKind effect, int strength)
    {
        region.Intersect(new Rectangle(Point.Empty, Image.Size));
        if (region.Width < 1 || region.Height < 1) return false;
        var edit = new Annotation { Kind = AnnotationKind.PixelPatch, Bounds = region, EffectKind = effect, EffectStrength = Math.Clamp(strength, 2, 64) };
        Checkpoint(); Annotations.Add(edit); Selection = edit.Id; Changed(); return true;
    }
    public Bitmap PreviewPixelEffect(Rectangle region, Services.PixelEffectKind effect, int strength)
    {
        region.Intersect(new Rectangle(Point.Empty, Image.Size));
        if (region.Width < 1 || region.Height < 1) throw new ArgumentException("효과를 적용할 영역이 없습니다.");
        using var composed = Compose();
        return Services.PixelEffectService.Create(composed, region, effect, strength);
    }
    internal SessionDocument ExportSession()
    {
        if (undo.Count > 256 || redo.Count > 256 || Annotations.Count > 2048) throw new InvalidDataException("세션의 편집 이력 또는 주석 수가 제한을 초과합니다.");
        Services.ImageSafety.CheckSize(Image.Size); Services.ImageSafety.CheckSize(CanvasSize);
        return new(Name, SavePath, Dirty, Zoom, Selection, Encode(Image), Annotations,
            undo.Select(state => new SessionUndo(state.Annotations, state.Image == null ? null : Encode(state.Image), state.CanvasSize?.Width ?? 0, state.CanvasSize?.Height ?? 0, state.ImageBounds)).ToList(),
            redo.Select(state => new SessionUndo(state.Annotations, state.Image == null ? null : Encode(state.Image), state.CanvasSize?.Width ?? 0, state.CanvasSize?.Height ?? 0, state.ImageBounds)).ToList(), CanvasSize.Width, CanvasSize.Height, ImageBounds);
    }
    internal static CaptureDocument ImportSession(SessionDocument state)
    {
        var doc = new CaptureDocument(Decode(state.Image), state.Name) { SavePath = state.SavePath, Dirty = state.Dirty, Zoom = state.Zoom, Selection = state.Selection, Annotations = state.Annotations };
        if (state.CanvasWidth > 0 && state.CanvasHeight > 0) doc.CanvasSize = new Size(state.CanvasWidth, state.CanvasHeight);
        if (state.ImageBounds is { } imageBounds) doc.ImageBounds = imageBounds;
        try
        {
            foreach (var entry in state.Undo.AsEnumerable().Reverse()) doc.undo.Push(new(entry.Annotations, entry.Image == null ? null : Decode(entry.Image), entry.CanvasWidth > 0 && entry.CanvasHeight > 0 ? new Size(entry.CanvasWidth, entry.CanvasHeight) : null, entry.ImageBounds));
            foreach (var entry in (state.Redo ?? []).AsEnumerable().Reverse()) doc.redo.Push(new(entry.Annotations, entry.Image == null ? null : Decode(entry.Image), entry.CanvasWidth > 0 && entry.CanvasHeight > 0 ? new Size(entry.CanvasWidth, entry.CanvasHeight) : null, entry.ImageBounds));
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

internal sealed record SessionUndo(List<Annotation> Annotations, string? Image, int CanvasWidth = 0, int CanvasHeight = 0, RectangleF? ImageBounds = null);
internal sealed record SessionDocument(string Name, string? SavePath, bool Dirty, float Zoom, Guid? Selection, string Image, List<Annotation> Annotations, List<SessionUndo> Undo, List<SessionUndo>? Redo = null, int CanvasWidth = 0, int CanvasHeight = 0, RectangleF? ImageBounds = null);
