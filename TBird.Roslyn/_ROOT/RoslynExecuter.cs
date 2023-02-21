using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Roslyn
{
    public abstract partial class RoslynExecuter : IDisposable
    {
        public abstract void Dispose();

        public abstract Task RunAsync();
    }

    public partial class RoslynExecuter<T> : RoslynExecuter
    {
        public RoslynExecuter(string path, T target)
        {
            RoslynSetting.Instance.Save();

            _target = new RoslynObject<T>(target);
            _script = CSharpScript.Create(
                File.ReadAllText(path),
                ScriptOptions.Default
                    .WithImports(RoslynSetting.Instance.Imports)
                    .WithReferences(Assemblies),
                typeof(RoslynObject<T>)
            );
        }

        /// <summary>ｽｸﾘﾌﾟﾄが読み込むｱｾﾝﾌﾞﾘﾘｽﾄ</summary>
        private static Assembly[] Assemblies
        {
            get => _Assemblies = _Assemblies ?? RoslynSetting.Instance.Assemblies
                .Select(dll => Assembly.LoadFrom(dll))
                .ToArray();
        }
        private static Assembly[] _Assemblies = null;

        /// <summary>Rosylnｽｸﾘﾌﾟﾄ</summary>
        private Script<object> _script;

        /// <summary>ｽｸﾘﾌﾟﾄﾊﾟﾗﾒｰﾀ</summary>
        private RoslynObject<T> _target;

        /// <summary>
        /// ｽｸﾘﾌﾟﾄを実行します。
        /// </summary>
        /// <returns></returns>
        public override Task RunAsync()
        {
            return _script.RunAsync(_target);
        }

    }
}
