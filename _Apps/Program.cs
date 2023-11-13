using TBird.Core;
using EBook2PDF;

/* **************************************************
 *  ﾒｲﾝﾒｿｯﾄﾞ
 ************************************************** */

var task = Process.Execute(args);

if (!task.TryCatch().Result)
{
	// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
	Console.ReadLine();
}

AppSetting.Instance.Save();

return 0;