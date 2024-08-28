using Browser.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NuGet.Packaging;

namespace Browser.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            Results.Clear();
            Results.AddRange(System.IO.Directory.GetFiles(@"C:\Work").Select(x => new Result(x)));
        }

        public void OnPost()
        {
            IsPost = true;
        }

        public bool IsPost { get; set; } = false;

        public IList<Result> Results { get; set; } = new List<Result>();
    }
}