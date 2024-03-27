using System;

namespace RabbitVerse.Consuming.Models.Contract.Provider
{
    public interface IConsumingProviderFactory
    {
        IConsumingProvider StartScope();
    }
}