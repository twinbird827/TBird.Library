using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Roslyn
{
    public interface IRoslynExecuter : IDisposable
    {
        Task RunAsync();
    }

    public partial class RoslynExecuter<T> : IRoslynExecuter
    {
        public RoslynExecuter(string path, T target)
        {
            RoslynSetting.Instance.Save();

            using (MessageService.Measure())
            {
                _target = new RoslynObject<T>(target);
                _script = CSharpScript.Create(
                    File.ReadAllText(path),
                    ScriptOptions.Default
                        .WithImports(RoslynSetting.Instance.Imports)
                        .WithReferences(
                            typeof(object).Assembly,
                            typeof(Uri).Assembly,
                            typeof(Enumerable).Assembly,
                            Assembly.GetEntryAssembly()
                        ),
                    typeof(RoslynObject<T>)
                );
            }
        }

        /// <summary>Rosylnｽｸﾘﾌﾟﾄ</summary>
        private Script<object> _script;

        /// <summary>ｽｸﾘﾌﾟﾄﾊﾟﾗﾒｰﾀ</summary>
        private RoslynObject<T> _target;

        /// <summary>
        /// ｽｸﾘﾌﾟﾄを実行します。
        /// </summary>
        /// <returns></returns>
        public Task RunAsync()
        {
            return _script.RunAsync(_target);
        }

    }
}