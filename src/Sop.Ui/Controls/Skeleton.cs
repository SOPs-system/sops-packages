using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// A placeholder block that fades in and out while real content is being
    /// read — the shape of what is coming, rather than a blank panel or a
    /// spinner where the content will be. Give it a Width/Height (or let it
    /// stretch) and it takes the surface's sunken tone.
    ///
    /// Like <see cref="Spinner"/>, it animates only while it can actually be
    /// seen: a screen kept built behind the one showing would otherwise pulse
    /// for nobody, and every one of these is a running animation.
    /// </summary>
    public class Skeleton : Control
    {
        static Skeleton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Skeleton), new FrameworkPropertyMetadata(typeof(Skeleton)));
            FocusableProperty.OverrideMetadata(typeof(Skeleton), new FrameworkPropertyMetadata(false));
        }

        /// <summary>
        /// How long one breath takes. Slow enough to read as waiting rather
        /// than as something blinking for attention.
        /// </summary>
        public static TimeSpan Period { get; set; } = TimeSpan.FromSeconds(1.1);

        public Skeleton()
        {
            IsVisibleChanged += (_, e) => Pulse(e.NewValue is bool visible && visible);
        }

        private void Pulse(bool on)
        {
            if (!on || !Motion.Enabled)
            {
                BeginAnimation(OpacityProperty, null);
                return;
            }
            var breath = new DoubleAnimation(1.0, 0.45, Period)
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            };
            BeginAnimation(OpacityProperty, breath);
        }
    }
}
