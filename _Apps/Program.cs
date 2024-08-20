using TBird.Core;
using DojinRename;

/* **************************************************
 *  ﾒｲﾝﾒｿｯﾄﾞ
 ************************************************** */

var task = MyCode.Execute(args);

if (!task.TryCatch().Result)
{
	// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
	Console.ReadLine();
}

AppSetting.Instance.Save();

return 0;