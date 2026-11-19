using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// One glyph of the app icon set (Themes/Icons.xaml, <c>Icon.&lt;Glyph&gt;</c>):
    /// a 24×24 stroke outline scaled to <see cref="Size"/>, drawn in the
    /// control's Foreground. <c>&lt;ui:Icon Glyph="Bell" Size="17" /&gt;</c>.
    /// The stroke scales with the box, as an SVG viewBox would, and the glyph
    /// never mirrors under RTL (icons are pictures, not text).
    /// </summary>
    public class Icon : Control
    {
        static Icon()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(typeof(Icon)));
            FocusableProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
            IsHitTestVisibleProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
            FlowDirectionProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(FlowDirection.LeftToRight));
        }

        public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
            nameof(Glyph), typeof(string), typeof(Icon),
            new FrameworkPropertyMetadata(null, (d, e) => ((Icon)d).OnGlyphChanged((string?)e.NewValue)));

        /// <summary>Pixel size of the square box (default 18, the web's default).</summary>
        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(double), typeof(Icon),
            new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>The resolved geometry — set from <see cref="Glyph"/>, readable for custom drawing.</summary>
        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data), typeof(Geometry), typeof(Icon), new FrameworkPropertyMetadata(null));

        public string? Glyph
        {
            get => (string?)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public double Size
        {
            get => (double)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public Geometry? Data
        {
            get => (Geometry?)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        private void OnGlyphChanged(string? glyph)
        {
            if (string.IsNullOrEmpty(glyph)) ClearValue(DataProperty);
            // A resource reference (not a lookup) so a glyph added to the
            // dictionary later, or a re-merged theme, still resolves.
            else SetResourceReference(DataProperty, "Icon." + glyph);
        }
    }

    /// <summary>
    /// The SOP logo mark, filled with the Foreground. <see cref="Width"/> sets
    /// the rendered width; the height follows the mark's 436×193 aspect.
    /// </summary>
    public class BrandLogo : Control
    {
        static BrandLogo()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BrandLogo), new FrameworkPropertyMetadata(typeof(BrandLogo)));
            FocusableProperty.OverrideMetadata(typeof(BrandLogo), new FrameworkPropertyMetadata(false));
            IsHitTestVisibleProperty.OverrideMetadata(typeof(BrandLogo), new FrameworkPropertyMetadata(false));
            FlowDirectionProperty.OverrideMetadata(typeof(BrandLogo), new FrameworkPropertyMetadata(FlowDirection.LeftToRight));
        }
    }
}
