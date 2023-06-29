using System.Windows;

namespace TBird.Wpf.Behaviors
{
    public partial class FrameworkElementBehavior
    {
        public static DependencyProperty ToolTipProperty = BehaviorUtil.RegisterAttached(
            "ToolTip", typeof(FrameworkElementBehavior), "", OnSetToolTipCallback
        );

        public static void SetToolTip(DependencyObject target, object value)
        {
            target.SetValue(ToolTipProperty, value);
        }

        public static object GetToolTip(DependencyObject target)
        {
            return target.GetValue(ToolTipProperty);
        }

        private static void OnSetToolTipCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is FrameworkElement element)
            {
                BehaviorUtil.Loaded(element, FrameworkElementBehavior_ToolTip);
            }
        }

        /// <summary>
        /// 項目読込時にﾂｰﾙﾁｯﾌﾟを設定します。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FrameworkElementBehavior_ToolTip(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var target = GetToolTip(element);

                // 設定がない場合
                if (target == null) return;

                if (target is string targetString && !string.IsNullOrWhiteSpace(targetString))
                {
                    element.ToolTip = targetString;
                }
                else if (target is FrameworkElement targetElement)
                {
                    var formattedText = ControlUtil.GetFormattedText(targetElement);
                    if (!string.IsNullOrWhiteSpace(formattedText.Text))
                    {
                        element.ToolTip = target;
                    }
                }
                else
                {
                    element.ToolTip = target;
                }
            }
        }
    }
}