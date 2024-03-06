using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace NukeBuildHelpers.Common;

internal static class YamlExtension
{
    private static readonly Serializer _serializer = new();
    private static readonly Deserializer _deserializer = new();

    internal static string Serialize(object? obj)
    {
        return _serializer.Serialize(obj);
    }

    internal static T Deserialize<T>(string input)
    {
        return _deserializer.Deserialize<T>(input);
    }
}
