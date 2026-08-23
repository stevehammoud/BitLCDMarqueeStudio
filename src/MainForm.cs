using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
        private readonly FlowLayoutPanel _resourceTiles;
        private readonly ResourceSearchService _searchService;

        public MainForm()
        {
            Text = "BitLCD Marquee Studio";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 760);
            Size = new Size(1280, 820);
            BackColor = Color.FromArgb(9, 14, 28);
            ForeColor = Color.White;

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

            _preview = new CanvasPreviewControl
            {
                Dock = DockStyle.Fill,
                LayoutModel = MarqueeLayout.CreateJukeboxDefault()
            };
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
            AddHeader(flow, "Fixed Layout");
            AddSmallNote(flow, "Canvas is locked at 1920 x 360. Jukebox panels are fixed: left 360 x 360, middle 1200 x 360, right 360 x 360.");
            AddSmallNote(flow, "Choose artwork for L / M / R. If M is empty, the app draws the title and artist using the built-in style.");

            var generate = CreateButton("Generate Marquee");
            generate.BackColor = Color.FromArgb(25, 165, 85);
            generate.Click += OnGenerateMarquee;
            flow.Controls.Add(generate);

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

            var note = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Search gathers Apple Music / MusicBrainz / FanArt candidates. Select artwork placement: L, M, or R.",
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
                Height = 50,
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

            _preview.SetJukeboxText(request.Artist, request.Title, request.FeaturedArtist);
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

        private void OnGenerateMarquee(object sender, EventArgs e)
        {
            string mp4Name = PromptForText("Generate Marquee", "Enter the matching MP4 filename:");
            if (string.IsNullOrWhiteSpace(mp4Name)) return;

            string outputName = BuildMarqueeFilename(mp4Name);
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

            bool hasImage = !string.IsNullOrWhiteSpace(result.CachedImagePath) && File.Exists(result.CachedImagePath);
            AddTileButton(tile, "Set L", 8, 152, hasImage, delegate { _preview.SetLeftImage(result.CachedImagePath); });
            AddTileButton(tile, "Set M", 94, 152, hasImage, delegate { _preview.SetMiddleImage(result.CachedImagePath); });
            AddTileButton(tile, "Set R", 180, 152, hasImage, delegate { _preview.SetRightImage(result.CachedImagePath); });

            return tile;
        }

        private static void AddTileButton(Control parent, string text, int x, int y, bool enabled, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                Left = x,
                Top = y,
                Width = 80,
                Height = 30,
                Enabled = enabled,
                BackColor = enabled ? Color.FromArgb(42, 105, 235) : Color.FromArgb(45, 52, 66),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            button.Click += click;
            parent.Controls.Add(button);
        }

        private static string BuildMarqueeFilename(string mp4Name)
        {
            string baseName = Path.GetFileNameWithoutExtension((mp4Name ?? string.Empty).Trim());
            if (baseName.EndsWith(" (JUKE)", StringComparison.OrdinalIgnoreCase))
            {
                baseName = baseName.Substring(0, baseName.Length - " (JUKE)".Length);
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(c, '_');
            }

            if (string.IsNullOrWhiteSpace(baseName)) baseName = "marquee";
            return baseName + " (JUKE).jpg";
        }

        private string PromptForText(string title, string prompt)
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
    }
}
