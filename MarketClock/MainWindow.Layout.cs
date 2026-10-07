using System.Windows;
using MahApps.Metro.IconPacks;
using Controls = System.Windows.Controls;
using Media = System.Windows.Media;

namespace MarketClock
{
    // Dashboard layout. The dashboard is up to eight columns plus a strip along the bottom
    // ("zones"); each zone stacks panels top to bottom. A column has a heading that can be
    // renamed, and F1 to F8 show or hide columns 1 to 8. This file:
    //   - builds the grid of columns, rows and draggable gaps from the saved layout
    //   - lets the gaps be dragged to resize columns, panels and the bottom strip
    //   - lets a panel be dragged by the small handle at its top edge into any zone
    //   - saves the arrangement after every change (see DashboardLayout)
    // Which panels are shown at all is decided in the column menus (MainWindow.Panels.cs).
    public partial class MainWindow
    {
        private const double DashboardGap = 8;        // thickness of the draggable gaps
        private const double DragStartDistance = 6;   // how far the mouse must move before a drag begins

        private readonly Dictionary<string, Controls.Border> dashboardPanels = new();
        private DashboardLayout dashboardLayout = new();

        // What the current grid was built from, so sizes can be read back after a resize
        // and drop positions worked out during a drag.
        private readonly List<List<(Controls.DefinitionBase Definition, Action<double> Store, Func<double> Read)>> dashboardSizeGroups = new();
        private readonly List<(DashboardZone Zone, Controls.Grid Container)> dashboardZoneContainers = new();

        // Drag in progress.
        private string? dragKey;
        private System.Windows.Point dragStart;
        private bool dragStarted;
        private (DashboardZone Zone, int Index)? dropTarget;

        /// <summary>Takes ownership of the panels declared in XAML and loads the saved arrangement.</summary>
        private void InitializeDashboard()
        {
            dashboardPanels["Clock"] = ClockPanel;
            dashboardPanels["HourlyBell"] = HourlyBellPanel;
            dashboardPanels["Countdown"] = CountdownPanel;
            dashboardPanels["Jee"] = JeePanel;
            dashboardPanels["NetWorth"] = NetWorthPanel;
            dashboardPanels["NetWorthGraph"] = NetWorthGraphPanel;
            dashboardPanels["Accounts"] = AccountsPanel;
            dashboardPanels["Balance"] = BalancePanel;
            dashboardPanels["Recent"] = RecentPanel;
            dashboardPanels["Top"] = TopPanel;
            dashboardPanels["DailyExpenses"] = DailyExpensesPanel;
            dashboardPanels["MonthSummary"] = MonthSummaryPanel;
            dashboardPanels["Songs"] = SongsPanel;
            dashboardPanels["Equalizer"] = EqualizerPanel;
            dashboardPanels["Todo"] = TodoPanel;
            dashboardPanels["Reminders"] = RemindersPanel;
            dashboardPanels["Notes"] = NotesPanel;
            dashboardPanels["Habits"] = HabitsPanel;

            foreach (var (key, panel) in dashboardPanels)
            {
                DetachPanel(panel);
                panel.ClipToBounds = true; // a panel made small crops its content instead of spilling over its neighbours
                AddDragHandle(key, panel);
            }

            dashboardLayout = DashboardLayout.Load(dashboardPanels.Keys);

            PreviewKeyDown += Dashboard_PreviewKeyDown;
        }

        // F1 to F8 show or hide columns 1 to 8.
        private void Dashboard_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key < System.Windows.Input.Key.F1 || e.Key > System.Windows.Input.Key.F8
                || System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.None)
            {
                return;
            }

            var index = e.Key - System.Windows.Input.Key.F1;
            if (index >= dashboardLayout.Columns.Count)
            {
                return;
            }

