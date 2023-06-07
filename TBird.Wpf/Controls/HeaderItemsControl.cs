using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TBird.Core;

namespace TBird.Wpf.Controls
{
    [StyleTypedProperty(Property = "ItemStyle", StyleTargetType = typeof(ItemsControl))]
    public class HeaderItemsControl : Control
    {
        private static Type Type = typeof(HeaderItemsControl);

        static HeaderItemsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(Type, new FrameworkPropertyMetadata(Type));
        }

        /// <summary>
        /// 行ﾍｯﾀﾞのﾃﾝﾌﾟﾚｰﾄ
        /// </summary>
        public GridLength RowHeaderWidth
        {
            get => (GridLength)GetValue(RowHeaderWidthProperty);
            set => SetValue(RowHeaderWidthProperty, value);
        }

        public static readonly DependencyProperty RowHeaderWidthProperty =
            BehaviorUtil.Register(nameof(RowHeaderWidth), Type, new GridLength(0), null);

        /// <summary>
        /// 行ﾍｯﾀﾞのﾃﾝﾌﾟﾚｰﾄ
        /// </summary>
        public DataTemplate RowHeaderTemplate
        {
            get => (DataTemplate)GetValue(RowHeaderTemplateProperty);
            set => SetValue(RowHeaderTemplateProperty, value);
        }

        public static readonly DependencyProperty RowHeaderTemplateProperty =
            BehaviorUtil.Register(nameof(RowHeaderTemplate), Type, default(DataTemplate), RowHeaderTemplatePropertyChanged);

