#nullable enable
using Musoq.Schema.DataSources;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Musoq.Schema.Xml
{
    internal class XmlSource : RowSourceBase<DynamicElement>
    {
        private readonly string _filePath;
        private readonly RuntimeContext _context;

        public XmlSource(string filePath, RuntimeContext context)
        {
            _filePath = filePath;
            _context = context;
        }

        protected override void CollectChunks(System.Collections.Concurrent.BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
        {
            using var file = File.OpenRead(_filePath);
            using var stringReader = new StreamReader(file);
            using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings()
            {
                IgnoreWhitespace = true,
                IgnoreComments = true
            });
            xmlReader.MoveToContent();

            var chunk = new List<IObjectResolver>(1000);
            var elements = new Stack<DynamicElement>();

            do
            {
                switch (xmlReader.NodeType)
                {
                    case XmlNodeType.Element:
                        var dictionary = new Dictionary<string, object?>
                        {
                            {"element", xmlReader.LocalName},
                            {"parent", elements.Count > 0 ? elements.Peek() : null},
                            {"value", xmlReader.HasValue ? xmlReader.Value : null}
                        };
                        
                        var element = new DynamicElement(dictionary);
                        
                        dictionary.Add(xmlReader.Name, element);
                        elements.Push(element);

                        if (xmlReader.HasAttributes)
                        {
                            while (xmlReader.MoveToNextAttribute())
                            {
                                dictionary.Add(xmlReader.Name, xmlReader.Value);
                            }
                        }

                        xmlReader.MoveToElement();
                        break;
                    case XmlNodeType.Text:
                        elements.Peek().Add("text", xmlReader.Value);
                        break;
                    case XmlNodeType.EndElement:
                        var dynamicElement = elements.Pop();
                        
                        var nameToIndexMap = new Dictionary<string, int>();
                        
                        foreach (var key in dynamicElement.Keys)
                        {
                            nameToIndexMap.Add(key, nameToIndexMap.Count);
                        }
                        
                        var indexToMethodAccessMap = new Dictionary<int, System.Func<DynamicElement, object?>>();
                        
                        foreach (var key in dynamicElement.Keys)
                        {
                            indexToMethodAccessMap.Add(indexToMethodAccessMap.Count, dynamicElementAccess => dynamicElementAccess.Values[key]);
                        }
                        
                        chunk.Add(new XmlResolver<DynamicElement>(dynamicElement, nameToIndexMap, indexToMethodAccessMap));
                        break;
                }

                if (chunk.Count >= 1000)
                {
                    chunkedSource.Add(chunk);
                    chunk = new List<IObjectResolver>(1000);
                }
            }
            while (xmlReader.Read());

            if (chunk.Count > 0)
                chunkedSource.Add(chunk);
        }
    }
}
