using TBird.Core;
using ZIPConverter;

/* **************************************************
 *  ﾒｲﾝﾒｿｯﾄﾞ
 ************************************************** */

// 引数で/sourcefilepathを与えている
// ｵﾌﾟｼｮﾝを選択
Console.WriteLine("起動ｵﾌﾟｼｮﾝを選択してください。");
Console.WriteLine("0: 全て実行する。");
Console.WriteLine("1: 画像縮小をｽｷｯﾌﾟする。");
Console.WriteLine($"ﾃﾞﾌｫﾙﾄ: {AppSetting.Instance.Option}");

// ｵﾌﾟｼｮﾝを選択
var option = Process.GetOption(Console.ReadLine());

var task = Process.Execute(option, args);

if (!task.TryCatch().Result)
{
	// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
	Console.ReadLine();
}

AppSetting.Instance.Save();

return 0;