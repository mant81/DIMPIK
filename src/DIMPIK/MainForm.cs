using DIMPIK.Models;
using DIMPIK.Services;
using DIMPIK.UI;
using System.Runtime.InteropServices;

namespace DIMPIK;

public sealed class MainForm : Form
{
    private readonly TabControl tabs = new DocumentTabHost() { Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(280, 38), Padding = new Point(10, 5) };
    private readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private ToolStripStatusLabel? imageInfoStatus, dimensionsStatus, fileSizeStatus;
    private ToolStripSeparator? imageMetadataSeparator;
    private CaptureDocument? measuredSizeDocument;
    private int measuredSizeRevision = -1;
    private string? measuredSizeSavePath;
    private long measuredPngBytes;
    private readonly ClipboardService clipboard = new();
    private readonly CaptureGallery gallery = new() { Visible = false };
    private readonly RegionClipboardPanel regionClipboard = new();
    private readonly NotifyIcon tray;
    private readonly GlobalHotkeyService hotkeys;
    private readonly AppSettings settings;
    private Label? selectedLabel;
    private Panel? homePanel;
    private Panel? ribbonPanel;
    private Action? relayout;
    private Action? syncProperties;
    private readonly List<(Button Button, AnnotationKind? Kind)> ribbonToolButtons = [];
    private readonly List<Control> documentActions = [];
    private readonly List<ToolStripItem> documentStatusItems = [];
    private readonly List<ColorSwatch> swatches = [];
    private Color currentInk = Color.FromArgb(239, 68, 68);
    private Color? currentFillColor;
    private float currentStroke = 3;
    private bool currentFilled, currentDashed, currentBold;
    private float currentTextSize = 24;
    private AnnotationKind currentShape = AnnotationKind.Rectangle;
    private Button? shapeToolButton;
    private readonly ToolTip tips = new() { InitialDelay = 500, ReshowDelay = 100, AutoPopDelay = 5000 };
    private TrackBar? strokeControl;
    private Label? strokeValueLabel;
    private ToolStripButton? undoButton, redoButton;
    private bool menuAnnotationClipboard;
    private uint menuClipboardSequence;
    private CaptureDocument? copiedAnnotationSource;
    private int annotationPasteCount;
    private ToolStripDropDownButton? zoomIndicator;
    private ToolStripDropDown? toolOptionsDropDown;
    private ToolStripDropDown? shapeOptionsDropDown;
    private ContextMenuStrip? sizeOptionsMenu;
    private bool exiting, capturing, updatingInspector, openingSettings;
    private bool fullScreen;
    private FormBorderStyle windowedBorder;
    private FormWindowState windowedState;
    private Rectangle windowedBounds;
    private bool deferCaptureStartup, startupInitialized;
    private readonly bool captureOnlyStartup;
    private Action? initializeStartup;
    private EditorCanvas? Editor => tabs.SelectedTab?.Controls.OfType<Panel>().FirstOrDefault()?.Controls.OfType<EditorCanvas>().FirstOrDefault();
    private int ScaleUi(int logical) => Math.Max(1, (int)Math.Round(logical * DeviceDpi / 96f));
    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    public MainForm(bool managedTray = false, string? startupAction = null)
    {
        captureOnlyStartup = managedTray && startupAction is "--capture-region" or "--capture-full";
        deferCaptureStartup = captureOnlyStartup;
        Icon = AppIcon.Load();
        Disposed += (_, _) => Icon?.Dispose();
        var version = typeof(MainForm).Assembly.GetName().Version;
        Text = version != null ? $"딤픽 v{version.ToString(3)}" : "딤픽";
        Width = 1400; Height = 820; MinimumSize = new Size(900, 500); StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 9.5f); BackColor = Color.FromArgb(245, 247, 250);
        settings = SettingsService.Load();
        var header = new Panel { Dock = DockStyle.None, Height = ScaleUi(136), BackColor = Color.FromArgb(237, 243, 252), Padding = Padding.Empty };
        var toolbarCard = new Panel { AccessibleName = "주 도구 모음", BackColor = Color.FromArgb(246, 248, 255) };
        var headerActions = new BufferedFlowLayoutPanel { Dock = DockStyle.None, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = false, Padding = Padding.Empty, Margin = Padding.Empty, BackColor = Color.FromArgb(252, 253, 255) };
        header.Controls.Add(toolbarCard); toolbarCard.Controls.Add(headerActions);
        var menuBar = new MenuStrip { Dock = DockStyle.None, AutoSize = false, Height = ScaleUi(32), Padding = new Padding(ScaleUi(24), ScaleUi(2), 0, ScaleUi(2)), ImageScalingSize = new Size(ScaleUi(20), ScaleUi(20)), BackColor = Color.FromArgb(237, 243, 252), Font = new Font("맑은 고딕", 11, FontStyle.Regular), Renderer = new EditorMenuRenderer(), AccessibleName = "주 메뉴" };
        header.Controls.Add(menuBar); MainMenuStrip = menuBar;
        void SaveViewSettings() { try { SettingsService.Save(settings); } catch (Exception error) { status.Text = "보기 설정 저장 실패: " + error.Message; } relayout?.Invoke(); }
        var fileMenu = new ToolStripMenuItem("파일");
        fileMenu.Margin = new Padding(0, 0, 10, 0);
        var fileOpen = new ToolStripMenuItem("열기...", null, (_, _) => OpenImage()) { ShortcutKeys = Keys.Control | Keys.O };
        var fileSave = new ToolStripMenuItem("저장", null, (_, _) => Save()) { ShortcutKeyDisplayString = "Ctrl+S" };
        var filePrint = new ToolStripMenuItem("인쇄", null, (_, _) => PrintImage());
        var fileProperties = new ToolStripMenuItem("이미지 속성", null, (_, _) => ShowImageProperties());
        var fileHelp = new ToolStripMenuItem("도움말", null, (_, _) => { using var dialog = new HelpForm(); dialog.ShowDialog(this); });
        var fileClose = new ToolStripMenuItem("닫기", null, (_, _) => { if (tabs.SelectedIndex >= 0) CloseTab(tabs.SelectedIndex); }) { ShortcutKeyDisplayString = "Ctrl+W" };
        fileMenu.DropDownItems.AddRange([fileOpen, new ToolStripSeparator(), fileSave, filePrint, fileProperties, new ToolStripSeparator(), fileHelp, new ToolStripSeparator(), fileClose]);
        fileMenu.DropDownOpening += (_, _) => { fileSave.Enabled = filePrint.Enabled = fileProperties.Enabled = Editor != null; fileClose.Enabled = true; };
        var editMenu = new ToolStripMenuItem("편집");
        editMenu.Margin = new Padding(0, 0, 10, 0);
        var editCut = new ToolStripMenuItem("잘라내기", null, (_, _) => CutFromMenu()) { ShortcutKeyDisplayString = "Ctrl+X" };
        var editCopy = new ToolStripMenuItem("복사", null, (_, _) => CopyFromMenu()) { ShortcutKeyDisplayString = "Ctrl+C" };
        var editPaste = new ToolStripMenuItem("붙여넣기", null, (_, _) => PasteFromMenu()) { ShortcutKeyDisplayString = "Ctrl+V" };
        editMenu.DropDownItems.AddRange([editCut, editCopy, editPaste]);
        editMenu.DropDownOpening += (_, _) => { editCut.Enabled = Editor?.Selected != null || Editor?.IsEditingText == true; editCopy.Enabled = editPaste.Enabled = Editor != null; };
        var viewMenu = new ToolStripMenuItem("보기");
        viewMenu.Margin = new Padding(0, 0, 9, 0);
        var viewRulers = new ToolStripMenuItem("눈금자") { CheckOnClick = true };
        var viewGrid = new ToolStripMenuItem("격자") { CheckOnClick = true };
        var viewStatus = new ToolStripMenuItem("상태표시줄") { CheckOnClick = true };
        var showToolbar = new ToolStripMenuItem("툴바 보이기"); var hideToolbar = new ToolStripMenuItem("툴바 숨기기");
        viewRulers.Click += (_, _) => { settings.ShowRulers = viewRulers.Checked; foreach (TabPage page in tabs.TabPages) foreach (var ruler in page.Controls.OfType<CanvasRuler>()) ruler.Visible = settings.ShowRulers; SaveViewSettings(); };
        viewGrid.Click += (_, _) => { settings.ShowGrid = viewGrid.Checked; foreach (TabPage page in tabs.TabPages) foreach (var canvas in page.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<EditorCanvas>())) { canvas.ShowGrid = settings.ShowGrid; canvas.Invalidate(); } SaveViewSettings(); };
        viewStatus.Click += (_, _) => { settings.ShowStatusBar = viewStatus.Checked; SaveViewSettings(); };
        showToolbar.Click += (_, _) => { settings.ShowToolbar = true; SaveViewSettings(); };
        hideToolbar.Click += (_, _) => { settings.ShowToolbar = false; SaveViewSettings(); };
        viewMenu.DropDownItems.AddRange([viewRulers, viewGrid, viewStatus, showToolbar, hideToolbar]);
        viewMenu.DropDownOpening += (_, _) => { viewRulers.Checked = settings.ShowRulers; viewGrid.Checked = settings.ShowGrid; viewStatus.Checked = settings.ShowStatusBar; showToolbar.Enabled = !settings.ShowToolbar; hideToolbar.Enabled = settings.ShowToolbar; };
        var primaryCommandsSeparator = new ToolStripSeparator { AutoSize = false, Width = 18 };
        menuBar.Items.AddRange([fileMenu, editMenu, viewMenu, primaryCommandsSeparator]);
        var menuIcons = new List<Bitmap>();
        Bitmap LoadMenuIcon(string name)
        {
            const int renderScale = 4;
            int size = 20 * renderScale;
            var image = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Color.Transparent);
            ChromeButton.DrawMenuIcon(graphics, name, new Rectangle(0, 0, size, size), Color.FromArgb(30, 41, 59));
            return image;
        }
        ToolStripButton MenuAction(string label, string icon, Action action)
        {
            var image = LoadMenuIcon(icon);
            menuIcons.Add(image);
            var button = new ToolStripButton { Image = image, DisplayStyle = ToolStripItemDisplayStyle.Image, AccessibleName = label, ToolTipText = label };
            button.Click += (_, _) => action(); menuBar.Items.Add(button); return button;
        }
        var saveCommand = MenuAction("저장", "save", () => Save()); documentStatusItems.Add(saveCommand);
        saveCommand.Margin = new Padding(0, 0, 24, 0);
        var exportCommand = MenuAction("이미지 내보내기", "export", CopyImage); documentStatusItems.Add(exportCommand);
        exportCommand.Margin = new Padding(0, 0, 20, 0);
        undoButton = MenuAction("실행 취소", "undo", Undo); undoButton.Margin = new Padding(0, 0, 13, 0);
        redoButton = MenuAction("다시 실행", "redo", Redo);
        editMenu.DropDownItems.Add(new ToolStripMenuItem("오른쪽으로 90° 회전", null, (_, _) => RotateImageClockwise()));
        var settingsCommand = MenuAction("설정", "settings", OpenSettings); settingsCommand.Alignment = ToolStripItemAlignment.Right;
        settingsCommand.AutoSize = false; settingsCommand.Size = new Size(ScaleUi(46), ScaleUi(38)); settingsCommand.Margin = Padding.Empty;
        Disposed += (_, _) => { foreach (var image in menuIcons) image.Dispose(); };
        var newButton = HeaderButton("새 캡처", false); newButton.Width = 72; newButton.Height = 78; ((ChromeButton)newButton).IconAboveText = true; newButton.ForeColor = Color.FromArgb(37, 99, 235); var captureMenu = new ContextMenuStrip();
        captureMenu.Items.Add("영역 캡처", null, (_, _) => _ = CaptureAsync(true));
        captureMenu.Items.Add("전체 화면", null, (_, _) => _ = CaptureAsync(false));
        newButton.Text = "새 캡처";
        newButton.Margin = new Padding(0, 0, 0, 0);
        newButton.Click += (_, _) => _ = CaptureAsync(settings.NewCaptureSelectRegion);
        newButton.ContextMenuStrip = captureMenu;
        Disposed += (_, _) => captureMenu.Dispose(); headerActions.Controls.Add(newButton); tips.SetToolTip(newButton, "옵션에서 지정한 방식으로 캡처 · 오른쪽 클릭: 캡처 방식 선택");
        var captureOptions = new ChromeButton { ShowDropArrow = true, Width = 24, Height = newButton.Height, BackColor = newButton.BackColor, ForeColor = newButton.ForeColor, Margin = Padding.Empty, AccessibleName = "캡처 방식 선택" };
        captureOptions.Click += (_, _) => captureMenu.Show(newButton, new Point(0, newButton.Height)); headerActions.Controls.Add(captureOptions);
        headerActions.Controls.Add(new Panel { Width = 1, Height = 62, BackColor = Color.FromArgb(196, 210, 235), Margin = new Padding(8, 10, 8, 0) });


        void SetInk(Color chosen)
        {
            currentInk = chosen;
            if (Editor is { } editor)
            {
                editor.Ink = chosen;
                if (editor.Document.Annotations.Find(a => a.Id == editor.Document.Selection) is { } selected)
                { editor.Document.Checkpoint(); selected.Color = chosen; editor.NotifyEdit(); }
            }
            UpdateSwatches(chosen);
        }
        var width = new TrackBar { Minimum = 1, Maximum = 10, Value = 3, TickStyle = TickStyle.None, SmallChange = 1, LargeChange = 1, Width = 112, Height = 28, Margin = Padding.Empty, AccessibleName = "선 굵기" };
        strokeValueLabel = new Label { Text = "3 px", Width = 34, Height = 26, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(71, 85, 105) };
        tips.SetToolTip(width, "선 굵기");
        width.ValueChanged += (_, _) =>
        {
            if (updatingInspector) return;
            float stroke = width.Value;
            currentStroke = stroke;
            if (strokeValueLabel != null) strokeValueLabel.Text = $"{stroke:0} px";
            if (Editor is not { } editor) return;
            editor.Stroke = stroke;
            if (editor.Document.Annotations.Find(a => a.Id == editor.Document.Selection) is { } a)
            { editor.Document.Checkpoint(); a.Width = editor.Stroke; editor.NotifyEdit(); }
        };

        strokeControl = width;

        ribbonPanel = new Panel { Dock = DockStyle.None, BackColor = Color.FromArgb(248, 250, 252), Padding = Padding.Empty, Visible = false };
        var ribbon = new BufferedFlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 0, 4, 0), BackColor = Color.FromArgb(248, 250, 252) };
        ribbonPanel.Controls.Add(ribbon);
        FlowLayoutPanel AddRibbonGroup(string title, int groupWidth, bool inRibbon = true)
        {
            var group = new Panel { Width = groupWidth, Height = 42, Margin = new Padding(0, 0, 6, 0), BackColor = Color.Transparent, AccessibleName = title };
            if (inRibbon && ribbon.Controls.Count > 0)
            {
                var separator = new Panel { Width = 1, Height = 24, BackColor = Color.FromArgb(226, 232, 240), Margin = new Padding(5, 6, 8, 0) };
                ribbon.Controls.Add(separator);
                group.VisibleChanged += (_, _) => separator.Visible = group.Visible;
            }
            var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(2, 1, 2, 0), BackColor = Color.Transparent };
            var caption = new Label { Text = title, Dock = DockStyle.Bottom, Height = 18, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 8) };
            group.Controls.Add(content); caption.Dispose(); if (inRibbon) ribbon.Controls.Add(group); return content;
        }
        var toolbarLabelFont = new Font("맑은 고딕", 9f, FontStyle.Bold);
        var zoomFont = new Font("Segoe UI", 11f);
        Disposed += (_, _) => { toolbarLabelFont.Dispose(); zoomFont.Dispose(); };
        Button RibbonButton(string title, string icon, Action action, AnnotationKind? kind = null)
        {
            var button = new ChromeButton { Text = title, IconKey = icon switch { "↖" => "select", "↗" => "arrow", "□" when title == "맞춤" => "fit", "□" => "rectangle", "ellipse" => "ellipse", "rounded-rectangle" => "rounded-rectangle", "line" => "line", "triangle" => "triangle", "T" => "text", "①" => "number", "▰" => "highlight", "−" => "minus", "+" => "plus", _ => "copy" }, IconAboveText = true, IconSize = 24, Width = 60, Height = 76, BackColor = Color.White, ForeColor = Color.FromArgb(51, 65, 85), Font = toolbarLabelFont, Margin = new Padding(1, 0, 1, 0), AccessibleName = title, Tag = kind };
            button.Click += (_, _) => { toolOptionsDropDown?.Close(); Editor?.FinishTextEdit(); Editor?.CancelCrop(); bool selectedShape = Editor?.Selected?.Kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.RoundedRectangle or AnnotationKind.Line or AnnotationKind.Arrow or AnnotationKind.Triangle; if (title != "도형" || !selectedShape) { action(); if (Editor != null) Editor.Document.Selection = null; } RefreshStatus(); };
            tips.SetToolTip(button, kind == AnnotationKind.Number ? "클릭한 위치에 번호를 추가하고 선택 도구로 돌아옵니다." : title);
            if (kind.HasValue || title == "선택") ribbonToolButtons.Add((button, kind));
            return button;
        }
        var editGroup = AddRibbonGroup("주석", 530);
        var splitOptionButtons = new List<Button>();
        AnnotationKind popupKind = AnnotationKind.Rectangle;
        var shapeButton = RibbonButton("도형", "□", () => { if (Editor != null) Editor.Tool = currentShape; }, AnnotationKind.Rectangle);
        ((ChromeButton)shapeButton).IconKey = "shape"; ((ChromeButton)shapeButton).ShowDropArrow = true;
        shapeToolButton = shapeButton; editGroup.Controls.Add(shapeButton);
        var penButton = RibbonButton("펜", "line", () => { if (Editor != null) Editor.Tool = AnnotationKind.Pen; }, AnnotationKind.Pen);
        ((ChromeButton)penButton).IconKey = "pen";
        editGroup.Controls.Add(penButton);
        var highlightButton = RibbonButton("형광펜", "▰", () => { if (Editor != null) Editor.Tool = AnnotationKind.Highlight; }, AnnotationKind.Highlight);
        editGroup.Controls.Add(highlightButton);
        editGroup.Controls.Add(RibbonButton("텍스트", "T", () => { if (Editor != null) Editor.Tool = AnnotationKind.Text; }, AnnotationKind.Text));
        editGroup.Controls.Add(RibbonButton("번호", "①", () => { if (Editor != null) Editor.Tool = AnnotationKind.Number; }, AnnotationKind.Number));
        var cropTool = new ChromeButton { Text = "자르기", IconKey = "crop", IconAboveText = true, Width = 60, Height = 76, BackColor = Color.White, Margin = new Padding(1, 0, 1, 0), AccessibleName = "선택 자르기" };
        var effectsGroup = AddRibbonGroup("효과", 140);
        cropTool.Click += (_, _) => Editor?.StartCrop();
        tips.SetToolTip(cropTool, "선택 자르기 · 드래그 후 Enter로 적용, Esc로 취소");
        var mosaicTool = new ChromeButton { Text = "모자이크", IconKey = "mosaic", IconAboveText = true, Width = 60, Height = 76, AccessibleName = "모자이크" };
        var blurTool = new ChromeButton { Text = "블러", IconKey = "blur", IconAboveText = true, Width = 56, Height = 76, AccessibleName = "블러" };
        mosaicTool.Click += (_, _) => Editor?.StartPixelEffect(PixelEffectKind.Mosaic); effectsGroup.Controls.Add(mosaicTool);
        blurTool.Click += (_, _) => Editor?.StartPixelEffect(PixelEffectKind.Blur); effectsGroup.Controls.Add(blurTool);
        var cropGroup = AddRibbonGroup("이미지 편집", 140);
        var rotateTool = new ChromeButton { Text = "90° 회전", IconKey = "rotate-right", IconAboveText = true, Width = 66, Height = 76, BackColor = Color.White, AccessibleName = "오른쪽으로 90도 회전" };
        rotateTool.Click += (_, _) => RotateImageClockwise(); cropGroup.Controls.Add(rotateTool); documentActions.Add(rotateTool);
        tips.SetToolTip(rotateTool, "이미지와 주석을 오른쪽으로 90° 회전");
        cropGroup.Controls.Add(cropTool);
        var resizeTool = new ChromeButton { Text = "크기 조절", IconKey = "resize", IconAboveText = true, Width = 64, Height = 76, BackColor = Color.White, AccessibleName = "이미지 크기 조절" };
        resizeTool.Click += (_, _) => ResizeImage(); cropGroup.Controls.Add(resizeTool); documentActions.Add(resizeTool);
        tips.SetToolTip(mosaicTool, "기본 24px 블록 크기. 영역을 큰 네모 픽셀 타일로 가립니다.");
        tips.SetToolTip(blurTool, "흐림 반경을 조절해 선택 영역의 세부 묘사를 부드럽게 흐립니다.");
        var regionGroup = AddRibbonGroup("이미지 영역", 350);
        var regionCopy = new ChromeButton { Text = "영역 복사", IconKey = "region-copy", IconAboveText = true, Width = 60, Height = 76, AccessibleName = "선택한 이미지 영역 복사" };
        regionCopy.Click += (_, _) => Editor?.StartCrop(true); regionGroup.Controls.Add(regionCopy);
        var regionPaste = new ChromeButton { Text = "붙여넣기", IconKey = "paste", IconAboveText = true, Width = 60, Height = 76, AccessibleName = "이미지 붙여넣기" };
        regionPaste.Click += (_, _) => PasteImage(); regionGroup.Controls.Add(regionPaste);
        var clipboardToggle = new ChromeButton { Text = "클립보드", IconKey = "paste", IconAboveText = true, Width = 60, Height = 76, AccessibleName = "복사한 영역 목록 열기" };
        clipboardToggle.Click += (_, _) => { regionClipboard.Visible = !regionClipboard.Visible; relayout?.Invoke(); };
        regionGroup.Controls.Add(clipboardToggle);
        tips.SetToolTip(regionCopy, "드래그를 놓으면 영역 자동 복사 · Esc: 취소");
        tips.SetToolTip(regionPaste, "클립보드 이미지를 이동 가능한 객체로 붙여넣기 (Ctrl+V)");
        var colorGroup = AddRibbonGroup("색상", 240, false);
        Color[] paletteColors =
        [
            Color.FromArgb(15, 15, 15), Color.FromArgb(112, 112, 112), Color.FromArgb(180, 0, 40), Color.FromArgb(239, 44, 54), Color.FromArgb(245, 130, 32), Color.FromArgb(255, 231, 0),
            Color.White, Color.FromArgb(194, 194, 194), Color.FromArgb(177, 111, 76), Color.FromArgb(235, 151, 172), Color.FromArgb(239, 177, 103), Color.FromArgb(237, 210, 132)
        ];
        foreach (var swatch in paletteColors)
        {
            var chip = new ColorSwatch { Width = 21, Height = 21, SwatchColor = swatch, BackColor = Color.FromArgb(248, 250, 252), Margin = new Padding(2), AccessibleName = $"색상 #{swatch.R:X2}{swatch.G:X2}{swatch.B:X2}" };
            chip.Click += (_, _) => SetInk(swatch); colorGroup.Controls.Add(chip); swatches.Add(chip);
        }
        var customColor = new ChromeButton { Text = "", IconKey = "color-wheel", Width = 36, Height = 36, IconSize = 36, BackColor = Color.White, ForeColor = Color.FromArgb(37, 99, 235), Margin = Padding.Empty, AccessibleName = "사용자 지정 색상" };
        customColor.Click += (_, _) =>
        {
            var popup = toolOptionsDropDown;
            if (popup != null) popup.AutoClose = false;
            try { using var picker = new ColorDialog { Color = Editor?.Ink ?? currentInk, FullOpen = true }; if (picker.ShowDialog(this) == DialogResult.OK) SetInk(picker.Color); }
            finally { if (popup is { IsDisposed: false }) popup.AutoClose = true; }
        };
        colorGroup.Parent!.Controls.Add(customColor);
        customColor.SetBounds(158, 20, 36, 36);
        var properties = new FlowLayoutPanel { Size = new Size(340, 340), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16, 12, 16, 12), BackColor = Color.White };
        Label SectionTitle(string text) => new() { Text = text, AutoSize = false, Width = 292, Height = 20, Margin = new Padding(0, 2, 0, 3), ForeColor = Color.FromArgb(31, 41, 55), Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        var shapeTitle = SectionTitle("도형 종류");
        var shapeRow = new FlowLayoutPanel { Width = 292, Height = 46, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 8), Padding = Padding.Empty, BackColor = Color.White };
        Panel PopupSeparator() => new() { Width = 292, Height = 1, Margin = new Padding(0, 1, 0, 6), BackColor = Color.FromArgb(226, 232, 240) };
        var shapeSeparator = PopupSeparator();
        var shapeChoices = new Dictionary<AnnotationKind, ChromeButton>
        {
            [AnnotationKind.Rectangle] = ShapeChoice("rectangle", "사각형", AnnotationKind.Rectangle),
            [AnnotationKind.Ellipse] = ShapeChoice("ellipse", "원", AnnotationKind.Ellipse),
            [AnnotationKind.RoundedRectangle] = ShapeChoice("rounded-rectangle", "둥근 사각형", AnnotationKind.RoundedRectangle),
            [AnnotationKind.Line] = ShapeChoice("line", "직선", AnnotationKind.Line),
            [AnnotationKind.Arrow] = ShapeChoice("arrow", "화살표", AnnotationKind.Arrow),
            [AnnotationKind.Triangle] = ShapeChoice("triangle", "삼각형", AnnotationKind.Triangle)
        };
        ChromeButton ShapeChoice(string icon, string label, AnnotationKind shape)
        {
            var choice = new ChromeButton { IconKey = icon, Width = 38, Height = 38, Margin = new Padding(0, 0, 8, 0), BackColor = Color.White, AccessibleName = label + " 도형" };
            choice.Click += (_, _) =>
            {
                currentShape = shape;
                popupKind = shape;
                if (Editor is { } editor)
                {
                    editor.FinishTextEdit();
                    if (editor.Selected is { } selected && selected.Kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.RoundedRectangle or AnnotationKind.Line or AnnotationKind.Arrow or AnnotationKind.Triangle)
                    {
                        if (selected.Kind != shape) { editor.Document.Checkpoint(); selected.Kind = shape; editor.NotifyEdit(); }
                    }
                    else { editor.Document.Selection = null; editor.Tool = shape; }
                }
                UpdateRibbonSelection(); syncProperties?.Invoke();
            };
            tips.SetToolTip(choice, label);
            return choice;
        }
        shapeRow.Controls.AddRange(shapeChoices.Values.Cast<Control>().ToArray());
        colorGroup.Parent!.Width = 202; colorGroup.Parent.Height = 76; colorGroup.Parent.Padding = Padding.Empty;
        colorGroup.Dock = DockStyle.None; colorGroup.SetBounds(0, 0, 150, 76); colorGroup.WrapContents = true; colorGroup.Padding = Padding.Empty;
        var thicknessTitle = SectionTitle("선 굵기");
        var fill = new CheckBox { Text = "채우기", AutoSize = true, Margin = new Padding(8, 7, 8, 0) };
        var dashed = new CheckBox { Text = "점선", AutoSize = true, Margin = new Padding(8, 7, 8, 0) };
        var fillSwatch = new ColorSwatch { Width = 40, Height = 32, Margin = new Padding(8, 1, 0, 0), SwatchColor = Color.FromArgb(55, 239, 68, 68), IsFillPicker = true, AccessibleName = "채우기 색상" };
        fillSwatch.Click += (_, _) =>
        {
            var popup = toolOptionsDropDown;
            if (popup != null) popup.AutoClose = false;
            try
            {
                using var picker = new ColorDialog { Color = Editor?.Selected?.FillColor ?? Editor?.FillColor ?? currentFillColor ?? currentInk, FullOpen = true };
                if (picker.ShowDialog(this) == DialogResult.OK) { currentFillColor = picker.Color; Editor?.SetFillColor(picker.Color); fillSwatch.SwatchColor = Color.FromArgb(55, picker.Color); fillSwatch.Invalidate(); }
            }
            finally { if (popup is { IsDisposed: false }) popup.AutoClose = true; }
        };
        var fontSize = new NumericUpDown { Minimum = 8, Maximum = 144, Value = 24, Width = 62, Margin = new Padding(4, 4, 6, 0) };
        var bold = new CheckBox { Text = "굵게", AutoSize = true, Margin = new Padding(6, 7, 12, 0) };
        var fontSizeLabel = new Label { Text = "글자 크기", AutoSize = true, Margin = new Padding(12, 7, 2, 0) };
        var shapePanel = new FlowLayoutPanel { Size = new Size(324, 94), FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12, 8, 0, 0), BackColor = Color.White };
        shapePanel.Controls.AddRange([shapeTitle, shapeRow]);
        var shapePopup = new ToolStripDropDown { Padding = Padding.Empty, AutoSize = false, Size = shapePanel.Size };
        shapeOptionsDropDown = shapePopup;
        shapePopup.Items.Add(new ToolStripControlHost(shapePanel) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = shapePanel.Size });
        shapeButton.Click += (_, _) => { popupKind = currentShape; syncProperties?.Invoke(); shapeTitle.Visible = shapeRow.Visible = true; shapePopup.Show(shapeButton, new Point(0, shapeButton.Height)); };
        foreach (var choice in shapeChoices.Values) choice.Click += (_, _) => shapePopup.Close();
        Disposed += (_, _) => shapePopup.Dispose();
        selectedLabel = new Label { Text = "선택", Width = 70, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Visible = false };
        properties.Controls.Add(selectedLabel);
        var cropApply = new ChromeButton { Text = "자르기 적용", IconKey = "check", Primary = true, Width = 138, Height = 32, Visible = false };
        cropApply.Click += (_, _) => { if (Editor?.RegionCopyMode == true) CopyRegion(); else ApplyCrop(); };
        var cropCancel = new ChromeButton { Text = "취소", IconKey = "close", Width = 84, Height = 32, BackColor = Color.White, Visible = false };
        cropCancel.Click += (_, _) => Editor?.CancelCrop();
        var cropSize = new Label { AutoSize = true, Visible = false, Margin = new Padding(8, 7, 0, 0) };
        var effectLabel = new Label { Text = "효과 크기 (px)", AutoSize = true, Visible = false, Margin = new Padding(6, 7, 0, 0) };
        string EffectDescription(PixelEffectKind kind, int amount) => kind == PixelEffectKind.Mosaic
            ? $"네모 블록으로 가립니다 · 블록 {amount}px · 드래그 후 놓으면 적용 / Esc 취소"
            : $"주변 색을 섞어 부드럽게 흐립니다 · 반경 {amount}px · 드래그 후 놓으면 적용 / Esc 취소";
        var effectStrength = new NumericUpDown { Minimum = 2, Maximum = 64, Value = 12, Width = 62, Visible = false, Margin = new Padding(4, 4, 8, 0), AccessibleName = "효과 크기" };
        effectStrength.ValueChanged += (_, _) => { if (!updatingInspector) { Editor?.SetEffectStrength((int)effectStrength.Value); if (Editor?.PixelEffect is { } kind) cropSize.Text = EffectDescription(kind, (int)effectStrength.Value); } };
        effectStrength.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Editor?.CancelCrop(); Editor?.Focus(); }
        };
        void ApplyStyle()
        {
            if (updatingInspector) return;
            currentFilled = fill.Checked; currentDashed = dashed.Checked; currentTextSize = (float)fontSize.Value; currentBold = bold.Checked;
            Editor?.SetStyleOptions(currentFilled, currentDashed, currentTextSize, currentBold);
        }
        fill.CheckedChanged += (_, _) => { fillSwatch.Enabled = fill.Checked; ApplyStyle(); }; dashed.CheckedChanged += (_, _) => ApplyStyle();
        fontSize.ValueChanged += (_, _) => ApplyStyle(); bold.CheckedChanged += (_, _) => ApplyStyle();
        var duplicate = new ChromeButton { Text = "복제", IconKey = "copy", Width = 80, Height = 32, BackColor = Color.White };
        var delete = new ChromeButton { Text = "삭제", IconKey = "delete", Width = 84, Height = 32, BackColor = Color.White };
        var help = new Label { AutoSize = true, ForeColor = Color.SlateGray, Margin = new Padding(14, 7, 0, 0) };
        var propertyPanel = new Panel { Size = new Size(320, 88), BackColor = Color.White, Visible = false, Margin = new Padding(0, 0, 6, 0), AccessibleName = "주석 속성" };
        var propertyPalette = colorGroup.Parent!;
        propertyPalette.SetBounds(0, 0, 316, 48);
        colorGroup.SetBounds(8, 1, 300, 46);
        propertyPanel.Controls.Add(propertyPalette);
        thicknessTitle.SetBounds(4, 53, 48, 25); thicknessTitle.Margin = Padding.Empty; thicknessTitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        width.SetBounds(48, 51, 112, 30); width.Margin = Padding.Empty;
        strokeValueLabel!.SetBounds(160, 53, 34, 25);
        fill.SetBounds(196, 53, 58, 25); fill.Margin = Padding.Empty;
        fillSwatch.SetBounds(254, 51, 27, 27); fillSwatch.Margin = Padding.Empty;
        dashed.SetBounds(282, 53, 42, 25); dashed.Margin = Padding.Empty;
        fontSizeLabel.SetBounds(4, 53, 65, 25); fontSizeLabel.Margin = Padding.Empty;
        fontSize.SetBounds(68, 51, 58, 27); fontSize.Margin = Padding.Empty;
        bold.SetBounds(132, 53, 55, 25); bold.Margin = Padding.Empty;
        propertyPanel.Controls.AddRange([thicknessTitle, width, strokeValueLabel, fill, fillSwatch, dashed, fontSizeLabel, fontSize, bold]);
        var colorTitle = SectionTitle("선 색상");
        propertyPalette.Width = 292; propertyPalette.Height = 44; propertyPalette.Margin = Padding.Empty;
        colorGroup.WrapContents = false;
        var thicknessRow = new FlowLayoutPanel { Width = 292, Height = 42, WrapContents = false, Margin = Padding.Empty };
        thicknessTitle.Width = 130; thicknessTitle.Height = 32; thicknessTitle.Text = "선 두께";
        var widthChoice = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "선 두께" };
        widthChoice.Items.AddRange(["1 px", "2 px", "3 px", "5 px", "8 px", "10 px"]); widthChoice.SelectedIndex = 2;
        widthChoice.SelectedIndexChanged += (_, _) => { if (!updatingInspector && widthChoice.SelectedItem is string value && int.TryParse(value.Replace(" px", ""), out int chosen)) width.Value = chosen; };
        thicknessRow.Controls.Add(thicknessTitle); thicknessRow.Controls.Add(widthChoice);
        var fillTitle = SectionTitle("채우기");
        var fillSeparator = PopupSeparator();
        var fillRow = new FlowLayoutPanel { Width = 292, Height = 42, WrapContents = false, Margin = Padding.Empty };
        fill.Text = "도형 내부 채우기"; fill.AutoSize = true; fill.Margin = new Padding(0, 6, 12, 0);
        fillSwatch.Size = new Size(88, 32); fillSwatch.Margin = new Padding(0, 2, 0, 0); fillRow.Controls.Add(fill); fillRow.Controls.Add(fillSwatch);
        var fontRow = new FlowLayoutPanel { Width = 292, Height = 36, WrapContents = false, Margin = Padding.Empty };
        fontSizeLabel.Margin = new Padding(0, 6, 4, 0); fontSize.Margin = new Padding(2, 2, 8, 0); bold.Margin = new Padding(4, 6, 0, 0);
        fontRow.Controls.Add(fontSizeLabel); fontRow.Controls.Add(fontSize); fontRow.Controls.Add(bold);
        properties.Controls.AddRange([thicknessRow, fillSeparator, fillTitle, fillRow, fontRow]);
        propertyPalette.Parent?.Controls.Remove(propertyPalette);
        propertyPalette.Size = new Size(202, 76); propertyPalette.Margin = new Padding(4, 0, 4, 0);
        colorGroup.WrapContents = true; colorGroup.Padding = Padding.Empty;
        var sizeGroup = AddRibbonGroup("크기", 66, false);
        var sizeButton = new ChromeButton { Text = "크기", IconKey = "line-width", IconAboveText = true, ShowDropArrow = true, IconSize = 28, Width = 58, Height = 76, Font = toolbarLabelFont, AccessibleName = "선 크기" };
        sizeGroup.Controls.Add(sizeButton);
        var sizeMenu = new ContextMenuStrip();
        sizeOptionsMenu = sizeMenu;
        sizeMenu.Opening += (_, e) =>
        {
            bool textMode = (Editor?.Selected?.Kind ?? Editor?.Tool) == AnnotationKind.Text;
            int[] choices = textMode ? [8, 10, 12, 14, 16, 18, 24, 32, 48, 64, 96, 144] : [1, 2, 3, 5, 8, 10];
            float activeSize = textMode ? Editor?.Selected?.FontSize ?? Editor?.TextSize ?? currentTextSize : width.Value;
            foreach (ToolStripItem oldItem in sizeMenu.Items.Cast<ToolStripItem>().ToArray()) oldItem.Dispose();
            sizeMenu.Items.Clear();
            foreach (int choice in choices)
            {
                var item = new ToolStripMenuItem($"{choice} px") { Checked = Math.Abs(activeSize - choice) < .5f };
                item.Click += (_, _) =>
                {
                    if (!textMode) { width.Value = choice; return; }
                    currentTextSize = choice;
                    updatingInspector = true; try { fontSize.Value = choice; } finally { updatingInspector = false; }
                    Editor?.SetTextSize(choice); RefreshStatus();
                };
                sizeMenu.Items.Add(item);
            }
            e.Cancel = false;
        };
        sizeButton.Click += (_, _) => sizeMenu.Show(sizeButton, new Point(0, sizeButton.Height));
        Disposed += (_, _) => sizeMenu.Dispose();
        Disposed += (_, _) => propertyPanel.Dispose();
        syncProperties = () =>
        {
            properties.SuspendLayout();
            ribbon.SuspendLayout();
            try
            {
            cropTool.Selected = Editor?.CropMode == true && !Editor.RegionCopyMode && Editor.PixelEffect == null;
            propertyPanel.Visible = false;
            mosaicTool.Selected = Editor?.PixelEffect == PixelEffectKind.Mosaic;
            blurTool.Selected = Editor?.PixelEffect == PixelEffectKind.Blur;
            regionCopy.Selected = Editor?.RegionCopyMode == true;
            cropApply.Text = Editor?.RegionCopyMode == true ? "영역 복사" : "자르기 적용";
            bool cropping = Editor?.CropMode == true;
            selectedLabel.Visible = false;
            cropApply.Visible = cropCancel.Visible = cropping;
            cropSize.Visible = cropping || Editor?.PixelEffect != null;
            if (Editor?.RegionCopyMode == true) cropApply.Visible = false;
            effectLabel.Visible = effectStrength.Visible = Editor?.PixelEffect != null;
            if (Editor?.PixelEffect is { } activeEffect)
            {
                effectLabel.Text = activeEffect == PixelEffectKind.Mosaic ? "블록 크기 (px)" : "흐림 반경 (px)";
                effectStrength.AccessibleName = activeEffect == PixelEffectKind.Mosaic ? "모자이크 블록 크기" : "블러 흐림 반경";
                decimal minimumStrength = activeEffect == PixelEffectKind.Mosaic ? 8 : 2;
                updatingInspector = true;
                try { if (effectStrength.Value < minimumStrength) effectStrength.Value = minimumStrength; effectStrength.Minimum = minimumStrength; effectStrength.Value = Editor.EffectStrength; }
                finally { updatingInspector = false; }
            }
            cropApply.Enabled = Editor?.CanApplyCrop == true;
            cropSize.Text = Editor?.PixelEffect is { } cropEffect
                ? EffectDescription(cropEffect, Editor.EffectStrength)
                : Editor?.RegionCopyMode == true
                ? (Editor.CanApplyCrop ? $"{Editor.CropBounds.Width} × {Editor.CropBounds.Height} px · 놓으면 자동 복사 / Esc 취소" : "복사할 영역을 드래그하세요 · 놓으면 자동 복사 / Esc 취소")
                : Editor?.CanApplyCrop == true ? $"{Editor.CropBounds.Width} × {Editor.CropBounds.Height} px · Enter 적용 / Esc 취소" : "영역을 드래그하세요 · Esc 취소";
            if (Editor is not { } ed)
            {
                propertyPalette.Enabled = true;
                bool popupIsShape = popupKind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.RoundedRectangle or AnnotationKind.Line or AnnotationKind.Arrow or AnnotationKind.Triangle;
                bool popupCanFill = popupKind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.RoundedRectangle or AnnotationKind.Triangle;
                shapeTitle.Visible = shapeRow.Visible = shapeSeparator.Visible = popupIsShape;
                foreach (var (shape, choice) in shapeChoices) choice.Selected = shape == currentShape;
                propertyPalette.Visible = thicknessRow.Visible = true;
                fillSeparator.Visible = fillTitle.Visible = fillRow.Visible = fill.Visible = fillSwatch.Visible = popupCanFill;
                thicknessTitle.Visible = widthChoice.Visible = true;
                dashed.Visible = fontSizeLabel.Visible = fontSize.Visible = bold.Visible = fontRow.Visible = false;
                UpdateSwatches(currentInk);
                updatingInspector = true;
                try { width.Value = Math.Clamp((int)Math.Round(currentStroke), width.Minimum, width.Maximum); fill.Checked = currentFilled; dashed.Checked = currentDashed; fontSize.Value = Math.Clamp((decimal)currentTextSize, 8, 144); bold.Checked = currentBold; var sizes = new[] { 1, 2, 3, 5, 8, 10 }; widthChoice.SelectedIndex = Array.IndexOf(sizes, sizes.OrderBy(size => Math.Abs(size - currentStroke)).First()); }
                finally { updatingInspector = false; }
                fillSwatch.Enabled = fill.Checked;
                strokeValueLabel.Text = $"{width.Value} px";
                return;
            }
            var a = ed.Selected;
            var kind = a?.Kind ?? ed.Tool;
            bool isShape = !cropping && kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.RoundedRectangle or AnnotationKind.Line or AnnotationKind.Arrow or AnnotationKind.Triangle;
            shapeTitle.Visible = shapeRow.Visible = shapeSeparator.Visible = isShape;
            foreach (var (shape, choice) in shapeChoices) choice.Selected = kind == shape;
            bool showFill = !cropping && kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.RoundedRectangle or AnnotationKind.Triangle;
            fill.Visible = fillSwatch.Visible = showFill;
            fillSeparator.Visible = fillTitle.Visible = fillRow.Visible = showFill;
            dashed.Visible = !cropping && kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.RoundedRectangle or AnnotationKind.Line or AnnotationKind.Arrow or AnnotationKind.Triangle;
            fontSizeLabel.Visible = fontSize.Visible = bold.Visible = fontRow.Visible = !cropping && kind == AnnotationKind.Text;
            duplicate.Visible = delete.Visible = !cropping && a != null;
            bool hasStroke = !cropping && (kind == null || kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.RoundedRectangle or AnnotationKind.Line or AnnotationKind.Arrow or AnnotationKind.Triangle or AnnotationKind.Highlight or AnnotationKind.Pen);
            thicknessTitle.Visible = widthChoice.Visible = thicknessRow.Visible = hasStroke;
            bool hasColor = !cropping && kind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.RoundedRectangle or AnnotationKind.Line or AnnotationKind.Arrow or AnnotationKind.Triangle or AnnotationKind.Text or AnnotationKind.Number or AnnotationKind.Highlight or AnnotationKind.Pen;
            propertyPalette.Visible = true; propertyPalette.Enabled = (hasColor || kind == null) && ed.PixelEffect == null && !ed.RegionCopyMode;
            help.Visible = false;
            help.Text = a?.Kind is AnnotationKind.Text or AnnotationKind.Number ? "더블클릭: 수정 · Delete: 삭제"
                : a != null ? "드래그: 이동 · 핸들: 크기 조절"
                : kind == AnnotationKind.Number ? "클릭한 위치에 번호를 추가합니다."
                : kind == AnnotationKind.Text ? "클릭: 텍스트 입력 · Ctrl+Enter: 확정"
                : kind != null ? "드래그로 주석 추가 · Esc: 선택" : "객체를 클릭해 선택하거나 편집 도구를 고르세요";
            updatingInspector = true;
            try { fill.Checked = a?.Filled ?? ed.Filled; dashed.Checked = a?.Dashed ?? ed.Dashed; fontSize.Value = Math.Clamp((decimal)(a?.FontSize ?? ed.TextSize), 8, 144); bold.Checked = a?.Bold ?? ed.Bold; var sizes = new[] { 1, 2, 3, 5, 8, 10 }; var stroke = a?.Width ?? ed.Stroke; widthChoice.SelectedIndex = Array.IndexOf(sizes, sizes.OrderBy(size => Math.Abs(size - stroke)).First()); }
            finally { updatingInspector = false; }
            fillSwatch.Enabled = fill.Checked;
            fillSwatch.SwatchColor = Color.FromArgb(55, a?.FillColor ?? ed.FillColor ?? a?.Color ?? ed.Ink); fillSwatch.Invalidate();
            }
            finally
            {
                properties.ResumeLayout(true);
                ribbon.ResumeLayout(true);
            }
        };
        duplicate.Click += (_, _) => Editor?.DuplicateSelection(); properties.Controls.Add(duplicate);
        tips.SetToolTip(duplicate, "선택 객체 복제 (Ctrl+D)");
        delete.Click += (_, _) => Editor?.DeleteSelection(); properties.Controls.Add(delete);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add("현재 도구 속성", null, (_, _) => ShowToolOptions(shapeButton));
        var dashedItem = new ToolStripMenuItem("점선") { CheckOnClick = true };
        dashedItem.CheckedChanged += (_, _) => dashed.Checked = dashedItem.Checked;
        editMenu.DropDownOpening += (_, _) => dashedItem.Checked = dashed.Checked;
        editMenu.DropDownItems.Add(dashedItem);
        editMenu.DropDownItems.Add("선택 객체 복제", null, (_, _) => Editor?.DuplicateSelection());
        editMenu.DropDownItems.Add("선택 객체 삭제", null, (_, _) => Editor?.DeleteSelection());
        editMenu.DropDownItems.Add("클립보드 목록", null, (_, _) => { regionClipboard.Visible = !regionClipboard.Visible; relayout?.Invoke(); });
        properties.Controls.Add(help);
        properties.Controls.AddRange([cropApply, cropCancel, effectLabel, effectStrength, cropSize]);

        var toolOptions = new ToolStripDropDown { AutoSize = false, Size = new Size(properties.Width + 2, properties.Height + 2), Padding = new Padding(1), BackColor = Color.FromArgb(223, 230, 240), DropShadowEnabled = true };
        var popupOutline = new System.Drawing.Drawing2D.GraphicsPath();
        int popupWidth = toolOptions.Width - 1, popupHeight = toolOptions.Height - 1, corner = 20;
        popupOutline.AddArc(0, 0, corner, corner, 180, 90); popupOutline.AddArc(popupWidth - corner, 0, corner, corner, 270, 90);
        popupOutline.AddArc(popupWidth - corner, popupHeight - corner, corner, corner, 0, 90); popupOutline.AddArc(0, popupHeight - corner, corner, corner, 90, 90); popupOutline.CloseFigure();
        var popupRegion = new Region(popupOutline); toolOptions.Region = popupRegion;
        toolOptions.Paint += (_, e) => { using var border = new Pen(Color.FromArgb(203, 213, 225)); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; e.Graphics.DrawPath(border, popupOutline); };
        toolOptions.Disposed += (_, _) => { popupRegion.Dispose(); popupOutline.Dispose(); };
        toolOptionsDropDown = toolOptions;
        var optionsHost = new ToolStripControlHost(properties) { AutoSize = false, Size = properties.Size, Margin = Padding.Empty, Padding = Padding.Empty };
        toolOptions.Items.Add(optionsHost);
        void ShowToolOptions(Control anchor)
        {
            if (toolOptionsDropDown is not { } dropdown) return;
            if (dropdown.Visible) { dropdown.Close(); return; }
            var editor = Editor;
            var parent = editor?.Parent as Panel;
            var editorLocation = editor?.Location;
            var scroll = parent?.AutoScrollPosition ?? Point.Empty;
            syncProperties?.Invoke();
            dropdown.Show(anchor, new Point(0, anchor.Height));
            void RestoreViewport()
            {
                if (editor is null || editor.IsDisposed || parent is null || editorLocation is null) return;
                parent.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
                editor.Location = editorLocation.Value;
            }
            RestoreViewport();
            if (IsHandleCreated) BeginInvoke(new Action(RestoreViewport));
        }
        var hint = new Label { Text = "도구를 고르고 캔버스에서 드래그해 주석을 추가하세요.", Dock = DockStyle.None, Height = 28, ForeColor = Color.FromArgb(100, 116, 139), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8) };
        var workspace = new Panel { BackColor = Color.FromArgb(200, 224, 252) };
        var centerHost = new Panel { Dock = DockStyle.None, BackColor = Color.White };
        var welcome = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(241, 245, 249) };
        var welcomeContent = new Panel { Size = new Size(640, 142), BackColor = Color.Transparent };
        var welcomeTitle = new Label { Text = "캡처하고, 바로 편집하세요", Font = new Font("Segoe UI Semibold", 19, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), TextAlign = ContentAlignment.MiddleCenter };
        var welcomeSubtitle = new Label { Text = "Capture · Edit · Share   |   화면을 캡처하고, 편집해 공유하세요.", Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(100, 116, 139), TextAlign = ContentAlignment.MiddleCenter };
        welcomeTitle.SetBounds(0, 0, welcomeContent.Width, 48);
        welcomeSubtitle.SetBounds(0, 48, welcomeContent.Width, 36);
        welcomeContent.Controls.Add(welcomeTitle);
        welcomeContent.Controls.Add(welcomeSubtitle);
        var welcomeButtonHost = new Panel { Location = new Point(0, 84), Size = new Size(welcomeContent.Width, 58) };
        var welcomeButtons = new FlowLayoutPanel { Width = 420, Height = 46, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty };
        var welcomeRegion = HeaderButton("영역 캡처", true); welcomeRegion.Width = 132; welcomeRegion.Click += (_, _) => _ = CaptureAsync(true);
        var welcomeFull = HeaderButton("전체 화면", false); welcomeFull.Width = 132; welcomeFull.Click += (_, _) => _ = CaptureAsync(false);
        var welcomeOpen = HeaderButton("이미지 열기", false); welcomeOpen.Width = 132; welcomeOpen.Click += (_, _) => OpenImage();
        welcomeButtons.Controls.AddRange([welcomeRegion, welcomeFull, welcomeOpen]); welcomeButtonHost.Controls.Add(welcomeButtons);
        welcomeContent.Controls.Add(welcomeButtonHost);
        void CenterWelcomeContent()
        {
            int width = Math.Min(640, Math.Max(0, welcome.ClientSize.Width - 32));
            welcomeContent.Size = new Size(width, 142);
            welcomeTitle.SetBounds(0, 0, width, 48);
            welcomeSubtitle.SetBounds(0, 48, width, 36);
            welcomeButtonHost.SetBounds(0, 84, width, 58);
            welcomeButtons.Location = new Point(Math.Max(0, (width - welcomeButtons.Width) / 2), 8);
            welcomeContent.Location = new Point((welcome.ClientSize.Width - width) / 2, (welcome.ClientSize.Height - welcomeContent.Height) / 2);
        }
        welcome.Controls.Add(welcomeContent);
        welcome.Resize += (_, _) => CenterWelcomeContent();
        CenterWelcomeContent();
        homePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(235, 241, 248) };
        homePanel.Controls.Add(welcome); centerHost.Controls.Add(tabs); centerHost.Controls.Add(homePanel);
        var tabStrip = new DocumentTabStrip(tabs) { Visible = false };
        tabStrip.CloseRequested += (_, index) => CloseTab(index);
        tabStrip.CaptureRequested += (_, _) => _ = CaptureAsync(settings.NewCaptureSelectRegion);
        centerHost.Controls.Add(tabStrip); tabStrip.BringToFront();
        void PositionPlusTab()
        {
            tabStrip.SetBounds(0, 0, centerHost.ClientSize.Width, tabStrip.PreferredHeight);
            tabStrip.Visible = tabs.TabCount > 0;
            tabStrip.BringToFront();
        }
        centerHost.Resize += (_, _) => PositionPlusTab();
        tabs.ControlAdded += (_, _) => BeginInvoke(new Action(PositionPlusTab));
        tabs.ControlRemoved += (_, _) => BeginInvoke(new Action(PositionPlusTab));
        tabs.Visible = false;
        workspace.Controls.Add(centerHost);
        workspace.Controls.Add(regionClipboard);
        regionClipboard.Dismissed += (_, _) => relayout?.Invoke();
        regionClipboard.ContentsChanged += (_, _) => relayout?.Invoke();
        regionClipboard.PasteRequested += (_, _) => Guard(() =>
        {
            if (Editor is not { } editor || regionClipboard.Snapshot is not { } image) return;
            editor.FinishTextEdit();
            if (clipboard.PasteImage(editor.Document, image))
            { editor.CancelCrop(); editor.Tool = null; editor.NotifyEdit(); RefreshStatus(); }
        });
        Bitmap StatusIcon(string key, int slotSize, int glyphSize)
        {
            const int renderScale = 4;
            int size = slotSize * renderScale;
            int glyph = glyphSize * renderScale;
            var bitmap = new Bitmap(size, size);
            using var graphics = Graphics.FromImage(bitmap);
            ChromeButton.DrawIcon(graphics, key, new Rectangle((size - glyph) / 2, (size - glyph) / 2, glyph, glyph), Color.FromArgb(30, 41, 59), 1.5f);
            return bitmap;
        }
        var infoIcon = StatusIcon("image", 20, 18); var zoomDownIcon = StatusIcon("zoom-minus", 20, 20); var zoomUpIcon = StatusIcon("zoom-plus", 20, 20); var fullScreenIcon = StatusIcon("fullscreen", 22, 20);
        Disposed += (_, _) => { infoIcon.Dispose(); zoomDownIcon.Dispose(); zoomUpIcon.Dispose(); fullScreenIcon.Dispose(); };
        var bar = new StatusStrip { Renderer = new EditorStatusRenderer(), Dock = DockStyle.None, AutoSize = false, BackColor = Color.FromArgb(232, 240, 253), SizingGrip = false, Padding = new Padding(ScaleUi(12), ScaleUi(2), ScaleUi(12), ScaleUi(2)), ImageScalingSize = new Size(ScaleUi(20), ScaleUi(20)) };
        DpiChanged += (_, _) =>
        {
            menuBar.ImageScalingSize = new Size(ScaleUi(20), ScaleUi(20));
            settingsCommand.Size = new Size(ScaleUi(46), ScaleUi(38));
            menuBar.Padding = new Padding(ScaleUi(24), ScaleUi(2), 0, ScaleUi(2));
            bar.ImageScalingSize = new Size(ScaleUi(20), ScaleUi(20));
            bar.Padding = new Padding(ScaleUi(12), ScaleUi(2), ScaleUi(12), ScaleUi(2));
            relayout?.Invoke();
        };
        var imageInfo = imageInfoStatus = new ToolStripStatusLabel { Image = infoIcon, DisplayStyle = ToolStripItemDisplayStyle.Image, AccessibleName = "이미지 정보" };
        var dimensionsInfo = dimensionsStatus = new ToolStripStatusLabel { TextAlign = ContentAlignment.MiddleLeft, AccessibleName = "이미지 픽셀 크기" };
        var metadataSeparator = imageMetadataSeparator = new ToolStripSeparator { AutoSize = false, Width = ScaleUi(1), Margin = new Padding(ScaleUi(16), ScaleUi(6), ScaleUi(16), ScaleUi(6)) };
        var fileSizeInfo = fileSizeStatus = new ToolStripStatusLabel { TextAlign = ContentAlignment.MiddleLeft, AccessibleName = "파일 크기" };
        bar.Items.AddRange([imageInfo, dimensionsInfo, metadataSeparator, fileSizeInfo, status]);
        documentStatusItems.AddRange([imageInfo, dimensionsInfo, metadataSeparator, fileSizeInfo]);
        var zoomDown = new ToolStripButton { Image = zoomDownIcon, DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = "축소", AccessibleName = "축소", Margin = new Padding(ScaleUi(4), 0, ScaleUi(2), 0) };
        zoomDown.Click += (_, _) => Zoom(.8f); bar.Items.Add(zoomDown); documentStatusItems.Add(zoomDown);
        zoomIndicator = new ToolStripDropDownButton("100%") { DisplayStyle = ToolStripItemDisplayStyle.Text, AccessibleName = "확대 비율", Font = zoomFont, Margin = new Padding(ScaleUi(2), 0, ScaleUi(2), 0) };
        foreach (int percent in new[] { 25, 50, 75, 100, 125, 150, 200 })
            zoomIndicator.DropDownItems.Add($"{percent}%", null, (_, _) => { if (Editor is { } ed) Zoom(percent / 100f / ed.Document.Zoom); });
        zoomIndicator.DropDownItems.Add("화면 맞춤", null, (_, _) => Fit());
        bar.Items.Add(zoomIndicator); documentStatusItems.Add(zoomIndicator);
        var zoomUp = new ToolStripButton { Image = zoomUpIcon, DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = "확대", AccessibleName = "확대", Margin = new Padding(ScaleUi(2), 0, ScaleUi(8), 0) };
        zoomUp.Click += (_, _) => Zoom(1.25f); bar.Items.Add(zoomUp); documentStatusItems.Add(zoomUp);
        bar.Items.Add(new ToolStripSeparator { AutoSize = false, Width = ScaleUi(1), Margin = new Padding(ScaleUi(12), ScaleUi(6), ScaleUi(12), ScaleUi(6)) });
        var fullScreenButton = new ToolStripButton { Image = fullScreenIcon, DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = "전체 화면 (F11)", AccessibleName = "전체 화면" };
        fullScreenButton.Click += (_, _) => ToggleFullScreen(); bar.Items.Add(fullScreenButton);
        var shell = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        shell.Controls.Add(workspace); shell.Controls.Add(bar); shell.Controls.Add(hint); shell.Controls.Add(ribbonPanel); shell.Controls.Add(header); shell.Controls.Add(gallery);
        gallery.DocumentSelected += document =>
        {
            Editor?.FinishTextEdit();
            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().FirstOrDefault(page => ReferenceEquals(page.Tag, document));
        };
        gallery.CloseRequested += (_, _) =>
        {
            settings.ShowGallery = false;
            relayout?.Invoke(); RefreshStatus();
            try { SettingsService.Save(settings); }
            catch (Exception e) { status.Text = "갤러리를 닫았습니다. 표시 설정 저장 실패: " + e.Message; }
        };
        // Keep file commands in the compact strip; drawing properties stay between annotation and image tools.
        foreach (Control command in ribbon.Controls.Cast<Control>().ToArray())
        {
            ribbon.Controls.Remove(command);
            headerActions.Controls.Add(command);
        }
        int effectsIndex = headerActions.Controls.GetChildIndex(effectsGroup.Parent!);
        headerActions.Controls.Add(propertyPalette); headerActions.Controls.SetChildIndex(propertyPalette, effectsIndex);
        headerActions.Controls.Add(sizeGroup.Parent!); headerActions.Controls.SetChildIndex(sizeGroup.Parent!, effectsIndex + 1);
        var toolTitles = ribbonToolButtons.ToDictionary(item => item.Button, item => item.Button.Text);
        var headerTitles = headerActions.Controls.OfType<ChromeButton>().ToDictionary(button => button, button => (button.Text, button.Width));
        foreach (var button in headerActions.Controls.OfType<ChromeButton>()) button.BackColor = toolbarCard.BackColor;
        headerActions.BackColor = toolbarCard.BackColor;
        var toolbarOrder = headerActions.Controls.Cast<Control>().ToArray();
        var toolbarGroups = new[] { editGroup.Parent!, propertyPalette, sizeGroup.Parent!, effectsGroup.Parent!, cropGroup.Parent!, regionGroup.Parent! };
        var toolbarSeparators = headerActions.Controls.OfType<Panel>().Where(panel => panel.Width == 1).ToArray();
        var sizedGroups = new[] { editGroup, sizeGroup, effectsGroup, cropGroup, regionGroup };
        bool groupsInLeftRail = false;
        void LayoutShell()
        {
            int w = shell.ClientSize.Width, h = shell.ClientSize.Height;
            bool leftTools = settings.ShowToolbar && settings.ToolbarPosition == "Left" && tabs.TabCount > 0;
            bool compact = false;
            resizeTool.Visible = true;
            if (leftTools != groupsInLeftRail)
            {
                foreach (var group in toolbarGroups)
                {
                    group.Parent?.Controls.Remove(group);
                    (leftTools ? ribbon : headerActions).Controls.Add(group);
                }
                if (!leftTools)
                {
                    for (int index = 0; index < toolbarOrder.Length; index++)
                        headerActions.Controls.SetChildIndex(toolbarOrder[index], index);
                }
                groupsInLeftRail = leftTools;
            }
            foreach (var separator in toolbarSeparators) separator.Visible = !leftTools;
            void SizeToolbar()
            {
                foreach (var (button, _) in ribbonToolButtons)
                {
                    button.Text = leftTools || compact ? "" : toolTitles[button];
                    button.Height = ScaleUi(compact && !leftTools ? 52 : 76);
                    button.Width = leftTools || compact ? ScaleUi(44) : ScaleUi(60);
                }
                foreach (var button in new[] { rotateTool, cropTool, mosaicTool, blurTool, resizeTool, regionCopy, regionPaste, clipboardToggle })
                {
                    button.IconSize = 24; button.Font = toolbarLabelFont;
                    button.Height = ScaleUi(compact && !leftTools ? 52 : 76);
                    if (button != clipboardToggle)
                        button.Text = leftTools || compact ? "" : button == rotateTool ? "90° 회전" : button == cropTool ? "자르기" : button == mosaicTool ? "모자이크" : button == blurTool ? "블러" : button == resizeTool ? "크기 조절" : button == regionPaste ? "붙여넣기" : "영역 복사";
                    button.Width = leftTools || compact ? ScaleUi(44) : ScaleUi(68);
                }
                sizeButton.Text = "크기";
                sizeButton.Width = ScaleUi(58);
                sizeButton.Height = ScaleUi(compact && !leftTools ? 52 : 76);
                foreach (var (button, originalHeader) in headerTitles)
                {
                    if (!button.IconAboveText || button.Parent != headerActions) continue;
                    button.IconSize = 24;
                    button.Text = originalHeader.Text;
                    button.Height = ScaleUi(compact && !leftTools ? 52 : 78);
                    button.Width = ScaleUi(originalHeader.Width);
                    if (button != newButton) button.Font = toolbarLabelFont;
                }
                foreach (var group in sizedGroups)
                {
                    group.Parent!.Height = leftTools ? group == editGroup ? ScaleUi(240) : ScaleUi(94) : ScaleUi(compact ? 52 : 76);
                    // Ancestors are hidden during capture; calculate intrinsic widths regardless of visibility.
                    group.Parent.Width = leftTools ? 156 : group.Padding.Horizontal + group.Controls.Cast<Control>()
                        .Where(control => control != clipboardToggle && control != regionPaste && control != rotateTool)
                        .Sum(control => control.Width + control.Margin.Horizontal) + ScaleUi(4);
                    group.Width = leftTools ? ScaleUi(150) : group.Parent.Width;
                    group.WrapContents = leftTools; group.AutoScroll = false;
                }
            }
            regionPaste.Visible = false; clipboardToggle.Visible = false; rotateTool.Visible = false;
            foreach (var option in splitOptionButtons) option.Width = ScaleUi(compact ? 14 : 16);
            foreach (var separator in toolbarSeparators) separator.Margin = compact ? new Padding(ScaleUi(1), ScaleUi(12), ScaleUi(1), 0) : new Padding(ScaleUi(5), ScaleUi(12), ScaleUi(5), 0);
            editGroup.Parent!.Visible = effectsGroup.Parent!.Visible = cropGroup.Parent!.Visible = regionGroup.Parent!.Visible = true;
            propertyPalette.Size = new Size(ScaleUi(238), ScaleUi(76));
            colorGroup.Dock = DockStyle.None; colorGroup.SetBounds(0, 0, ScaleUi(188), ScaleUi(76)); colorGroup.WrapContents = true;
            customColor.SetBounds(ScaleUi(192), 0, ScaleUi(36), ScaleUi(36));
            customColor.BringToFront();
            foreach (var chip in swatches)
            {
                chip.Size = new Size(ScaleUi(26), ScaleUi(22));
                chip.Margin = new Padding(ScaleUi(2));
            }
            int paletteTop = colorGroup.Top + colorGroup.Padding.Top;
            int paletteRowsHeight = 2 * (ScaleUi(22) + 2 * ScaleUi(2));
            customColor.Top = paletteTop + (paletteRowsHeight - customColor.Height) / 2;
            headerActions.Padding = compact ? new Padding(0, ScaleUi(4), 0, 0) : new Padding(0, ScaleUi(1), 0, 0);
            propertyPalette.Height = ScaleUi(76);
            captureOptions.Height = ScaleUi(76);
            foreach (var group in sizedGroups)
                foreach (Control child in group.Controls)
                    if (child != clipboardToggle) child.Height = ScaleUi(76);
            headerActions.SuspendLayout(); ribbon.SuspendLayout();
            foreach (var group in sizedGroups) group.SuspendLayout();
            try
            {
                SizeToolbar();
                int requiredWidth = headerActions.Controls.Cast<Control>()
                    .Where(control => control != captureOptions || control.Visible)
                    .Sum(control => control.Width + control.Margin.Horizontal) + ScaleUi(32);
                compact = !leftTools && requiredWidth > w;
                propertyPalette.Height = ScaleUi(compact ? 52 : 76);
                colorGroup.Height = propertyPalette.Height;
                captureOptions.Height = ScaleUi(compact ? 52 : 76);
                if (compact) SizeToolbar();
            }
            finally
            {
                foreach (var group in sizedGroups) group.ResumeLayout(true);
                ribbon.ResumeLayout(true); headerActions.ResumeLayout(true);
            }
            ribbon.FlowDirection = leftTools ? FlowDirection.TopDown : FlowDirection.LeftToRight;
            ribbon.WrapContents = false;
            ribbon.AutoScroll = leftTools;
            ribbon.Padding = Padding.Empty;
            headerActions.PerformLayout();
            ribbon.PerformLayout();
            headerActions.WrapContents = false;
            headerActions.AutoScroll = compact && !leftTools;
            int toolbarHeight = settings.ShowToolbar ? ScaleUi(compact ? 118 : 144) : ScaleUi(42);
            int statusHeight = settings.ShowStatusBar ? ScaleUi(44) : 0;
            bar.Visible = settings.ShowStatusBar; toolbarCard.Visible = settings.ShowToolbar;
            int galleryHeight = settings.ShowGallery && tabs.TabCount > 0 ? ScaleUi(112) : 0;
            gallery.Visible = galleryHeight > 0;

            ribbonPanel.Visible = leftTools;

            hint.Visible = false;
            header.Bounds = new Rectangle(0, 0, w, toolbarHeight);
            menuBar.Bounds = new Rectangle(0, 0, w, ScaleUi(42));
            int toolbarCardHeight = ScaleUi(compact ? 76 : 102);
            toolbarCard.Bounds = new Rectangle(0, ScaleUi(42), w, toolbarCardHeight);
            headerActions.Bounds = new Rectangle(ScaleUi(24), ScaleUi(compact ? 4 : 12), Math.Max(0, toolbarCard.ClientSize.Width - ScaleUi(32)), ScaleUi(compact ? 72 : 88));
            ribbonPanel.Bounds = leftTools
                ? new Rectangle(0, toolbarHeight, ScaleUi(164), Math.Max(0, h - toolbarHeight - statusHeight - galleryHeight))
                : Rectangle.Empty;
            workspace.Bounds = leftTools
                ? new Rectangle(ScaleUi(164), toolbarHeight, Math.Max(0, w - ScaleUi(164)), Math.Max(0, h - toolbarHeight - statusHeight - galleryHeight))
                : new Rectangle(0, toolbarHeight, w, Math.Max(0, h - toolbarHeight - statusHeight - galleryHeight));
            gallery.Bounds = new Rectangle(0, Math.Max(0, h - statusHeight - galleryHeight), w, galleryHeight);
            bar.Bounds = new Rectangle(0, Math.Max(0, h - statusHeight), w, statusHeight);
            centerHost.Bounds = new Rectangle(0, 0, workspace.Width, workspace.Height);
            PositionPlusTab();
            if (regionClipboard.Visible)
            {
                int gap = Math.Max(8, (int)(12 * DeviceDpi / 96f));
                int cardWidth = Math.Min((int)(280 * DeviceDpi / 96f), Math.Max(0, workspace.Width - gap * 2));
                int cardHeight = regionClipboard.GetCardHeight(cardWidth, Math.Max(0, workspace.Height - gap * 2));
                regionClipboard.Bounds = new Rectangle(workspace.Width - cardWidth - gap, gap, cardWidth, cardHeight);
                regionClipboard.BringToFront();
            }
        }
        shell.Resize += (_, _) => LayoutShell();
        shell.Dock = DockStyle.None;
        Controls.Add(shell);
        FormBorderStyle = FormBorderStyle.None;
        var titleBar = new EditorTitleBar(this) { Dock = DockStyle.None };
        Controls.Add(titleBar);
        var resizeInput = new WindowResizeBorder(this);
        Disposed += (_, _) => resizeInput.Dispose();
        bool layingOutWindow = false;
        void LayoutWindow()
        {
            if (layingOutWindow || IsDisposed) return;
            layingOutWindow = true;
            try
            {
                titleBar.SetBounds(0, 0, ClientSize.Width, ScaleUi(32));
                shell.SetBounds(0, titleBar.Bottom, ClientSize.Width, Math.Max(0, ClientSize.Height - titleBar.Bottom));
                LayoutShell();
            }
            finally { layingOutWindow = false; }
        }
        relayout = LayoutWindow;
        Layout += (_, _) => LayoutWindow();
        ClientSizeChanged += (_, _) => LayoutWindow();
        void ConstrainWindow()
        {
            if (WindowState == FormWindowState.Normal)
            {
                var area = Screen.FromControl(this).WorkingArea;
                MinimumSize = new Size(Math.Min(ScaleUi(900), area.Width), Math.Min(ScaleUi(500), area.Height));
                int width = Math.Min(Width, area.Width), height = Math.Min(Height, area.Height);
                Bounds = new Rectangle(Math.Clamp(Left, area.Left, area.Right - width),
                    Math.Clamp(Top, area.Top, area.Bottom - height), width, height);
            }
            LayoutWindow();
        }
        Shown += (_, _) => BeginInvoke(new Action(ConstrainWindow));
        ResizeEnd += (_, _) => ConstrainWindow();
        // WinForms scales child bounds after DpiChanged; apply final geometry after that pass.
        DpiChanged += (_, _) => { if (IsHandleCreated) BeginInvoke(new Action(ConstrainWindow)); };
        LayoutWindow();
        tabs.SelectedIndexChanged += (_, _) => { RefreshStatus(); relayout?.Invoke(); };
        var menu = new ContextMenuStrip();
        menu.Items.Add("딤픽 열기", null, (_, _) => Restore());
        menu.Items.Add("영역 캡처", null, (_, _) => _ = CaptureAsync(true));
        menu.Items.Add("전체 캡처", null, (_, _) => _ = CaptureAsync(false));
        menu.Items.Add("종료", null, (_, _) => { exiting = true; Close(); });
        tray = new NotifyIcon { Icon = Icon, Text = "딤픽", Visible = !managedTray, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => Restore();
        hotkeys = new GlobalHotkeyService(Handle);
        initializeStartup = () =>
        {
            if (startupInitialized) return;
            startupInitialized = true;
            bool region = hotkeys.Set(1, settings.RegionHotkey), full = hotkeys.Set(2, settings.FullscreenHotkey);
            status.Text = region && full ? "영역 또는 전체 화면을 캡처하세요." : "일부 전역 단축키를 등록하지 못했습니다. 설정에서 변경하세요.";
            if (managedTray) BeginInvoke(new Action(() =>
            {
                Guard(() =>
                {
                    var session = SessionService.Load();
                    foreach (var document in session.Documents) AddDocumentCore(document);
                    if (tabs.TabCount > 0) tabs.SelectedIndex = Math.Clamp(session.SelectedIndex, 0, tabs.TabCount - 1);
                    BeginInvoke(new Action(() =>
                    {
                        for (int i = 0; i < Math.Min(tabs.TabCount, session.Scrolls.Count); i++)
                            tabs.TabPages[i].Controls.OfType<Panel>().Single().AutoScrollPosition = session.Scrolls[i];
                        if (startupAction == "--capture-region") _ = CaptureAsync(true);
                        else if (startupAction == "--capture-full") _ = CaptureAsync(false);
                        else if (startupAction == "--settings") Configure();
                    }));
                });
            }));
        };
        Shown += (_, _) => initializeStartup();
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing && !exiting)
            {
                if (!managedTray) { e.Cancel = true; Hide(); return; }
                try
                {
                    foreach (TabPage page in tabs.TabPages) page.Controls.OfType<Panel>().Single().Controls.OfType<EditorCanvas>().Single().FinishTextEdit();
                    SessionService.Save(tabs.TabPages.Cast<TabPage>().Select(Document), tabs.TabPages.Cast<TabPage>().Select(page =>
                    {
                        var scroll = page.Controls.OfType<Panel>().Single().AutoScrollPosition; return new Point(-scroll.X, -scroll.Y);
                    }).ToList(), tabs.SelectedIndex);
                }
                catch (Exception error) { e.Cancel = true; status.Text = "편집 상태 보관 실패: " + error.Message; }
            }
        };
        FormClosed += (_, _) => { hotkeys.Dispose(); tray.Dispose(); menu.Dispose(); tips.Dispose(); toolOptions.Dispose(); foreach (TabPage page in tabs.TabPages) Document(page).Dispose(); };
        RefreshStatus();
    }
    protected override void SetVisibleCore(bool value)
    {
        if (value && deferCaptureStartup)
        {
            deferCaptureStartup = false;
            base.SetVisibleCore(false);
            BeginInvoke(new Action(() => initializeStartup?.Invoke()));
            return;
        }
        base.SetVisibleCore(value);
    }
    private static Button HeaderButton(string title, bool primary)
    {
        return new ChromeButton { Text = title, IconKey = title switch { "새 캡처" or "영역 캡처" => "capture", "전체 화면" => "monitor", "열기" or "이미지 열기" => "open", "저장" => "save", "복사" => "copy", "실행 취소" => "undo", _ => "" }, Primary = primary, Width = primary ? 128 : 92, Height = 36, BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(245, 247, 250), ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(6, 0, 0, 0), AccessibleName = title };
    }
    private Button AddHeaderCommand(FlowLayoutPanel strip, string title, Action action)
    {
        var button = HeaderButton(title, false);
        button.Width = 64; button.Height = 76; button.BackColor = Color.White; button.Margin = new Padding(1, 0, 1, 0);
        if (button is ChromeButton chrome) { chrome.IconAboveText = true; chrome.IconKey = title switch { "열기" => "open", "저장" => "save", "복사" => "copy", "실행 취소" => "undo", "다시 실행" => "redo", _ => "" }; }
        button.AccessibleName = title;
        button.Click += (_, _) => action(); strip.Controls.Add(button); tips.SetToolTip(button, title switch { "저장" => "저장 (Ctrl+S)", "복사" => "합성 이미지 복사 (Ctrl+C)", "실행 취소" => "실행 취소 (Ctrl+Z)", "다시 실행" => "다시 실행 (Ctrl+Y)", _ => title }); return button;
    }
    private void UpdateRibbonSelection()
    {
        foreach (var (button, kind) in ribbonToolButtons)
        {
            var activeKind = Editor?.Selected?.Kind ?? Editor?.Tool;
            bool active = button == shapeToolButton
                ? Editor != null && !Editor.CropMode && activeKind is AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.RoundedRectangle or AnnotationKind.Line or AnnotationKind.Arrow or AnnotationKind.Triangle
                : Editor != null && !Editor.CropMode && (kind == null ? Editor.Tool == null : activeKind == kind);
            if (button is ChromeButton chrome) chrome.Selected = active;
        }
    }
    private static CaptureDocument Document(TabPage page) => (CaptureDocument)page.Tag!;
    private void Restore() { Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); }
    private void Guard(Action action) { try { action(); } catch (Exception e) { if (captureOnlyStartup && !Visible) Restore(); status.Text = $"작업 실패: {e.Message}"; } }
    private async Task CaptureAsync(bool region)
    {
        if (capturing || !Enabled || OwnedForms.Any(form => form.Visible)) return;
        capturing = true; bool wasVisible = Visible;
        try
        {
            Hide(); await Task.Delay(200);
            Bitmap image;
            if (region)
            {
                using var selection = new SelectionForm();
                if (selection.ShowDialog() != DialogResult.OK) { if (wasVisible) Restore(); return; }
                image = selection.CaptureSelection();
            }
            else image = CaptureService.Capture(Screen.FromPoint(Cursor.Position).Bounds);
            AddDocument(image); Restore();
            if (settings.AutoCopy) CopyImage();
            AutoSaveCapture(Editor!.Document);
        }
        catch (Exception e) { Restore(); status.Text = $"캡처 실패: {e.Message}"; }
        finally
        {
            capturing = false;
            // A cancelled cold capture leaves the saved session untouched and releases
            // the editor so the native tray can register its hotkeys again.
            if (captureOnlyStartup && !Visible) { exiting = true; Close(); }
        }
    }
    private void AutoSaveCapture(CaptureDocument document)
    {
        if (!settings.AutoSave) return;
        try
        {
            string path = AutoSaveService.SaveOriginal(document, settings.AutoSaveFolder);
            document.Saved();
            RefreshStatus();
            status.Text = "원본 자동 저장: " + path;
        }
        catch (Exception e) { status.Text = "자동 저장 실패 (캡처는 탭에 유지됩니다): " + e.Message; }
    }
    private void AddDocument(Bitmap image) => AddNamedDocument(image, null);
    private void AddNamedDocument(Bitmap image, string? displayName)
    {
        string name = displayName ?? CaptureNames.Create(DateTime.Now, tabs.TabPages.Cast<TabPage>().Select(page => Document(page).Name));
        var document = new CaptureDocument(image, name);
        AddDocumentCore(document);
    }
    private void AddDocumentCore(CaptureDocument document)
    {
        string name = document.Name;
        var page = new TabPage(CaptureNames.TabTitle(name) + " ●") { Tag = document };
        var panel = new CanvasViewport { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new Padding(16) };
        var editor = new EditorCanvas(document) { Location = new Point(16, 16), Ink = currentInk, Stroke = currentStroke, ShowGrid = settings.ShowGrid };
        editor.SetFillColor(currentFillColor);
        editor.SetStyleOptions(currentFilled, currentDashed, currentTextSize, currentBold);
        editor.Edited += (_, _) => RefreshStatus();
        editor.SelectionChanged += (_, _) => RefreshStatus();
        editor.RegionCopyRequested += (_, _) => { if (ReferenceEquals(Editor, editor)) CopyRegion(); };
        editor.MouseWheel += (_, e) => { if ((ModifierKeys & Keys.Control) != 0) { if (e is HandledMouseEventArgs handled) handled.Handled = true; Zoom(e.Delta > 0 ? 1.25f : .8f, e.Location); } };
        panel.ZoomRequested += (_, e) =>
        {
            if (ReferenceEquals(Editor, editor))
                Zoom(e.Delta > 0 ? 1.25f : .8f, editor.PointToClient(panel.PointToScreen(e.Location)));
        };
        panel.Resize += (_, _) =>
        {
            editor.Location = new Point(Math.Max(16, (panel.ClientSize.Width - editor.Width) / 2) + panel.AutoScrollPosition.X, Math.Max(16, (panel.ClientSize.Height - editor.Height) / 2) + panel.AutoScrollPosition.Y);
        };
        panel.Controls.Add(editor); page.Controls.Add(panel);
        page.Controls.Add(new CanvasRuler(editor, true) { Visible = settings.ShowRulers });
        page.Controls.Add(new CanvasRuler(editor, false) { Visible = settings.ShowRulers });
        tabs.TabPages.Add(page);
        tabs.Visible = true; if (homePanel != null) homePanel.Visible = false;
        tabs.SelectedTab = page; relayout?.Invoke(); tabs.PerformLayout(); page.PerformLayout(); panel.PerformLayout();
        BeginInvoke(new Action(() =>
        {
            if (IsDisposed || editor.IsDisposed) return;
            editor.UpdateSize();
            panel.AutoScrollPosition = Point.Empty;
            editor.Location = new Point(Math.Max(16, (panel.ClientSize.Width - editor.Width) / 2), Math.Max(16, (panel.ClientSize.Height - editor.Height) / 2));
            if (Editor == editor) { RefreshStatus(); editor.Focus(); }
        }));
    }
    private void OpenImage() => Guard(() =>
    {
        using var dialog = new OpenFileDialog { Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        AddNamedDocument(ImageSafety.LoadFile(dialog.FileName), Path.GetFileName(dialog.FileName));
    });
    private bool Save()
    {
        if (Editor is not { } editor) return false;
        editor.FinishTextEdit();
        var document = editor.Document; string? path = document.SavePath;
        if (path == null)
        {
            using var dialog = new SaveFileDialog { Filter = "PNG 이미지|*.png", FileName = document.Name, DefaultExt = "png", OverwritePrompt = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            path = dialog.FileName;
        }
        try
        {
            using var image = document.Compose(); image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            document.SavePath = path; document.Rename(Path.GetFileName(path)); document.Saved(); RefreshStatus(); tabs.Invalidate(); return true;
        }
        catch (Exception e) { status.Text = $"저장 실패: {e.Message}"; return false; }
    }
    private void CopyRegion() => Guard(() =>
    {
        if (Editor is not { } editor) return;
        editor.FinishTextEdit();
        if (!editor.CanApplyCrop) { status.Text = "영역 복사를 누르고 복사할 범위를 드래그하세요."; return; }
        if (clipboard.CopyRegion(editor.Document, editor.CropBounds))
        {
            using var image = Clipboard.GetImage();
            if (image != null) regionClipboard.ShowSnapshot(image);
            editor.CancelCrop(); relayout?.Invoke();
            status.Text = "영역을 복사했습니다. 다른 탭에서 Ctrl+V로 붙여넣으세요.";
        }
    });
    private void PrintImage() => Guard(() =>
    {
        if (Editor is not { } editor) return;
        editor.FinishTextEdit();
        DocumentPrintService.Print(this, editor.Document);
    });

    private void ShowImageProperties()
    {
        if (Editor is not { } editor) return;
        var document = editor.Document;
        MessageBox.Show(this, $"이름: {document.Name}\n크기: {document.Image.Width} × {document.Image.Height} px", "이미지 속성", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CutFromMenu() => Guard(() =>
    {
        if (Editor is not { } editor || editor.Selected == null) { status.Text = "잘라낼 주석을 선택하세요."; return; }
        clipboard.CopyAnnotations(editor.Document, true);
        menuAnnotationClipboard = true; menuClipboardSequence = GetClipboardSequenceNumber();
        copiedAnnotationSource = editor.Document; annotationPasteCount = 0;
        editor.DeleteSelection(); RefreshStatus();
    });

    private void CopyFromMenu() => Guard(() =>
    {
        if (Editor is not { } editor) return;
        if (editor.Selected == null) { menuAnnotationClipboard = false; CopyImage(); return; }
        clipboard.CopyAnnotations(editor.Document, true);
        menuAnnotationClipboard = true; menuClipboardSequence = GetClipboardSequenceNumber();
        copiedAnnotationSource = editor.Document; annotationPasteCount = 0;
        status.Text = "선택 주석을 복사했습니다.";
    });

    private void PasteFromMenu() => Guard(() =>
    {
        if (Editor is not { } editor) return;
        if (menuAnnotationClipboard && menuClipboardSequence == GetClipboardSequenceNumber())
        {
            float offset = ReferenceEquals(copiedAnnotationSource, editor.Document) ? 12 * ++annotationPasteCount : 0;
            clipboard.Paste(editor.Document, selectPasted: true, offset: offset);
            editor.Tool = null; editor.NotifyEdit(); RefreshStatus();
            status.Text = "복사한 주석을 붙여넣었습니다.";
        }
        else { menuAnnotationClipboard = false; PasteImage(); }
    });

    private void PasteImage() => Guard(() =>
    {
        if (Editor is not { } editor) return;
        editor.FinishTextEdit();
        if (!clipboard.PasteImage(editor.Document)) { status.Text = "클립보드에 이미지가 없습니다."; return; }
        editor.CancelCrop(); editor.Tool = null; editor.NotifyEdit(); RefreshStatus();
    });
    private void CopyImage() => Guard(() =>
    {
        if (Editor == null) return;
        Editor.FinishTextEdit();
        clipboard.CopyImage(Editor.Document);
        status.Text = "✓ 캡처 이미지가 클립보드에 복사되었습니다. Ctrl+V로 붙여넣으세요.";
    });
    private void Undo()
    {
        if (Editor is not { } editor) return;
        editor.FinishTextEdit(); editor.CancelCrop(); var oldSize = editor.Document.Image.Size;
        editor.Document.Undo();
        if (editor.Document.Image.Size != oldSize) ResetCanvasViewport(editor);
        editor.Invalidate(); RefreshStatus();
    }
    private void Redo()
    {
        if (Editor is not { } editor) return;
        editor.FinishTextEdit(); editor.CancelCrop(); var oldSize = editor.Document.Image.Size;
        editor.Document.Redo();
        if (editor.Document.Image.Size != oldSize) ResetCanvasViewport(editor);
        editor.Invalidate(); RefreshStatus();
    }
    private void ApplyCrop()
    {
        if (Editor is not { } editor) return;
        Guard(() => { if (editor.ApplyCrop()) { ResetCanvasViewport(editor); RefreshStatus(); } });
    }
    private void RotateImageClockwise()
    {
        if (Editor is not { } editor) return;
        Guard(() =>
        {
            editor.FinishTextEdit(); editor.CancelCrop();
            if (editor.Document.RotateClockwise())
            {
                editor.Tool = null; ResetCanvasViewport(editor); editor.NotifyEdit(); RefreshStatus();
            }
        });
    }
    private void ResizeImage()
    {
        if (Editor is not { } editor) return;
        using var dialog = new Form { Text = "이미지 크기 조절", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(300, 168), MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false };
        var original = editor.Document.Image.Size;
        var widthInput = new NumericUpDown { Minimum = 1, Maximum = 32768, Value = original.Width, Location = new Point(105, 18), Width = 150 };
        var heightInput = new NumericUpDown { Minimum = 1, Maximum = 32768, Value = original.Height, Location = new Point(105, 52), Width = 150 };
        var keepRatio = new CheckBox { Text = "가로세로 비율 유지", Checked = true, AutoSize = true, Location = new Point(18, 87) };
        bool updating = false;
        widthInput.ValueChanged += (_, _) => { if (!updating && keepRatio.Checked) { updating = true; heightInput.Value = Math.Clamp(Math.Round(widthInput.Value * original.Height / original.Width), 1, 32768); updating = false; } };
        heightInput.ValueChanged += (_, _) => { if (!updating && keepRatio.Checked) { updating = true; widthInput.Value = Math.Clamp(Math.Round(heightInput.Value * original.Width / original.Height), 1, 32768); updating = false; } };
        var apply = new Button { Text = "적용", DialogResult = DialogResult.OK, Location = new Point(133, 126), Size = new Size(72, 28) };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Location = new Point(211, 126), Size = new Size(72, 28) };
        dialog.Controls.AddRange([new Label { Text = "너비 (px)", AutoSize = true, Location = new Point(18, 21) }, new Label { Text = "높이 (px)", AutoSize = true, Location = new Point(18, 55) }, widthInput, heightInput, keepRatio, apply, cancel]);
        dialog.AcceptButton = apply; dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        Guard(() => { editor.FinishTextEdit(); editor.CancelCrop(); if (editor.Document.ResizeImage(new Size((int)widthInput.Value, (int)heightInput.Value))) { ResetCanvasViewport(editor); RefreshStatus(); } });
    }
    private static void ResetCanvasViewport(EditorCanvas editor)
    {
        editor.UpdateSize();
        if (editor.Parent is not Panel panel) return;
        panel.AutoScrollPosition = Point.Empty;
        int left = Math.Max(panel.Padding.Left, (panel.DisplayRectangle.Width - editor.Width) / 2);
        int top = Math.Max(panel.Padding.Top, (panel.DisplayRectangle.Height - editor.Height) / 2);
        editor.Location = new Point(left, top);
    }
    private bool CloseTab(int index, bool confirmSave = true)
    {
        tabs.SelectedIndex = index; var page = tabs.TabPages[index]; var document = Document(page);
        if (confirmSave && document.Dirty)
        {
            var answer = MessageBox.Show(this, $"{document.Name}\n변경 내용을 저장할까요?\n예: 저장 / 아니요: 저장 안 함", "DIMPIK", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !Save())) return false;
        }
        tabs.TabPages.Remove(page); page.Dispose(); document.Dispose();
        if (tabs.TabCount == 0) { tabs.Visible = false; if (homePanel != null) homePanel.Visible = true; }
        relayout?.Invoke();
        RefreshStatus(); return true;
    }
    private void Zoom(float factor, Point? anchor = null)
    {
        if (Editor is not { } editor || editor.Parent is not Panel panel) return;
        var point = anchor ?? new Point(editor.Width / 2, editor.Height / 2);
        var viewport = panel.PointToClient(editor.PointToScreen(point));
        float old = editor.Document.Zoom; editor.Document.Zoom = Math.Clamp(old * factor, .05f, 8);
        panel.AutoScrollPosition = Point.Empty; editor.UpdateSize();
        editor.Location = new Point(Math.Max(16, (panel.ClientSize.Width - editor.Width) / 2), Math.Max(16, (panel.ClientSize.Height - editor.Height) / 2));
        panel.AutoScrollPosition = new Point(Math.Max(0, editor.Left + (int)(point.X * editor.Document.Zoom / old) - viewport.X), Math.Max(0, editor.Top + (int)(point.Y * editor.Document.Zoom / old) - viewport.Y));
        RefreshStatus();
    }
    private void ToggleFullScreen()
    {
        if (!fullScreen)
        {
            var screen = Screen.FromControl(this);
            windowedBorder = FormBorderStyle; windowedState = WindowState;
            windowedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            WindowState = FormWindowState.Normal; FormBorderStyle = FormBorderStyle.None;
        MaximizedBounds = Screen.FromControl(this).WorkingArea;
        LocationChanged += (_, _) => { if (WindowState == FormWindowState.Normal) MaximizedBounds = Screen.FromControl(this).WorkingArea; }; Bounds = screen.Bounds; fullScreen = true;
        }
        else
        {
            FormBorderStyle = windowedBorder;
            if (windowedBounds.Width > 0 && windowedBounds.Height > 0) Bounds = windowedBounds;
            WindowState = windowedState; fullScreen = false;
        }
        relayout?.Invoke();
    }
    private void Fit()
    {
        if (Editor is not { } editor || editor.Parent is not Panel panel) return;
        float availableWidth = Math.Max(1, panel.ClientSize.Width - panel.Padding.Horizontal);
        float availableHeight = Math.Max(1, panel.ClientSize.Height - panel.Padding.Vertical);
        editor.Document.Zoom = Math.Clamp(Math.Min(1f, Math.Min(availableWidth / editor.Document.Image.Width, availableHeight / editor.Document.Image.Height)), .05f, 1);
        ResetCanvasViewport(editor);
        RefreshStatus();
    }
    private void RefreshStatus()
    {
        regionClipboard.SetPasteEnabled(Editor != null);
        foreach (TabPage page in tabs.TabPages) page.Text = CaptureNames.TabTitle(Document(page).Name) + (Document(page).Dirty ? " ●" : "");
        bool hasDocument = Editor is { };
        imageInfoStatus!.Visible = dimensionsStatus!.Visible = imageMetadataSeparator!.Visible = fileSizeStatus!.Visible = hasDocument;
        if (Editor is { } e)
        {
            dimensionsStatus.Text = $"{e.Document.Image.Width} × {e.Document.Image.Height} px";
            fileSizeStatus.Text = $"파일 크기: {FormatFileSize(GetPngByteSize(e.Document))}";
            status.Text = string.Empty;
        }
        else status.Text = "영역 또는 전체 화면을 캡처하세요.";
        if (zoomIndicator != null) zoomIndicator.Text = Editor is { } current ? $"{current.Document.Zoom:P0}" : "100%";
        foreach (var action in documentActions) action.Enabled = Editor != null;
        if (undoButton != null) undoButton.Enabled = Editor?.Document.CanUndo == true;
        if (redoButton != null) redoButton.Enabled = Editor?.Document.CanRedo == true;
        foreach (var action in documentStatusItems) action.Enabled = Editor != null;
        var selected = Editor?.Document.Annotations.Find(a => a.Id == Editor.Document.Selection);
        if (selectedLabel != null)
        {
            selectedLabel.Text = (selected?.Kind ?? Editor?.Tool) switch
            {
                AnnotationKind.Arrow => "화살표",
                AnnotationKind.Rectangle => "사각형",
                AnnotationKind.Ellipse => "원",
                AnnotationKind.RoundedRectangle => "둥근 사각형",
                AnnotationKind.Line => "직선",
                AnnotationKind.Triangle => "삼각형",
                AnnotationKind.Text => "텍스트",
                AnnotationKind.Number => "번호",
                AnnotationKind.Highlight => "형광펜",
                AnnotationKind.Image => "이미지",
                _ => "선택"
            };
        }
        if (Editor is { } activeEditor)
        {
            var ink = selected?.Color ?? activeEditor.Ink;
            currentInk = ink;
            currentFillColor = selected?.FillColor ?? activeEditor.FillColor;
            currentFilled = selected?.Filled ?? activeEditor.Filled;
            currentDashed = selected?.Dashed ?? activeEditor.Dashed;
            currentTextSize = selected?.FontSize ?? activeEditor.TextSize;
            currentBold = selected?.Bold ?? activeEditor.Bold;
            UpdateSwatches(ink);
            if (strokeControl != null)
            {
                updatingInspector = true;
                try
                {
                    var stroke = selected?.Width ?? activeEditor.Stroke;
                    currentStroke = stroke;
                    strokeControl.Value = Math.Clamp((int)Math.Round(stroke), strokeControl.Minimum, strokeControl.Maximum);
                    if (strokeValueLabel != null) strokeValueLabel.Text = $"{strokeControl.Value} px";
                }
                finally { updatingInspector = false; }
            }
        }
        UpdateRibbonSelection();
        syncProperties?.Invoke();
        if (settings.ShowGallery && tabs.TabCount > 0)
            gallery.SetDocuments(tabs.TabPages.Cast<TabPage>().Select(Document).ToArray(), Editor?.Document);
        else if (gallery.ThumbnailCount > 0) gallery.Clear();
        tabs.Invalidate();
    }
    private void UpdateSwatches(Color color)
    {
        foreach (var swatch in swatches) swatch.Selected = swatch.SwatchColor.ToArgb() == color.ToArgb();
    }
    private void Configure()
    {
        using var dialog = new OptionsForm(settings, ApplyOptions, hotkeys.CheckAvailability, StartupService.Available ? StartupService.Enabled : null, StartupService.SetEnabled);
        dialog.ShowDialog(this);
    }
    private void OpenSettings()
    {
        if (openingSettings || OwnedForms.Any(form => form is OptionsForm && form.Visible)) return;
        openingSettings = true;
        try
        {
            BeginInvoke(new Action(() =>
            {
                try { Configure(); }
                catch (Exception error) { status.Text = "설정을 열지 못했습니다: " + error.Message; }
                finally { openingSettings = false; }
            }));
        }
        catch
        {
            openingSettings = false;
            throw;
        }
    }

    private long GetPngByteSize(CaptureDocument document)
    {
        if (!document.Dirty && document.SavePath is { } path && File.Exists(path))
            return new FileInfo(path).Length;
        if (ReferenceEquals(measuredSizeDocument, document) && measuredSizeRevision == document.Revision &&
            string.Equals(measuredSizeSavePath, document.SavePath, StringComparison.OrdinalIgnoreCase))
            return measuredPngBytes;

        using var composed = document.Compose();
        using var encoded = new MemoryStream();
        composed.Save(encoded, System.Drawing.Imaging.ImageFormat.Png);
        measuredSizeDocument = document;
        measuredSizeRevision = document.Revision;
        measuredSizeSavePath = document.SavePath;
        measuredPngBytes = encoded.Length;
        return measuredPngBytes;
    }

    private static string FormatFileSize(long bytes) => bytes >= 1024L * 1024
        ? $"{bytes / (1024d * 1024d):0.#} MB"
        : bytes >= 1024 ? $"{Math.Round(bytes / 1024d):0} KB" : $"{bytes} B";
    private string? ApplyOptions(AppSettings updated)
    {
        var oldRegion = settings.RegionHotkey; var oldFull = settings.FullscreenHotkey;
        bool changed = updated.RegionHotkey != oldRegion || updated.FullscreenHotkey != oldFull;
        if (changed)
        {
            hotkeys.Dispose();
            if (!hotkeys.Set(1, updated.RegionHotkey) || !hotkeys.Set(2, updated.FullscreenHotkey))
            {
                hotkeys.Dispose();
                bool r = hotkeys.Set(1, oldRegion), f = hotkeys.Set(2, oldFull);
                return r && f ? "단축키 등록에 실패해 기존 키로 복원했습니다. 다른 키를 지정하세요." : "단축키 등록과 일부 복원에 실패했습니다. 다른 키를 지정하세요.";
            }
        }
        try { SettingsService.Save(updated); }
        catch (Exception e)
        {
            bool restored = true;
            if (changed) { hotkeys.Dispose(); bool r = hotkeys.Set(1, oldRegion), f = hotkeys.Set(2, oldFull); restored = r && f; }
            return "설정 저장 실패: " + e.Message + (restored ? "" : " · 단축키 복원도 실패했습니다.");
        }
        settings.RegionHotkey = updated.RegionHotkey; settings.FullscreenHotkey = updated.FullscreenHotkey;
        settings.NewCaptureSelectRegion = updated.NewCaptureSelectRegion; settings.AutoCopy = updated.AutoCopy;
        settings.AutoSave = updated.AutoSave; settings.AutoSaveFolder = updated.AutoSaveFolder;
        settings.ShowGallery = updated.ShowGallery; settings.ShowRulers = updated.ShowRulers; settings.ShowGrid = updated.ShowGrid;
        settings.ShowStatusBar = updated.ShowStatusBar; settings.ShowToolbar = updated.ShowToolbar; settings.ToolbarPosition = updated.ToolbarPosition;
        relayout?.Invoke(); RefreshStatus();
        return null;
    }
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.Style |= 0x00020000 | 0x00010000; // Minimize/maximize without the native thick frame.
            return parameters;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowMinMaxInfo
    {
        public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    protected override void WndProc(ref Message m)
    {
        const int WmPaint = 0x000F;
        if (m.Msg == WmPaint)
        {
            base.WndProc(ref m);
            if (!fullScreen && FormBorderStyle == FormBorderStyle.None && WindowState != FormWindowState.Minimized && ClientSize.Width > 1 && ClientSize.Height > 1)
            {
                // The custom chrome has no native non-client frame, so draw a quiet
                // perimeter over the client edge to keep the window distinct on white desktops.
                using var graphics = Graphics.FromHwnd(Handle);
                using var pen = new Pen(Color.FromArgb(203, 213, 225), Math.Max(1, DeviceDpi / 96f));
                float inset = pen.Width / 2f;
                graphics.DrawRectangle(pen, inset, inset, ClientSize.Width - pen.Width, ClientSize.Height - pen.Width);
            }
            return;
        }
        if (m.Msg == 0x24 && FormBorderStyle == FormBorderStyle.None)
        {
            base.WndProc(ref m);
            var screen = Screen.FromHandle(Handle);
            var area = fullScreen ? screen.Bounds : screen.WorkingArea;
            var limits = Marshal.PtrToStructure<WindowMinMaxInfo>(m.LParam);
            limits.MaxPosition = new Point(area.Left - screen.Bounds.Left, area.Top - screen.Bounds.Top);
            limits.MaxSize = new Point(area.Width, area.Height);
            Marshal.StructureToPtr(limits, m.LParam, false);
            m.Result = IntPtr.Zero;
            return;
        }
        if (m.Msg == 0x83 && FormBorderStyle == FormBorderStyle.None)
        {
            m.Result = IntPtr.Zero; // Keep the custom title bar while retaining native resizing styles.
            return;
        }
        if (m.Msg == 0xA3 && m.WParam.ToInt32() == 2 && FormBorderStyle == FormBorderStyle.None)
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            return;
        }
        if (m.Msg == 0x84 && FormBorderStyle == FormBorderStyle.None)
        {
            base.WndProc(ref m);
            var point = PointToClient(new Point(unchecked((short)m.LParam.ToInt64()), unchecked((short)(m.LParam.ToInt64() >> 16))));
            int edge = ScaleUi(5);
            bool left = point.X < edge, right = point.X >= ClientSize.Width - edge;
            bool top = point.Y < edge, bottom = point.Y >= ClientSize.Height - edge;
            int hit = top ? left ? 13 : right ? 14 : 12 : bottom ? left ? 16 : right ? 17 : 15 : left ? 10 : right ? 11 : 0;
            if (hit != 0 && WindowState == FormWindowState.Normal) m.Result = (IntPtr)1; // Edge input is handled without a native frame.
            else if (!fullScreen && Controls.OfType<EditorTitleBar>().FirstOrDefault() is { } title && title.Bounds.Contains(point))
            {
                var child = title.GetChildAtPoint(new Point(point.X - title.Left, point.Y - title.Top));
                if (child is not Button) m.Result = (IntPtr)2; // HTCAPTION: native dragging and double-click messages.
            }
            return;
        }
        if (m.Msg == 0x8000 + 41)
        {
            if (!Enabled || OwnedForms.Any(form => form.Visible)) return;
            int action = m.WParam.ToInt32();
            if (action == 1 || action == 2) _ = CaptureAsync(action == 1);
            else { Restore(); if (action == 3) Configure(); }
            return;
        }
        if (m.Msg == 0x8000 + 42)
        {
            foreach (var dialog in OwnedForms) dialog.Close();
            Guard(SessionService.Clear); exiting = true; Close(); return;
        }
        if (m.Msg == 0x0312) { _ = CaptureAsync(m.WParam.ToInt32() == 1); return; }
        base.WndProc(ref m);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Control? focused = ActiveControl;
        while (focused is ContainerControl container && container.ActiveControl != null) focused = container.ActiveControl;
        if (Editor?.IsEditingText == true) return base.ProcessCmdKey(ref msg, keyData);
        if (focused is TextBoxBase || focused is NumericUpDown || focused is ComboBox) return base.ProcessCmdKey(ref msg, keyData);
        switch (keyData)
        {
            case Keys.Control | Keys.O: OpenImage(); return true;
            case Keys.Control | Keys.C: CopyFromMenu(); return true;
            case Keys.Control | Keys.X: CutFromMenu(); return true;
            case Keys.Control | Keys.V: PasteFromMenu(); return true;
            case Keys.Control | Keys.S: Save(); return true;
            case Keys.Control | Keys.Z: Undo(); return true;
            case Keys.Control | Keys.Y: Redo(); return true;
            case Keys.Control | Keys.W: if (tabs.SelectedIndex >= 0) CloseTab(tabs.SelectedIndex); return true;
            case Keys.Control | Keys.Tab: if (tabs.TabCount > 0) tabs.SelectedIndex = (tabs.SelectedIndex + 1) % tabs.TabCount; return true;
            case Keys.Control | Keys.D0: Fit(); return true;
            case Keys.Control | Keys.D: Editor?.DuplicateSelection(); return true;
            case Keys.Enter when Editor?.RegionCopyMode == true: CopyRegion(); return true;
            case Keys.Enter when Editor?.CropMode == true: ApplyCrop(); return true;
            case Keys.F11: ToggleFullScreen(); return true;
            case Keys.Escape when fullScreen && Editor?.CropMode != true: ToggleFullScreen(); return true;
            case Keys.Escape: if (Editor != null) { Editor.CancelCrop(); Editor.Tool = null; Editor.Document.Selection = null; Editor.Invalidate(); RefreshStatus(); } return true;
            case Keys.Delete: Editor?.DeleteSelection(); return true;
            case Keys.Control | Keys.Shift | Keys.C: menuAnnotationClipboard = false; if (Editor != null) clipboard.CopyAnnotations(Editor.Document, false); return true;
            case Keys.Control | Keys.Alt | Keys.C:
                if (Editor?.CropMode == true) { CopyRegion(); return true; }
                menuAnnotationClipboard = false; if (Editor != null) { clipboard.CopyAnnotations(Editor.Document, true); status.Text = Editor.Document.Selection == null ? "먼저 주석을 선택하세요." : "선택 주석을 복사했습니다."; } return true;
            case Keys.Control | Keys.Shift | Keys.V: if (Editor != null) { clipboard.Paste(Editor.Document); Editor.Invalidate(); RefreshStatus(); } return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
