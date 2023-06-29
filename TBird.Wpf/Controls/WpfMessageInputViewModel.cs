using System;
using System.Windows.Input;

namespace TBird.Wpf.Controls
{
    public class WpfMessageInputViewModel : DialogViewModel
    {
        public WpfMessageInputViewModel(string title, string message, string header, bool isrequired)
            : this(title, message, header, isrequired, () => true)
        {
            // 別のｺﾝｽﾄﾗｸﾀで処理する
        }

        public WpfMessageInputViewModel(string title, string message, string header, bool isrequired, Func<bool> iscanok)
        {
            Title = title;
            Message = message;
            Header = header;
            IsRequired = isrequired;
            _iscanok = iscanok;
        }

        /// <summary>
        /// ﾀｲﾄﾙ
        /// </summary>
        public string Title
        {
            get => _Title;
            set => SetProperty(ref _Title, value);
        }
        private string _Title;

        /// <summary>
        /// ﾒｯｾｰｼﾞ
        /// </summary>
        public string Message
        {
            get => _Message;
            set => SetProperty(ref _Message, value);
        }
        private string _Message;

        /// <summary>
        /// 入力文字ﾍｯﾀﾞ
        /// </summary>
        public string Header
        {
            get => _Header;
            set => SetProperty(ref _Header, value);
        }
        private string _Header;

        /// <summary>
        /// 入力文字
        /// </summary>
        public string Value
        {
            get => _Value;
            set => SetProperty(ref _Value, value);
        }
        private string _Value;

        /// <summary>
        /// 入力文字が必須項目かどうか
        /// </summary>
        public bool IsRequired
        {
            get => _IsRequired;
            set => SetProperty(ref _IsRequired, IsRequired);
        }
        private bool _IsRequired;

        protected override ICommand GetOKCommand()
        {
            return RelayCommand.Create(_ =>
            {
                if (_iscanok())
                {
                    DialogResult = true;
                }
            }, _ =>
            {
                return !IsRequired || !string.IsNullOrEmpty(Value);
            }).AddCanExecuteChanged(
                this, nameof(IsRequired), nameof(Value)
            );
        }

        private Func<bool> _iscanok;

    }
}