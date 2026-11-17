using System.Runtime.InteropServices;
using System.Text.Json;

namespace WoodFactoryErp.Desktop;

// Durable drafts and outbox commit together. Pending data never means officially posted.
internal sealed class ProductionOutbox : IDisposable
{
    private IntPtr database;
    static ProductionOutbox()
    {
        NativeLibrary.SetDllImportResolver(typeof(ProductionOutbox).Assembly, (name, _, _) =>
            name == "wood_sqlite" ? NativeLibrary.Load(OperatingSystem.IsWindows() ? "winsqlite3.dll" : OperatingSystem.IsMacOS() ? "libsqlite3.dylib" : "libsqlite3.so.0") : IntPtr.Zero);
    }
    public ProductionOutbox(string? file = null)
    {
        file ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoodFactoryErp", "production.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        if (Open(file, out database, 2 | 4 | 65536, IntPtr.Zero) != 0) throw new InvalidOperationException("تعذر فتح قاعدة مسودات الإنتاج المحلية.");
        Run("PRAGMA journal_mode=WAL"); Run("PRAGMA synchronous=FULL"); Run("PRAGMA busy_timeout=3000");
        Run("CREATE TABLE IF NOT EXISTS execution_drafts(operation_id TEXT PRIMARY KEY,user_id TEXT NOT NULL,job_id TEXT NOT NULL,fingerprint TEXT NOT NULL,payload TEXT NOT NULL,status TEXT NOT NULL DEFAULT 'pending',created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)");
        Run("CREATE UNIQUE INDEX IF NOT EXISTS ix_drafts_pending ON execution_drafts(user_id,fingerprint) WHERE status IN ('pending','blocked')");
        Run("CREATE TABLE IF NOT EXISTS execution_outbox(operation_id TEXT PRIMARY KEY REFERENCES execution_drafts(operation_id),status TEXT NOT NULL DEFAULT 'pending',detail TEXT NOT NULL DEFAULT '',acknowledged_at TEXT)");
        Run("CREATE TABLE IF NOT EXISTS execution_cache(key TEXT PRIMARY KEY,json TEXT NOT NULL,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)");
    }
    public string Enqueue(string user, string job, string fingerprint, Func<string,string> payload)
    {
        var existing = Query("SELECT operation_id FROM execution_drafts WHERE user_id=? AND fingerprint=? AND status IN ('pending','blocked')", user, fingerprint);
        if (existing.Count > 0) return existing[0][0];
        var id = Guid.NewGuid().ToString();
        Transaction(() => {
            Run("INSERT INTO execution_drafts(operation_id,user_id,job_id,fingerprint,payload) VALUES(?,?,?,?,?)", id,user,job,fingerprint,payload(id));
            Run("INSERT INTO execution_outbox(operation_id) VALUES(?)",id);
        });
        return id;
    }
    public List<QueuedProductionCommand> Commands(string user) => Query("SELECT d.operation_id,d.job_id,d.payload,o.status,o.detail FROM execution_drafts d JOIN execution_outbox o ON o.operation_id=d.operation_id WHERE d.user_id=? AND o.status IN ('pending','blocked') ORDER BY d.rowid", user)
        .Select(x=>new QueuedProductionCommand(x[0],x[1],x[2],x[3],x[4])).ToList();
    public QueuedProductionCommand? Command(string user,string id) => Query("SELECT d.operation_id,d.job_id,d.payload,o.status,o.detail FROM execution_drafts d JOIN execution_outbox o ON o.operation_id=d.operation_id WHERE d.user_id=? AND d.operation_id=?",user,id)
        .Select(x=>new QueuedProductionCommand(x[0],x[1],x[2],x[3],x[4])).FirstOrDefault();
    public void Mark(string id,string status,string detail)
    {
        Transaction(() => {
            Run("UPDATE execution_drafts SET status=? WHERE operation_id=?",status,id);
            Run("UPDATE execution_outbox SET status=?,detail=?,acknowledged_at=CASE WHEN ?='accepted' THEN CURRENT_TIMESTAMP ELSE acknowledged_at END WHERE operation_id=?",status,detail,status,id);
        });
    }
    public void Cache(string key,string json) => Run("INSERT INTO execution_cache(key,json) VALUES(?,?) ON CONFLICT(key) DO UPDATE SET json=excluded.json,updated_at=CURRENT_TIMESTAMP",key,json);
    public string? Cached(string key) => Query("SELECT json FROM execution_cache WHERE key=?",key).FirstOrDefault()?[0];
    private void Transaction(Action action)
    {
        Run("BEGIN IMMEDIATE");
        try { action(); Run("COMMIT"); } catch { Run("ROLLBACK"); throw; }
    }
    private void Run(string sql,params string[] values) => Query(sql,values);
    private List<string[]> Query(string sql,params string[] values)
    {
        if (Prepare(database,sql,-1,out var statement,IntPtr.Zero)!=0) throw new InvalidOperationException("SQLite: "+Marshal.PtrToStringUTF8(Error(database)));
        try {
            for(var i=0;i<values.Length;i++) if(Bind(statement,i+1,values[i],-1,new IntPtr(-1))!=0) throw new InvalidOperationException("SQLite binding failed.");
            var rows=new List<string[]>(); int state;
            while((state=Step(statement))==100) {
                var row=new string[ColumnCount(statement)];
                for(var i=0;i<row.Length;i++) row[i]=Marshal.PtrToStringUTF8(ColumnText(statement,i))??"";
                rows.Add(row);
            }
            if(state!=101) throw new InvalidOperationException("SQLite: "+Marshal.PtrToStringUTF8(Error(database)));
            return rows;
        } finally {Finalize(statement);}
    }
    public void Dispose() {if(database!=IntPtr.Zero) {Close(database);database=IntPtr.Zero;}}
    [DllImport("wood_sqlite",EntryPoint="sqlite3_open_v2",CallingConvention=CallingConvention.Cdecl)] private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string file,out IntPtr db,int flags,IntPtr vfs);
    [DllImport("wood_sqlite",EntryPoint="sqlite3_close_v2",CallingConvention=CallingConvention.Cdecl)] private static extern int Close(IntPtr db);
    [DllImport("wood_sqlite",EntryPoint="sqlite3_prepare_v2",CallingConvention=CallingConvention.Cdecl)] private static extern int Prepare(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)] string sql,int bytes,out IntPtr statement,IntPtr tail);
    [DllImport("wood_sqlite",EntryPoint="sqlite3_bind_text",CallingConvention=CallingConvention.Cdecl)] private static extern int Bind(IntPtr statement,int index,[MarshalAs(UnmanagedType.LPUTF8Str)] string value,int bytes,IntPtr destructor);
    [DllImport("wood_sqlite",EntryPoint="sqlite3_step",CallingConvention=CallingConvention.Cdecl)] private static extern int Step(IntPtr statement);
    [DllImport("wood_sqlite",EntryPoint="sqlite3_finalize",CallingConvention=CallingConvention.Cdecl)] private static extern int Finalize(IntPtr statement);
    [DllImport("wood_sqlite",EntryPoint="sqlite3_column_count",CallingConvention=CallingConvention.Cdecl)] private static extern int ColumnCount(IntPtr statement);
    [DllImport("wood_sqlite",EntryPoint="sqlite3_column_text",CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr ColumnText(IntPtr statement,int index);
    [DllImport("wood_sqlite",EntryPoint="sqlite3_errmsg",CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr Error(IntPtr db);
}
internal sealed record QueuedProductionCommand(string Id,string JobId,string Payload,string Status,string Detail);
