using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        public Visibility RowHeaderVisibility
        {
            get => (Visibility)GetValue(RowHeaderVisibilityProperty);
            set => SetValue(RowHeaderVisibilityProperty, value);
        }

        public static readonly DependencyProperty RowHeaderVisibilityProperty =
            BehaviorUtil.Register(nameof(RowHeaderVisibility), Type, Visibility.Collapsed, null);

        /// <summary>
        /// 行ﾍｯﾀﾞのﾃﾝﾌﾟﾚｰﾄ
        /// </summary>
        public DataTemplate RowHeaderTemplate
        {
            get => (DataTemplate)GetValue(RowHeaderTemplateProperty);
            set => SetValue(RowHeaderTemplateProperty, value);
        }

        public static readonly DependencyProperty RowHeaderTemplateProperty =
            BehaviorUtil.Register(nameof(RowHeaderTemplate), Type, new DataTemplate(), RowHeaderTemplatePropertyChanged);

        private static void RowHeaderTemplatePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is HeaderItemsControl c && e.NewValue is DataTemplate template)
            {
                c.RowHeaderVisibility = template != null ? Visibility.Visible : Visibility.Collapsed;
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
            BehaviorUtil.Register(nameof(RowItemTemplate), Type, new DataTemplate(), null);

        /// <summary>
        /// 列ﾍｯﾀﾞのﾃﾝﾌﾟﾚｰﾄ
        /// </summary>
        public DataTemplate ColumnHeaderTemplate
        {
            get => (DataTemplate)GetValue(ColumnHeaderTemplateProperty);
            set => SetValue(ColumnHeaderTemplateProperty, value);
        }

        public static readonly DependencyProperty ColumnHeaderTemplateProperty =
            BehaviorUtil.Register(nameof(ColumnHeaderTemplate), Type, new DataTemplate(), null);

        /// <summary>
        /// 明細のﾃﾝﾌﾟﾚｰﾄ
        /// </summary>
        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateProperty =
            BehaviorUtil.Register(nameof(ItemTemplate), Type, new DataTemplate(), null);

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
        /// 明細を仮想化するかどうか
        /// </summary>
        public bool IsVirtualizing
        {
            get => (bool)GetValue(IsVirtualizingProperty);
            set => SetValue(IsVirtualizingProperty, value);
        }

        public static readonly DependencyProperty IsVirtualizingProperty =
            BehaviorUtil.Register(nameof(IsVirtualizing), Type, false, null);

        private static void ItemsSourcePropertyChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is HeaderItemsControl c && e.NewValue is INotifyCollectionChanged notify)
            {
                BehaviorUtil.AddCollectionChanged(c, notify, (dummy, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Reset)
                    {
                        if (args.Action != NotifyCollectionChangedAction.Reset) return;

                        var arr = new[] { "RScrollViewer", "CScrollViewer", "IScrollViewer", "IItemsControl" }
                            .Select(x => BehaviorUtil.GetScrollViewer(c.Template.FindName(x, c) as DependencyObject))
                            .Where(x => x != null);

                        arr.ForEach(x =>
                        {
                            x.ScrollToTop();
                            x.ScrollToLeftEnd();
                        });
                    }
                });
            }
        }

    }
}