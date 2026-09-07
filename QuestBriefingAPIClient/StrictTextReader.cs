using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Manimal.QuestBriefingAPI;

internal sealed class StrictTextReader(TextReader reader) : JsonTextReader(reader)
{
    private readonly Stack<HashSet<string>> _objects = new();

    public override bool Read()
    {
        while (base.Read())
        {
            if (TokenType == JsonToken.Comment)
            {
                continue;
            }
            
            switch (TokenType)
            {
                case JsonToken.StartObject:
                {
                    _objects.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                }
            
                case JsonToken.EndObject:
                {
                    if (_objects.Count > 0)
                    {
                        _objects.Pop();
                    }

                    break;
                }
            
                case JsonToken.PropertyName:
                {
                    if (_objects.Count == 0)
                    {
                        throw new JsonReaderException($"Property '{Value}' encountered outside an object.");
                    }

                    var name = (string)Value;

                    if (!_objects.Peek().Add(name))
                    {
                        throw new JsonReaderException($"Duplicate JSON property '{name}'.");
                    }

                    break;
                }
            }
            
            return true;
        }

        return false;
    }
}