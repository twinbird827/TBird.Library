using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TBird.Wpf
{
    public static class ControlUtil
    {
        /// <summary>
        /// ｱｸﾃｨﾌﾞな画面を取得します。
        /// </summary>
        public static Window GetActiveWindow()
        {
            foreach (var win in Application.Current.Windows)
            {
                if (win is Window active && active.IsActive)
                {
                    return active;
                }
            }
            return Application.Current.MainWindow;
        }

        /// <summary>
        /// ﾌｫｰｶｽをｸﾘｱします。
        /// </summary>
        /// <param name="fe">ｸﾘｱ処理を実行したい項目</param>
        public static void ClearFocus(FrameworkElement fe)
        {
            // 一時的に対象の項目へﾌｫｰｶｽを移す
            var focusable = fe.Focusable;
            fe.Focusable = true;
            fe.Focus();
            fe.Focusable = focusable;

            // ﾌｫｰｶｽｸﾘｱ
            Keyboard.ClearFocus();
        }

        /// <summary>
        /// <see cref="FormattedText"/>を作成します。
        /// </summary>
        /// <param name="visual"><see cref="FormattedText"/>を配置する親ｺﾝﾄﾛｰﾙ</param>
        /// <param name="typeface">ﾀｲﾌﾟﾌｪｲｽ</param>
        /// <param name="size">ﾌｫﾝﾄｻｲｽﾞ</param>
        /// <param name="brush">色</param>
        /// <param name="text">ﾃｷｽﾄ</param>
        /// <returns></returns>
        public static FormattedText GetFormattedText(Visual visual, Typeface typeface, double size, Brush brush, string text)
        {
            return new FormattedText(text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                size,
                brush,
                VisualTreeHelper.GetDpi(visual).PixelsPerDip
            );
        }

        /// <summary>
        /// <see cref="FormattedText"/>を作成します。
        /// </summary>
        /// <param name="visual"><see cref="FormattedText"/>を配置する親ｺﾝﾄﾛｰﾙ</param>
        /// <param name="family">関連するﾌｫﾝﾄのﾌｧﾐﾘ</param>
        /// <param name="style">ﾌｫﾝﾄﾌｪｲｽのｽﾀｲﾙ</param>
        /// <param name="weight">書体の密度</param>
        /// <param name="stretch">ﾌｫﾝﾄの伸縮する程度</param>
        /// <param name="size">ﾌｫﾝﾄｻｲｽﾞ</param>
        /// <param name="brush">色</param>
        /// <param name="text">ﾃｷｽﾄ</param>
        /// <returns></returns>
        public static FormattedText GetFormattedText(Visual visual, FontFamily family, FontStyle style, FontWeight weight, FontStretch stretch, double size, Brush brush, string text)
        {
            return GetFormattedText(visual,
                new Typeface(family, style, weight, stretch),
                size,
                brush,
                text
            );
        }

        public static FormattedText GetFormattedText(Control control, string text)
        {
            return GetFormattedText(
                control,
                control.FontFamily,
                control.FontStyle,
                control.FontWeight,
                control.FontStretch,
                control.FontSize,
                control.Foreground,
                text
            );
        }

        public static FormattedText GetFormattedText(FrameworkElement fe)
        {
            if (fe is TextBlock block)
            {
                return GetFormattedText(
                    block,
                    block.FontFamily,
                    block.FontStyle,
                    block.FontWeight,
                    block.FontStretch,
                    block.FontSize,
                    block.Foreground,
                    block.Text
                );
            }
            else if (fe is ContentControl cc)
            {
                return cc.Content is string content
                    ? GetFormattedText(cc, content)
                    : GetFormattedText(cc.Content as FrameworkElement);
            }
            //else if (fe is Panel panel)
            //{
            //    return GetFormattedText(
            //           panel.Children.OfType<TextBlock>().FirstOrDefault()
            //        ?? panel.Children.OfType<ContentControl>().FirstOrDefault()
            //        ?? panel.Children.OfType<FrameworkElement>().FirstOrDefault()
            //    );
            //}
            else
            {
                return GetFormattedText(new TextBlock());
            }
        }

    }
}
