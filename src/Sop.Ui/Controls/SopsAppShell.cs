using System.Windows;
using System.Windows.Controls;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Complete composite application shell layout container from SOPs Shell architecture (sops-backup/wpf-beta).
    /// Orchestrates the Header (TopBar), Left Side Header (Sidebar), Right Side Header (SidePanel),
    /// Bottom Status Bar (StatusBar), and Center Workspace Content in a unified grid.
    /// </summary>
    public class SopsAppShell : ContentControl
    {
        static SopsAppShell()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SopsAppShell), new FrameworkPropertyMetadata(typeof(SopsAppShell)));
        }

        public static readonly DependencyProperty TopBarProperty =
            DependencyProperty.Register(nameof(TopBar), typeof(object), typeof(SopsAppShell), new PropertyMetadata(null));

        public object? TopBar
        {
            get => GetValue(TopBarProperty);
            set => SetValue(TopBarProperty, value);
        }

        public static readonly DependencyProperty SidebarProperty =
            DependencyProperty.Register(nameof(Sidebar), typeof(object), typeof(SopsAppShell), new PropertyMetadata(null));

        public object? Sidebar
        {
            get => GetValue(SidebarProperty);
            set => SetValue(SidebarProperty, value);
        }

        public static readonly DependencyProperty RightPanelProperty =
            DependencyProperty.Register(nameof(RightPanel), typeof(object), typeof(SopsAppShell), new PropertyMetadata(null));

        public object? RightPanel
        {
            get => GetValue(RightPanelProperty);
            set => SetValue(RightPanelProperty, value);
        }

        public static readonly DependencyProperty StatusBarProperty =
            DependencyProperty.Register(nameof(StatusBar), typeof(object), typeof(SopsAppShell), new PropertyMetadata(null));

        public object? StatusBar
        {
            get => GetValue(StatusBarProperty);
            set => SetValue(StatusBarProperty, value);
        }

        public static readonly DependencyProperty IsRightPanelOpenProperty =
            DependencyProperty.Register(nameof(IsRightPanelOpen), typeof(bool), typeof(SopsAppShell), new PropertyMetadata(true));

        public bool IsRightPanelOpen
        {
            get => (bool)GetValue(IsRightPanelOpenProperty);
            set => SetValue(IsRightPanelOpenProperty, value);
        }
    }
}
