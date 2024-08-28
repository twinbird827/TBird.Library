namespace Browser.Models
{
    public class Result
    {
        public Result(string file) : this(new FileInfo(file))
        {

        }

        public Result(FileInfo info)
        {
            Date = new DateTime[] { info.LastWriteTime, info.CreationTime }.Max();
            Title = System.IO.Path.GetFileNameWithoutExtension(info.FullName);
        }

        public DateTime Date { get; set; }

        public string Title { get; set; }
    }
}