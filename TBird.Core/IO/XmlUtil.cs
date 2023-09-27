using System.IO;
using System.Xml.Linq;

namespace TBird.Core
{
	public static class XmlUtil
	{
		/// <summary>
		/// 文字列をXml形式に変換します。
		/// </summary>
		/// <param name="value">文字列</param>
		public static XElement ToXml(string value)
		{
			using (var sr = new StringReader(value))
			{
				return XDocument.Load(sr).Root;
			}
		}

		public static XElement Load(string path)
		{
			return XDocument.Load(Directories.GetAbsolutePath(path)).Root;
		}
	}
}