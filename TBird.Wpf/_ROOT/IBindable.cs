using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf
{
    public interface IBindable : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// プロパティ値が変更されたことをリスナーに通知します。
        /// </summary>
        /// <param name="propertyName">リスナーに通知するために使用するプロパティの名前。
        /// この値は省略可能で、
        /// <see cref="CallerMemberNameAttribute"/> をサポートするコンパイラから呼び出す場合に自動的に指定できます。</param>
        void OnPropertyChanged([CallerMemberName] string propertyName = null);

        /// <summary>
        /// ﾌﾟﾛﾊﾟﾃｨ変更時ｲﾍﾞﾝﾄを追加します。
        /// </summary>
        /// <param name="bindable">追加元のｲﾝｽﾀﾝｽ</param>
        /// <param name="handler">追加するｲﾍﾞﾝﾄの中身</param>
        void AddOnPropertyChanged(IBindable bindable, PropertyChangedEventHandler handler);

        /// <summary>
        /// ﾌﾟﾛﾊﾟﾃｨ変更時ｲﾍﾞﾝﾄを追加します。
        /// </summary>
        /// <param name="bindable">追加元のｲﾝｽﾀﾝｽ</param>
        /// <param name="handler">追加するｲﾍﾞﾝﾄの中身</param>
        /// <param name="name">ｲﾍﾞﾝﾄを実行するﾌﾟﾛﾊﾟﾃｨの名前</param>
        /// <param name="execute">初回ｲﾍﾞﾝﾄを発生させるかどうか</param>
        void AddOnPropertyChanged(IBindable bindable, PropertyChangedEventHandler handler, string name, bool execute);

        /// <summary>
        /// ｲﾝｽﾀﾝｽ破棄時のｲﾍﾞﾝﾄ
        /// </summary>
        event EventHandler Disposed;

        /// <summary>
        /// ｲﾝｽﾀﾝｽ破棄時ｲﾍﾞﾝﾄを追加します。
        /// </summary>
        /// <param name="bindable">一緒に追加するｲﾝｽﾀﾝｽ</param>
        /// <param name="handler">破棄ｲﾍﾞﾝﾄ</param>
        void AddDisposed(IBindable bindable, EventHandler handler);

    }
}
