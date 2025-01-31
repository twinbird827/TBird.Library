using System.ComponentModel.DataAnnotations;

namespace Browser.Models
{
    public class Result
    {
        public Result(string file) : this(new FileInfo(file))
        {

        }

        public Result(FileInfo info)
        {
            Date = new DateTime[] { info.LastWriteTime }.Max();
            Title = System.IO.Path.GetFileNameWithoutExtension(info.FullName);
        }

        [DisplayFormat(DataFormatString = "{0:yy/MM/dd HH:mm:ss}")]
        public DateTime Date { get; set; }

        public string Title { get; set; }
    }
}