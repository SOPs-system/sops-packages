using System.Windows;

namespace Sop.Ui.Controls
{
    /// <summary>Toggle switch with built-in label (the Content).</summary>
    public class Switch : System.Windows.Controls.CheckBox
    {
        static Switch()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Switch), new FrameworkPropertyMetadata(typeof(Switch)));
        }
    }
}
