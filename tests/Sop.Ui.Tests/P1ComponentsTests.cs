using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Sop.Ui.Controls;
using Xunit;

namespace Sop.Ui.Tests
{
    public class P1ComponentsTests
    {
        [Fact]
        public void NumericKeypad_HandleKey_UpdatesValueCorrectly()
        {
            ExecuteInSta(() =>
            {
                var keypad = new NumericKeypad();
                keypad.HandleKey("1");
                keypad.HandleKey("5");
                keypad.HandleKey(".");
                keypad.HandleKey("5");

                Assert.Equal("15.5", keypad.Value);

                keypad.HandleKey("BACKSPACE");
                Assert.Equal("15.", keypad.Value);

                keypad.HandleKey("CLEAR");
                Assert.Equal(string.Empty, keypad.Value);
            });
        }

        [Fact]
        public void NumericKeypad_QuickCash_IncrementsValue()
        {
            ExecuteInSta(() =>
            {
                var keypad = new NumericKeypad
                {
                    Value = "20.00"
                };

                keypad.HandleKey("+50");
                Assert.Equal("70.00", keypad.Value);
            });
        }

        [Fact]
        public void NumericKeypad_EnterKey_FiresEnterPressedEvent()
        {
            ExecuteInSta(() =>
            {
                var keypad = new NumericKeypad();
                bool entered = false;
                keypad.EnterPressed += (s, e) => entered = true;

                keypad.HandleKey("ENTER");
                Assert.True(entered);
            });
        }

        [Fact]
        public void TableToolbar_Properties_AssignAndClearSearch()
        {
            ExecuteInSta(() =>
            {
                var toolbar = new TableToolbar
                {
                    SearchPlaceholder = "Filter items...",
                    SearchText = "Antibiotic",
                    ItemCountText = "48 items",
                    FilterBadgeCount = 3
                };

                Assert.Equal("Filter items...", toolbar.SearchPlaceholder);
                Assert.Equal("Antibiotic", toolbar.SearchText);
                Assert.Equal("48 items", toolbar.ItemCountText);
                Assert.Equal(3, toolbar.FilterBadgeCount);
            });
        }

        [Fact]
        public void ShortcutOverlay_Filter_GroupsAndFiltersShortcuts()
        {
            ExecuteInSta(() =>
            {
                var overlay = new ShortcutOverlay
                {
                    ShortcutsSource = new List<ShortcutEntry>
                    {
                        new ShortcutEntry("F1", "Help and documentation", "General"),
                        new ShortcutEntry("F2", "New POS Sale", "POS"),
                        new ShortcutEntry("F9", "Quick Pay Cash", "POS"),
                        new ShortcutEntry("Ctrl+P", "Print receipt", "General")
                    }
                };

                // Filter by "POS"
                overlay.FilterText = "POS";
                var filtered = overlay.FilteredShortcuts?.Cast<IGrouping<string, ShortcutEntry>>().ToList();

                Assert.NotNull(filtered);
                Assert.Single(filtered);
                Assert.Equal("POS", filtered![0].Key);
                Assert.Equal(2, filtered[0].Count());
            });
        }

        [Fact]
        public void SyncStatus_StatesAndCounts_Verify()
        {
            ExecuteInSta(() =>
            {
                var status = new SyncStatus
                {
                    State = SyncState.Pending,
                    PendingCount = 7,
                    LastSyncedText = "5 mins ago"
                };

                Assert.Equal(SyncState.Pending, status.State);
                Assert.Equal(7, status.PendingCount);
                Assert.Equal("5 mins ago", status.LastSyncedText);

                status.State = SyncState.Conflict;
                status.ConflictCount = 1;
                Assert.Equal(SyncState.Conflict, status.State);
                Assert.Equal(1, status.ConflictCount);
            });
        }

        [Fact]
        public void SopsStepper_StepNavigation_UpdatesCompletedAndActiveStates()
        {
            ExecuteInSta(() =>
            {
                var steps = new List<StepItem>
                {
                    new StepItem("Select File", "Choose backup archive"),
                    new StepItem("Verification", "Check checksums"),
                    new StepItem("Restore", "Write database records"),
                    new StepItem("Complete", "Verification summary")
                };

                var stepper = new SopsStepper
                {
                    StepsSource = steps,
                    CurrentStep = 1 // Step 2 (Verification)
                };

                var formatted = stepper.FormattedSteps?.Cast<StepItem>().ToList();
                Assert.NotNull(formatted);
                Assert.Equal(4, formatted!.Count);

                // Step 0 should be completed
                Assert.Equal(StepState.Completed, formatted[0].State);
                Assert.Equal(1, formatted[0].StepNumber);

                // Step 1 should be active
                Assert.Equal(StepState.Active, formatted[1].State);
                Assert.Equal(2, formatted[1].StepNumber);

                // Step 2 and 3 should be pending
                Assert.Equal(StepState.Pending, formatted[2].State);
                Assert.Equal(StepState.Pending, formatted[3].State);
                Assert.True(formatted[3].IsLast);
            });
        }

        private static void ExecuteInSta(Action action)
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null) throw caught;
        }
    }
}
