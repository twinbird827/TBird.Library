using Browser.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TBird.Core;

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
            Results.AddRange(AppSetting.Instance.TargetDirs
                .SelectMany(path => DirectoryUtil.GetFiles(path, "*.csv"))
                .Select(file => new Result(file))
            );
        }

        public IList<Result> Results { get; set; } = new List<Result>();
    }
}