using System.Windows;
using System.Windows.Controls.Primitives;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Keeps a <see cref="Popup"/> with its window.
    ///
    /// A Popup is its own top-level window, and WPF never ties its lifetime to
    /// the one it was opened from. <c>StaysOpen=false</c> closes it on a click
    /// elsewhere IN THE APP and nothing else does — so left alone a dropdown
    /// floats over the desktop after the register is minimised, sent behind
    /// another program, moved or resized, painted on top of whatever the user
    /// switched to.
    ///
    /// <code>ui:PopupGuard.FollowsOwner="True"</code> on the popup closes it
    /// whenever its window stops being the one in front.
    /// </summary>
    public static class PopupGuard
    {
        public static readonly DependencyProperty FollowsOwnerProperty = DependencyProperty.RegisterAttached(
            "FollowsOwner", typeof(bool), typeof(PopupGuard),
            new FrameworkPropertyMetadata(false, OnFollowsOwnerChanged));

        public static void SetFollowsOwner(DependencyObject element, bool value) =>
            element.SetValue(FollowsOwnerProperty, value);

        public static bool GetFollowsOwner(DependencyObject element) =>
            (bool)element.GetValue(FollowsOwnerProperty);

        /// <summary>The window a popup is currently watching, so it can let go.</summary>
        private static readonly DependencyProperty OwnerProperty = DependencyProperty.RegisterAttached(
            "Owner", typeof(Window), typeof(PopupGuard), new FrameworkPropertyMetadata((Window?)null));

        private static void OnFollowsOwnerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Popup popup) return;
            if ((bool)e.NewValue)
            {
                popup.Opened += OnOpened;
                popup.Closed += OnClosed;
                if (popup.IsOpen) Attach(popup);
            }
            else
            {
                popup.Opened -= OnOpened;
                popup.Closed -= OnClosed;
                Detach(popup);
            }
        }

        private static void OnOpened(object? sender, System.EventArgs e)
        {
            if (sender is Popup popup) Attach(popup);
        }

        private static void OnClosed(object? sender, System.EventArgs e)
        {
            if (sender is Popup popup) Detach(popup);
        }

        /// <summary>
        /// Watch the window the popup hangs off. Its placement target is the
        /// honest answer; a popup with none falls back to its own tree, which
        /// for a popup declared in XAML is the window that declared it.
        /// </summary>
        private static void Attach(Popup popup)
        {
            Detach(popup);
            // GetWindow walks the visual/logical tree, so it needs a real
            // element: the popup itself is not in one until it opens.
            var anchor = popup.PlacementTarget ?? popup.Child;
            var owner = anchor == null ? null : Window.GetWindow(anchor);
            if (owner == null) return;
            popup.SetValue(OwnerProperty, owner);
            owner.Deactivated += OnOwnerLeft;
            owner.StateChanged += OnOwnerLeft;
            owner.LocationChanged += OnOwnerLeft;
            owner.SizeChanged += OnOwnerResized;
            // Remember which popup to close: one window can own several.
            Watchers.Add((owner, popup));
        }

        private static void Detach(Popup popup)
        {
            if (popup.GetValue(OwnerProperty) is not Window owner) return;
            popup.ClearValue(OwnerProperty);
            Watchers.RemoveAll(pair => ReferenceEquals(pair.Popup, popup));
            if (Watchers.Exists(pair => ReferenceEquals(pair.Owner, owner))) return;
            owner.Deactivated -= OnOwnerLeft;
            owner.StateChanged -= OnOwnerLeft;
            owner.LocationChanged -= OnOwnerLeft;
            owner.SizeChanged -= OnOwnerResized;
        }

        private static readonly System.Collections.Generic.List<(Window Owner, Popup Popup)> Watchers =
            new System.Collections.Generic.List<(Window, Popup)>();

        private static void OnOwnerLeft(object? sender, System.EventArgs e) => CloseFor(sender as Window);

        private static void OnOwnerResized(object? sender, SizeChangedEventArgs e) => CloseFor(sender as Window);

        private static void CloseFor(Window? owner)
        {
            if (owner == null) return;
            foreach (var pair in Watchers.ToArray())
            {
                if (ReferenceEquals(pair.Owner, owner)) pair.Popup.IsOpen = false;
            }
        }
    }
}
