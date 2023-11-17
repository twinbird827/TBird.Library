using TBird.Core;
using PDF2JPG;

/* **************************************************
 *  ﾒｲﾝﾒｿｯﾄﾞ
 ************************************************** */

// 引数で/sourcefilepathを与えている
// ｵﾌﾟｼｮﾝを選択
Console.WriteLine("起動ｵﾌﾟｼｮﾝを選択してください。");
Console.WriteLine($"0: 元となるPDFﾌｧｲﾙを残す。");
Console.WriteLine($"1: 処理が完了したらPDFﾌｧｲﾙを削除する。");
Console.WriteLine($"ﾃﾞﾌｫﾙﾄ: {AppSetting.Instance.Option}");

// ｵﾌﾟｼｮﾝを選択
var option = MyCode.GetOption(Console.ReadLine());

var task = MyCode.Execute(option, args);

if (!task.TryCatch().Result)
{
	// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
	Console.ReadLine();
}

AppSetting.Instance.Save();

return 0;