using System.Threading.Channels;

namespace Azma.Runs.Api.Application.Runs;

public sealed class RunScheduleSignal
{
    private readonly Channel<bool> _channel = Channel.CreateUnbounded<bool>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public void Pulse() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        var delay = Task.Delay(timeout, ct);
        var signal = _channel.Reader.WaitToReadAsync(ct).AsTask();
        var completed = await Task.WhenAny(delay, signal);
        if (completed == signal && await signal)
        {
            while (_channel.Reader.TryRead(out _)) { }
        }
    }
}

public sealed class RunAutomationSignal
{
    private readonly Channel<bool> _channel = Channel.CreateUnbounded<bool>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public void Pulse() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        var delay = Task.Delay(timeout, ct);
        var signal = _channel.Reader.WaitToReadAsync(ct).AsTask();
        var completed = await Task.WhenAny(delay, signal);
        if (completed == signal && await signal)
        {
            while (_channel.Reader.TryRead(out _)) { }
        }
    }
}
