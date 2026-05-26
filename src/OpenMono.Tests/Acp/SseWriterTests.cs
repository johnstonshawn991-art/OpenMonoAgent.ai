using System.Text;
using FluentAssertions;
using OpenMono.Acp;
using Xunit;

namespace OpenMono.Tests.Acp;

public sealed class SseWriterTests
{
    [Fact]
    public async Task WriteEventAsync_emits_event_data_blank_line_format()
    {
        var ms = new MemoryStream();
        var writer = new SseWriter(ms, CancellationToken.None);

        await writer.WriteEventAsync("text_delta", new { content = "hi" });

        var text = Encoding.UTF8.GetString(ms.ToArray());
        text.Should().Be("event: text_delta\ndata: {\"content\":\"hi\"}\n\n");
    }

    [Fact]
    public async Task WriteEventAsync_serializes_payloads_as_compact_json()
    {
        var ms = new MemoryStream();
        var writer = new SseWriter(ms, CancellationToken.None);

        await writer.WriteEventAsync("usage", new { input_tokens = 42, output_tokens = 7, total_tokens = 49 });

        var text = Encoding.UTF8.GetString(ms.ToArray());
        text.Should().Contain("data: {\"input_tokens\":42,\"output_tokens\":7,\"total_tokens\":49}\n\n");
        text.Should().NotContain("  ", because: "JSON must be unindented");
        text.Should().NotContain("\r\n", because: "SSE framing uses LF, not CRLF");
    }

    [Fact]
    public async Task WriteEventAsync_flushes_after_every_write()
    {
        var stream = new FlushTrackingStream();
        var writer = new SseWriter(stream, CancellationToken.None);

        await writer.WriteEventAsync("text_delta", new { content = "a" });
        await writer.WriteEventAsync("text_delta", new { content = "b" });
        await writer.WriteEventAsync("done", new { });

        stream.FlushCount.Should().Be(3, "every event must flush so the client receives it immediately");
    }

    [Fact]
    public async Task Concurrent_WriteEventAsync_calls_are_serialized()
    {
        var stream = new ConcurrencyDetectingStream();
        var writer = new SseWriter(stream, CancellationToken.None);




        var producers = Enumerable.Range(0, 3).Select(p => Task.Run(async () =>
        {
            for (var i = 0; i < 16; i++)
                await writer.WriteEventAsync("text_delta", new { content = $"p{p}_{i}" });
        }));
        await Task.WhenAll(producers);

        stream.MaxObservedConcurrency.Should().Be(1,
            "SseWriter's semaphore must prevent concurrent writes to the underlying body stream");


        var text = Encoding.UTF8.GetString(stream.ToArray());
        var blocks = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        blocks.Should().HaveCount(48, "3 producers × 16 events = 48 complete event frames");

        foreach (var block in blocks)
        {
            var lines = block.Split('\n');
            lines.Should().HaveCount(2);
            lines[0].Should().StartWith("event: text_delta");
            lines[1].Should().StartWith("data: {").And.EndWith("}");
        }
    }



    private sealed class FlushTrackingStream : MemoryStream
    {
        public int FlushCount { get; private set; }


        public override Task FlushAsync(CancellationToken ct)
        {
            FlushCount++;
            return Task.CompletedTask;
        }
    }





    private sealed class ConcurrencyDetectingStream : MemoryStream
    {
        private int _inFlight;
        private int _maxObserved;

        public int MaxObservedConcurrency => _maxObserved;

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
        {
            var current = Interlocked.Increment(ref _inFlight);
            InterlockedMax(ref _maxObserved, current);
            try
            {

                await Task.Yield();
                await base.WriteAsync(buffer, ct);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int prev;
            do { prev = target; if (value <= prev) return; }
            while (Interlocked.CompareExchange(ref target, value, prev) != prev);
        }
    }
}
