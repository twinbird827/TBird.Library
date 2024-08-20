using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TBird.Core
{
	public class FileAppendWriter : StreamWriter
	{
		private const int bufferSize = 1024 * 1024 * 10;
		private Stream? _stream;

		public FileAppendWriter(string path) : this(path, Encoding.UTF8)
		{

		}

		public FileAppendWriter(string path, Encoding encoding) : this(GetStream(path), encoding)
		{

		}

		private FileAppendWriter(Stream stream, Encoding encoding) : base(stream, encoding, bufferSize)
		{
			_stream = stream;

			AutoFlush = false;
		}

		private static Stream GetStream(string path)
		{
			var mode = File.Exists(path) ? FileMode.Append : FileMode.CreateNew;
			if (mode == FileMode.CreateNew) FileUtil.BeforeCreate(path);
			return new FileStream(path, mode, FileAccess.Write, FileShare.None, bufferSize);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (_stream != null)
			{
				_stream.Dispose();
				_stream = null;
			}
		}
	}
}