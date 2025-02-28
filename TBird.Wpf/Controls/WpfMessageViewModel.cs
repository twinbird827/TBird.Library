using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TBird.Core;

namespace TBird.Wpf.Controls
{
    public class WpfMessageViewModel : DialogViewModel
    {
        public WpfMessageViewModel(WpfMessageType messageType, string message)
        {
            Title = messageType.GetLabel();
            Message = message;
            MessageType = messageType;
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
        /// ｱｲｺﾝの種類
        /// </summary>
        public WpfMessageType MessageType
        {
            get => _MessageType;
            set => SetMessageType(value);
        }
        private WpfMessageType _MessageType;

        /// <summary>
        /// ｱｲｺﾝの種類を変更します。
        /// </summary>
        private void SetMessageType(WpfMessageType value)
        {
            SetProperty(ref _MessageType, value, false, nameof(MessageType));
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(IsOkOnly));
        }

        /// <summary>
        /// ｱｲｺﾝ
        /// </summary>
        public ImageSource Icon => GetIcon();

        /// <summary>
        /// ｱｲｺﾝを取得します。
        /// </summary>
        /// <returns></returns>
        private ImageSource GetIcon()
        {
            switch (MessageType)
            {
                case WpfMessageType.Information:
                    return ToImageSource(SystemIcons.Information);
                case WpfMessageType.Error:
                    return ToImageSource(SystemIcons.Error);
                case WpfMessageType.Confirm:
                    return ToImageSource(SystemIcons.Question);
                default:
                    return ToImageSource(SystemIcons.Information);
            }
        }

        /// <summary>
        /// IconをImageSourceに変換します。
        /// </summary>
        /// <param name="icon">Iconｲﾒｰｼﾞ</param>
        /// <returns></returns>
        private ImageSource ToImageSource(Icon icon)
        {
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions()
            ).Frozen();
        }

        /// <summary>
        /// OKﾎﾞﾀﾝのみにするかどうか
        /// </summary>
        public bool IsOkOnly => MessageType != WpfMessageType.Confirm;
    }
}