﻿using System.IO;
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

    public async Task<Message[]> GetMessagesAsync(CancellationToken cancellationToken)
    {
        _dbc ??= await ParseFromPathAsync(cancellationToken);
        
        return _dbc.Messages.ToArray();
    }

    public Message[] GetMessages(CancellationToken cancellationToken)
    {
        var parseTask = Task.Run(() => ParseFromPathAsync(cancellationToken), cancellationToken);
        _dbc ??= parseTask.GetAwaiter().GetResult();
        
        return _dbc.Messages.ToArray();
    }

    public async Task<(Signal Signal, string MessageName)[]> GetMessagesSignalsAsync(CancellationToken cancellationToken)
    {
        _dbc ??= await ParseFromPathAsync(cancellationToken);
        
        return _dbc.Messages.SelectMany(f => f.Signals.Select(s => (s, f.Name))).ToArray();
    }

    private async Task<Dbc> ParseFromPathAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        await using var stream = File.OpenRead(_dbcPath);
        using var reader = new StreamReader(stream, leaveOpen: true);
        using var modifiedFileStream = new MemoryStream();
        await using var writer = new StreamWriter(modifiedFileStream, leaveOpen: true);
        
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var line = await reader.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(line))
            {
                await writer.WriteLineAsync();
                continue;
            }

            if (line.StartsWith("VAL_TABLE_") || line.StartsWith("VAL_"))
            {
                var lastIndexOfSemicolon = line.LastIndexOf(';');

                if (lastIndexOfSemicolon == -1)
                    goto EndOfFix;
                
                var howManyWhiteSpacesAfterLastCharacterAndSemicolon = 1;
                
                while (line[lastIndexOfSemicolon - howManyWhiteSpacesAfterLastCharacterAndSemicolon] == ' ')
                    howManyWhiteSpacesAfterLastCharacterAndSemicolon++;

                if (howManyWhiteSpacesAfterLastCharacterAndSemicolon > 1)
                {
                    var newLine = line[..(lastIndexOfSemicolon - howManyWhiteSpacesAfterLastCharacterAndSemicolon + 1)];
                    newLine += line[lastIndexOfSemicolon];
                    line = newLine;
                }
            }
            
            EndOfFix:
            await writer.WriteLineAsync(line);
        }
        
        await writer.FlushAsync();
        
        modifiedFileStream.Seek(0, SeekOrigin.Begin);
        var dbc = DbcParserLib.Parser.ParseFromStream(modifiedFileStream);
        return dbc;
    }
}