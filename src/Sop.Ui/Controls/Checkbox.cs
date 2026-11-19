using System.Windows;

namespace Sop.Ui.Controls
{
    /// <summary>Checkbox with built-in label (the Content).</summary>
    public class Checkbox : System.Windows.Controls.CheckBox
    {
        static Checkbox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Checkbox), new FrameworkPropertyMetadata(typeof(Checkbox)));
        }
    }
}
