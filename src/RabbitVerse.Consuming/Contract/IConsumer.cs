using System;
using System.Threading.Tasks;
using RabbitVerse.Consuming.DTO;

namespace RabbitVerse.Consuming
{
    public interface IConsumer<TPayload> : IConsumer
    {
        ValueTask<ConsumerResult> Handle(TPayload payload);
    }

    public interface IConsumer
    {
        Type PayloadType { get; }

        ValueTask<ConsumerResult> Handle(object payload);
    }

    public abstract class ConsumerBase<TPayload> : IConsumer<TPayload>
    {
        public Type PayloadType => typeof(TPayload);

        public abstract ValueTask<ConsumerResult> Handle(TPayload payload);

        public ValueTask<ConsumerResult> Handle(object payload)
        {
            return Handle((TPayload)payload);
        }
    }
}