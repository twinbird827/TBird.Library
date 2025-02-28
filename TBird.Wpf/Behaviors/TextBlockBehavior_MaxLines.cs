using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace TBird.Wpf.Behaviors
{
    public partial class TextBlockBehavior
    {
        public static DependencyProperty MaxLinesProperty = BehaviorUtil.RegisterAttached(
            "MaxLines", typeof(TextBlockBehavior), 1, null
        );

        public static void SetMaxLines(DependencyObject target, object value)
        {
            target.SetValue(MaxLinesProperty, value);
        }

        public static int GetMaxLines(DependencyObject target)
        {
            return (int)target.GetValue(MaxLinesProperty);
        }

        public static DependencyProperty MaxTextProperty = BehaviorUtil.RegisterAttached(
            "MaxText", typeof(TextBlockBehavior), default(string), OnSetMaxTextCallback
        );

        public static void SetMaxText(DependencyObject target, object value)
        {
            target.SetValue(MaxTextProperty, value);
        }

        public static string GetMaxText(DependencyObject target)
        {
            return (string)target.GetValue(MaxTextProperty);
        }

        private static void OnSetMaxTextCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is TextBlock block)
            {
                block.TextWrapping = TextWrapping.Wrap;
                block.TextTrimming = TextTrimming.CharacterEllipsis;
                block.Text = (string)e.NewValue;

                // 行の高さが設定されている場合はその値、未設定であればFormattedTextの高さを行の高さとする
                var line = double.IsInfinity(block.LineHeight) || double.IsNaN(block.LineHeight)
                    ? ControlUtil.GetFormattedText(block).Height
                    : block.LineHeight;

                // 最大高さ設定
                block.MaxHeight = line * GetMaxLines(block);
                block.Height = block.MaxHeight;

                var binding = BindingOperations.GetBinding(block, TextBlock.TextProperty);

                if (binding == null) return;

                // Textﾌﾟﾛﾊﾟﾃｨ変更時に通知されるようにする
                BindingOperations.SetBinding(block, TextBlock.TextProperty, new Binding()
                {
                    Path = binding.Path,
                    NotifyOnTargetUpdated = true
                });
            }
        }
    }
}