            e.Handled = true;
            ToggleColumn(index);
        }

        /// <summary>Shows a hidden column or hides a shown one (its F-key, or its menu).</summary>
        private void ToggleColumn(int index)
        {
            var column = dashboardLayout.Columns[index];
            column.Visible = !column.Visible;
            dashboardLayout.Save();
            BuildDashboard();
        }

        /// <summary>
        /// The heading of a column: its F-key, its name, and at the end an icon that turns the name
        /// into a text box (pencil) and saves what was typed (tick). Enter also saves; Esc cancels.
        /// </summary>
        private FrameworkElement BuildColumnHeader(DashboardZone zone, int index)
        {
            var header = new Controls.Grid { Margin = new Thickness(4, 0, 0, 4) };
            header.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new Controls.ColumnDefinition());
            header.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = GridLength.Auto });

            var key = new Controls.TextBlock
            {
                Text = $"F{index + 1}",
                Style = (Style)FindResource("PanelEmptyText"),
                FontSize = 10,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = $"Press F{index + 1} to show or hide this column",
            };

            var name = new Controls.TextBlock
            {
                Text = zone.Name,
                Style = (Style)FindResource("PanelHeader"),
                Margin = new Thickness(0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            };

            var box = new Controls.TextBox
            {
                MaxLength = 40,
                Visibility = Visibility.Collapsed,
                Background = new Media.SolidColorBrush(Media.Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
                Foreground = Media.Brushes.White,
                CaretBrush = Media.Brushes.White,
                BorderBrush = new Media.SolidColorBrush(Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 1, 4, 1),
                FontFamily = (Media.FontFamily)FindResource("JetBrainsMonoRegular"),
                FontSize = 12,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            };

            var icon = new PackIconMaterial { Kind = PackIconMaterialKind.PencilOutline, Width = 12, Height = 12 };
            var button = new Controls.Button
            {
                Style = (Style)FindResource("PanelIconButton"),
                Content = icon,
                Foreground = new Media.SolidColorBrush(Media.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                ToolTip = "Rename this column",
            };

            var editing = false;

            void BeginEdit()
            {
                editing = true;
                box.Text = zone.Name;
                name.Visibility = Visibility.Collapsed;
                box.Visibility = Visibility.Visible;
                icon.Kind = PackIconMaterialKind.Check;
                button.ToolTip = "Save the name";
                box.Focus();
                box.SelectAll();
            }

            void EndEdit(bool save)
            {
                if (!editing)
                {
                    return;
                }

                editing = false;

                if (save)
                {
                    // An empty name goes back to the standard one.
                    var typed = box.Text.Trim();
                    zone.Name = typed.Length > 0 ? typed : DashboardLayout.DefaultColumnName(index);
                    name.Text = zone.Name;
                    dashboardLayout.Save();
                }

                box.Visibility = Visibility.Collapsed;
                name.Visibility = Visibility.Visible;
                icon.Kind = PackIconMaterialKind.PencilOutline;
                button.ToolTip = "Rename this column";
            }

            button.Click += (_, _) =>
            {
                if (editing)
                {
                    EndEdit(save: true);
                }
                else
                {
                    BeginEdit();
                }
            };

            box.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    e.Handled = true;
                    EndEdit(save: true);
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    e.Handled = true;
                    EndEdit(save: false);
                }
            };

            // Clicking elsewhere keeps what was typed.
            box.LostKeyboardFocus += (_, _) => EndEdit(save: true);

            Controls.Grid.SetColumn(name, 1);
            Controls.Grid.SetColumn(box, 1);
            Controls.Grid.SetColumn(button, 2);
            header.Children.Add(key);
            header.Children.Add(name);
            header.Children.Add(box);
            header.Children.Add(button);
            return header;
        }

        private static void DetachPanel(Controls.Border panel)
        {
            if (panel.Parent is Controls.Panel parent)
            {
                parent.Children.Remove(panel);
            }
        }

        /// <summary>Adds the invisible grab area at the top edge of a panel that it is dragged by.</summary>
        private void AddDragHandle(string key, Controls.Border panel)
        {
            var content = panel.Child;
            panel.Child = null;

            var handle = new Controls.Border
            {
                Tag = key,
                Width = 48,
                Height = 12,
                Background = Media.Brushes.Transparent, // invisible, but still catches the mouse
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new Thickness(0, -11, 0, 0),    // sits in the panel's padding, above the content
                Cursor = System.Windows.Input.Cursors.SizeAll,
                ToolTip = "Drag to move this panel",
            };
            handle.MouseLeftButtonDown += DragHandle_MouseLeftButtonDown;

            var host = new Controls.Grid();
            if (content != null)
            {
                host.Children.Add(content);
            }

            host.Children.Add(handle);
            panel.Child = host;
        }

        // ---- Building the grid ----

        private List<DashboardSlot> VisibleSlots(DashboardZone zone) =>
            zone.Panels.Where(slot => dashboardPanels.ContainsKey(slot.Key) && IsPanelShown(slot.Key)).ToList();

        /// <summary>
        /// Rebuilds the dashboard from the layout. While a panel is being dragged
        /// (<paramref name="arranging"/>), the bottom strip is shown even when empty so it can be dropped into.
        /// </summary>
        private void BuildDashboard(bool arranging = false)
        {
            foreach (var panel in dashboardPanels.Values)
            {
                DetachPanel(panel);
            }

            DashboardHost.Children.Clear();
            DashboardHost.RowDefinitions.Clear();
            dashboardSizeGroups.Clear();
            dashboardZoneContainers.Clear();

            // Columns.
            var columnsGrid = new Controls.Grid();
            var columnSizes = NewSizeGroup();

            for (var index = 0; index < dashboardLayout.Columns.Count; index++)
            {
                var zone = dashboardLayout.Columns[index];

                // A column switched off with its F-key is left out altogether.
                if (!zone.Visible)
                {
                    continue;
                }

                // A column that is on is shown even with no panels in it: its heading and an empty outline.
                var slots = VisibleSlots(zone);

                if (columnsGrid.ColumnDefinitions.Count > 0)
                {
                    columnsGrid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(DashboardGap) });
                    var splitter = CreateSplitter(betweenColumns: true, enabled: !arranging);
                    Controls.Grid.SetColumn(splitter, columnsGrid.ColumnDefinitions.Count - 1);
                    columnsGrid.Children.Add(splitter);
                }

                var column = new Controls.ColumnDefinition { MinWidth = 60, Width = new GridLength(zone.Weight, GridUnitType.Star) };
                columnSizes.Add((column, weight => zone.Weight = weight, () => column.Width.Value));

                columnsGrid.ColumnDefinitions.Add(column);

                // The column itself: its heading, then its panels.
                var columnHost = new Controls.Grid();
                columnHost.RowDefinitions.Add(new Controls.RowDefinition { Height = GridLength.Auto });
                columnHost.RowDefinitions.Add(new Controls.RowDefinition());
                columnHost.Children.Add(BuildColumnHeader(zone, index));

                var container = BuildZone(zone, slots, arranging);
                Controls.Grid.SetRow(container, 1);
                columnHost.Children.Add(container);

                Controls.Grid.SetColumn(columnHost, columnsGrid.ColumnDefinitions.Count - 1);
                columnsGrid.Children.Add(columnHost);
            }

            // Columns on top, bottom strip underneath.
            var bottomSlots = VisibleSlots(dashboardLayout.Bottom);
            var hasColumns = columnsGrid.ColumnDefinitions.Count > 0;
            var hasBottom = bottomSlots.Count > 0 || arranging;
            var hostSizes = NewSizeGroup();

            if (hasColumns)
            {
                var mainRow = new Controls.RowDefinition { Height = new GridLength(dashboardLayout.MainWeight, GridUnitType.Star), MinHeight = 60 };
                DashboardHost.RowDefinitions.Add(mainRow);
                DashboardHost.Children.Add(columnsGrid);

                if (bottomSlots.Count > 0)
                {
                    hostSizes.Add((mainRow, weight => dashboardLayout.MainWeight = weight, () => mainRow.Height.Value));
                }
            }

            if (hasBottom)
            {
                if (hasColumns)
                {
                    DashboardHost.RowDefinitions.Add(new Controls.RowDefinition { Height = new GridLength(DashboardGap) });
                    var splitter = CreateSplitter(betweenColumns: false, enabled: !arranging && bottomSlots.Count > 0);
                    Controls.Grid.SetRow(splitter, DashboardHost.RowDefinitions.Count - 1);
                    DashboardHost.Children.Add(splitter);
                }

                var bottomRow = new Controls.RowDefinition { MinHeight = 40 };
                if (bottomSlots.Count == 0)
                {
                    bottomRow.Height = new GridLength(60); // empty strip, shown only as a drop target
                }
                else
                {
                    bottomRow.Height = new GridLength(dashboardLayout.Bottom.Weight, GridUnitType.Star);
                    if (hasColumns)
                    {
                        hostSizes.Add((bottomRow, weight => dashboardLayout.Bottom.Weight = weight, () => bottomRow.Height.Value));
                    }
                }

                DashboardHost.RowDefinitions.Add(bottomRow);

                var container = BuildZone(dashboardLayout.Bottom, bottomSlots, arranging);
                Controls.Grid.SetRow(container, DashboardHost.RowDefinitions.Count - 1);
                DashboardHost.Children.Add(container);
            }
        }

        /// <summary>One zone: its visible panels stacked top to bottom with a draggable gap between each pair.</summary>
        private Controls.Grid BuildZone(DashboardZone zone, List<DashboardSlot> slots, bool arranging)
        {
            var container = new Controls.Grid();
            var rowSizes = NewSizeGroup();

            dashboardZoneContainers.Add((zone, container));

            if (slots.Count == 0)
            {
                // An empty column, or (only while arranging) the empty bottom strip: outlined, so it
                // reads as a place to drop a panel. The outline is brighter while a panel is being dragged.
                container.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Stroke = new Media.SolidColorBrush(Media.Color.FromArgb((byte)(arranging ? 0x66 : 0x2A), 0xFF, 0xFF, 0xFF)),
                    StrokeThickness = 1,
                    StrokeDashArray = new Media.DoubleCollection { 4, 4 },
                    RadiusX = 14,
                    RadiusY = 14,
                });
                container.Children.Add(new Controls.TextBlock
                {
                    Text = arranging ? "drop here" : "empty",
                    FontSize = 10,
                    Foreground = new Media.SolidColorBrush(Media.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                });
                return container;
            }

            foreach (var slot in slots)
            {
                if (container.RowDefinitions.Count > 0)
                {
                    container.RowDefinitions.Add(new Controls.RowDefinition { Height = new GridLength(DashboardGap) });
                    var splitter = CreateSplitter(betweenColumns: false, enabled: !arranging);
                    Controls.Grid.SetRow(splitter, container.RowDefinitions.Count - 1);
                    container.Children.Add(splitter);
                }

                var row = new Controls.RowDefinition { Height = new GridLength(slot.Weight, GridUnitType.Star), MinHeight = 36 };
                container.RowDefinitions.Add(row);
                rowSizes.Add((row, weight => slot.Weight = weight, () => row.Height.Value));

                var panel = dashboardPanels[slot.Key];
                panel.Visibility = Visibility.Visible;
                Controls.Grid.SetRow(panel, container.RowDefinitions.Count - 1);
                container.Children.Add(panel);
            }

            return container;
        }

        private List<(Controls.DefinitionBase, Action<double>, Func<double>)> NewSizeGroup()
        {
            var group = new List<(Controls.DefinitionBase, Action<double>, Func<double>)>();
            dashboardSizeGroups.Add(group);
            return group;
        }

        // ---- Resizing ----

        private Controls.GridSplitter CreateSplitter(bool betweenColumns, bool enabled)
        {
            var splitter = new Controls.GridSplitter
            {
                Style = (Style)FindResource("DashboardSplitter"),
                ResizeDirection = betweenColumns ? Controls.GridResizeDirection.Columns : Controls.GridResizeDirection.Rows,
                ResizeBehavior = Controls.GridResizeBehavior.PreviousAndNext,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Cursor = betweenColumns ? System.Windows.Input.Cursors.SizeWE : System.Windows.Input.Cursors.SizeNS,
                IsEnabled = enabled,
            };
            splitter.DragCompleted += (_, _) => StoreSizesAndSave();
            return splitter;
        }

        /// <summary>After a gap has been dragged, copies the new sizes into the layout and saves it.</summary>
        private void StoreSizesAndSave()
        {
            foreach (var group in dashboardSizeGroups)
            {
                if (group.Count == 0)
                {
                    continue;
                }

                // Dragging turns the shares into pixel-sized numbers. Scale them back so they stay
                // comparable with panels that are hidden right now.
                var total = group.Sum(entry => entry.Read());
                if (total <= 0)
                {
                    continue;
                }

                var scale = group.Count / total;
                foreach (var (_, store, read) in group)
                {
                    store(Math.Max(0.05, read() * scale));
                }
            }

            dashboardLayout.Save();
        }

        // ---- Dragging a panel to a new place ----

        private void DragHandle_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Handled here so the press does not also start moving the whole window.
            e.Handled = true;

            dragKey = (string)((FrameworkElement)sender).Tag;
            dragStart = e.GetPosition(this);
            dragStarted = false;
            dropTarget = null;

            // The window takes the mouse, because the panel itself is re-parented during the drag.
            PreviewMouseMove += Drag_MouseMove;
            PreviewMouseLeftButtonUp += Drag_MouseLeftButtonUp;
            LostMouseCapture += Drag_LostMouseCapture;
            CaptureMouse();
        }

        private void Drag_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (dragKey == null)
            {
                return;
            }

            if (!dragStarted)
            {
                var moved = e.GetPosition(this) - dragStart;
                if (Math.Abs(moved.X) < DragStartDistance && Math.Abs(moved.Y) < DragStartDistance)
                {
                    return;
                }

                dragStarted = true;
                BuildDashboard(arranging: true);
                dashboardPanels[dragKey].Opacity = 0.45;
                DashboardHost.UpdateLayout();
            }

            UpdateDropTarget(e.GetPosition(DashboardHost));
        }

        /// <summary>Works out which zone and position the mouse is over, and shows the marker there.</summary>
        private void UpdateDropTarget(System.Windows.Point mouse)
        {
            dropTarget = null;
            DropIndicator.Visibility = Visibility.Collapsed;

            foreach (var (zone, container) in dashboardZoneContainers)
            {
                var origin = container.TranslatePoint(new System.Windows.Point(0, 0), DashboardHost);
                var bounds = new Rect(origin.X, origin.Y, container.ActualWidth, container.ActualHeight);
                bounds.Inflate(DashboardGap / 2, DashboardGap / 2);

                if (!bounds.Contains(mouse))
                {
                    continue;
                }

                // Position within the zone: before the first panel whose middle is below the mouse.
                var slots = VisibleSlots(zone);
                var position = slots.Count;
                var markerY = origin.Y;

                for (var i = 0; i < slots.Count; i++)
                {
                    var panel = dashboardPanels[slots[i].Key];
                    var top = panel.TranslatePoint(new System.Windows.Point(0, 0), DashboardHost).Y;

                    if (mouse.Y < top + panel.ActualHeight / 2)
                    {
                        position = i;
                        markerY = top - DashboardGap / 2;
                        break;
                    }

                    markerY = top + panel.ActualHeight + DashboardGap / 2;
                }

                // Turn "position among the visible panels" into a position in the zone's full list.
                var index = position < slots.Count
                    ? zone.Panels.IndexOf(slots[position])
                    : slots.Count > 0 ? zone.Panels.IndexOf(slots[^1]) + 1 : zone.Panels.Count;

                dropTarget = (zone, index);

                DropIndicator.Width = Math.Max(0, container.ActualWidth);
                Controls.Canvas.SetLeft(DropIndicator, origin.X);
                Controls.Canvas.SetTop(DropIndicator, Math.Clamp(markerY - 1.5, 0, Math.Max(0, DashboardHost.ActualHeight - 3)));
                DropIndicator.Visibility = Visibility.Visible;
                return;
            }
        }

        private void Drag_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EndDrag(drop: true);
        }

        private void Drag_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Something else took the mouse (Alt+Tab, for example): put everything back.
            EndDrag(drop: false);
        }

        private void EndDrag(bool drop)
        {
            if (dragKey == null)
            {
                return;
            }

            var key = dragKey;
            var started = dragStarted;
            var target = dropTarget;

            dragKey = null;
            dragStarted = false;
            dropTarget = null;

            PreviewMouseMove -= Drag_MouseMove;
            PreviewMouseLeftButtonUp -= Drag_MouseLeftButtonUp;
            LostMouseCapture -= Drag_LostMouseCapture;
            ReleaseMouseCapture();

            DropIndicator.Visibility = Visibility.Collapsed;
            dashboardPanels[key].Opacity = 1;

            if (!started)
            {
                return; // a click on the handle, not a drag
            }

            if (drop && target != null)
            {
                MovePanel(key, target.Value.Zone, target.Value.Index);
                dashboardLayout.Save();
            }

            BuildDashboard();
        }

        private void MovePanel(string key, DashboardZone targetZone, int index)
        {
            var sourceZone = dashboardLayout.AllZones.First(zone => zone.Panels.Any(slot => slot.Key == key));
            var slot = sourceZone.Panels.First(s => s.Key == key);
            var sourceIndex = sourceZone.Panels.IndexOf(slot);

            sourceZone.Panels.RemoveAt(sourceIndex);

            if (ReferenceEquals(sourceZone, targetZone))
            {
                if (sourceIndex < index)
                {
                    index--;
                }
            }
            else
            {
                // Arriving in a different zone: take an average share of it, whatever size it had before.
                var neighbours = VisibleSlots(targetZone);
                slot.Weight = neighbours.Count > 0 ? neighbours.Average(s => s.Weight) : 1;
            }

            targetZone.Panels.Insert(Math.Clamp(index, 0, targetZone.Panels.Count), slot);
        }
    }
}
