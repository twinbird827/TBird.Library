using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Roslyn
{
    public partial class RoslynManager
    {
        private const string _ctxroot = "scripts";

        private IList<RoslynExecuter> _list = new List<RoslynExecuter>();

        public static RoslynManager Instance
        {
            get => _Instance = _Instance ?? new RoslynManager();
        }
        private static RoslynManager _Instance;

        public void Initialize<T>(T parameter)
        {
            Directory.GetFiles(Path.Combine(Directories.Root, _ctxroot), "*.ctx")
                .ForEach(path => Add(path, parameter));
        }

        public void Add<T>(string path, T parameter)
        {
            _list.Add(new RoslynExecuter<T>(path, parameter));
        }

        public Task RunAsync()
        {
            return _list.Select(x => x.RunAsync()).WhenAll();
        }
    }
}