        private static void RowHeaderTemplatePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is HeaderItemsControl c && e.NewValue is DataTemplate template)
            {
                c.RowHeaderWidth = template != null ? GridLength.Auto : new GridLength(0);
            }
        }

        /// <summary>
        /// 行明細のﾃﾝﾌﾟﾚｰﾄ
        /// </summary>
        public DataTemplate RowItemTemplate
        {
            get => (DataTemplate)GetValue(RowItemTemplateProperty);
            set => SetValue(RowItemTemplateProperty, value);
        }

        public static readonly DependencyProperty RowItemTemplateProperty =
            BehaviorUtil.Register(nameof(RowItemTemplate), Type, default(DataTemplate), null);

        /// <summary>
        /// 列ﾍｯﾀﾞのﾃﾝﾌﾟﾚｰﾄ
        /// </summary>
        public DataTemplate ColumnHeaderTemplate
        {
            get => (DataTemplate)GetValue(ColumnHeaderTemplateProperty);
            set => SetValue(ColumnHeaderTemplateProperty, value);
        }

        public static readonly DependencyProperty ColumnHeaderTemplateProperty =
            BehaviorUtil.Register(nameof(ColumnHeaderTemplate), Type, default(DataTemplate), null);

        /// <summary>
        /// 明細のﾃﾝﾌﾟﾚｰﾄ
        /// </summary>
        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateProperty =
            BehaviorUtil.Register(nameof(ItemTemplate), Type, default(DataTemplate), null);

        /// <summary>
        /// 明細のｽﾀｲﾙ
        /// </summary>
        public Style ItemStyle
        {
            get => (Style)GetValue(ItemStyleProperty);
            set => SetValue(ItemStyleProperty, value);
        }

        public static readonly DependencyProperty ItemStyleProperty =
            BehaviorUtil.Register(nameof(ItemStyle), Type, default(Style), null);

        /// <summary>
        /// 明細ﾘｽﾄ
        /// </summary>
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            BehaviorUtil.Register(nameof(ItemsSource), Type, default(IEnumerable), ItemsSourcePropertyChangedCallback);

        /// <summary>
        /// 明細ﾘｽﾄ内のｲﾍﾞﾝﾄで使用するScrollViewer
        /// </summary>
        private ScrollViewer[] ItemsSourceSV
        {
            get => (ScrollViewer[])GetValue(ItemsSourceSVProperty);
            set => SetValue(ItemsSourceSVProperty, value);
        }

        private static readonly DependencyProperty ItemsSourceSVProperty =
            BehaviorUtil.Register(nameof(ItemsSourceSV), Type, default(ScrollViewer[]), null);

        private static void ItemsSourcePropertyChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is HeaderItemsControl c && e.NewValue is INotifyCollectionChanged notify)
            {
                BehaviorUtil.AddCollectionChanged(c, notify, (dummy, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Reset)
                    {
                        if (args.Action != NotifyCollectionChangedAction.Reset) return;

                        var arr = c.ItemsSourceSV ?? (c.ItemsSourceSV = new[] { "RScrollViewer", "CScrollViewer", "IScrollViewer" }
                            .Select(x => c.Template.FindName(x, c) as ScrollViewer)
                            .Where(x => x != null)
                            .ToArray());

                        arr.ForEach(x =>
                        {
                            x.ScrollToTop();
                            x.ScrollToLeftEnd();
                        });
                    }
                });
            }
        }

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

        private static DependencyProperty SyncRFooterSBProperty = BehaviorUtil.RegisterAttached(
            "SyncRFooterSB", Type, default(ScrollBar), null
        );

        private static ScrollBar SetSyncRFooterSB(DependencyObject target, ScrollBar value)
        {
            target.SetValue(SyncRFooterSBProperty, value);
            return value;
        }

        private static ScrollBar GetSyncRFooterSB(DependencyObject target)
        {
            return (ScrollBar)target.GetValue(SyncRFooterSBProperty);
        }

        private static void OnSetSyncRFooterCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is ScrollViewer viewer)
            {
                BehaviorUtil.SetEventHandler(viewer,
                    x => x.ScrollChanged += ScrollViewerBehavior_SyncRFooter_ScrollChanged,
                    x => x.ScrollChanged -= ScrollViewerBehavior_SyncRFooter_ScrollChanged
                );
            }
        }

        private static void ScrollViewerBehavior_SyncRFooter_ScrollChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer sv && GetSyncRFooter(sv) is RowDefinition def)
            {
                var scrollbar = GetSyncRFooterSB(sv) ??
                    SetSyncRFooterSB(sv, sv.Template.FindName("PART_HorizontalScrollBar", sv) as ScrollBar);
                var height = scrollbar != null ? scrollbar.ActualHeight : 0d;

                if (def.Height.Value != height)
                {
                    def.Height = new GridLength(height);
                }
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

        private static DependencyProperty SyncCFooterSBProperty = BehaviorUtil.RegisterAttached(
            "SyncCFooterSB", Type, default(ScrollBar), null
        );

        private static ScrollBar SetSyncCFooterSB(DependencyObject target, ScrollBar value)
        {
            target.SetValue(SyncCFooterSBProperty, value);
            return value;
        }

        private static ScrollBar GetSyncCFooterSB(DependencyObject target)
        {
            return (ScrollBar)target.GetValue(SyncCFooterSBProperty);
        }

        private static void OnSetSyncCFooterCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is ScrollViewer viewer)
            {
                BehaviorUtil.SetEventHandler(viewer,
                    x => x.ScrollChanged += ScrollViewerBehavior_SyncCFooter_ScrollChanged,
                    x => x.ScrollChanged -= ScrollViewerBehavior_SyncCFooter_ScrollChanged
                );
            }
        }

        private static void ScrollViewerBehavior_SyncCFooter_ScrollChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer sv && GetSyncCFooter(sv) is ColumnDefinition def)
            {
                var scrollbar = GetSyncCFooterSB(sv) ??
                    SetSyncCFooterSB(sv, sv.Template.FindName("PART_VerticalScrollBar", sv) as ScrollBar);
                var width = scrollbar != null ? scrollbar.ActualWidth : 0d;

                if (def.Width.Value != width)
                {
                    def.Width = new GridLength(width);
                }
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
            if (target is ScrollViewer viewer)
            {
                BehaviorUtil.SetEventHandler(viewer,
                    x => x.ScrollChanged += ScrollViewerBehavior_SyncRScroll_ScrollChanged,
                    x => x.ScrollChanged -= ScrollViewerBehavior_SyncRScroll_ScrollChanged
                );
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
            if (target is ScrollViewer viewer)
            {
                BehaviorUtil.SetEventHandler(viewer,
                    x => x.ScrollChanged += ScrollViewerBehavior_SyncCScroll_ScrollChanged,
                    x => x.ScrollChanged -= ScrollViewerBehavior_SyncCScroll_ScrollChanged
                );
            }
        }

        private static void ScrollViewerBehavior_SyncCScroll_ScrollChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer src && GetSyncCScroll(src) is ScrollViewer tgt && src.HorizontalOffset != tgt.HorizontalOffset)
            {
                tgt.ScrollToHorizontalOffset(src.HorizontalOffset);
            }
        }

    }
}
