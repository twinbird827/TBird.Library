using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TBird.Wpf.Behaviors
{
    public partial class ScrollViewerBehavior
    {
        private static Type Type = typeof(ScrollViewerBehavior);

        public static DependencyProperty SyncRFooterProperty = BehaviorUtil.RegisterAttached(
            "SyncRFooter", Type, default(RowDefinition), OnSetSyncRFooterCallback
        );

        public static void SetSyncRFooter(DependencyObject target, object value)
        {
            target.SetValue(SyncRFooterProperty, value);
        }

        public static RowDefinition GetSyncRFooter(DependencyObject target)
        {
            return (RowDefinition)target.GetValue(SyncRFooterProperty);
        }

        private static void OnSetSyncRFooterCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is ScrollViewer viewer)
            {
                BehaviorUtil.Loaded(viewer, ScrollViewerBehavior_SyncRFooter_Loaded);
            }
        }

        private static void ScrollViewerBehavior_SyncRFooter_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer viewer && viewer.Template.FindName("PART_HorizontalScrollBar", viewer) is ScrollBar bar && GetSyncRFooter(viewer) is RowDefinition def)
            {
                RoutedEventHandler loadedhandler = (dummy, args) =>
                {
                    if (def.Height.Value != bar.ActualHeight)
                    {
                        def.Height = new GridLength(bar.ActualHeight);
                    }
                };
                DependencyPropertyChangedEventHandler visiblechangedhandler = (dummy, args) =>
                {
                    BehaviorUtil.Loaded(bar, loadedhandler);
                };
                BehaviorUtil.SetEventHandler(bar,
                    x => x.IsVisibleChanged += visiblechangedhandler,
                    x => x.IsVisibleChanged -= visiblechangedhandler
                );
                BehaviorUtil.Loaded(bar, loadedhandler);
            }
        }

        public static DependencyProperty SyncCFooterProperty = BehaviorUtil.RegisterAttached(
            "SyncCFooter", Type, default(ColumnDefinition), OnSetSyncCFooterCallback
        );

        public static void SetSyncCFooter(DependencyObject target, object value)
        {
            target.SetValue(SyncCFooterProperty, value);
        }

        public static ColumnDefinition GetSyncCFooter(DependencyObject target)
        {
            return (ColumnDefinition)target.GetValue(SyncCFooterProperty);
        }

        private static void OnSetSyncCFooterCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is ScrollViewer viewer)
            {
                BehaviorUtil.Loaded(viewer, ScrollViewerBehavior_SyncCFooter_Loaded);
            }
        }

        private static void ScrollViewerBehavior_SyncCFooter_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer viewer && viewer.Template.FindName("PART_VerticalScrollBar", viewer) is ScrollBar bar && GetSyncCFooter(viewer) is ColumnDefinition def)
            {
                RoutedEventHandler loadedhandler = (dummy, args) =>
                {
                    if (def.Width.Value != bar.ActualWidth)
                    {
                        def.Width = new GridLength(bar.ActualWidth);
                    }
                };
                DependencyPropertyChangedEventHandler visiblechangedhandler = (dummy, args) =>
                {
                    BehaviorUtil.Loaded(bar, loadedhandler);
                };
                BehaviorUtil.SetEventHandler(bar,
                    x => x.IsVisibleChanged += visiblechangedhandler,
                    x => x.IsVisibleChanged -= visiblechangedhandler
                );
                BehaviorUtil.Loaded(bar, loadedhandler);
            }
        }

        public static DependencyProperty SyncRScrollProperty = BehaviorUtil.RegisterAttached(
            "SyncRScroll", Type, default(ScrollViewer), OnSetSyncRScrollCallback
        );

        public static void SetSyncRScroll(DependencyObject target, object value)
        {
            target.SetValue(SyncRScrollProperty, value);
        }

        public static ScrollViewer GetSyncRScroll(DependencyObject target)
        {
            return (ScrollViewer)target.GetValue(SyncRScrollProperty);
        }

        private static void OnSetSyncRScrollCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is ScrollViewer src && e.NewValue is ScrollViewer tgt)
            {
                BehaviorUtil.SetEventHandler(src,
                    x => x.ScrollChanged += ScrollViewerBehavior_SyncRScroll_ScrollChanged,
                    x => x.ScrollChanged -= ScrollViewerBehavior_SyncRScroll_ScrollChanged
                );

                ScrollViewerBehavior_SyncMouseWheel(src, tgt);
            }
        }

        private static void ScrollViewerBehavior_SyncRScroll_ScrollChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer src && GetSyncRScroll(src) is ScrollViewer tgt && src.VerticalOffset != tgt.VerticalOffset)
            {
                tgt.ScrollToVerticalOffset(src.VerticalOffset);
            }
        }

        public static DependencyProperty SyncCScrollProperty = BehaviorUtil.RegisterAttached(
            "SyncCScroll", Type, default(ScrollViewer), OnSetSyncCScrollCallback
        );

        public static void SetSyncCScroll(DependencyObject target, object value)
        {
            target.SetValue(SyncCScrollProperty, value);
        }

        public static ScrollViewer GetSyncCScroll(DependencyObject target)
        {
            return (ScrollViewer)target.GetValue(SyncCScrollProperty);
        }

        private static void OnSetSyncCScrollCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is ScrollViewer src && e.NewValue is ScrollViewer tgt)
            {
                BehaviorUtil.SetEventHandler(src,
                    x => x.ScrollChanged += ScrollViewerBehavior_SyncCScroll_ScrollChanged,
                    x => x.ScrollChanged -= ScrollViewerBehavior_SyncCScroll_ScrollChanged
                );

                ScrollViewerBehavior_SyncMouseWheel(src, tgt);
            }
        }

        private static void ScrollViewerBehavior_SyncCScroll_ScrollChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer src && GetSyncCScroll(src) is ScrollViewer tgt && src.HorizontalOffset != tgt.HorizontalOffset)
            {
                tgt.ScrollToHorizontalOffset(src.HorizontalOffset);
            }
        }

        private static void ScrollViewerBehavior_SyncMouseWheel(ScrollViewer src, ScrollViewer tgt)
        {
            MouseWheelEventHandler handler = (sender, e) =>
            {
                var onmousewheel = (MethodInfo)src.GetValue(OnMouseWheelProperty);
                if (onmousewheel == null)
                {
                    src.SetValue(OnMouseWheelProperty,
                        onmousewheel = src.GetType().GetMethod("OnMouseWheel", BindingFlags.NonPublic | BindingFlags.Instance)
                    );
                }
                onmousewheel.Invoke(src, new object[] { e });
                e.Handled = true;
            };

            BehaviorUtil.SetEventHandler(tgt,
                x => x.PreviewMouseWheel += handler,
                x => x.PreviewMouseWheel -= handler
            );
        }

        private static DependencyProperty OnMouseWheelProperty = BehaviorUtil.RegisterAttached(
            "OnMouseWheel", Type, default(MethodInfo), null
        );
    }
}