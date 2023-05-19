using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Wpf
{
    public partial class BindableBase : IBindable
    {
        public BindableBase()
        {

        }

        /// <summary>
        /// ﾌﾟﾛﾊﾟﾃｨの変更を通知するためのﾏﾙﾁｷｬｽﾄ ｲﾍﾞﾝﾄ。
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// ﾌﾟﾛﾊﾟﾃｨが既に目的の値と一致しているかどうかを確認します。必要な場合のみ、
        /// ﾌﾟﾛﾊﾟﾃｨを設定し、ﾘｽﾅに通知します。
        /// </summary>
        /// <typeparam name="T">ﾌﾟﾛﾊﾟﾃｨの型。</typeparam>
        /// <param name="storage">get ｱｸｾｽ操作子と set ｱｸｾｽ操作子両方を使用したﾌﾟﾛﾊﾟﾃｨへの参照。</param>
        /// <param name="value">ﾌﾟﾛﾊﾟﾃｨに必要な値。</param>
        /// <param name="isDisposeOld">trueの場合、ﾌﾟﾛﾊﾟﾃｨの値を変更する前に変更前の値がDispose可能ならDisposeする</param>
        /// <param name="propertyName">ﾘｽﾅに通知するために使用するﾌﾟﾛﾊﾟﾃｨの名前。この値は省略可能で、CallerMemberName をｻﾎﾟｰﾄするｺﾝﾊﾟｲﾗから呼び出す場合に自動的に指定できます。</param>
        /// <returns>値が変更された場合は true、既存の値が目的の値に一致した場合はfalse です。</returns>
        protected virtual bool SetProperty<T>(ref T storage, T value, bool isDisposeOld = false, [CallerMemberName] string propertyName = null)
        {
            if (object.Equals(storage, value)) return false;

            if (isDisposeOld)
            {
                storage.TryDispose();
            }

            // ﾌﾟﾛﾊﾟﾃｨ値変更
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual BindableBase SetPropertyAnd<T>(ref T storage, T value, bool isDisposeOld = false, [CallerMemberName] string propertyName = null)
        {
            SetProperty(ref storage, value, isDisposeOld, propertyName);
            return this;
        }

        /// <summary>
        /// ﾌﾟﾛﾊﾟﾃｨ値が変更されたことをﾘｽﾅに通知します。
        /// </summary>
        /// <param name="propertyName">ﾘｽﾅに通知するために使用するﾌﾟﾛﾊﾟﾃｨの名前。この値は省略可能で、<see cref="CallerMemberNameAttribute"/> をサポートするコンパイラから呼び出す場合に自動的に指定できます。</param>
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// ﾌﾟﾛﾊﾟﾃｨ変更時ｲﾍﾞﾝﾄを追加します。
        /// </summary>
        /// <param name="bindable">追加元のｲﾝｽﾀﾝｽ</param>
        /// <param name="handler">追加するｲﾍﾞﾝﾄの中身</param>
        public void AddOnPropertyChanged(IBindable bindable, PropertyChangedEventHandler handler)
        {
            if (handler == null) return;

            PropertyChanged -= handler;
            PropertyChanged += handler;

            bindable.AddDisposed((sender, e) =>
            {
                PropertyChanged -= handler;
            });
        }

        /// <summary>
        /// ﾌﾟﾛﾊﾟﾃｨ変更時ｲﾍﾞﾝﾄを追加します。
        /// </summary>
        /// <param name="bindable">追加元のｲﾝｽﾀﾝｽ</param>
        /// <param name="handler">追加するｲﾍﾞﾝﾄの中身</param>
        /// <param name="name">ｲﾍﾞﾝﾄを実行するﾌﾟﾛﾊﾟﾃｨの名前</param>
        /// <param name="execute">ｲﾍﾞﾝﾄ追加後にｲﾍﾞﾝﾄﾊﾝﾄﾞﾙを実行するかどうか</param>
        public void AddOnPropertyChanged(IBindable bindable, PropertyChangedEventHandler handler, string name, bool execute)
        {
            if (handler == null) return;

            AddOnPropertyChanged(bindable, (sender, e) =>
            {
                if (e.PropertyName != name) return;

                handler.Invoke(sender, e);
            });

            if (execute)
            {
                handler.Invoke(bindable, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// CollectionChangedにｲﾍﾞﾝﾄを追加します。
        /// </summary>
        /// <param name="notify">INotifyCollectionChangedを実装したﾘｽﾄｲﾝｽﾀﾝｽ</param>
        /// <param name="handler">ｲﾍﾞﾝﾄ</param>
        public void AddCollectionChanged(INotifyCollectionChanged notify, NotifyCollectionChangedEventHandler handler)
        {
            notify.CollectionChanged -= handler;
            notify.CollectionChanged += handler;

            AddDisposed((sender, e) =>
            {
                notify.CollectionChanged -= handler;
            });
        }

        /// <summary>
        /// ｲﾝｽﾀﾝｽ破棄時ｲﾍﾞﾝﾄを追加します。
        /// </summary>
        /// <param name="bindable">一緒に追加するｲﾝｽﾀﾝｽ</param>
        /// <param name="handler">破棄ｲﾍﾞﾝﾄ</param>
        public void AddDisposed(EventHandler handler)
        {
            // ｲﾝｽﾀﾝｽ破棄ｲﾍﾞﾝﾄ自体を破棄するﾊﾝﾄﾞﾗを作成する
            EventHandler disposed = null; disposed = (sender, e) =>
            {
                Disposed -= handler;
                Disposed -= disposed;
            };

            Disposed -= handler;
            Disposed += handler;
            Disposed -= disposed;
            Disposed += disposed;
        }
    }
}
