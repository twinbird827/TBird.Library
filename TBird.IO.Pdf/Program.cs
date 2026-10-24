using System;
using TBird.Core;
using TBird.IO.Pdf;

/* **************************************************
 *  ﾒｲﾝﾒｿｯﾄﾞ
 ************************************************** */

try
{
	PdfUtil.Execute(args);

	return 0;
}
catch (Exception ex)
{
	// 未処理例外終了(0xE0434352)だと WER のﾀﾞﾝﾌﾟ収集で親のﾌﾟﾛｾｽ終了待ちがｽﾄｰﾙするため、
	// 失敗理由を log/yyyy-MM-dd.log に残したうえで exit 1 に確定させる。
	MessageService.Exception(ex);

	return 1;
}