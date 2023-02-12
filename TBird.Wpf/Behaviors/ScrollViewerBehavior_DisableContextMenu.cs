using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TBird.Core;

namespace TBird.Wpf.Behaviors
{
    public partial class ScrollViewerBehavior
    {
        public static DependencyProperty DisableContextMenuProperty = BehaviorUtil.RegisterAttached(
            "DisableContextMenu", typeof(ScrollViewerBehavior), false, OnSetDisableContextMenuCallback
        );
        public static void SetDisableContextMenu(DependencyObject target, object value)
        {
            target.SetValue(DisableContextMenuProperty, value);
        }
        public static bool GetDisableContextMenu(DependencyObject target)
        {
            return (bool)target.GetValue(DisableContextMenuProperty);
        }

        private static void OnSetDisableContextMenuCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is ScrollViewer viewer)
            {
                BehaviorUtil.Loaded(viewer, ScrollViewerBehavior_DisableContextMenu_Loaded);
            }
        }

        private static void ScrollViewerBehavior_DisableContextMenu_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer viewer && GetDisableContextMenu(viewer))
            {
                var bars = new[] { "PART_VerticalScrollBar", "PART_HorizontalScrollBar" }
                    .Select(name => viewer.Template.FindName(name, viewer) as ScrollBar)
                    .Where(bar => bar != null);

                bars.ForEach(bar =>
                {
                    BehaviorUtil.SetEventHandler(bar,
                        x => x.ContextMenuOpening += ScrollViewerBehavior_DisableContextMenu_ContextMenuOpening,
                        x => x.ContextMenuOpening -= ScrollViewerBehavior_DisableContextMenu_ContextMenuOpening
                    );
                });
            }
        }

        private static void ScrollViewerBehavior_DisableContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // ｲﾍﾞﾝﾄを処理済みにしてｺﾝﾃｷｽﾄﾒﾆｭｰが開かないようにする
            e.Handled = true;
        }
    }
}
