using System.Threading.Tasks;
using SQLite;

namespace TBird.Maui.DB;

/// <summary>
/// スキーマ migration の最小インタフェース。
/// FromVersion はこの migration が想定する開始バージョン
/// （実行することで DB が FromVersion → FromVersion + 1 に上がる）。
/// 実装は冪等であること（CREATE INDEX IF NOT EXISTS / DROP INDEX IF EXISTS /
/// INSERT OR IGNORE / WHERE 句限定 DELETE 等）。
/// </summary>
public interface IMigration
{
    int FromVersion { get; }
    Task ExecuteAsync(SQLiteAsyncConnection conn);
}
