namespace Sop.Ui
{
    /// <summary>
    /// Whether the primitives that move actually move — the spinner's ring,
    /// a skeleton's breath.
    ///
    /// A visual capture compares pixels against an approved baseline, and a
    /// running animation is at whatever phase the frame happened to catch it:
    /// the spinner's ring alone made the gallery's baselines fail on a busy
    /// machine and pass on an idle one. A capture run turns this off, so the
    /// moving parts are pinned at their resting frame like the clock and the
    /// window size already are.
    ///
    /// Off is not "hidden": a spinner still draws its ring and a skeleton
    /// still shows its block, so the capture proves the same pixels — they
    /// simply stop where they start.
    /// </summary>
    public static class Motion
    {
        public static bool Enabled { get; set; } = true;
    }
}
