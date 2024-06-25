using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace NukeBuildHelpers.Common;

internal class MultilineScalarFlowStyleEmitter(IEventEmitter nextEmitter) : ChainedEventEmitter(nextEmitter)
{
    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {
        if (typeof(string).IsAssignableFrom(eventInfo.Source.Type))
        {
            string? value = eventInfo.Source.Value as string;
            if (!string.IsNullOrEmpty(value))
            {
                bool isMultiLine = value.IndexOfAny(['\r', '\n', '\x85', '\x2028', '\x2029']) >= 0;
                if (isMultiLine)
                {
                    eventInfo = new ScalarEventInfo(eventInfo.Source)
                    {
                        Style = ScalarStyle.Literal
                    };
                }
            }
        }

        nextEmitter.Emit(eventInfo, emitter);
    }
}

internal static class YamlExtension
{
    internal static string Serialize(object? obj)
    {
        var serializer = new SerializerBuilder()
            .WithEventEmitter(nextEmitter => new MultilineScalarFlowStyleEmitter(nextEmitter))
            .Build();
        return serializer.Serialize(obj);
    }

    internal static T Deserialize<T>(string input)
    {
        var _deserializer = new Deserializer();
        return _deserializer.Deserialize<T>(input);
    }
}
