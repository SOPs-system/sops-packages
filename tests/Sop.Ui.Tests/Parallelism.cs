using Xunit;

// One Application, one theme: every test here drives the same process-wide WPF
// state (Theme.Mode, Theme.IsRtl, the popups on screen), so two classes running
// at once would read each other's settings — an RTL test mirroring the app
// while another sets it back to left-to-right. Run them one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
