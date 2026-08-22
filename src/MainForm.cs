using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BitLCDMarqueeStudio
{
    internal sealed class MainForm : Form
    {
        private readonly CanvasPreviewControl _preview;
        private readonly Dictionary<string, TextBox> _panelFields;
        private readonly TextBox _artistText;
        private readonly TextBox _titleText;
        private readonly TextBox _albumText;
        private readonly TextBox _featuredText;
        private readonly TextBox _yearText;
        private readonly FlowLayoutPanel _resourceTiles;
        private readonly MarqueeLayout _layout;
        private readonly ResourceSearchService _searchService;

        public MainForm()
        {
            Text = "BitLCD Marquee Studio";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 760);
            Size = new Size(1280, 820);
            BackColor = Color.FromArgb(9, 14, 28);
            ForeColor = Color.White;

            _layout = MarqueeLayout.CreateJukeboxDefault();
            _panelFields = new Dictionary<string, TextBox>();
            _searchService = new ResourceSearchService();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(12),
                BackColor = BackColor
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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

            _preview = new CanvasPreviewControl { Dock = DockStyle.Fill, LayoutModel = _layout };
            right.Controls.Add(_preview, 0, 0);

            _resourceTiles = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 22, 40),
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(8)
            };
            right.Controls.Add(CreateResourceGroup(), 0, 1);

            _artistText = GetField(left, "Artist");
            _titleText = GetField(left, "Title");
            _albumText = GetField(left, "Album / Release");
            _featuredText = GetField(left, "Featured Artist");
            _yearText = GetField(left, "Release Year");

            PopulatePanelFields();
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
            AddTypeButton(flow, "Jukebox", true, true);
            AddTypeButton(flow, "Arcade", false, false);
            AddTypeButton(flow, "System", false, false);
            AddTypeButton(flow, "Collection", false, false);
            AddTypeButton(flow, "Custom", false, false);

            AddDivider(flow);
            AddHeader(flow, "Jukebox Search");
            AddLabeledTextBox(flow, "Artist", true);
            AddLabeledTextBox(flow, "Title", true);
            AddLabeledTextBox(flow, "Album / Release", false);
            AddLabeledTextBox(flow, "Featured Artist", false);
            AddLabeledTextBox(flow, "Release Year", false);

            var search = CreateButton("Search Resources");
            search.Click += OnSearchResources;
            flow.Controls.Add(search);

            AddDivider(flow);
            AddHeader(flow, "Layout Settings");
            AddSmallNote(flow, "Canvas is locked at 1920 x 360. Panel placement is editable.");
            AddPanelFields(flow, "Left", "L");
            AddPanelFields(flow, "Center", "C");
            AddPanelFields(flow, "Right", "R");

            var buttonRow = new FlowLayoutPanel
            {
                Width = 315,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            var reset = CreateButton("Reset");
            reset.Width = 96;
            reset.Click += delegate { ResetLayout(); };
            var mirror = CreateButton("Mirror R");
            mirror.Width = 96;
            mirror.Click += delegate { MirrorRightFromLeft(); };
            var center = CreateButton("Center");
            center.Width = 96;
            center.Click += delegate { CenterBetweenPanels(); };
            buttonRow.Controls.Add(reset);
            buttonRow.Controls.Add(mirror);
            buttonRow.Controls.Add(center);
            flow.Controls.Add(buttonRow);

            var apply = CreateButton("Apply Layout");
            apply.Click += delegate { ApplyLayoutFromFields(); };
            flow.Controls.Add(apply);

            return panel;
        }

        private GroupBox CreateResourceGroup()
        {
            var group = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Resource Tiles",
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

            var note = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Search gathers Apple Music / MusicBrainz / FanArt candidates. Use tile buttons to place artwork on the canvas.",
                ForeColor = Color.FromArgb(210, 220, 235),
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(note, 0, 0);
            layout.Controls.Add(_resourceTiles, 0, 1);
            return group;
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
                Height = 36,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(180, 195, 215)
            });
        }

        private static void AddTypeButton(FlowLayoutPanel flow, string text, bool isChecked, bool enabled)
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

        private void AddPanelFields(FlowLayoutPanel flow, string label, string prefix)
        {
            flow.Controls.Add(new Label
            {
                Text = label + " panel",
                Width = 315,
                Height = 20,
                ForeColor = Color.FromArgb(220, 230, 245)
            });

            var row = new FlowLayoutPanel
            {
                Width = 315,
                Height = 28,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = flow.BackColor
            };
            foreach (string part in new[] { "X", "Y", "W", "H" })
            {
                row.Controls.Add(new Label
                {
                    Text = part,
                    Width = 16,
                    Height = 24,
                    ForeColor = Color.FromArgb(180, 195, 215),
                    TextAlign = ContentAlignment.MiddleCenter
                });
                var box = new TextBox
                {
                    Name = prefix + part,
                    Width = 56,
                    Height = 24,
                    BackColor = Color.FromArgb(22, 30, 50),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                _panelFields[prefix + part] = box;
                row.Controls.Add(box);
            }
            flow.Controls.Add(row);
        }

        private static Button CreateButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 315,
                Height = 32,
                BackColor = Color.FromArgb(42, 105, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
        }

        private static TextBox GetField(Control root, string label)
        {
            Control[] matches = root.Controls.Find("Field_" + label, true);
            return matches.Length > 0 ? (TextBox)matches[0] : null;
        }

        private void OnSearchResources(object sender, EventArgs e)
        {
            string artist = (_artistText.Text ?? string.Empty).Trim();
            string title = (_titleText.Text ?? string.Empty).Trim();
            if (artist.Length == 0 || title.Length == 0)
            {
                MessageBox.Show(this, "Artist and Title are required for Jukebox searches.", "Missing Required Fields", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var request = new JukeboxSearchRequest
            {
                Artist = artist,
                Title = title,
                AlbumOrRelease = (_albumText.Text ?? string.Empty).Trim(),
                FeaturedArtist = (_featuredText.Text ?? string.Empty).Trim(),
                ReleaseYear = (_yearText.Text ?? string.Empty).Trim()
            };

            _resourceTiles.Controls.Clear();
            _resourceTiles.Controls.Add(CreateStatusTile("Searching resources..."));
            UseWaitCursor = true;
            Enabled = false;
            try
            {
                IList<ResourceResult> results = _searchService.SearchJukebox(request);
                _resourceTiles.Controls.Clear();
                foreach (ResourceResult result in results)
                {
                    _resourceTiles.Controls.Add(CreateResourceTile(result));
                }
                if (results.Count == 0) _resourceTiles.Controls.Add(CreateStatusTile("No resources found."));
            }
            catch (Exception ex)
            {
                _resourceTiles.Controls.Clear();
                _resourceTiles.Controls.Add(CreateStatusTile("Search failed: " + ex.Message));
                MessageBox.Show(this, ex.Message, "Search Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Enabled = true;
                UseWaitCursor = false;
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

        private Control CreateResourceTile(ResourceResult result)
        {
            var tile = new Panel
            {
                Width = 285,
                Height = 206,
                BackColor = Color.FromArgb(22, 30, 50),
                Margin = new Padding(6),
                Padding = new Padding(8)
            };

            var image = new PictureBox
            {
                Width = 92,
                Height = 92,
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
            tile.Controls.Add(image);

            var title = new Label
            {
                Left = 108,
                Top = 6,
                Width = 166,
                Height = 44,
                Text = result.Label,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            tile.Controls.Add(title);

            var meta = new Label
            {
                Left = 108,
                Top = 52,
                Width = 166,
                Height = 48,
                Text = result.Source + "\r\n" + result.ResourceType,
                ForeColor = Color.FromArgb(180, 205, 230),
                Font = new Font("Segoe UI", 8f)
            };
            tile.Controls.Add(meta);

            var detail = new Label
            {
                Left = 8,
                Top = 105,
                Width = 266,
                Height = 38,
                Text = result.Detail,
                ForeColor = Color.FromArgb(200, 210, 225),
                Font = new Font("Segoe UI", 7.5f)
            };
            tile.Controls.Add(detail);

            AddTileButton(tile, "Set Left", 8, 152, delegate { _preview.SetLeftImage(result.CachedImagePath); });
            AddTileButton(tile, "Set Right", 94, 152, delegate { _preview.SetRightImage(result.CachedImagePath); });
            AddTileButton(tile, "Background", 180, 152, delegate { _preview.SetBackgroundImage(result.CachedImagePath); });

            return tile;
        }

        private static void AddTileButton(Control parent, string text, int x, int y, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                Left = x,
                Top = y,
                Width = 80,
                Height = 30,
                BackColor = Color.FromArgb(42, 105, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            button.Click += click;
            parent.Controls.Add(button);
        }

        private void PopulatePanelFields()
        {
            SetPanelFields("L", _layout.LeftPanel);
            SetPanelFields("C", _layout.CenterPanel);
            SetPanelFields("R", _layout.RightPanel);
        }

        private void SetPanelFields(string prefix, Rectangle rect)
        {
            _panelFields[prefix + "X"].Text = rect.X.ToString();
            _panelFields[prefix + "Y"].Text = rect.Y.ToString();
            _panelFields[prefix + "W"].Text = rect.Width.ToString();
            _panelFields[prefix + "H"].Text = rect.Height.ToString();
        }

        private Rectangle ReadPanelFields(string prefix)
        {
            return new Rectangle(
                ReadInt(prefix + "X"),
                ReadInt(prefix + "Y"),
                ReadInt(prefix + "W"),
                ReadInt(prefix + "H"));
        }

        private int ReadInt(string key)
        {
            int value;
            if (!int.TryParse(_panelFields[key].Text, out value)) value = 0;
            return value;
        }

        private void ApplyLayoutFromFields()
        {
            _layout.LeftPanel = ReadPanelFields("L");
            _layout.CenterPanel = ReadPanelFields("C");
            _layout.RightPanel = ReadPanelFields("R");
            _preview.LayoutModel = _layout;
        }

        private void ResetLayout()
        {
            var defaults = MarqueeLayout.CreateJukeboxDefault();
            _layout.LeftPanel = defaults.LeftPanel;
            _layout.CenterPanel = defaults.CenterPanel;
            _layout.RightPanel = defaults.RightPanel;
            PopulatePanelFields();
            _preview.LayoutModel = _layout;
        }

        private void MirrorRightFromLeft()
        {
            _layout.LeftPanel = ReadPanelFields("L");
            _layout.RightPanel = new Rectangle(
                MarqueeLayout.CanvasWidth - _layout.LeftPanel.X - _layout.LeftPanel.Width,
                _layout.LeftPanel.Y,
                _layout.LeftPanel.Width,
                _layout.LeftPanel.Height);
            SetPanelFields("R", _layout.RightPanel);
            ApplyLayoutFromFields();
        }

        private void CenterBetweenPanels()
        {
            _layout.LeftPanel = ReadPanelFields("L");
            _layout.RightPanel = ReadPanelFields("R");
            int x = _layout.LeftPanel.Right;
            int w = Math.Max(1, _layout.RightPanel.Left - x);
            _layout.CenterPanel = new Rectangle(x, 0, w, MarqueeLayout.CanvasHeight);
            SetPanelFields("C", _layout.CenterPanel);
            ApplyLayoutFromFields();
        }
    }
}
