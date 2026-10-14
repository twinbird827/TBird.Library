namespace TBird.Wpf
{
	public class ComboboxItemModel : BindableBase
	{
		public ComboboxItemModel(string value, string display)
		{
			Value = value;
			Display = display;
		}

		public string Value
		{
			get => _Value;
			set => SetProperty(ref _Value, value);
		}
		private string _Value;

		public string Display
		{
			get => _Display;
			set => SetProperty(ref _Display, value);
		}
		private string _Display;

		/// <summary>
		/// ﾊｯｼｭｺｰﾄﾞを取得します。
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return Value != null
				? Value.GetHashCode()
				: base.GetHashCode();
		}

		/// <summary>
		/// ｺﾝﾎﾞﾎﾞｯｸｽ値が等価であるか比較します。
		/// </summary>
		/// <param name="obj">対象ｺﾝﾎﾞﾎﾞｯｸｽ</param>
		/// <returns></returns>
		public override bool Equals(object obj)
		{
			return Value != null && obj is ComboboxItemModel cmb
				? Value.Equals(cmb.Value)
				: base.Equals(obj);
		}

		public string CopyToClipboard()
		{
			return $"{Value}\t{Display}";
		}
	}
}