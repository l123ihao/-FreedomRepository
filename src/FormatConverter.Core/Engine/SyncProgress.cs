namespace FormatConverter.Core.Engine;

/// <summary>
/// 同步直传的 IProgress&lt;T&gt;:不经过 SynchronizationContext 排队。
/// 线程切换由最外层(App 层)用 Progress&lt;T&gt; 完成,Core 内部只做直通转发。
/// </summary>
internal sealed class SyncProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public SyncProgress(Action<T> handler) => _handler = handler;

    public void Report(T value) => _handler(value);
}
