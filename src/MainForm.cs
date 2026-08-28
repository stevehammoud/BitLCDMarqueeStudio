using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BitLCDMarqueeStudio
{
    internal sealed class MainForm : Form
    {
        private readonly CanvasPreviewControl _preview;
        private readonly TextBox _artistText;
        private readonly TextBox _titleText;
        private readonly TextBox _albumText;
        private readonly TextBox _featuredText;
        private readonly TextBox _yearText;
        private readonly TextBox _arcadeGameText;
        private readonly TextBox _arcadeRomText;
        private readonly ComboBox _arcadeSystemList;
        private readonly ComboBox _backgroundGalleryList;
        private readonly ComboBox _themeEntryList;
        private readonly Label _themeDropTarget;
        private readonly FlowLayoutPanel _resourceTiles;
        private readonly ResourceSearchService _searchService;
        private readonly List<JukeboxThemeEntry> _themeEntries;
        private readonly List<ResourceResult> _lastSearchResults;
        private readonly Stack<EditorState> _undoStack;
        private readonly Stack<EditorState> _redoStack;
        private ComboBox _resourceSourceFilter;
        private ComboBox _resourceTypeFilter;
        private Button _undoButton;
        private Button _redoButton;
        private RadioButton _jukeboxTypeButton;
        private RadioButton _arcadeTypeButton;
        private RadioButton _jukeboxFixedLayoutButton;
        private RadioButton _jukeboxCanvasLayoutButton;
        private FlowLayoutPanel _jukeboxSearchSection;
        private FlowLayoutPanel _arcadeSearchSection;
        private FlowLayoutPanel _jukeboxLayoutSection;
        private FlowLayoutPanel _backgroundSection;
        private FlowLayoutPanel _fixedLayoutSection;
        private FlowLayoutPanel _layerListSection;
        private FlowLayoutPanel _freeformToolsSection;
        private FlowLayoutPanel _textToolsSection;
        private FlowLayoutPanel _animationToolsSection;
        private TextBox _freeformTextInput;
        private ComboBox _fontFamilyList;
        private NumericUpDown _fontSizeInput;
        private ComboBox _animationTypeList;
        private NumericUpDown _animationStartInput;
        private NumericUpDown _animationDurationInput;
        private NumericUpDown _visibleFromInput;
        private NumericUpDown _visibleToInput;
        private ListBox _layerListBox;
        private Timer _animationPreviewTimer;
        private Stopwatch _animationPreviewClock;
        private CheckBox _fontBoldCheck;
        private CheckBox _fontItalicCheck;
        private CheckBox _fontUnderlineCheck;
        private CheckBox _textShadowCheck;
        private CheckBox _textGlowCheck;
        private RadioButton _alignLeftButton;
        private RadioButton _alignCenterButton;
        private RadioButton _alignRightButton;
        private Color _selectedTextColor = Color.White;
        private bool _restoringHistory;
        private bool _syncingTextControls;

        public MainForm()
        {
            Text = "BitLCD Marquee Studio";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1380, 760);
            Size = new Size(1520, 820);
            BackColor = Color.FromArgb(9, 14, 28);
            ForeColor = Color.White;

            _searchService = new ResourceSearchService();
            _themeEntries = new List<JukeboxThemeEntry>();
            _lastSearchResults = new List<ResourceResult>();
            _undoStack = new Stack<EditorState>();
            _redoStack = new Stack<EditorState>();
            _animationPreviewClock = new Stopwatch();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(12),
                BackColor = BackColor
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            Controls.Add(root);

            var left = CreateLeftPanel();
            root.Controls.Add(left, 0, 0);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor
            };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(right, 1, 0);

            _preview = new CanvasPreviewControl
            {
                Dock = DockStyle.Fill,
                LayoutModel = MarqueeLayout.CreateJukeboxDefault()
            };
            _preview.SelectedLayerChanged += delegate
            {
                UpdateHistoryButtons();
                SyncSelectedLayerControls();
            };
            right.Controls.Add(_preview, 0, 0);
            _animationPreviewTimer = new Timer { Interval = 33 };
            _animationPreviewTimer.Tick += OnAnimationPreviewTick;

            _resourceTiles = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 22, 40),
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8)
            };
            right.Controls.Add(CreateResourceGroup(), 0, 1);

            var tools = CreateToolsPanel();
            root.Controls.Add(tools, 2, 0);

            _artistText = GetField(left, "Artist");
            _titleText = GetField(left, "Title");
            _albumText = GetField(left, "Album / Release");
            _featuredText = GetField(left, "Featured Artist");
            _yearText = GetField(left, "Release Year");
            _arcadeGameText = GetField(left, "Arcade Game Name");
            _arcadeRomText = GetField(left, "ROM Name / Filename");
            _arcadeSystemList = GetComboBox(left, "ArcadeSystemList");
            _backgroundGalleryList = GetComboBox(left, "BackgroundGalleryList");
            _themeEntryList = GetThemeEntryList(left);
            _themeDropTarget = GetThemeDropTarget(left);
            OnMarqueeTypeChanged();
            UpdateHistoryButtons();
        }

        private bool IsArcadeMode()
        {
            return _arcadeTypeButton != null && _arcadeTypeButton.Checked;
        }

        private bool IsBlankCanvasMode()
        {
            return _jukeboxCanvasLayoutButton != null && _jukeboxCanvasLayoutButton.Checked;
        }

        private void OnMarqueeTypeChanged()
        {
            if (_preview == null) return;
            bool freeform = IsBlankCanvasMode();
            UpdateDynamicSections();
            RunHistoryAction(delegate
            {
                _preview.EditMode = freeform ? CanvasEditMode.Freeform : CanvasEditMode.JukeboxFixed;
            });
        }

        private void UpdateDynamicSections()
        {
            bool arcade = IsArcadeMode();
            bool blankCanvas = IsBlankCanvasMode();
            if (_jukeboxSearchSection != null) _jukeboxSearchSection.Visible = !arcade;
            if (_arcadeSearchSection != null) _arcadeSearchSection.Visible = arcade;
            if (_jukeboxLayoutSection != null) _jukeboxLayoutSection.Visible = true;
            if (_fixedLayoutSection != null) _fixedLayoutSection.Visible = !blankCanvas;
            if (_layerListSection != null) _layerListSection.Visible = blankCanvas;
            if (_freeformToolsSection != null) _freeformToolsSection.Visible = blankCanvas;
            if (_textToolsSection != null) _textToolsSection.Visible = blankCanvas;
            if (_animationToolsSection != null) _animationToolsSection.Visible = blankCanvas;
        }

        private Control CreateLeftPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 18, 34), Padding = new Padding(14) };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = panel.BackColor
            };
            panel.Controls.Add(flow);

            AddHeader(flow, "Marquee Type");
            _arcadeTypeButton = AddTypeButton(flow, "Arcade", true, true);
            _jukeboxTypeButton = AddTypeButton(flow, "Jukebox", false, true);
            _jukeboxTypeButton.CheckedChanged += delegate { OnMarqueeTypeChanged(); };
            _arcadeTypeButton.CheckedChanged += delegate { OnMarqueeTypeChanged(); };

            _jukeboxSearchSection = CreateSection("Jukebox Search");
            AddLabeledTextBox(_jukeboxSearchSection, "Artist", true);
            AddLabeledTextBox(_jukeboxSearchSection, "Title", true);
            AddLabeledTextBox(_jukeboxSearchSection, "Album / Release", false);
            AddLabeledTextBox(_jukeboxSearchSection, "Featured Artist", false);
            AddLabeledTextBox(_jukeboxSearchSection, "Release Year", false);
            AddThemeEntryPicker(_jukeboxSearchSection);
            flow.Controls.Add(_jukeboxSearchSection);

            _arcadeSearchSection = CreateSection("Arcade Search");
            AddLabeledTextBox(_arcadeSearchSection, "Arcade Game Name", false);
            AddLabeledTextBox(_arcadeSearchSection, "ROM Name / Filename", false);
            AddArcadeSystemPicker(_arcadeSearchSection);
            flow.Controls.Add(_arcadeSearchSection);

            var search = CreateButton("Search Resources");
            search.Click += OnSearchResources;
            flow.Controls.Add(search);

            _backgroundSection = CreateSection("Background");
            AddBackgroundControls(_backgroundSection);
            flow.Controls.Add(_backgroundSection);

            _jukeboxLayoutSection = CreateSection("Layout");
            var layoutRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            _jukeboxFixedLayoutButton = AddCompactTypeButton(layoutRow, "Fixed Panels", false);
            _jukeboxCanvasLayoutButton = AddCompactTypeButton(layoutRow, "Blank Canvas", true);
            _jukeboxFixedLayoutButton.CheckedChanged += delegate { OnMarqueeTypeChanged(); };
            _jukeboxCanvasLayoutButton.CheckedChanged += delegate { OnMarqueeTypeChanged(); };
            _jukeboxLayoutSection.Controls.Add(layoutRow);
            flow.Controls.Add(_jukeboxLayoutSection);

            AddHeader(flow, "Edit");

            var historyRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            _undoButton = CreateSmallButton("Undo");
            _undoButton.Click += delegate { Undo(); };
            _redoButton = CreateSmallButton("Redo");
            _redoButton.Click += delegate { Redo(); };
            historyRow.Controls.Add(_undoButton);
            historyRow.Controls.Add(_redoButton);
            flow.Controls.Add(historyRow);

            var generateStatic = CreatePrimaryButton("Generate Static JPG");
            generateStatic.Click += OnGenerateMarquee;
            flow.Controls.Add(generateStatic);

            var generateAnimated = CreateAccentButton("Generate Animated MP4");
            generateAnimated.Click += OnGenerateAnimatedMarquee;
            flow.Controls.Add(generateAnimated);

            return panel;
        }

        private Control CreateToolsPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 18, 34), Padding = new Padding(14) };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = panel.BackColor
            };
            panel.Controls.Add(flow);

            _fixedLayoutSection = CreateSection("Fixed Layout Tools");
            var clearRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddClearButton(clearRow, "Clear L", delegate { RunHistoryAction(delegate { _preview.ClearLeftImage(); }); });
            AddClearButton(clearRow, "Clear M", delegate { RunHistoryAction(delegate { _preview.ClearMiddleImage(); }); });
            AddClearButton(clearRow, "Clear R", delegate { RunHistoryAction(delegate { _preview.ClearRightImage(); }); });
            _fixedLayoutSection.Controls.Add(clearRow);
            flow.Controls.Add(_fixedLayoutSection);

            _layerListSection = CreateSection("Layers");
            AddLayerListTools(_layerListSection);
            flow.Controls.Add(_layerListSection);

            _freeformToolsSection = CreateSection("Freeform Tools");
            var moveRow1 = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddClearButton(moveRow1, "Up", delegate { RunHistoryAction(delegate { _preview.MoveSelectedLayer(0, -8); }); });
            AddClearButton(moveRow1, "Down", delegate { RunHistoryAction(delegate { _preview.MoveSelectedLayer(0, 8); }); });
            AddClearButton(moveRow1, "Delete", delegate { RunHistoryAction(delegate { _preview.DeleteSelectedLayer(); }); });
            _freeformToolsSection.Controls.Add(moveRow1);

            var moveRow2 = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddClearButton(moveRow2, "Left", delegate { RunHistoryAction(delegate { _preview.MoveSelectedLayer(-8, 0); }); });
            AddClearButton(moveRow2, "Right", delegate { RunHistoryAction(delegate { _preview.MoveSelectedLayer(8, 0); }); });
            _freeformToolsSection.Controls.Add(moveRow2);

            var scaleRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddClearButton(scaleRow, "+", delegate { RunHistoryAction(delegate { _preview.ScaleSelectedLayer(1.06f); }); });
            AddClearButton(scaleRow, "-", delegate { RunHistoryAction(delegate { _preview.ScaleSelectedLayer(0.94f); }); });
            _freeformToolsSection.Controls.Add(scaleRow);

            var flipRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddWideButton(flipRow, "Flip Horizontal", delegate { RunHistoryAction(delegate { _preview.FlipSelectedLayerHorizontal(); }); });
            AddWideButton(flipRow, "Flip Vertical", delegate { RunHistoryAction(delegate { _preview.FlipSelectedLayerVertical(); }); });
            _freeformToolsSection.Controls.Add(flipRow);

            var rotateRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddWideButton(rotateRow, "Rotate...", OnRotateSelectedLayer);
            _freeformToolsSection.Controls.Add(rotateRow);

            var layerRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddClearButton(layerRow, "Forward", delegate { RunHistoryAction(delegate { _preview.BringSelectedLayerForward(); }); });
            AddClearButton(layerRow, "Backward", delegate { RunHistoryAction(delegate { _preview.SendSelectedLayerBackward(); }); });
            AddClearButton(layerRow, "Clear Art", delegate { RunHistoryAction(delegate { _preview.ClearFreeformLayers(); }); });
            _freeformToolsSection.Controls.Add(layerRow);

            var modeRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddClearButton(modeRow, "Fit", delegate { RunHistoryAction(delegate { _preview.SetSelectedLayerImageMode(PanelImageMode.Fit); }); });
            AddClearButton(modeRow, "Fill", delegate { RunHistoryAction(delegate { _preview.SetSelectedLayerImageMode(PanelImageMode.Fill); }); });
            _freeformToolsSection.Controls.Add(modeRow);
            flow.Controls.Add(_freeformToolsSection);

            _textToolsSection = CreateSection("Text Tools");
            AddTextTools(_textToolsSection);
            flow.Controls.Add(_textToolsSection);

            _animationToolsSection = CreateSection("Animation");
            AddAnimationTools(_animationToolsSection);
            flow.Controls.Add(_animationToolsSection);

            return panel;
        }

        private GroupBox CreateResourceGroup()
        {
            var group = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Artwork Candidates",
                ForeColor = Color.White,
                BackColor = BackColor,
                Padding = new Padding(12)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = BackColor
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            group.Controls.Add(layout);

            var filterRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor
            };
            _resourceSourceFilter = CreateFilterCombo("All Sources");
            _resourceTypeFilter = CreateFilterCombo("All Types");
            _resourceSourceFilter.SelectedIndexChanged += delegate { RenderResourceResults(); };
            _resourceTypeFilter.SelectedIndexChanged += delegate { RenderResourceResults(); };
            filterRow.Controls.Add(_resourceSourceFilter);
            filterRow.Controls.Add(_resourceTypeFilter);

            layout.Controls.Add(filterRow, 0, 0);
            layout.Controls.Add(_resourceTiles, 0, 1);
            return group;
        }

        private static ComboBox CreateFilterCombo(string allText)
        {
            var combo = new ComboBox
            {
                Width = 210,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White,
                Margin = new Padding(0, 4, 8, 0)
            };
            combo.Items.Add(new FilterOption(allText, string.Empty));
            combo.SelectedIndex = 0;
            return combo;
        }

        private static void AddHeader(FlowLayoutPanel flow, string text)
        {
            flow.Controls.Add(new Label
            {
                Text = text,
                Width = 315,
                Height = 28,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 235, 255),
                TextAlign = ContentAlignment.MiddleLeft
            });
        }

        private static FlowLayoutPanel CreateSection(string header)
        {
            var section = new FlowLayoutPanel
            {
                Width = 315,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(12, 18, 34),
                Margin = new Padding(0, 6, 0, 0)
            };
            AddHeader(section, header);
            return section;
        }

        private static void AddDivider(FlowLayoutPanel flow)
        {
            flow.Controls.Add(new Label { Width = 315, Height = 12 });
        }

        private static void AddSmallNote(FlowLayoutPanel flow, string text)
        {
            flow.Controls.Add(new Label
            {
                Text = text,
                Width = 315,
                Height = 50,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(180, 195, 215)
            });
        }

        private static RadioButton AddTypeButton(FlowLayoutPanel flow, string text, bool isChecked, bool enabled)
        {
            var radio = new RadioButton
            {
                Text = enabled ? text : text + "  (future)",
                Checked = isChecked,
                Enabled = enabled,
                Width = 315,
                Height = 28,
                ForeColor = enabled ? Color.White : Color.FromArgb(120, 130, 150),
                BackColor = flow.BackColor
            };
            flow.Controls.Add(radio);
            return radio;
        }

        private static RadioButton AddCompactTypeButton(FlowLayoutPanel flow, string text, bool isChecked)
        {
            var radio = new RadioButton
            {
                Text = text,
                Checked = isChecked,
                Appearance = Appearance.Button,
                TextAlign = ContentAlignment.MiddleCenter,
                Width = 148,
                Height = 30,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(35, 45, 62),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 8, 0)
            };
            flow.Controls.Add(radio);
            return radio;
        }

        private static void AddLabeledTextBox(FlowLayoutPanel flow, string label, bool required)
        {
            flow.Controls.Add(new Label
            {
                Text = required ? label + " *" : label,
                Width = 315,
                Height = 20,
                ForeColor = Color.FromArgb(220, 230, 245)
            });
            flow.Controls.Add(new TextBox
            {
                Name = "Field_" + label,
                Width = 315,
                Height = 24,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            });
        }

        private static void AddArcadeSystemPicker(FlowLayoutPanel flow)
        {
            flow.Controls.Add(new Label
            {
                Text = "System",
                Width = 315,
                Height = 20,
                ForeColor = Color.FromArgb(220, 230, 245)
            });

            var combo = new ComboBox
            {
                Name = "ArcadeSystemList",
                Width = 315,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White
            };
            combo.Items.Add(ArcadeSystemOption.Blank);
            foreach (ArcadeSystemOption option in GetArcadeSystemOptions())
            {
                combo.Items.Add(option);
            }
            combo.SelectedIndex = 0;
            flow.Controls.Add(combo);
        }

        private static void AddThemeEntryPicker(FlowLayoutPanel flow)
        {
            flow.Controls.Add(new Label
            {
                Text = "Jukebox Theme File",
                Width = 315,
                Height = 20,
                ForeColor = Color.FromArgb(220, 230, 245)
            });

            var row = new FlowLayoutPanel
            {
                Width = 315,
                Height = 64,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };

            var list = new ComboBox
            {
                Name = "ThemeEntryList",
                Width = 315,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White
            };

            var load = new Button
            {
                Text = "Load File",
                Width = 315,
                Height = 28,
                BackColor = Color.FromArgb(42, 105, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            row.Controls.Add(list);
            row.Controls.Add(load);
            flow.Controls.Add(row);

            var drop = new Label
            {
                Name = "ThemeDropTarget",
                Text = "Drop theme file here",
                Width = 315,
                Height = 34,
                AllowDrop = true,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.FromArgb(190, 220, 240),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter
            };
            flow.Controls.Add(drop);

            load.Click += delegate
            {
                Form form = flow.FindForm();
                var main = form as MainForm;
                if (main != null) main.OnLoadThemeFile();
            };
            list.SelectedIndexChanged += delegate
            {
                Form form = flow.FindForm();
                var main = form as MainForm;
                if (main != null) main.OnThemeEntrySelected();
            };
            drop.DragEnter += delegate(object sender, DragEventArgs e)
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effect = DragDropEffects.Copy;
                }
            };
            drop.DragDrop += delegate(object sender, DragEventArgs e)
            {
                if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0) return;

                Form form = flow.FindForm();
                var main = form as MainForm;
                if (main != null) main.LoadThemeFile(files[0]);
            };
        }

        private void AddBackgroundControls(FlowLayoutPanel flow)
        {
            flow.Controls.Add(new Label
            {
                Text = "Gallery",
                Width = 315,
                Height = 20,
                ForeColor = Color.FromArgb(220, 230, 245)
            });
            var gallery = new ComboBox
            {
                Name = "BackgroundGalleryList",
                Width = 315,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White
            };
            foreach (BackgroundGalleryItem item in GetBackgroundGalleryItems())
            {
                gallery.Items.Add(item);
            }
            if (gallery.Items.Count > 0) gallery.SelectedIndex = 0;
            flow.Controls.Add(gallery);

            var row1 = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddClearButton(row1, "Use Gallery", OnUseGalleryBackground);
            AddClearButton(row1, "Solid Color", OnUseSolidBackground);
            flow.Controls.Add(row1);

            var row2 = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddClearButton(row2, "Load Image", OnLoadBackgroundImage);
            AddClearButton(row2, "Clear BG", delegate { RunHistoryAction(delegate { _preview.ClearBackgroundImage(); }); });
            flow.Controls.Add(row2);
        }

        private void AddTextTools(FlowLayoutPanel flow)
        {
            flow.Controls.Add(new Label
            {
                Text = "Text",
                Width = 315,
                Height = 20,
                ForeColor = Color.FromArgb(220, 230, 245)
            });

            _freeformTextInput = new TextBox
            {
                Width = 315,
                Height = 58,
                Multiline = true,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _freeformTextInput.TextChanged += delegate { ApplySelectedTextControls(); };
            flow.Controls.Add(_freeformTextInput);

            flow.Controls.Add(new Label
            {
                Text = "Font",
                Width = 315,
                Height = 20,
                ForeColor = Color.FromArgb(220, 230, 245)
            });

            _fontFamilyList = new ComboBox
            {
                Width = 315,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White
            };
            foreach (FontFamily family in FontFamily.Families.OrderBy(f => f.Name))
            {
                _fontFamilyList.Items.Add(new FontOption(family.Name, string.Empty));
            }
            if (_fontFamilyList.Items.Count > 0) _fontFamilyList.SelectedIndex = 0;
            _fontFamilyList.SelectedIndexChanged += delegate { ApplySelectedTextControls(); };
            flow.Controls.Add(_fontFamilyList);

            var fontRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 34,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            _fontSizeInput = new NumericUpDown
            {
                Width = 74,
                Height = 26,
                Minimum = 8,
                Maximum = 300,
                Value = 92,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White
            };
            _fontSizeInput.ValueChanged += delegate { ApplySelectedTextControls(); };
            fontRow.Controls.Add(_fontSizeInput);
            AddClearButton(fontRow, "Load Font", OnLoadCustomFont);
            AddClearButton(fontRow, "Color", OnChooseTextColor);
            flow.Controls.Add(fontRow);

            var styleRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 58,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            _fontBoldCheck = CreateStyleCheck("Bold");
            _fontItalicCheck = CreateStyleCheck("Italic");
            _fontUnderlineCheck = CreateStyleCheck("Underline");
            _textShadowCheck = CreateStyleCheck("Shadow");
            _textGlowCheck = CreateStyleCheck("Glow");
            _textShadowCheck.Checked = true;
            _textGlowCheck.Checked = true;
            _fontBoldCheck.CheckedChanged += delegate { ApplySelectedTextControls(); };
            _fontItalicCheck.CheckedChanged += delegate { ApplySelectedTextControls(); };
            _fontUnderlineCheck.CheckedChanged += delegate { ApplySelectedTextControls(); };
            _textShadowCheck.CheckedChanged += delegate { ApplySelectedTextControls(); };
            _textGlowCheck.CheckedChanged += delegate { ApplySelectedTextControls(); };
            styleRow.Controls.Add(_fontBoldCheck);
            styleRow.Controls.Add(_fontItalicCheck);
            styleRow.Controls.Add(_fontUnderlineCheck);
            styleRow.Controls.Add(_textShadowCheck);
            styleRow.Controls.Add(_textGlowCheck);
            flow.Controls.Add(styleRow);

            var alignRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 34,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            _alignLeftButton = AddMiniRadioButton(alignRow, "Left", false);
            _alignCenterButton = AddMiniRadioButton(alignRow, "Center", true);
            _alignRightButton = AddMiniRadioButton(alignRow, "Right", false);
            _alignLeftButton.CheckedChanged += delegate { ApplySelectedTextControls(); };
            _alignCenterButton.CheckedChanged += delegate { ApplySelectedTextControls(); };
            _alignRightButton.CheckedChanged += delegate { ApplySelectedTextControls(); };
            flow.Controls.Add(alignRow);

            var addText = CreateButton("Add Text Box");
            addText.Click += OnAddTextLayer;
            flow.Controls.Add(addText);
        }

        private static CheckBox CreateStyleCheck(string text)
        {
            return new CheckBox
            {
                Text = text,
                Width = 96,
                Height = 24,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(12, 18, 34),
                FlatStyle = FlatStyle.Flat
            };
        }

        private static RadioButton AddMiniRadioButton(Control parent, string text, bool isChecked)
        {
            var button = new RadioButton
            {
                Text = text,
                Width = 96,
                Height = 24,
                Checked = isChecked,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(12, 18, 34),
                FlatStyle = FlatStyle.Flat
            };
            parent.Controls.Add(button);
            return button;
        }

        private void OnLoadCustomFont(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Load Font";
                dialog.Filter = "Font files (*.ttf;*.otf)|*.ttf;*.otf|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string label = Path.GetFileNameWithoutExtension(dialog.FileName);
                var option = new FontOption(label + " (custom)", dialog.FileName);
                _fontFamilyList.Items.Add(option);
                _fontFamilyList.SelectedItem = option;
                ApplySelectedTextControls();
            }
        }

        private void OnChooseTextColor(object sender, EventArgs e)
        {
            using (var dialog = new ColorDialog())
            {
                dialog.Color = _selectedTextColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _selectedTextColor = dialog.Color;
                    ApplySelectedTextControls();
                }
            }
        }

        private void OnAddTextLayer(object sender, EventArgs e)
        {
            string text = _freeformTextInput == null ? string.Empty : (_freeformTextInput.Text ?? string.Empty);
            if (string.IsNullOrWhiteSpace(text)) text = "Text";

            var fontOption = _fontFamilyList == null ? null : _fontFamilyList.SelectedItem as FontOption;
            string fontName = fontOption == null ? "Arial" : fontOption.FontFamilyName;
            string fontPath = fontOption == null ? string.Empty : fontOption.FontFilePath;
            TextJustification alignment = TextJustification.Center;
            if (_alignLeftButton != null && _alignLeftButton.Checked) alignment = TextJustification.Left;
            if (_alignRightButton != null && _alignRightButton.Checked) alignment = TextJustification.Right;

            RunHistoryAction(delegate
            {
                _preview.AddFreeformText(
                    text,
                    fontName,
                    fontPath,
                    _fontSizeInput == null ? 92f : (float)_fontSizeInput.Value,
                    _fontBoldCheck != null && _fontBoldCheck.Checked,
                    _fontItalicCheck != null && _fontItalicCheck.Checked,
                    _fontUnderlineCheck != null && _fontUnderlineCheck.Checked,
                    _selectedTextColor,
                    alignment,
                    _textShadowCheck == null || _textShadowCheck.Checked,
                    _textGlowCheck == null || _textGlowCheck.Checked);
            });
            SyncSelectedLayerControls();
            if (_freeformTextInput != null)
            {
                _freeformTextInput.Focus();
                _freeformTextInput.SelectAll();
            }
        }

        private void OnRotateSelectedLayer(object sender, EventArgs e)
        {
            string input = PromptForText("Rotate Layer", "Enter rotation degrees (-180 to 180):", "0");
            if (string.IsNullOrWhiteSpace(input)) return;

            float degrees;
            if (!float.TryParse(input.Trim(), out degrees))
            {
                MessageBox.Show(this, "Enter a valid number of degrees.", "Invalid Rotation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RunHistoryAction(delegate { _preview.RotateSelectedLayer(degrees); });
        }

        private void AddAnimationTools(FlowLayoutPanel flow)
        {
            flow.Controls.Add(CreateFieldLabel("Preset"));

            _animationTypeList = new ComboBox
            {
                Width = 315,
                Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 0, 8)
            };
            foreach (LayerAnimationType value in Enum.GetValues(typeof(LayerAnimationType)))
            {
                _animationTypeList.Items.Add(value);
            }
            _animationTypeList.SelectedItem = LayerAnimationType.None;
            flow.Controls.Add(_animationTypeList);

            var timingPanel = new Panel
            {
                Width = 315,
                BackColor = flow.BackColor,
                Margin = new Padding(0, 0, 0, 8)
            };
            timingPanel.Height = 104;
            timingPanel.Controls.Add(CreatePositionedLabel("Start (s)", 0, 0));
            _animationStartInput = CreateSecondsInput(0m);
            _animationStartInput.Left = 0;
            _animationStartInput.Top = 20;
            timingPanel.Controls.Add(_animationStartInput);

            timingPanel.Controls.Add(CreatePositionedLabel("Duration (s)", 0, 54));
            _animationDurationInput = CreateSecondsInput(1.5m);
            _animationDurationInput.Left = 0;
            _animationDurationInput.Top = 74;
            timingPanel.Controls.Add(_animationDurationInput);
            flow.Controls.Add(timingPanel);

            var visibilityPanel = new Panel
            {
                Width = 315,
                Height = 104,
                BackColor = flow.BackColor,
                Margin = new Padding(0, 0, 0, 8)
            };
            visibilityPanel.Controls.Add(CreatePositionedLabel("Visible From (s)", 0, 0));
            _visibleFromInput = CreateSecondsInput(0m);
            _visibleFromInput.Left = 0;
            _visibleFromInput.Top = 20;
            visibilityPanel.Controls.Add(_visibleFromInput);

            visibilityPanel.Controls.Add(CreatePositionedLabel("Visible To (s)", 0, 54));
            _visibleToInput = CreateSecondsInput(20m);
            _visibleToInput.Left = 0;
            _visibleToInput.Top = 74;
            visibilityPanel.Controls.Add(_visibleToInput);
            flow.Controls.Add(visibilityPanel);

            var row = new FlowLayoutPanel
            {
                Width = 315,
                Height = 38,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddMediumButton(row, "Apply", OnApplyLayerAnimation);
            AddMediumButton(row, "Preview", OnStartAnimationPreview);
            AddMediumButton(row, "Stop", OnStopAnimationPreview);
            flow.Controls.Add(row);
        }

        private void AddLayerListTools(FlowLayoutPanel flow)
        {
            _layerListBox = new ListBox
            {
                Width = 315,
                Height = 100,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _layerListBox.SelectedIndexChanged += OnLayerListSelected;
            flow.Controls.Add(_layerListBox);

            var row = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            AddMediumButton(row, "Duplicate", OnDuplicateSelectedLayer);
            AddMediumButton(row, "Refresh", delegate { RefreshLayerList(); });
            flow.Controls.Add(row);
        }

        private void OnLayerListSelected(object sender, EventArgs e)
        {
            if (_syncingTextControls || _layerListBox == null || _layerListBox.SelectedIndex < 0) return;
            _preview.SelectLayer(_layerListBox.SelectedIndex);
        }

        private void OnDuplicateSelectedLayer(object sender, EventArgs e)
        {
            RunHistoryAction(delegate { _preview.DuplicateSelectedLayer(); });
            RefreshLayerList();
        }

        private void RefreshLayerList()
        {
            if (_layerListBox == null || _preview == null) return;
            IList<FreeformArtLayer> layers = _preview.GetLayerSnapshots();
            _syncingTextControls = true;
            try
            {
                _layerListBox.Items.Clear();
                for (int i = 0; i < layers.Count; i++)
                {
                    _layerListBox.Items.Add(BuildLayerLabel(i, layers[i]));
                }
                if (_preview.SelectedLayerIndex >= 0 && _preview.SelectedLayerIndex < _layerListBox.Items.Count)
                {
                    _layerListBox.SelectedIndex = _preview.SelectedLayerIndex;
                }
            }
            finally
            {
                _syncingTextControls = false;
            }
        }

        private static string BuildLayerLabel(int index, FreeformArtLayer layer)
        {
            string kind = layer != null && layer.IsTextLayer ? "Text" : "Image";
            string name = string.Empty;
            if (layer != null && layer.IsTextLayer)
            {
                name = layer.Text ?? string.Empty;
            }
            else if (layer != null)
            {
                name = Path.GetFileNameWithoutExtension(layer.ImagePath ?? string.Empty);
            }
            if (name.Length > 22) name = name.Substring(0, 22) + "...";
            if (string.IsNullOrWhiteSpace(name)) name = "Layer";
            return string.Format("{0}. {1}: {2}", index + 1, kind, name);
        }

        private static Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Width = 315,
                Height = 20,
                ForeColor = Color.FromArgb(220, 230, 245),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 2)
            };
        }

        private static Label CreatePositionedLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Left = x,
                Top = y,
                Width = 315,
                Height = 18,
                ForeColor = Color.FromArgb(190, 215, 240),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static NumericUpDown CreateSecondsInput(decimal value)
        {
            return new NumericUpDown
            {
                Width = 315,
                Height = 28,
                DecimalPlaces = 1,
                Increment = 0.1m,
                Minimum = 0,
                Maximum = 3600,
                Value = value,
                BackColor = Color.FromArgb(22, 30, 50),
                ForeColor = Color.White,
                Margin = new Padding(0)
            };
        }

        private void OnApplyLayerAnimation(object sender, EventArgs e)
        {
            LayerAnimationType type = LayerAnimationType.None;
            if (_animationTypeList != null && _animationTypeList.SelectedItem is LayerAnimationType)
            {
                type = (LayerAnimationType)_animationTypeList.SelectedItem;
            }

            float start = _animationStartInput == null ? 0f : (float)_animationStartInput.Value;
            float duration = _animationDurationInput == null ? 1f : (float)_animationDurationInput.Value;
            float visibleFrom = _visibleFromInput == null ? 0f : (float)_visibleFromInput.Value;
            float visibleTo = _visibleToInput == null ? 20f : (float)_visibleToInput.Value;
            RunHistoryAction(delegate { _preview.UpdateSelectedLayerAnimation(type, start, duration, visibleFrom, visibleTo); });
        }

        private void OnStartAnimationPreview(object sender, EventArgs e)
        {
            OnApplyLayerAnimation(sender, e);
            _animationPreviewClock.Reset();
            _animationPreviewClock.Start();
            _animationPreviewTimer.Start();
            _preview.SetAnimationPreview(0f, true);
        }

        private void OnStopAnimationPreview(object sender, EventArgs e)
        {
            _animationPreviewTimer.Stop();
            _animationPreviewClock.Reset();
            _preview.SetAnimationPreview(0f, false);
        }

        private void OnAnimationPreviewTick(object sender, EventArgs e)
        {
            float seconds = (float)_animationPreviewClock.Elapsed.TotalSeconds;
            float previewDuration = Math.Max(1f, _preview.GetAnimationDurationSeconds());
            if (seconds > previewDuration)
            {
                _animationPreviewClock.Restart();
                seconds = 0f;
            }
            _preview.SetAnimationPreview(seconds, true);
        }

        private void SyncSelectedLayerControls()
        {
            if (_syncingTextControls || _preview == null) return;
            FreeformArtLayer layer = _preview.GetSelectedLayerSnapshot();
            if (layer == null) return;

            _syncingTextControls = true;
            try
            {
                if (_animationTypeList != null) _animationTypeList.SelectedItem = layer.AnimationType;
                if (_animationStartInput != null)
                {
                    decimal value = (decimal)Math.Max((float)_animationStartInput.Minimum, Math.Min((float)_animationStartInput.Maximum, layer.AnimationStartSeconds));
                    _animationStartInput.Value = value;
                }
                if (_animationDurationInput != null)
                {
                    decimal value = (decimal)Math.Max((float)_animationDurationInput.Minimum, Math.Min((float)_animationDurationInput.Maximum, layer.AnimationDurationSeconds <= 0 ? 1f : layer.AnimationDurationSeconds));
                    _animationDurationInput.Value = value;
                }
                if (_visibleFromInput != null)
                {
                    decimal value = (decimal)Math.Max((float)_visibleFromInput.Minimum, Math.Min((float)_visibleFromInput.Maximum, layer.VisibleFromSeconds));
                    _visibleFromInput.Value = value;
                }
                if (_visibleToInput != null)
                {
                    decimal value = (decimal)Math.Max((float)_visibleToInput.Minimum, Math.Min((float)_visibleToInput.Maximum, layer.VisibleToSeconds <= 0 ? 20f : layer.VisibleToSeconds));
                    _visibleToInput.Value = value;
                }
                RefreshLayerList();

                if (layer.IsTextLayer)
                {
                    if (_freeformTextInput != null) _freeformTextInput.Text = layer.Text ?? string.Empty;
                    SelectFontOption(layer);
                    if (_fontSizeInput != null)
                    {
                        decimal value = (decimal)Math.Max((float)_fontSizeInput.Minimum, Math.Min((float)_fontSizeInput.Maximum, layer.FontSize <= 0 ? 92f : layer.FontSize));
                        _fontSizeInput.Value = value;
                    }
                    if (_fontBoldCheck != null) _fontBoldCheck.Checked = layer.FontBold;
                    if (_fontItalicCheck != null) _fontItalicCheck.Checked = layer.FontItalic;
                    if (_fontUnderlineCheck != null) _fontUnderlineCheck.Checked = layer.FontUnderline;
                    if (_textShadowCheck != null) _textShadowCheck.Checked = layer.TextShadow;
                    if (_textGlowCheck != null) _textGlowCheck.Checked = layer.TextGlow;
                    _selectedTextColor = layer.TextColor.IsEmpty ? Color.White : layer.TextColor;
                    if (_alignLeftButton != null) _alignLeftButton.Checked = layer.TextAlignment == TextJustification.Left;
                    if (_alignCenterButton != null) _alignCenterButton.Checked = layer.TextAlignment == TextJustification.Center;
                    if (_alignRightButton != null) _alignRightButton.Checked = layer.TextAlignment == TextJustification.Right;
                }
            }
            finally
            {
                _syncingTextControls = false;
            }

            if (layer.IsTextLayer && _freeformTextInput != null)
            {
                _freeformTextInput.Focus();
                _freeformTextInput.SelectAll();
            }
        }

        private void SelectFontOption(FreeformArtLayer layer)
        {
            if (_fontFamilyList == null || layer == null) return;
            string fontPath = layer.FontFilePath ?? string.Empty;
            string fontName = layer.FontFamilyName ?? string.Empty;
            foreach (object item in _fontFamilyList.Items)
            {
                var option = item as FontOption;
                if (option == null) continue;
                if (!string.IsNullOrWhiteSpace(fontPath) &&
                    string.Equals(option.FontFilePath ?? string.Empty, fontPath, StringComparison.OrdinalIgnoreCase))
                {
                    _fontFamilyList.SelectedItem = option;
                    return;
                }
                if (string.IsNullOrWhiteSpace(fontPath) &&
                    string.Equals(option.FontFamilyName ?? string.Empty, fontName, StringComparison.OrdinalIgnoreCase))
                {
                    _fontFamilyList.SelectedItem = option;
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(fontPath) && File.Exists(fontPath))
            {
                var custom = new FontOption(string.IsNullOrWhiteSpace(fontName) ? Path.GetFileNameWithoutExtension(fontPath) + " (custom)" : fontName, fontPath);
                _fontFamilyList.Items.Add(custom);
                _fontFamilyList.SelectedItem = custom;
            }
        }

        private void ApplySelectedTextControls()
        {
            if (_syncingTextControls || _preview == null) return;
            FreeformArtLayer selected = _preview.GetSelectedLayerSnapshot();
            if (selected == null || !selected.IsTextLayer) return;

            var fontOption = _fontFamilyList == null ? null : _fontFamilyList.SelectedItem as FontOption;
            string fontName = fontOption == null ? "Arial" : fontOption.FontFamilyName;
            string fontPath = fontOption == null ? string.Empty : fontOption.FontFilePath;
            TextJustification alignment = TextJustification.Center;
            if (_alignLeftButton != null && _alignLeftButton.Checked) alignment = TextJustification.Left;
            if (_alignRightButton != null && _alignRightButton.Checked) alignment = TextJustification.Right;

            _preview.UpdateSelectedTextLayer(
                _freeformTextInput == null ? string.Empty : (_freeformTextInput.Text ?? string.Empty),
                fontName,
                fontPath,
                _fontSizeInput == null ? 92f : (float)_fontSizeInput.Value,
                _fontBoldCheck != null && _fontBoldCheck.Checked,
                _fontItalicCheck != null && _fontItalicCheck.Checked,
                _fontUnderlineCheck != null && _fontUnderlineCheck.Checked,
                _selectedTextColor,
                alignment,
                _textShadowCheck == null || _textShadowCheck.Checked,
                _textGlowCheck == null || _textGlowCheck.Checked);
        }

        private static Button CreateButton(string text)
        {
            return CreateStyledButton(text, 315, 32, Color.FromArgb(45, 112, 255), Color.FromArgb(120, 180, 255));
        }

        private static Button CreatePrimaryButton(string text)
        {
            return CreateStyledButton(text, 315, 34, Color.FromArgb(24, 170, 92), Color.FromArgb(120, 255, 180));
        }

        private static Button CreateAccentButton(string text)
        {
            return CreateStyledButton(text, 315, 34, Color.FromArgb(190, 72, 230), Color.FromArgb(245, 150, 255));
        }

        private static Button CreateStyledButton(string text, int width, int height, Color backColor, Color borderColor)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = Lighten(backColor, 28);
            button.FlatAppearance.MouseDownBackColor = Darken(backColor, 24);
            return button;
        }

        private static TextBox GetField(Control root, string label)
        {
            Control[] matches = root.Controls.Find("Field_" + label, true);
            return matches.Length > 0 ? (TextBox)matches[0] : null;
        }

        private static ComboBox GetComboBox(Control root, string name)
        {
            Control[] matches = root.Controls.Find(name, true);
            return matches.Length > 0 ? (ComboBox)matches[0] : null;
        }

        private static ComboBox GetThemeEntryList(Control root)
        {
            Control[] matches = root.Controls.Find("ThemeEntryList", true);
            return matches.Length > 0 ? (ComboBox)matches[0] : null;
        }

        private static Label GetThemeDropTarget(Control root)
        {
            Control[] matches = root.Controls.Find("ThemeDropTarget", true);
            return matches.Length > 0 ? (Label)matches[0] : null;
        }

        private static void AddClearButton(Control parent, string text, EventHandler click)
        {
            var button = CreateSmallButton(text);
            button.Click += click;
            parent.Controls.Add(button);
        }

        private static void AddWideButton(Control parent, string text, EventHandler click)
        {
            var button = CreateSmallButton(text);
            button.Width = 148;
            button.Click += click;
            parent.Controls.Add(button);
        }

        private static void AddMediumButton(Control parent, string text, EventHandler click)
        {
            var button = CreateSmallButton(text);
            button.Width = 97;
            button.Click += click;
            parent.Controls.Add(button);
        }

        private static Button CreateSmallButton(string text)
        {
            return CreateStyledButton(text, 75, 30, Color.FromArgb(42, 62, 94), Color.FromArgb(105, 160, 235));
        }

        private static Color Lighten(Color color, int amount)
        {
            return Color.FromArgb(color.A, Math.Min(255, color.R + amount), Math.Min(255, color.G + amount), Math.Min(255, color.B + amount));
        }

        private static Color Darken(Color color, int amount)
        {
            return Color.FromArgb(color.A, Math.Max(0, color.R - amount), Math.Max(0, color.G - amount), Math.Max(0, color.B - amount));
        }

        private void OnLoadThemeFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Load Jukebox Theme File";
                dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                LoadThemeFile(dialog.FileName);
            }
        }

        private void LoadThemeFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            var loaded = new List<JukeboxThemeEntry>();
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                JukeboxThemeEntry entry;
                if (TryParseThemeEntry(lines[i], i + 1, out entry))
                {
                    loaded.Add(entry);
                }
            }

            _themeEntries.Clear();
            _themeEntries.AddRange(loaded);
            _themeEntryList.Items.Clear();
            foreach (JukeboxThemeEntry entry in _themeEntries)
            {
                _themeEntryList.Items.Add(entry);
            }

            if (_themeEntries.Count > 0)
            {
                _themeEntryList.SelectedIndex = 0;
                if (_themeDropTarget != null) _themeDropTarget.Text = Path.GetFileName(path);
                MessageBox.Show(this, "Loaded " + _themeEntries.Count + " jukebox theme entries.", "Theme File Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                if (_themeDropTarget != null) _themeDropTarget.Text = "Drop theme file here";
                    MessageBox.Show(this, "No valid entries were found. Expected each line to look like:\r\nartist - title\r\nartist - title - album", "Theme File Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnThemeEntrySelected()
        {
            if (_restoringHistory) return;
            if (_themeEntryList == null || _themeEntryList.SelectedItem == null) return;
            var entry = _themeEntryList.SelectedItem as JukeboxThemeEntry;
            if (entry == null) return;

            RunHistoryAction(delegate { ApplyThemeEntry(entry); });
        }

        private void ApplyThemeEntry(JukeboxThemeEntry entry)
        {
            _artistText.Text = entry.Artist;
            _titleText.Text = entry.Title;
            _albumText.Text = entry.Album;
            _preview.SetJukeboxText(entry.Artist, entry.Title, _featuredText.Text);
        }

        private static bool TryParseThemeEntry(string line, int lineNumber, out JukeboxThemeEntry entry)
        {
            entry = null;
            string text = (line ?? string.Empty).Trim();
            if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal)) return false;

            int first = text.IndexOf(" - ", StringComparison.Ordinal);
            if (first < 1) return false;

            string artist = text.Substring(0, first).Trim();
            string title;
            string album;
            int second = text.IndexOf(" - ", first + 3, StringComparison.Ordinal);
            if (second > first + 3)
            {
                title = text.Substring(first + 3, second - first - 3).Trim();
                album = text.Substring(second + 3).Trim();
            }
            else
            {
                title = text.Substring(first + 3).Trim();
                album = string.Empty;
            }
            if (artist.Length == 0 || title.Length == 0) return false;

            entry = new JukeboxThemeEntry(lineNumber, artist, title, album);
            return true;
        }

        private ArcadeSystemOption SelectedArcadeSystem()
        {
            if (_arcadeSystemList == null || _arcadeSystemList.SelectedItem == null) return ArcadeSystemOption.Blank;
            var option = _arcadeSystemList.SelectedItem as ArcadeSystemOption;
            return option ?? ArcadeSystemOption.Blank;
        }

        private void OnSearchResources(object sender, EventArgs e)
        {
            IList<ResourceResult> results;
            if (IsArcadeMode())
            {
                string gameName = (_arcadeGameText.Text ?? string.Empty).Trim();
                string romName = (_arcadeRomText.Text ?? string.Empty).Trim();
                if (gameName.Length == 0 && romName.Length == 0)
                {
                    MessageBox.Show(this, "Enter an Arcade Game Name or ROM Name / Filename for Arcade searches.", "Missing Required Fields", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var request = new ArcadeSearchRequest
                {
                    GameName = gameName,
                    RomName = romName,
                    SystemId = SelectedArcadeSystem().ScreenScraperSystemId,
                    SystemName = SelectedArcadeSystem().Name,
                    SystemSuffix = SelectedArcadeSystem().Suffix
                };

                results = null;
                _resourceTiles.Controls.Clear();
                _resourceTiles.Controls.Add(CreateStatusTile("Searching ScreenScraper..."));
                UseWaitCursor = true;
                Enabled = false;
                try
                {
                    results = _searchService.SearchArcade(request);
                }
                catch (Exception ex)
                {
                    _resourceTiles.Controls.Clear();
                    _resourceTiles.Controls.Add(CreateStatusTile("Search failed: " + ex.Message));
                    MessageBox.Show(this, ex.Message, "Search Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                finally
                {
                    Enabled = true;
                    UseWaitCursor = false;
                }
            }
            else
            {
                string artist = (_artistText.Text ?? string.Empty).Trim();
                string title = (_titleText.Text ?? string.Empty).Trim();
                string album = (_albumText.Text ?? string.Empty).Trim();
                string featured = (_featuredText.Text ?? string.Empty).Trim();
                string year = (_yearText.Text ?? string.Empty).Trim();
                if (new[] { artist, title, album, featured, year }.All(string.IsNullOrWhiteSpace))
                {
                    MessageBox.Show(this, "At least one Jukebox search field is required.", "Missing Required Field", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var request = new JukeboxSearchRequest
                {
                    Artist = artist,
                    Title = title,
                    AlbumOrRelease = album,
                    FeaturedArtist = featured,
                    ReleaseYear = year
                };

                RunHistoryAction(delegate { _preview.SetJukeboxText(request.Artist, request.Title, request.FeaturedArtist); });
                results = null;
                _resourceTiles.Controls.Clear();
                _resourceTiles.Controls.Add(CreateStatusTile("Searching resources..."));
                UseWaitCursor = true;
                Enabled = false;
                try
                {
                    results = _searchService.SearchJukebox(request);
                }
                catch (Exception ex)
                {
                    _resourceTiles.Controls.Clear();
                    _resourceTiles.Controls.Add(CreateStatusTile("Search failed: " + ex.Message));
                    MessageBox.Show(this, ex.Message, "Search Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                finally
                {
                    Enabled = true;
                    UseWaitCursor = false;
                }
            }

            _lastSearchResults.Clear();
            _lastSearchResults.AddRange(results ?? new List<ResourceResult>());
            PopulateResourceFilters();
            RenderResourceResults();
        }

        private void OnUseGalleryBackground(object sender, EventArgs e)
        {
            if (_backgroundGalleryList == null || _backgroundGalleryList.SelectedItem == null) return;
            var item = _backgroundGalleryList.SelectedItem as BackgroundGalleryItem;
            if (item == null || string.IsNullOrWhiteSpace(item.ImagePath) || !File.Exists(item.ImagePath)) return;
            RunHistoryAction(delegate { _preview.SetBackgroundImage(item.ImagePath); });
        }

        private void OnUseSolidBackground(object sender, EventArgs e)
        {
            using (var dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.Color = Color.FromArgb(18, 20, 42);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string path = CreateSolidBackground(dialog.Color);
                RunHistoryAction(delegate { _preview.SetBackgroundImage(path); });
            }
        }

        private void OnLoadBackgroundImage(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Load Background Image";
                dialog.Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                RunHistoryAction(delegate { _preview.SetBackgroundImage(dialog.FileName); });
            }
        }

        private static IEnumerable<BackgroundGalleryItem> GetBackgroundGalleryItems()
        {
            var items = new List<BackgroundGalleryItem>();
            string galleryDir = FindDirectoryUp(AppDomain.CurrentDomain.BaseDirectory, "assets", "backgrounds");
            if (Directory.Exists(galleryDir))
            {
                foreach (string path in Directory.GetFiles(galleryDir)
                    .Where(IsSupportedImageFile)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    items.Add(new BackgroundGalleryItem(Path.GetFileNameWithoutExtension(path), path));
                }
            }

            string generatedDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "backgrounds");
            Directory.CreateDirectory(generatedDir);
            AddBuiltInBackground(items, generatedDir, "Neon Lines", DrawNeonLinesBackground);
            AddBuiltInBackground(items, generatedDir, "Retro Circles", DrawRetroCirclesBackground);
            AddBuiltInBackground(items, generatedDir, "Dark Equalizer", DrawDarkEqualizerBackground);
            AddBuiltInBackground(items, generatedDir, "Arcade Grid", DrawArcadeGridBackground);
            return items;
        }

        private static string FindDirectoryUp(string baseDirectory, params string[] pathParts)
        {
            string current = baseDirectory;
            for (int i = 0; i < 6 && !string.IsNullOrWhiteSpace(current); i++)
            {
                string candidate = Path.Combine(new[] { current }.Concat(pathParts).ToArray());
                if (Directory.Exists(candidate)) return candidate;
                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }
            return Path.Combine(new[] { baseDirectory }.Concat(pathParts).ToArray());
        }

        private static void AddBuiltInBackground(ICollection<BackgroundGalleryItem> items, string directory, string name, Action<Graphics, Rectangle> draw)
        {
            string fileName = RegexSafeFileName(name) + ".png";
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                using (var bitmap = new Bitmap(MarqueeLayout.CanvasWidth, MarqueeLayout.CanvasHeight))
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    draw(g, new Rectangle(0, 0, MarqueeLayout.CanvasWidth, MarqueeLayout.CanvasHeight));
                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            items.Add(new BackgroundGalleryItem(name, path));
        }

        private static string CreateSolidBackground(Color color)
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "backgrounds");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, string.Format("solid_{0:X2}{1:X2}{2:X2}.png", color.R, color.G, color.B));
            if (File.Exists(path)) return path;
            using (var bitmap = new Bitmap(MarqueeLayout.CanvasWidth, MarqueeLayout.CanvasHeight))
            using (var g = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, 0, 0, bitmap.Width, bitmap.Height);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            return path;
        }

        private static bool IsSupportedImageFile(string path)
        {
            string ext = Path.GetExtension(path);
            return string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".bmp", StringComparison.OrdinalIgnoreCase);
        }

        private static string RegexSafeFileName(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "background" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                text = text.Replace(c, '_');
            }
            return text.Replace(' ', '_').ToLowerInvariant();
        }

        private static void DrawNeonLinesBackground(Graphics g, Rectangle rect)
        {
            using (var bg = new LinearGradientBrush(rect, Color.FromArgb(12, 12, 26), Color.FromArgb(42, 14, 55), 0f))
            {
                g.FillRectangle(bg, rect);
            }
            Color[] colors =
            {
                Color.FromArgb(150, 255, 55, 190),
                Color.FromArgb(140, 35, 215, 255),
                Color.FromArgb(125, 255, 190, 60)
            };
            for (int i = -220; i < rect.Width + 260; i += 88)
            {
                using (var pen = new Pen(colors[Math.Abs(i / 88) % colors.Length], 7f))
                {
                    g.DrawLine(pen, i, rect.Top, i + 250, rect.Bottom);
                }
            }
            DrawVignette(g, rect);
        }

        private static void DrawRetroCirclesBackground(Graphics g, Rectangle rect)
        {
            using (var bg = new LinearGradientBrush(rect, Color.FromArgb(18, 18, 30), Color.FromArgb(20, 48, 60), 0f))
            {
                g.FillRectangle(bg, rect);
            }
            Color[] colors =
            {
                Color.FromArgb(95, 255, 210, 70),
                Color.FromArgb(95, 255, 70, 185),
                Color.FromArgb(95, 60, 230, 255),
                Color.FromArgb(80, 130, 255, 120)
            };
            for (int x = -60; x < rect.Width + 100; x += 150)
            {
                for (int y = -30; y < rect.Height + 80; y += 96)
                {
                    int colorIndex = Math.Abs((x / 150) + (y / 96)) % colors.Length;
                    using (var pen = new Pen(colors[colorIndex], 4f))
                    {
                        g.DrawEllipse(pen, x, y, 96, 96);
                        g.DrawEllipse(pen, x + 26, y + 26, 44, 44);
                    }
                }
            }
            DrawVignette(g, rect);
        }

        private static void DrawDarkEqualizerBackground(Graphics g, Rectangle rect)
        {
            using (var bg = new LinearGradientBrush(rect, Color.FromArgb(8, 10, 20), Color.FromArgb(32, 18, 38), 90f))
            {
                g.FillRectangle(bg, rect);
            }
            int barWidth = 18;
            int gap = 12;
            for (int i = 0; i < rect.Width / (barWidth + gap) + 2; i++)
            {
                int height = 40 + ((i * 37) % 230);
                int x = i * (barWidth + gap);
                int y = rect.Bottom - height - 20;
                using (var brush = new LinearGradientBrush(new Rectangle(x, y, barWidth, height), Color.FromArgb(210, 255, 80, 190), Color.FromArgb(210, 60, 220, 255), 90f))
                {
                    g.FillRectangle(brush, x, y, barWidth, height);
                }
            }
            using (var fade = new SolidBrush(Color.FromArgb(135, 4, 6, 14)))
            {
                g.FillRectangle(fade, rect);
            }
            DrawVignette(g, rect);
        }

        private static void DrawArcadeGridBackground(Graphics g, Rectangle rect)
        {
            using (var bg = new LinearGradientBrush(rect, Color.FromArgb(10, 8, 28), Color.FromArgb(26, 8, 38), 90f))
            {
                g.FillRectangle(bg, rect);
            }
            Point horizon = new Point(rect.Width / 2, rect.Height / 2);
            using (var pen = new Pen(Color.FromArgb(105, 255, 65, 210), 2f))
            {
                for (int x = -rect.Width; x <= rect.Width * 2; x += 110)
                {
                    g.DrawLine(pen, horizon.X, horizon.Y, x, rect.Bottom);
                }
                for (int y = horizon.Y; y < rect.Bottom; y += 26)
                {
                    g.DrawLine(pen, rect.Left, y, rect.Right, y);
                }
            }
            using (var sunBrush = new SolidBrush(Color.FromArgb(130, 255, 150, 45)))
            {
                g.FillEllipse(sunBrush, rect.Width / 2 - 105, 34, 210, 210);
            }
            DrawVignette(g, rect);
        }

        private static void DrawVignette(Graphics g, Rectangle rect)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(rect.Left - rect.Width / 4, rect.Top - rect.Height, rect.Width + rect.Width / 2, rect.Height * 3);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = Color.FromArgb(0, 0, 0, 0);
                    brush.SurroundColors = new[] { Color.FromArgb(160, 0, 0, 0) };
                    g.FillRectangle(brush, rect);
                }
            }
        }

        private void OnGenerateMarquee(object sender, EventArgs e)
        {
            string sourceName = GetDefaultMarqueeSourceName();
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                sourceName = PromptForText("Generate Static JPG", "Enter the matching filename or base name:", string.Empty);
                if (string.IsNullOrWhiteSpace(sourceName)) return;
            }

            string suffix = GetSelectedMarqueeSuffix();
            string outputName = BuildMarqueeFilename(sourceName, suffix, ".jpg");
            while (true)
            {
                GenerateChoice choice = ConfirmGenerateFilename(outputName);
                if (choice == GenerateChoice.Cancel) return;
                if (choice == GenerateChoice.Yes) break;

                string renamed = PromptForText("Rename Marquee", "Enter the matching filename or base name:", Path.GetFileNameWithoutExtension(outputName));
                if (string.IsNullOrWhiteSpace(renamed)) return;
                sourceName = renamed;
                outputName = BuildMarqueeFilename(sourceName, suffix, ".jpg");
            }

            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "marquees");
            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(outputDir, outputName);
            _preview.SaveJpeg(outputPath);

            try
            {
                Process.Start("explorer.exe", "/select,\"" + outputPath + "\"");
            }
            catch
            {
                Process.Start("explorer.exe", outputDir);
            }

            MessageBox.Show(this, "Generated marquee:\r\n" + outputPath, "Generated", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnGenerateAnimatedMarquee(object sender, EventArgs e)
        {
            string ffmpegPath = FindFfmpeg();
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                MessageBox.Show(this, "Animated MP4 export requires ffmpeg.exe. Add it to PATH, place it beside the app, or put it in assets\\tools\\ffmpeg.exe.", "ffmpeg Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string sourceName = GetDefaultMarqueeSourceName();
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                sourceName = PromptForText("Generate Animated MP4", "Enter the matching filename or base name:", string.Empty);
                if (string.IsNullOrWhiteSpace(sourceName)) return;
            }

            string suffix = GetSelectedMarqueeSuffix();
            string outputName = BuildMarqueeFilename(sourceName, suffix, ".mp4");
            while (true)
            {
                GenerateChoice choice = ConfirmGenerateFilename(outputName);
                if (choice == GenerateChoice.Cancel) return;
                if (choice == GenerateChoice.Yes) break;

                string renamed = PromptForText("Rename Marquee", "Enter the matching filename or base name:", Path.GetFileNameWithoutExtension(outputName));
                if (string.IsNullOrWhiteSpace(renamed)) return;
                sourceName = renamed;
                outputName = BuildMarqueeFilename(sourceName, suffix, ".mp4");
            }

            float defaultDuration = Math.Max(3f, _preview.GetAnimationDurationSeconds());
            string durationText = PromptForText("Animation Duration", "Enter animation duration in seconds:", defaultDuration.ToString("0.0"));
            if (string.IsNullOrWhiteSpace(durationText)) return;

            float durationSeconds;
            if (!float.TryParse(durationText.Trim(), out durationSeconds) || durationSeconds <= 0f)
            {
                MessageBox.Show(this, "Enter a valid animation duration.", "Invalid Duration", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "marquees");
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, outputName);
            string frameDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "animation_frames", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(frameDir);

            const int fps = 30;
            int frameCount = Math.Max(1, (int)Math.Ceiling(durationSeconds * fps));
            Enabled = false;
            UseWaitCursor = true;
            try
            {
                for (int i = 0; i < frameCount; i++)
                {
                    string framePath = Path.Combine(frameDir, string.Format("frame_{0:D5}.png", i));
                    _preview.SaveAnimationFramePng(framePath, i / (float)fps);
                }

                RunFfmpeg(ffmpegPath, frameDir, fps, outputPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Animated Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Enabled = true;
                UseWaitCursor = false;
                try { Directory.Delete(frameDir, true); } catch { }
            }

            try
            {
                Process.Start("explorer.exe", "/select,\"" + outputPath + "\"");
            }
            catch
            {
                Process.Start("explorer.exe", outputDir);
            }

            MessageBox.Show(this, "Generated animated marquee:\r\n" + outputPath, "Generated", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string GetDefaultMarqueeSourceName()
        {
            if (IsArcadeMode())
            {
                string romName = _arcadeRomText == null ? string.Empty : (_arcadeRomText.Text ?? string.Empty).Trim();
                if (romName.Length > 0) return romName;
                return _arcadeGameText == null ? string.Empty : (_arcadeGameText.Text ?? string.Empty).Trim();
            }

            string artist = _artistText == null ? string.Empty : (_artistText.Text ?? string.Empty).Trim();
            string title = _titleText == null ? string.Empty : (_titleText.Text ?? string.Empty).Trim();
            if (artist.Length > 0 && title.Length > 0) return artist + " - " + title;
            if (title.Length > 0) return title;
            return artist;
        }

        private GenerateChoice ConfirmGenerateFilename(string outputName)
        {
            using (var dialog = new Form())
            using (var label = new Label())
            using (var yes = new Button())
            using (var rename = new Button())
            using (var cancel = new Button())
            {
                dialog.Text = "Generate Marquee";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(560, 150);
                dialog.BackColor = Color.FromArgb(12, 18, 34);
                dialog.ForeColor = Color.White;

                label.Text = "Marquee will be saved as:\r\n" + outputName;
                label.Left = 14;
                label.Top = 14;
                label.Width = 532;
                label.Height = 56;
                label.ForeColor = Color.White;

                yes.Text = "Yes";
                yes.Left = 192;
                yes.Top = 96;
                yes.Width = 92;
                yes.DialogResult = DialogResult.Yes;
                yes.BackColor = Color.FromArgb(25, 165, 85);
                yes.ForeColor = Color.White;
                yes.FlatStyle = FlatStyle.Flat;

                rename.Text = "No - Rename";
                rename.Left = 294;
                rename.Top = 96;
                rename.Width = 116;
                rename.DialogResult = DialogResult.No;
                rename.BackColor = Color.FromArgb(42, 105, 235);
                rename.ForeColor = Color.White;
                rename.FlatStyle = FlatStyle.Flat;

                cancel.Text = "No - Cancel";
                cancel.Left = 420;
                cancel.Top = 96;
                cancel.Width = 116;
                cancel.DialogResult = DialogResult.Cancel;
                cancel.BackColor = Color.FromArgb(35, 45, 62);
                cancel.ForeColor = Color.White;
                cancel.FlatStyle = FlatStyle.Flat;

                dialog.Controls.Add(label);
                dialog.Controls.Add(yes);
                dialog.Controls.Add(rename);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = yes;
                dialog.CancelButton = cancel;

                DialogResult result = dialog.ShowDialog(this);
                if (result == DialogResult.Yes) return GenerateChoice.Yes;
                if (result == DialogResult.No) return GenerateChoice.Rename;
                return GenerateChoice.Cancel;
            }
        }

        private Control CreateStatusTile(string message)
        {
            return new Label
            {
                Width = 760,
                Height = 44,
                Text = message,
                ForeColor = Color.FromArgb(220, 230, 245),
                BackColor = Color.FromArgb(22, 30, 50),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10)
            };
        }

        private void PopulateResourceFilters()
        {
            PopulateFilter(_resourceSourceFilter, "All Sources", _lastSearchResults.Select(r => r.Source));
            PopulateFilter(_resourceTypeFilter, "All Types", _lastSearchResults.Select(r => r.ResourceType));
        }

        private static void PopulateFilter(ComboBox combo, string allText, IEnumerable<string> values)
        {
            if (combo == null) return;
            string previous = GetSelectedFilterValue(combo);
            combo.Items.Clear();
            combo.Items.Add(new FilterOption(allText, string.Empty));
            foreach (string value in values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                combo.Items.Add(new FilterOption(value, value));
            }
            combo.SelectedIndex = 0;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var option = combo.Items[i] as FilterOption;
                if (option != null && string.Equals(option.Value, previous, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    break;
                }
            }
        }

        private void RenderResourceResults()
        {
            if (_resourceTiles == null) return;
            _resourceTiles.Controls.Clear();
            IList<ResourceResult> visibleResults = FilterVisibleResults(_lastSearchResults)
                .Where(MatchesResourceFilters)
                .ToList();
            foreach (ResourceResult result in visibleResults)
            {
                _resourceTiles.Controls.Add(CreateResourceRow(result));
            }
            if (visibleResults.Count == 0) _resourceTiles.Controls.Add(CreateStatusTile("No artwork resources found for the selected filter."));
        }

        private bool MatchesResourceFilters(ResourceResult result)
        {
            string source = GetSelectedFilterValue(_resourceSourceFilter);
            string type = GetSelectedFilterValue(_resourceTypeFilter);
            if (!string.IsNullOrWhiteSpace(source) && !string.Equals(result.Source, source, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(type) && !string.Equals(result.ResourceType, type, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static string GetSelectedFilterValue(ComboBox combo)
        {
            if (combo == null || combo.SelectedItem == null) return string.Empty;
            var option = combo.SelectedItem as FilterOption;
            return option == null ? string.Empty : option.Value;
        }

        private static IList<ResourceResult> FilterVisibleResults(IEnumerable<ResourceResult> results)
        {
            var visible = new List<ResourceResult>();
            foreach (ResourceResult result in results)
            {
                bool hasImage = !string.IsNullOrWhiteSpace(result.CachedImagePath) && File.Exists(result.CachedImagePath);
                bool isMessage = string.Equals(result.ResourceType, "status", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(result.ResourceType, "error", StringComparison.OrdinalIgnoreCase);
                if (hasImage || isMessage)
                {
                    visible.Add(result);
                }
            }
            return visible;
        }

        private Control CreateResourceRow(ResourceResult result)
        {
            var row = new Panel
            {
                Width = 800,
                Height = 126,
                BackColor = Color.FromArgb(22, 30, 50),
                Margin = new Padding(4),
                Padding = new Padding(8)
            };

            var image = new PictureBox
            {
                Width = 110,
                Height = 110,
                Left = 8,
                Top = 8,
                BackColor = Color.FromArgb(8, 10, 20),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            if (!string.IsNullOrWhiteSpace(result.CachedImagePath) && File.Exists(result.CachedImagePath))
            {
                try
                {
                    using (var temp = Image.FromFile(result.CachedImagePath))
                    {
                        image.Image = new Bitmap(temp);
                    }
                }
                catch
                {
                }
            }
            row.Controls.Add(image);

            var title = new Label
            {
                Left = 130,
                Top = 8,
                Width = 430,
                Height = 24,
                Text = result.Label,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            row.Controls.Add(title);

            var meta = new Label
            {
                Left = 130,
                Top = 36,
                Width = 430,
                Height = 34,
                Text = result.Source + "\r\n" + result.ResourceType,
                ForeColor = Color.FromArgb(180, 205, 230),
                Font = new Font("Segoe UI", 8f)
            };
            row.Controls.Add(meta);

            var detail = new Label
            {
                Left = 130,
                Top = 74,
                Width = 430,
                Height = 38,
                Text = result.Detail,
                ForeColor = Color.FromArgb(200, 210, 225),
                Font = new Font("Segoe UI", 7.5f)
            };
            row.Controls.Add(detail);

            bool hasImage = !string.IsNullOrWhiteSpace(result.CachedImagePath) && File.Exists(result.CachedImagePath);
            if (IsBlankCanvasMode())
            {
                AddTileButton(row, "Add Fit", 580, 18, hasImage, delegate { RunHistoryAction(delegate { _preview.AddFreeformImage(result.CachedImagePath, PanelImageMode.Fit); }); });
                AddTileButton(row, "Add Fill", 666, 18, hasImage, delegate { RunHistoryAction(delegate { _preview.AddFreeformImage(result.CachedImagePath, PanelImageMode.Fill); }); });
            }
            else
            {
                AddTileButton(row, "L Fit", 580, 8, hasImage, delegate { RunHistoryAction(delegate { _preview.SetLeftImage(result.CachedImagePath, PanelImageMode.Fit); }); });
                AddTileButton(row, "M Fit", 666, 8, hasImage, delegate { RunHistoryAction(delegate { _preview.SetMiddleImage(result.CachedImagePath, PanelImageMode.Fit); }); });
                AddTileButton(row, "R Fit", 580, 44, hasImage, delegate { RunHistoryAction(delegate { _preview.SetRightImage(result.CachedImagePath, PanelImageMode.Fit); }); });
                AddTileButton(row, "L Fill", 666, 44, hasImage, delegate { RunHistoryAction(delegate { _preview.SetLeftImage(result.CachedImagePath, PanelImageMode.Fill); }); });
                AddTileButton(row, "M Fill", 580, 80, hasImage, delegate { RunHistoryAction(delegate { _preview.SetMiddleImage(result.CachedImagePath, PanelImageMode.Fill); }); });
                AddTileButton(row, "R Fill", 666, 80, hasImage, delegate { RunHistoryAction(delegate { _preview.SetRightImage(result.CachedImagePath, PanelImageMode.Fill); }); });
            }

            return row;
        }

        private static void AddTileButton(Control parent, string text, int x, int y, bool enabled, EventHandler click)
        {
            var button = CreateStyledButton(
                text,
                62,
                30,
                enabled ? Color.FromArgb(45, 112, 255) : Color.FromArgb(48, 54, 70),
                enabled ? Color.FromArgb(120, 180, 255) : Color.FromArgb(78, 86, 105));
            button.Left = x;
            button.Top = y;
            button.Enabled = enabled;
            button.Click += click;
            parent.Controls.Add(button);
        }

        private string GetSelectedMarqueeSuffix()
        {
            if (IsArcadeMode())
            {
                return SelectedArcadeSystem().Suffix;
            }
            return " (JUKE)";
        }

        private static string BuildMarqueeFilename(string mp4Name, string suffix)
        {
            return BuildMarqueeFilename(mp4Name, suffix, ".jpg");
        }

        private static string BuildMarqueeFilename(string mp4Name, string suffix, string extension)
        {
            string baseName = Path.GetFileNameWithoutExtension((mp4Name ?? string.Empty).Trim());
            if (string.IsNullOrWhiteSpace(suffix)) suffix = " (JUKE)";
            if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                baseName = baseName.Substring(0, baseName.Length - suffix.Length);
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(c, '_');
            }

            if (string.IsNullOrWhiteSpace(baseName)) baseName = "marquee";
            if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";
            if (!extension.StartsWith(".", StringComparison.Ordinal)) extension = "." + extension;
            return baseName + suffix + extension;
        }

        private static string FindFfmpeg()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "ffmpeg.exe"),
                Path.Combine(baseDir, "assets", "tools", "ffmpeg.exe"),
                Path.Combine(FindDirectoryUp(baseDir, "assets", "tools"), "ffmpeg.exe")
            };
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                string candidate = Path.Combine(dir.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }

            return string.Empty;
        }

        private static void RunFfmpeg(string ffmpegPath, string frameDir, int fps, string outputPath)
        {
            string inputPattern = Path.Combine(frameDir, "frame_%05d.png");
            var start = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-y -framerate " + fps + " -i " + Quote(inputPattern) + " -c:v libx264 -pix_fmt yuv420p -vf \"format=yuv420p\" -movflags +faststart " + Quote(outputPath),
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(start))
            {
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("ffmpeg failed with exit code " + process.ExitCode + ":\r\n" + error);
                }
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private string PromptForText(string title, string prompt)
        {
            return PromptForText(title, prompt, string.Empty);
        }

        private string PromptForText(string title, string prompt, string initialText)
        {
            using (var dialog = new Form())
            using (var label = new Label())
            using (var textBox = new TextBox())
            using (var ok = new Button())
            using (var cancel = new Button())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(520, 136);
                dialog.BackColor = Color.FromArgb(12, 18, 34);
                dialog.ForeColor = Color.White;

                label.Text = prompt;
                label.Left = 14;
                label.Top = 14;
                label.Width = 490;
                label.Height = 22;
                label.ForeColor = Color.White;

                textBox.Left = 14;
                textBox.Top = 42;
                textBox.Width = 490;
                textBox.Height = 24;
                textBox.BackColor = Color.FromArgb(22, 30, 50);
                textBox.ForeColor = Color.White;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.Text = initialText ?? string.Empty;
                textBox.SelectAll();

                ok.Text = "Generate";
                ok.Left = 302;
                ok.Top = 88;
                ok.Width = 96;
                ok.DialogResult = DialogResult.OK;
                ok.BackColor = Color.FromArgb(25, 165, 85);
                ok.ForeColor = Color.White;
                ok.FlatStyle = FlatStyle.Flat;

                cancel.Text = "Cancel";
                cancel.Left = 408;
                cancel.Top = 88;
                cancel.Width = 96;
                cancel.DialogResult = DialogResult.Cancel;
                cancel.BackColor = Color.FromArgb(35, 45, 62);
                cancel.ForeColor = Color.White;
                cancel.FlatStyle = FlatStyle.Flat;

                dialog.Controls.Add(label);
                dialog.Controls.Add(textBox);
                dialog.Controls.Add(ok);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                return dialog.ShowDialog(this) == DialogResult.OK ? textBox.Text : null;
            }
        }

        private void RunHistoryAction(Action action)
        {
            if (_restoringHistory)
            {
                action();
                return;
            }

            EditorState before = CaptureEditorState();
            action();
            EditorState after = CaptureEditorState();
            if (!before.HasSameValues(after))
            {
                _undoStack.Push(before);
                _redoStack.Clear();
                UpdateHistoryButtons();
            }
            RefreshLayerList();
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            EditorState current = CaptureEditorState();
            EditorState previous = _undoStack.Pop();
            _redoStack.Push(current);
            RestoreEditorState(previous);
            UpdateHistoryButtons();
            RefreshLayerList();
        }

        private void Redo()
        {
            if (_redoStack.Count == 0) return;
            EditorState current = CaptureEditorState();
            EditorState next = _redoStack.Pop();
            _undoStack.Push(current);
            RestoreEditorState(next);
            UpdateHistoryButtons();
            RefreshLayerList();
        }

        private EditorState CaptureEditorState()
        {
            return new EditorState
            {
                Artist = _artistText == null ? string.Empty : (_artistText.Text ?? string.Empty),
                Title = _titleText == null ? string.Empty : (_titleText.Text ?? string.Empty),
                Album = _albumText == null ? string.Empty : (_albumText.Text ?? string.Empty),
                FeaturedArtist = _featuredText == null ? string.Empty : (_featuredText.Text ?? string.Empty),
                ReleaseYear = _yearText == null ? string.Empty : (_yearText.Text ?? string.Empty),
                ArcadeGameName = _arcadeGameText == null ? string.Empty : (_arcadeGameText.Text ?? string.Empty),
                ArcadeRomName = _arcadeRomText == null ? string.Empty : (_arcadeRomText.Text ?? string.Empty),
                ArcadeSystemIndex = _arcadeSystemList == null ? 0 : _arcadeSystemList.SelectedIndex,
                IsArcade = IsArcadeMode(),
                IsJukeboxCanvas = IsBlankCanvasMode(),
                EditMode = IsBlankCanvasMode() ? CanvasEditMode.Freeform : CanvasEditMode.JukeboxFixed,
                ThemeEntryIndex = _themeEntryList == null ? -1 : _themeEntryList.SelectedIndex,
                Canvas = _preview == null ? new CanvasState() : _preview.CaptureState()
            };
        }

        private void RestoreEditorState(EditorState state)
        {
            if (state == null) return;
            _restoringHistory = true;
            try
            {
                _artistText.Text = state.Artist ?? string.Empty;
                _titleText.Text = state.Title ?? string.Empty;
                _albumText.Text = state.Album ?? string.Empty;
                _featuredText.Text = state.FeaturedArtist ?? string.Empty;
                _yearText.Text = state.ReleaseYear ?? string.Empty;
                _arcadeGameText.Text = state.ArcadeGameName ?? string.Empty;
                _arcadeRomText.Text = state.ArcadeRomName ?? string.Empty;
                if (_arcadeSystemList != null)
                {
                    _arcadeSystemList.SelectedIndex = state.ArcadeSystemIndex >= 0 && state.ArcadeSystemIndex < _arcadeSystemList.Items.Count ? state.ArcadeSystemIndex : 0;
                }
                if (_jukeboxTypeButton != null && _arcadeTypeButton != null)
                {
                    _arcadeTypeButton.Checked = state.IsArcade;
                    _jukeboxTypeButton.Checked = !state.IsArcade;
                }
                if (_jukeboxFixedLayoutButton != null && _jukeboxCanvasLayoutButton != null)
                {
                    _jukeboxCanvasLayoutButton.Checked = state.IsJukeboxCanvas;
                    _jukeboxFixedLayoutButton.Checked = !state.IsJukeboxCanvas;
                }
                if (_themeEntryList != null)
                {
                    if (state.ThemeEntryIndex >= 0 && state.ThemeEntryIndex < _themeEntryList.Items.Count)
                    {
                        _themeEntryList.SelectedIndex = state.ThemeEntryIndex;
                    }
                    else
                    {
                        _themeEntryList.SelectedIndex = -1;
                    }
                }
                if (_preview != null)
                {
                    _preview.RestoreState(state.Canvas);
                }
            }
            finally
            {
                _restoringHistory = false;
            }
        }

        private void UpdateHistoryButtons()
        {
            if (_undoButton != null) _undoButton.Enabled = _undoStack.Count > 0;
            if (_redoButton != null) _redoButton.Enabled = _redoStack.Count > 0;
        }

        private sealed class JukeboxThemeEntry
        {
            public JukeboxThemeEntry(int lineNumber, string artist, string title, string album)
            {
                LineNumber = lineNumber;
                Artist = artist;
                Title = title;
                Album = album;
            }

            public int LineNumber { get; private set; }
            public string Artist { get; private set; }
            public string Title { get; private set; }
            public string Album { get; private set; }

            public override string ToString()
            {
                return Artist + " - " + Title;
            }
        }

        private enum GenerateChoice
        {
            Yes,
            Rename,
            Cancel
        }

        private sealed class BackgroundGalleryItem
        {
            public BackgroundGalleryItem(string name, string imagePath)
            {
                Name = name;
                ImagePath = imagePath;
            }

            public string Name { get; private set; }
            public string ImagePath { get; private set; }

            public override string ToString()
            {
                return Name;
            }
        }

        private sealed class FilterOption
        {
            public FilterOption(string label, string value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; private set; }
            public string Value { get; private set; }

            public override string ToString()
            {
                return Label;
            }
        }

        private sealed class FontOption
        {
            public FontOption(string fontFamilyName, string fontFilePath)
            {
                FontFamilyName = fontFamilyName;
                FontFilePath = fontFilePath ?? string.Empty;
            }

            public string FontFamilyName { get; private set; }
            public string FontFilePath { get; private set; }

            public override string ToString()
            {
                return FontFamilyName;
            }
        }

        private sealed class ArcadeSystemOption
        {
            public static readonly ArcadeSystemOption Blank = new ArcadeSystemOption(string.Empty, " (JUKE)", string.Empty);

            public ArcadeSystemOption(string name, string suffix, string screenScraperSystemId)
            {
                Name = name;
                Suffix = suffix;
                ScreenScraperSystemId = screenScraperSystemId;
            }

            public string Name { get; private set; }
            public string Suffix { get; private set; }
            public string ScreenScraperSystemId { get; private set; }

            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(Name) ? "(blank)" : Name + " " + Suffix.Trim();
            }
        }

        private static IEnumerable<ArcadeSystemOption> GetArcadeSystemOptions()
        {
            return new[]
            {
                new ArcadeSystemOption("Amstrad CPC", " (CPC)", "62"),
                new ArcadeSystemOption("Amstrad GX4000", " (GX4K)", "63"),
                new ArcadeSystemOption("Apple II", " (Apple2)", "59"),
                new ArcadeSystemOption("Atari 2600", " (A26)", "16"),
                new ArcadeSystemOption("Atari 5200", " (A52)", "17"),
                new ArcadeSystemOption("Atari 7800", " (A78)", "18"),
                new ArcadeSystemOption("Atari 800", " (A800)", string.Empty),
                new ArcadeSystemOption("Atari Jaguar", " (JAG)", "20"),
                new ArcadeSystemOption("Atari Lynx", " (LYNX)", "19"),
                new ArcadeSystemOption("Atari ST", " (ST)", string.Empty),
                new ArcadeSystemOption("Bally Astrocade", " (ASTRO)", "47"),
                new ArcadeSystemOption("Bandai Wonderswan", " (WS)", "39"),
                new ArcadeSystemOption("Bandai Wonderswan Color", " (WSC)", "40"),
                new ArcadeSystemOption("ColecoVision", " (COL)", "42"),
                new ArcadeSystemOption("Commodore 64", " (C64)", "51"),
                new ArcadeSystemOption("Commodore Amiga", " (AMIGA)", "56"),
                new ArcadeSystemOption("Commodore Amiga CD32", " (CD32)", "57"),
                new ArcadeSystemOption("Commodore CDTV", " (CDTV)", "58"),
                new ArcadeSystemOption("Commodore VIC-20", " (VIC20)", "53"),
                new ArcadeSystemOption("Daphne", " (DAPH)", "2"),
                new ArcadeSystemOption("Emerson Arcadia 2001", " (EA2)", string.Empty),
                new ArcadeSystemOption("Entex Adventure Vision", " (EAV)", "120"),
                new ArcadeSystemOption("Epoch Super Cassette Vision", " (ESCV)", "119"),
                new ArcadeSystemOption("Fairchild Channel F", " (CHNF)", "46"),
                new ArcadeSystemOption("GCE Vectrex", " (VEC)", "45"),
                new ArcadeSystemOption("Jukebox", " (JUKE)", string.Empty),
                new ArcadeSystemOption("Light Gun", " (GUN)", string.Empty),
                new ArcadeSystemOption("Magnavox Odyssey 2", " (MAGO2)", "44"),
                new ArcadeSystemOption("MAIN", " (MENU)", string.Empty),
                new ArcadeSystemOption("MAME", " (MAME)", "75"),
                new ArcadeSystemOption("Mattel Intellivision", " (INT)", "43"),
                new ArcadeSystemOption("Microsoft MS-DOS", " (DOS)", "61"),
                new ArcadeSystemOption("Microsoft MSX", " (MSX)", "64"),
                new ArcadeSystemOption("NEC PC Engine", " (NECPC)", "33"),
                new ArcadeSystemOption("NEC PC Engine-CD", " (NECPCCD)", "35"),
                new ArcadeSystemOption("NEC PC-8801", " (8801)", "99"),
                new ArcadeSystemOption("NEC PC-9801", " (9801)", "100"),
                new ArcadeSystemOption("NEC SuperGrafx", " (SGFX)", "34"),
                new ArcadeSystemOption("NEC TurboGrafx-16", " (TG16)", "33"),
                new ArcadeSystemOption("NEC TurboGrafx-CD", " (TGCD)", "35"),
                new ArcadeSystemOption("Nintendo 64", " (N64)", "5"),
                new ArcadeSystemOption("Nintendo DS", " (NDS)", "12"),
                new ArcadeSystemOption("Nintendo Entertainment System", " (NES)", "3"),
                new ArcadeSystemOption("Nintendo Famicom", " (FAMNODSK)", "3"),
                new ArcadeSystemOption("Nintendo Famicom Disk System", " (FAM)", string.Empty),
                new ArcadeSystemOption("Nintendo Game & Watch", " (NGW)", "50"),
                new ArcadeSystemOption("Nintendo Game Boy", " (GB)", "9"),
                new ArcadeSystemOption("Nintendo Game Boy Advance", " (GBA)", "11"),
                new ArcadeSystemOption("Nintendo Game Boy Color", " (GBC)", "10"),
                new ArcadeSystemOption("Nintendo Pokemon Mini", " (PKM)", "49"),
                new ArcadeSystemOption("Nintendo Virtual Boy", " (VB)", "48"),
                new ArcadeSystemOption("Nokia N-Gage", " (NNG)", "124"),
                new ArcadeSystemOption("OpenBOR", " (OBOR)", "91"),
                new ArcadeSystemOption("Panasonic 3DO", " (3DO)", "37"),
                new ArcadeSystemOption("PC Games", " (PC)", "138"),
                new ArcadeSystemOption("Philips CD-i", " (PCDi)", "38"),
                new ArcadeSystemOption("PICO-8", " (PICO8)", "94"),
                new ArcadeSystemOption("Ports", " (PORTS)", string.Empty),
                new ArcadeSystemOption("RCA Studio II", " (RCA)", string.Empty),
                new ArcadeSystemOption("Sammy Atomiswave", " (SA)", "81"),
                new ArcadeSystemOption("ScummVM", " (SVM)", "92"),
                new ArcadeSystemOption("Sega 32X", " (32X)", "25"),
                new ArcadeSystemOption("Sega CD", " (SCD)", "24"),
                new ArcadeSystemOption("Sega Dreamcast", " (DC)", "27"),
                new ArcadeSystemOption("Sega Game Gear", " (GG)", "28"),
                new ArcadeSystemOption("Sega Genesis", " (GEN)", "23"),
                new ArcadeSystemOption("Sega Master System", " (SMS)", "1"),
                new ArcadeSystemOption("Sega MSU-MD", " (SMSU)", string.Empty),
                new ArcadeSystemOption("Sega Saturn", " (SSAT)", "26"),
                new ArcadeSystemOption("Sega SG-1000", " (SSG)", "21"),
                new ArcadeSystemOption("Sharp X1", " (SX1)", "97"),
                new ArcadeSystemOption("Sharp X68000", " (SX68)", "96"),
                new ArcadeSystemOption("Sinclair ZX Spectrum", " (ZX)", "8"),
                new ArcadeSystemOption("SNK Neo Geo AES", " (NGAES)", "29"),
                new ArcadeSystemOption("SNK Neo Geo CD", " (NGCD)", "30"),
                new ArcadeSystemOption("SNK Neo Geo Pocket Color", " (NGPC)", "32"),
                new ArcadeSystemOption("Sony PlayStation", " (PSX)", "13"),
                new ArcadeSystemOption("Sony PSP", " (PSPM)", "15"),
                new ArcadeSystemOption("Sony PSP Minis", " (PSP)", "15"),
                new ArcadeSystemOption("Super NES", " (SNES)", "4"),
                new ArcadeSystemOption("Super Nintendo Project NESted MSU1", " (NEST)", "4"),
                new ArcadeSystemOption("THEMES", " (THEME)", string.Empty),
                new ArcadeSystemOption("Thomson MO5", " (MO5)", "69"),
                new ArcadeSystemOption("Thomson TO7", " (TO7)", string.Empty),
                new ArcadeSystemOption("Thomson TO8", " (TO8)", "70"),
                new ArcadeSystemOption("TIC-80", " (TIC80)", "95"),
                new ArcadeSystemOption("VTech CreatiVision", " (VCV)", "112"),
                new ArcadeSystemOption("Watara Supervision", " (WSV)", "116"),
                new ArcadeSystemOption("WoW Action Max", " (WAM)", "143")
            };
        }
        private sealed class EditorState
        {
            public string Artist { get; set; }
            public string Title { get; set; }
            public string Album { get; set; }
            public string FeaturedArtist { get; set; }
            public string ReleaseYear { get; set; }
            public string ArcadeGameName { get; set; }
            public string ArcadeRomName { get; set; }
            public int ArcadeSystemIndex { get; set; }
            public bool IsArcade { get; set; }
            public bool IsJukeboxCanvas { get; set; }
            public CanvasEditMode EditMode { get; set; }
            public int ThemeEntryIndex { get; set; }
            public CanvasState Canvas { get; set; }

            public bool HasSameValues(EditorState other)
            {
                if (other == null) return false;
                return string.Equals(Artist ?? string.Empty, other.Artist ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(Title ?? string.Empty, other.Title ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(Album ?? string.Empty, other.Album ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(FeaturedArtist ?? string.Empty, other.FeaturedArtist ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(ReleaseYear ?? string.Empty, other.ReleaseYear ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(ArcadeGameName ?? string.Empty, other.ArcadeGameName ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(ArcadeRomName ?? string.Empty, other.ArcadeRomName ?? string.Empty, StringComparison.Ordinal) &&
                       ArcadeSystemIndex == other.ArcadeSystemIndex &&
                       IsArcade == other.IsArcade &&
                       IsJukeboxCanvas == other.IsJukeboxCanvas &&
                       EditMode == other.EditMode &&
                       ThemeEntryIndex == other.ThemeEntryIndex &&
                       CanvasHasSameValues(Canvas, other.Canvas);
            }

            private static bool CanvasHasSameValues(CanvasState left, CanvasState right)
            {
                if (left == null || right == null) return left == right;
                return string.Equals(left.LeftImagePath ?? string.Empty, right.LeftImagePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(left.MiddleImagePath ?? string.Empty, right.MiddleImagePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(left.RightImagePath ?? string.Empty, right.RightImagePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(left.BackgroundImagePath ?? string.Empty, right.BackgroundImagePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                       left.LeftImageMode == right.LeftImageMode &&
                       left.MiddleImageMode == right.MiddleImageMode &&
                       left.RightImageMode == right.RightImageMode &&
                       left.EditMode == right.EditMode &&
                       left.SelectedLayerIndex == right.SelectedLayerIndex &&
                       FreeformLayersHaveSameValues(left.FreeformLayers, right.FreeformLayers) &&
                       string.Equals(left.ArtistText ?? string.Empty, right.ArtistText ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(left.TitleText ?? string.Empty, right.TitleText ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(left.FeaturedArtistText ?? string.Empty, right.FeaturedArtistText ?? string.Empty, StringComparison.Ordinal);
            }

            private static bool FreeformLayersHaveSameValues(IList<FreeformArtLayer> left, IList<FreeformArtLayer> right)
            {
                if (left == null || right == null) return left == right;
                if (left.Count != right.Count) return false;
                for (int i = 0; i < left.Count; i++)
                {
                    if (left[i].IsTextLayer != right[i].IsTextLayer) return false;
                    if (!string.Equals(left[i].ImagePath ?? string.Empty, right[i].ImagePath ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return false;
                    if (left[i].Bounds != right[i].Bounds) return false;
                    if (left[i].ImageMode != right[i].ImageMode) return false;
                    if (left[i].FlipHorizontal != right[i].FlipHorizontal) return false;
                    if (left[i].FlipVertical != right[i].FlipVertical) return false;
                    if (Math.Abs(left[i].RotationDegrees - right[i].RotationDegrees) > 0.01f) return false;
                    if (left[i].AnimationType != right[i].AnimationType) return false;
                    if (Math.Abs(left[i].AnimationStartSeconds - right[i].AnimationStartSeconds) > 0.01f) return false;
                    if (Math.Abs(left[i].AnimationDurationSeconds - right[i].AnimationDurationSeconds) > 0.01f) return false;
                    if (Math.Abs(left[i].VisibleFromSeconds - right[i].VisibleFromSeconds) > 0.01f) return false;
                    if (Math.Abs(left[i].VisibleToSeconds - right[i].VisibleToSeconds) > 0.01f) return false;
                    if (!string.Equals(left[i].Text ?? string.Empty, right[i].Text ?? string.Empty, StringComparison.Ordinal)) return false;
                    if (!string.Equals(left[i].FontFamilyName ?? string.Empty, right[i].FontFamilyName ?? string.Empty, StringComparison.Ordinal)) return false;
                    if (!string.Equals(left[i].FontFilePath ?? string.Empty, right[i].FontFilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return false;
                    if (Math.Abs(left[i].FontSize - right[i].FontSize) > 0.01f) return false;
                    if (left[i].FontBold != right[i].FontBold) return false;
                    if (left[i].FontItalic != right[i].FontItalic) return false;
                    if (left[i].FontUnderline != right[i].FontUnderline) return false;
                    if (left[i].TextColor.ToArgb() != right[i].TextColor.ToArgb()) return false;
                    if (left[i].TextAlignment != right[i].TextAlignment) return false;
                    if (left[i].TextShadow != right[i].TextShadow) return false;
                    if (left[i].TextGlow != right[i].TextGlow) return false;
                }
                return true;
            }
        }
    }
}
