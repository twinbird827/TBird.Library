using Browser.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.VisualBasic.FileIO;
using System;
using System.IO;
using System.Text;
using TBird.Core;

namespace Browser.Pages
{
    public class ResultDetailModel : PageModel
    {
        private readonly ILogger<ResultDetailModel> _logger;

        public ResultDetailModel(ILogger<ResultDetailModel> logger)
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

            var filepath = AppSetting.Instance.TargetDirs
                .Select(x => Path.Combine(x, $"{title}.csv"))
                .FirstOrDefault(x => System.IO.File.Exists(x));

            if (string.IsNullOrEmpty(filepath)) return NotFound();

            using (var tfp = new TextFieldParser(filepath, Encoding.GetEncoding("Shift_JIS")))
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
                Races.AddRange(Enumerable.Range(1, 12));

                Results.Clear();
                Results.AddRange(lines
                    .Where(x => x.Race == Race && x.Place == Places[Place])
                    .OrderBy(x => x.Umano)
                );

                Displays.Clear();
                Displays.AddRange(Results.Select(x => new ResultDisplay(x, Results)));

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

        public IList<ResultDisplay> Displays { get; set; } = new List<ResultDisplay>();

        public IList<string> Places { get; set; } = new List<string>();

        public IList<int> Races { get; set; } = new List<int>();

    }
}