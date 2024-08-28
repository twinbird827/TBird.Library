using Browser.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.VisualBasic.FileIO;
using NuGet.Packaging;
using System.IO;
using System.Text;

namespace Browser.Pages.ResultDetails
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet(string title, int place, int race)
        {
            return OnPost(title, place, race);
        }

        public IActionResult OnPost(string title, int place, int race)
        {
            Title = title;
            Place = place;
            Race = race;

            var info = new FileInfo($@"C:\Work\{title}.CSV");

            using (var tfp = new TextFieldParser(info.FullName, Encoding.GetEncoding("Shift_JIS")))
            {
                //値がカンマで区切られているとする
                tfp.TextFieldType = FieldType.Delimited;
                tfp.Delimiters = new string[] { "," };
                tfp.TrimWhiteSpace = false;

                // 全行取得
                var lines = GetReadLines(tfp)
                    .Skip(1)
                    .OfType<string[]>()
                    .Select(line => new ResultDetail(line))
                    .Where(x => x.IsOK())
                    .ToArray();

                Places.Clear();
                Places.AddRange(lines.Select(x => x.Place).Distinct().OrderBy(x => x));

                Races.Clear();
                Races.AddRange(Enumerable.Range(Race < 3 ? 1 : 9 < Race ? 7 : Race - 2, 5));

                Results.Clear();
                Results.AddRange(lines.Where(x => x.Race == Race && x.Place == Places[Place]));

                foreach (var x in Results.Take(1))
                {
                    Netkeiba = x.Netkeiba;
                    Name = x.RaceName;
                    Class1 = x.Class1;
                }
                return Page();
            }

            IEnumerable<string[]?> GetReadLines(TextFieldParser tfp)
            {
                while (!tfp.EndOfData)
                {
                    yield return tfp.ReadFields();
                }
            }
        }

        public string Title { get; set; } = string.Empty;

        public string Netkeiba { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Class1 { get; set; } = string.Empty;

        public int Place { get; set; }

        public int Race { get; set; }

        public IList<ResultDetail> Results { get; set; } = new List<ResultDetail>();

        public IList<string> Places { get; set; } = new List<string>();

        public IList<int> Races { get; set; } = new List<int>();

    }
}