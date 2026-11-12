using System.Threading.Channels;
using Microsoft.Extensions.Options;
using PaymentReconciliation.Modules.Webhooks.Application;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public interface IWebhookIngressQueue
{
    bool TryEnqueue(WebhookEnvelope envelope);

    ChannelReader<WebhookEnvelope> Reader { get; }
}

public sealed class WebhookIngressQueue : IWebhookIngressQueue
{
    private readonly Channel<WebhookEnvelope> _channel;

    public WebhookIngressQueue(IOptions<WebhookIngestionOptions> options)
    {
        _channel = Channel.CreateBounded<WebhookEnvelope>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public ChannelReader<WebhookEnvelope> Reader => _channel.Reader;

    public bool TryEnqueue(WebhookEnvelope envelope) => _channel.Writer.TryWrite(envelope);
}
