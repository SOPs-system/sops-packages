namespace Sop.Ui
{
    /// <summary>Control heights map to the POS touch targets: Sm 36px · Md 44px · Lg 56px.</summary>
    public enum ControlSize { Sm, Md, Lg }

    /// <summary>Status semantics shared by Badge, Alert, Toast.</summary>
    public enum StatusVariant { Success, Warning, Danger, Info }

    /// <summary>The one button's looks. Variants live in Button.xaml and nowhere else.</summary>
    public enum ButtonVariant
    {
        Primary,
        Secondary,
        Soft,
        Ghost,
        Success,
        Danger,
        DangerSoft,
        Warning,
        WarningSoft,
    }

    public enum BadgeVariant { Neutral, Primary, Success, Warning, Danger, Info }

    public enum ToastVariant { Neutral, Success, Warning, Danger, Info }

    /// <summary>
    /// <c>Full</c> fills the window — for workspace dialogs that compose their
    /// own layout. It drops the radius, border and body padding.
    /// </summary>
    public enum ModalSize { Sm, Md, Lg, Full }

    /// <summary>Which edge of the trigger a dropdown lines up with (direction-aware).</summary>
    public enum MenuAlign { Start, End, Stretch }

    public enum ThemeMode { Light, Dark }
}
