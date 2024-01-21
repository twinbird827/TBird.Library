using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba
{
	public class ModelRow
	{
		[LoadColumn(1)]
		public double ﾚｰｽID { get; set; }
		[LoadColumn(2)]
		public double 開催日数 { get; set; }
		[LoadColumn(3)]
		[ColumnName("Label")]
		public double 着順 { get; set; }
		[LoadColumn(4)]
		public double 枠番 { get; set; }
		[LoadColumn(5)]
		public double 馬番 { get; set; }
		[LoadColumn(6)]
		public double 馬ID { get; set; }
		[LoadColumn(7)]
		public double 馬性 { get; set; }
		[LoadColumn(8)]
		public double 馬齢 { get; set; }
		[LoadColumn(9)]
		public double 馬齢差 { get; set; }
		[LoadColumn(10)]
		public double 斤量 { get; set; }
		[LoadColumn(11)]
		public double 斤量差 { get; set; }
		[LoadColumn(12)]
		public double 体重 { get; set; }
		[LoadColumn(13)]
		public double 増減 { get; set; }
		[LoadColumn(14)]
		public double 体重差 { get; set; }
		[LoadColumn(15)]
		public double 増減割 { get; set; }
		[LoadColumn(16)]
		public double 斤量割 { get; set; }
		[LoadColumn(17)]
		public double 調教場所 { get; set; }
		[LoadColumn(18)]
		public double 一言 { get; set; }
		[LoadColumn(19)]
		public double 追切 { get; set; }
		[LoadColumn(20)]
		public double 全勝_馬ID_馬ID { get; set; }
		[LoadColumn(21)]
		public double 全連_馬ID_馬ID { get; set; }
		[LoadColumn(22)]
		public double 全複_馬ID_馬ID { get; set; }
		[LoadColumn(23)]
		public double 直勝_馬ID_馬ID { get; set; }
		[LoadColumn(24)]
		public double 直連_馬ID_馬ID { get; set; }
		[LoadColumn(25)]
		public double 直複_馬ID_馬ID { get; set; }
		[LoadColumn(26)]
		public double 全勝_馬ID_開催場所 { get; set; }
		[LoadColumn(27)]
		public double 全連_馬ID_開催場所 { get; set; }
		[LoadColumn(28)]
		public double 全複_馬ID_開催場所 { get; set; }
		[LoadColumn(29)]
		public double 直勝_馬ID_開催場所 { get; set; }
		[LoadColumn(30)]
		public double 直連_馬ID_開催場所 { get; set; }
		[LoadColumn(31)]
		public double 直複_馬ID_開催場所 { get; set; }
		[LoadColumn(32)]
		public double 全勝_馬ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(33)]
		public double 全連_馬ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(34)]
		public double 全複_馬ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(35)]
		public double 直勝_馬ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(36)]
		public double 直連_馬ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(37)]
		public double 直複_馬ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(38)]
		public double 全勝_馬ID_回り { get; set; }
		[LoadColumn(39)]
		public double 全連_馬ID_回り { get; set; }
		[LoadColumn(40)]
		public double 全複_馬ID_回り { get; set; }
		[LoadColumn(41)]
		public double 直勝_馬ID_回り { get; set; }
		[LoadColumn(42)]
		public double 直連_馬ID_回り { get; set; }
		[LoadColumn(43)]
		public double 直複_馬ID_回り { get; set; }
		[LoadColumn(44)]
		public double 全勝_馬ID_天候 { get; set; }
		[LoadColumn(45)]
		public double 全連_馬ID_天候 { get; set; }
		[LoadColumn(46)]
		public double 全複_馬ID_天候 { get; set; }
		[LoadColumn(47)]
		public double 直勝_馬ID_天候 { get; set; }
		[LoadColumn(48)]
		public double 直連_馬ID_天候 { get; set; }
		[LoadColumn(49)]
		public double 直複_馬ID_天候 { get; set; }
		[LoadColumn(50)]
		public double 全勝_馬ID_馬場 { get; set; }
		[LoadColumn(51)]
		public double 全連_馬ID_馬場 { get; set; }
		[LoadColumn(52)]
		public double 全複_馬ID_馬場 { get; set; }
		[LoadColumn(53)]
		public double 直勝_馬ID_馬場 { get; set; }
		[LoadColumn(54)]
		public double 直連_馬ID_馬場 { get; set; }
		[LoadColumn(55)]
		public double 直複_馬ID_馬場 { get; set; }
		[LoadColumn(56)]
		public double 全勝_馬ID_馬場状態 { get; set; }
		[LoadColumn(57)]
		public double 全連_馬ID_馬場状態 { get; set; }
		[LoadColumn(58)]
		public double 全複_馬ID_馬場状態 { get; set; }
		[LoadColumn(59)]
		public double 直勝_馬ID_馬場状態 { get; set; }
		[LoadColumn(60)]
		public double 直連_馬ID_馬場状態 { get; set; }
		[LoadColumn(61)]
		public double 直複_馬ID_馬場状態 { get; set; }
		[LoadColumn(62)]
		public double 全勝_騎手ID_騎手ID { get; set; }
		[LoadColumn(63)]
		public double 全連_騎手ID_騎手ID { get; set; }
		[LoadColumn(64)]
		public double 全複_騎手ID_騎手ID { get; set; }
		[LoadColumn(65)]
		public double 直勝_騎手ID_騎手ID { get; set; }
		[LoadColumn(66)]
		public double 直連_騎手ID_騎手ID { get; set; }
		[LoadColumn(67)]
		public double 直複_騎手ID_騎手ID { get; set; }
		[LoadColumn(68)]
		public double 全勝_騎手ID_開催場所 { get; set; }
		[LoadColumn(69)]
		public double 全連_騎手ID_開催場所 { get; set; }
		[LoadColumn(70)]
		public double 全複_騎手ID_開催場所 { get; set; }
		[LoadColumn(71)]
		public double 直勝_騎手ID_開催場所 { get; set; }
		[LoadColumn(72)]
		public double 直連_騎手ID_開催場所 { get; set; }
		[LoadColumn(73)]
		public double 直複_騎手ID_開催場所 { get; set; }
		[LoadColumn(74)]
		public double 全勝_騎手ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(75)]
		public double 全連_騎手ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(76)]
		public double 全複_騎手ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(77)]
		public double 直勝_騎手ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(78)]
		public double 直連_騎手ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(79)]
		public double 直複_騎手ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(80)]
		public double 全勝_騎手ID_回り { get; set; }
		[LoadColumn(81)]
		public double 全連_騎手ID_回り { get; set; }
		[LoadColumn(82)]
		public double 全複_騎手ID_回り { get; set; }
		[LoadColumn(83)]
		public double 直勝_騎手ID_回り { get; set; }
		[LoadColumn(84)]
		public double 直連_騎手ID_回り { get; set; }
		[LoadColumn(85)]
		public double 直複_騎手ID_回り { get; set; }
		[LoadColumn(86)]
		public double 全勝_騎手ID_天候 { get; set; }
		[LoadColumn(87)]
		public double 全連_騎手ID_天候 { get; set; }
		[LoadColumn(88)]
		public double 全複_騎手ID_天候 { get; set; }
		[LoadColumn(89)]
		public double 直勝_騎手ID_天候 { get; set; }
		[LoadColumn(90)]
		public double 直連_騎手ID_天候 { get; set; }
		[LoadColumn(91)]
		public double 直複_騎手ID_天候 { get; set; }
		[LoadColumn(92)]
		public double 全勝_騎手ID_馬場 { get; set; }
		[LoadColumn(93)]
		public double 全連_騎手ID_馬場 { get; set; }
		[LoadColumn(94)]
		public double 全複_騎手ID_馬場 { get; set; }
		[LoadColumn(95)]
		public double 直勝_騎手ID_馬場 { get; set; }
		[LoadColumn(96)]
		public double 直連_騎手ID_馬場 { get; set; }
		[LoadColumn(97)]
		public double 直複_騎手ID_馬場 { get; set; }
		[LoadColumn(98)]
		public double 全勝_騎手ID_馬場状態 { get; set; }
		[LoadColumn(99)]
		public double 全連_騎手ID_馬場状態 { get; set; }
		[LoadColumn(100)]
		public double 全複_騎手ID_馬場状態 { get; set; }
		[LoadColumn(101)]
		public double 直勝_騎手ID_馬場状態 { get; set; }
		[LoadColumn(102)]
		public double 直連_騎手ID_馬場状態 { get; set; }
		[LoadColumn(103)]
		public double 直複_騎手ID_馬場状態 { get; set; }
		[LoadColumn(104)]
		public double 全勝_調教師ID_調教師ID { get; set; }
		[LoadColumn(105)]
		public double 全連_調教師ID_調教師ID { get; set; }
		[LoadColumn(106)]
		public double 全複_調教師ID_調教師ID { get; set; }
		[LoadColumn(107)]
		public double 直勝_調教師ID_調教師ID { get; set; }
		[LoadColumn(108)]
		public double 直連_調教師ID_調教師ID { get; set; }
		[LoadColumn(109)]
		public double 直複_調教師ID_調教師ID { get; set; }
		[LoadColumn(110)]
		public double 全勝_調教師ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(111)]
		public double 全連_調教師ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(112)]
		public double 全複_調教師ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(113)]
		public double 直勝_調教師ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(114)]
		public double 直連_調教師ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(115)]
		public double 直複_調教師ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(116)]
		public double 全勝_馬主ID_馬主ID { get; set; }
		[LoadColumn(117)]
		public double 全連_馬主ID_馬主ID { get; set; }
		[LoadColumn(118)]
		public double 全複_馬主ID_馬主ID { get; set; }
		[LoadColumn(119)]
		public double 直勝_馬主ID_馬主ID { get; set; }
		[LoadColumn(120)]
		public double 直連_馬主ID_馬主ID { get; set; }
		[LoadColumn(121)]
		public double 直複_馬主ID_馬主ID { get; set; }
		[LoadColumn(122)]
		public double 全勝_馬主ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(123)]
		public double 全連_馬主ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(124)]
		public double 全複_馬主ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(125)]
		public double 直勝_馬主ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(126)]
		public double 直連_馬主ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(127)]
		public double 直複_馬主ID_ﾗﾝｸ2 { get; set; }
		[LoadColumn(128)]
		public double 距離 { get; set; }
		[LoadColumn(129)]
		public double 馬_得意距離 { get; set; }
		[LoadColumn(130)]
		public double 馬_距離差 { get; set; }
		[LoadColumn(131)]
		public double 通過平均 { get; set; }
		[LoadColumn(132)]
		public double 上り平均 { get; set; }
		[LoadColumn(133)]
		public double 時間平均 { get; set; }
		[LoadColumn(134)]
		public double TOP時間差 { get; set; }
		[LoadColumn(135)]
		public double 前回出走 { get; set; }
		[LoadColumn(136)]
		public double 着順平均 { get; set; }
		[LoadColumn(137)]
		public double 斤量平均 { get; set; }
		[LoadColumn(138)]
		public double 着差平均 { get; set; }
		[LoadColumn(139)]
		public double 通過平均差 { get; set; }
		[LoadColumn(140)]
		public double 上り平均差 { get; set; }
		[LoadColumn(141)]
		public double 時間平均差 { get; set; }
		[LoadColumn(142)]
		public double 着差平均差 { get; set; }
		[LoadColumn(143)]
		public double 着順平均差 { get; set; }
		[LoadColumn(144)]
		public double 斤量平均差 { get; set; }
		[LoadColumn(145)]
		public double 単勝 { get; set; }
		[LoadColumn(146)]
		public double 人気 { get; set; }

	}

	public class ModelRowPrediction
	{
		[ColumnName("PredictedLabel")]
		// Predicted label from the trainer.
		public bool 着順 { get; set; }
	}
}
