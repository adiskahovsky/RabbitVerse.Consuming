using System.Collections.Generic;
using RabbitVerse.Consuming.DTO;

namespace RabbitVerse.Consuming.Contract
{
    public interface IEndpointsProvider
    {
        HashSet<Endpoint> GetEndpoinds();
    }
}