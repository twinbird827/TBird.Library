namespace TBird.Wpf
{
    public interface ICopyToClipboard
    {
        /// <summary>
        /// ｸﾘｯﾌﾟﾎﾞｰﾄﾞにｺﾋﾟｰする文字列を取得します。
        /// </summary>
        /// <returns></returns>
        string CopyToClipboard();
    }
}