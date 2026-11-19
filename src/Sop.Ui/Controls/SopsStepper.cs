using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    public enum StepState
    {
        Pending,
        Active,
        Completed,
        Error
    }

    public class StepItem : DependencyObject
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(StepItem), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(StepItem), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(StepState), typeof(StepItem), new PropertyMetadata(StepState.Pending));

        public static readonly DependencyProperty StepNumberProperty =
            DependencyProperty.Register(nameof(StepNumber), typeof(int), typeof(StepItem), new PropertyMetadata(1));

        public static readonly DependencyProperty IsLastProperty =
            DependencyProperty.Register(nameof(IsLast), typeof(bool), typeof(StepItem), new PropertyMetadata(false));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public StepState State
        {
            get => (StepState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public int StepNumber
        {
            get => (int)GetValue(StepNumberProperty);
            set => SetValue(StepNumberProperty, value);
        }

        public bool IsLast
        {
            get => (bool)GetValue(IsLastProperty);
            set => SetValue(IsLastProperty, value);
        }

        public StepItem() { }

        public StepItem(string title, string description = "", StepState state = StepState.Pending)
        {
            Title = title;
            Description = description;
            State = state;
        }
    }

    public class StepSelectedEventArgs : RoutedEventArgs
    {
        public int Index { get; }
        public StepItem Step { get; }

        public StepSelectedEventArgs(RoutedEvent routedEvent, int index, StepItem step) : base(routedEvent)
        {
            Index = index;
            Step = step;
        }
    }

    /// <summary>
    /// Stepper and timeline control for guided multi-stage workflows, backup &amp; restore wizards,
    /// approvals, and prescription dispensing stages.
    /// </summary>
    public class SopsStepper : Control
    {
        static SopsStepper()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SopsStepper), new FrameworkPropertyMetadata(typeof(SopsStepper)));
        }

        public static readonly DependencyProperty CurrentStepProperty =
            DependencyProperty.Register(nameof(CurrentStep), typeof(int), typeof(SopsStepper),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentStepChanged));

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(SopsStepper),
                new PropertyMetadata(Orientation.Horizontal));

        public static readonly DependencyProperty StepsSourceProperty =
            DependencyProperty.Register(nameof(StepsSource), typeof(IEnumerable), typeof(SopsStepper),
                new PropertyMetadata(null, OnStepsSourceChanged));

        public static readonly DependencyProperty FormattedStepsProperty =
            DependencyProperty.Register(nameof(FormattedSteps), typeof(IEnumerable), typeof(SopsStepper),
                new PropertyMetadata(null));

        public static readonly RoutedEvent StepSelectedEvent =
            EventManager.RegisterRoutedEvent(nameof(StepSelected), RoutingStrategy.Bubble,
                typeof(EventHandler<StepSelectedEventArgs>), typeof(SopsStepper));

        public int CurrentStep
        {
            get => (int)GetValue(CurrentStepProperty);
            set => SetValue(CurrentStepProperty, value);
        }

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public IEnumerable? StepsSource
        {
            get => (IEnumerable?)GetValue(StepsSourceProperty);
            set => SetValue(StepsSourceProperty, value);
        }

        public IEnumerable? FormattedSteps
        {
            get => (IEnumerable?)GetValue(FormattedStepsProperty);
            private set => SetValue(FormattedStepsProperty, value);
        }

        public event EventHandler<StepSelectedEventArgs> StepSelected
        {
            add => AddHandler(StepSelectedEvent, value);
            remove => RemoveHandler(StepSelectedEvent, value);
        }

        public SopsStepper()
        {
            AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(OnStepButtonClick));
            AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnStepMouseDown));
        }

        private void OnStepMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                var el = dep as FrameworkElement;
                if (el?.DataContext is StepItem step)
                {
                    int idx = step.StepNumber - 1;
                    CurrentStep = idx;
                    RaiseEvent(new StepSelectedEventArgs(StepSelectedEvent, idx, step));
                    e.Handled = true;
                }
            }
        }

        private void OnStepButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.Source is FrameworkElement el && el.DataContext is StepItem step)
            {
                int idx = step.StepNumber - 1;
                CurrentStep = idx;
                RaiseEvent(new StepSelectedEventArgs(StepSelectedEvent, idx, step));
                e.Handled = true;
            }
        }

        private static void OnCurrentStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SopsStepper stepper)
            {
                stepper.UpdateStepsState();
            }
        }

        private static void OnStepsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SopsStepper stepper)
            {
                stepper.UpdateStepsState();
            }
        }

        private void UpdateStepsState()
        {
            if (StepsSource == null)
            {
                FormattedSteps = null;
                return;
            }

            var list = StepsSource.OfType<StepItem>().ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var step = list[i];
                step.StepNumber = i + 1;
                step.IsLast = (i == list.Count - 1);

                if (i < CurrentStep)
                {
                    step.State = StepState.Completed;
                }
                else if (i == CurrentStep)
                {
                    step.State = StepState.Active;
                }
                else
                {
                    step.State = StepState.Pending;
                }
            }

            FormattedSteps = list;
        }
    }
}
