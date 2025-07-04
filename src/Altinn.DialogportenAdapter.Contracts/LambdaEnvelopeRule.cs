using Wolverine;

namespace Altinn.DialogportenAdapter.Contracts;

public class LambdaEnvelopeRule<T> : IEnvelopeRule
{
    private readonly Action<Envelope, T> _configure;

    public LambdaEnvelopeRule(Action<Envelope, T> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
    }

    public void Modify(Envelope envelope)
    {
        if (envelope.Message is T message)
        {
            _configure(envelope, message);
        }
    }
}