using System.Windows;
using Media = System.Windows.Media;

namespace MarketClock
{
    /// <summary>A colour theme: its name in the Theme menu and its highlight colour.</summary>
    public sealed record AppTheme(string Name, Media.Color Accent);

    // The app's highlight ("accent") colour and the themes that set it. Everything that used to
    // be drawn in pink now takes its colour from the brushes below: XAML through
    // {DynamicResource AccentBrush} and its paler relations, code through SetAccent. Choosing a
    // theme replaces those brushes in the application's resources, and everything on screen
    // follows at once. The choice is saved in the settings file.
    public static class Theme
    {
        // Resource keys. The paler ones are the same colour, more see-through.
        public const string AccentKey = "AccentBrush";              // text, icons, bars
        public const string TintKey = "AccentTintBrush";            // background under the mouse
        public const string SoftKey = "AccentSoftBrush";            // background of a pressed or selected item
        public const string PressedKey = "AccentPressedBrush";      // background of a pressed button
        public const string LineKey = "AccentLineBrush";            // the gaps between panels, under the mouse
        public const string BorderKey = "AccentBorderBrush";        // border under the mouse
        public const string LineColorKey = "AccentLineColor";       // LineKey's colour, where a Color is wanted

        /// <summary>The themes, in menu order. The first one is the default.</summary>
        public static readonly IReadOnlyList<AppTheme> All = new[]
        {
            new AppTheme("Dragon Fruit", Media.Color.FromRgb(0xFF, 0x00, 0xFF)),
            new AppTheme("Apple", Media.Color.FromRgb(0xFF, 0x3B, 0x30)),
            new AppTheme("Watermelon", Media.Color.FromRgb(0xFF, 0x4F, 0x79)),
            new AppTheme("Peach", Media.Color.FromRgb(0xFF, 0xAB, 0x7A)),
            new AppTheme("Orange", Media.Color.FromRgb(0xFF, 0x8C, 0x00)),
            new AppTheme("Mango", Media.Color.FromRgb(0xFF, 0xB6, 0x27)),
            new AppTheme("Banana", Media.Color.FromRgb(0xFF, 0xE1, 0x35)),
            new AppTheme("Lime", Media.Color.FromRgb(0xA8, 0xE6, 0x00)),
            new AppTheme("Blueberry", Media.Color.FromRgb(0x4F, 0x86, 0xFF)),
            new AppTheme("Grape", Media.Color.FromRgb(0xA5, 0x6B, 0xFF)),
        };

        public static AppTheme Current { get; private set; } = All[0];

        /// <summary>Switches to the theme with this name (the default one if there is no such theme).</summary>
        public static void Apply(string? name)
        {
            Current = All.FirstOrDefault(theme => string.Equals(theme.Name, name, StringComparison.OrdinalIgnoreCase)) ?? All[0];

            var resources = System.Windows.Application.Current.Resources;
            var accent = Current.Accent;

            resources[AccentKey] = Brush(accent, 0xFF);
            resources[TintKey] = Brush(accent, 0x22);
            resources[SoftKey] = Brush(accent, 0x33);
            resources[PressedKey] = Brush(accent, 0x44);
            resources[LineKey] = Brush(accent, 0x55);
            resources[BorderKey] = Brush(accent, 0xAA);
            resources[LineColorKey] = Media.Color.FromArgb(0x55, accent.R, accent.G, accent.B);
        }

        /// <summary>
        /// Colours a property with the accent while <paramref name="accent"/> is true, and with
        /// <paramref name="otherwise"/> while it is not. Unlike assigning a brush, this keeps
        /// following the theme when it is changed later.
        /// </summary>
        public static void SetAccent(FrameworkElement element, DependencyProperty property, bool accent, Media.Brush otherwise)
        {
            if (accent)
            {
                element.SetResourceReference(property, AccentKey);
            }
            else
            {
                element.SetValue(property, otherwise);
            }
        }

        private static Media.SolidColorBrush Brush(Media.Color colour, byte alpha)
        {
            var brush = new Media.SolidColorBrush(Media.Color.FromArgb(alpha, colour.R, colour.G, colour.B));
            brush.Freeze();
            return brush;
        }
    }
}
