using System;

namespace RabbitVerse.Consuming.DTO
{
    public class Endpoint
    {
        public Endpoint(string exchnage, RetryInfo retries)
        {
            Exchange = exchnage ?? throw new ArgumentNullException(nameof(exchnage));
            Retries = retries ?? throw new ArgumentNullException(nameof(retries));
        }

        public string Exchange { get; }

        public RetryInfo Retries { get; }

        public override int GetHashCode()
        {
            return Exchange.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var endpoint = obj as Endpoint;
            if (endpoint == null)
                return false;


            return Exchange == endpoint.Exchange;
        }
    }
}