using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace TBird.Wpf.Behaviors
{
    public partial class TextBlockBehavior
    {
        public static DependencyProperty MaxLinesProperty = BehaviorUtil.RegisterAttached(
            "MaxLines", typeof(TextBlockBehavior), 1, OnSetMaxLinesCallback
        );
        public static void SetMaxLines(DependencyObject target, object value)
        {
            target.SetValue(MaxLinesProperty, value);
        }
        public static int GetMaxLines(DependencyObject target)
        {
            return (int)target.GetValue(MaxLinesProperty);
        }

        private static void OnSetMaxLinesCallback(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is TextBlock textblock)
            {
                if (textblock.TextWrapping == TextWrapping.NoWrap)
                {
                    // 改行設定
                    textblock.TextWrapping = TextWrapping.Wrap;
                }
                if (textblock.TextTrimming == TextTrimming.None)
                {
                    // 省略文字設定
                    textblock.TextTrimming = TextTrimming.CharacterEllipsis;
                }

                BehaviorUtil.SetEventHandler(textblock,
                    (block) => block.TargetUpdated += TextBlockBehavior_MaxLines_TargetUpdated,
                    (block) => block.TargetUpdated -= TextBlockBehavior_MaxLines_TargetUpdated
                );

                BehaviorUtil.Loaded(textblock, TextBlockBehavior_MaxLines_Loaded);
            }
        }

        private static void TextBlockBehavior_MaxLines_Loaded(object sender, EventArgs e)
        {
            if (sender is TextBlock textblock)
            {
                var binding = BindingOperations.GetBinding(textblock, TextBlock.TextProperty);

                if (binding == null) return;

                // Textﾌﾟﾛﾊﾟﾃｨ変更時に通知されるようにする
                BindingOperations.SetBinding(textblock, TextBlock.TextProperty, new Binding()
                {
                    Path = binding.Path,
                    NotifyOnTargetUpdated = true
                });
            }
        }

        private static void TextBlockBehavior_MaxLines_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (sender is TextBlock block)
            {
                // 行の高さが設定されている場合はその値、未設定であればFormattedTextの高さを行の高さとする
                var line = double.IsInfinity(block.LineHeight) || double.IsNaN(block.LineHeight)
                    ? ControlUtil.GetFormattedText(block).Height
                    : block.LineHeight;

                // 最大高さ設定
                block.MaxHeight = line * GetMaxLines(block);
                block.Height = block.MaxHeight;
            }
        }
    }
}
