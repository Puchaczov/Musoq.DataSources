using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbcParserLib;
using DbcParserLib.Model;

namespace Musoq.DataSources.CANBus.Components;

internal class CANBusApi : ICANBusApi
{
    private readonly string _dbcPath;
    private Dbc? _dbc;

    public CANBusApi(string dbcPath)
    {
        _dbcPath = dbcPath;
        _dbc = null;
    }

    public Task<Message[]> GetMessagesAsync(CancellationToken cancellationToken)
    {
        _dbc ??= DbcParserLib.Parser.ParseFromPath(_dbcPath);
        
        return Task.FromResult(_dbc.Messages.ToArray());
    }

    public Message[] GetMessages()
    {
        _dbc ??= DbcParserLib.Parser.ParseFromPath(_dbcPath);
        
        return _dbc.Messages.ToArray();
    }

    public Task<(Signal Signal, string MessageName)[]> GetMessagesSignalsAsync(CancellationToken cancellationToken)
    {
        _dbc ??= DbcParserLib.Parser.ParseFromPath(_dbcPath);
        
        return Task.FromResult(_dbc.Messages.SelectMany(f => f.Signals.Select(s => (s, f.Name))).ToArray());
    }
}