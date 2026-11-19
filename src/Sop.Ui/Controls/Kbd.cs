using System.Windows;
using System.Windows.Controls;

namespace Sop.Ui.Controls
{
    /// <summary>Keyboard key cap — shortcut hints in the status bar, menus, overlays.</summary>
    public class Kbd : ContentControl
    {
        static Kbd()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Kbd), new FrameworkPropertyMetadata(typeof(Kbd)));
            FocusableProperty.OverrideMetadata(typeof(Kbd), new FrameworkPropertyMetadata(false));
        }
    }
}
