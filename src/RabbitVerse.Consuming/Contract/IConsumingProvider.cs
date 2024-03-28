using System;
using System.Collections.Generic;
using RabbitVerse.Consuming.DTO;

namespace RabbitVerse.Consuming
{
    public interface IConsumingProvider : IDisposable
    {

        ConsumerInfo GetConsumerInfo(Endpoint endpoint);
    }
}
