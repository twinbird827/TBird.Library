using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TBird.Core;

namespace TBird.Wpf.Behaviors
{
    public partial class ListBoxBehavior
    {
        public static readonly DependencyProperty MustSelectedProperty = BehaviorUtil.RegisterAttached(
            "MustSelected", typeof(ListBoxBehavior), false, OnSetMustSelectedCallback
        );
        public static bool GetMustSelected(DependencyObject obj)
        {
            return (bool)obj.GetValue(MustSelectedProperty);
        }
        public static void SetMustSelected(DependencyObject obj, bool value)
        {
            obj.SetValue(MustSelectedProperty, value);
        }

        private static void OnSetMustSelectedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ListBox listbox)
            {
                BehaviorUtil.SetEventHandler(listbox,
                    fe => fe.PreviewKeyDown += ListBoxBehavior_MustSelected_PreviewKeyDown,
                    fe => fe.PreviewKeyDown -= ListBoxBehavior_MustSelected_PreviewKeyDown
                );
            }
        }

        private static void ListBoxBehavior_MustSelected_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is ListBox listbox && GetMustSelected(listbox))
            {
                if (e.Key == Key.Space && EnumUtil.IsIncluded(Keyboard.Modifiers, ModifierKeys.Control))
                {
                    // 選択を解除するｷｰ入力をｷｬﾝｾﾙする。
                    e.Handled = true;
                }
            }
        }
    }
}
