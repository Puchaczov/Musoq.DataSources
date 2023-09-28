using System.Threading;
using System.Threading.Tasks;
using DbcParserLib.Model;

namespace Musoq.DataSources.CANBus.Components;

internal interface ICANBusApi
{
    Task<Message[]> GetMessagesAsync(CancellationToken cancellationToken);

    Message[] GetMessages();
    
    Task<Signal[]> GetMessagesSignalsAsync(CancellationToken cancellationToken);
}