using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EasyLotteryDomain.Services;

public static class YamlSerialization
{
    public static DeserializerBuilder CreateDeserializerBuilder() =>
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithTypeConverter(new DateTimeOffsetYamlTypeConverter());

    public static SerializerBuilder CreateSerializerBuilder() =>
        new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
            .WithTypeConverter(new DateTimeOffsetYamlTypeConverter());

    private sealed class DateTimeOffsetYamlTypeConverter : IYamlTypeConverter
    {
        private const string RoundTripFormat = "O";

        public bool Accepts(Type type) =>
            type == typeof(DateTimeOffset) ||
            type == typeof(DateTimeOffset?) ||
            type == typeof(DateTime) ||
            type == typeof(DateTime?);

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer nestedObjectDeserializer)
        {
            var scalar = parser.Consume<Scalar>();
            if (string.IsNullOrWhiteSpace(scalar.Value) || string.Equals(scalar.Value, "null", StringComparison.OrdinalIgnoreCase))
            {
                return type == typeof(DateTimeOffset?) || type == typeof(DateTime?)
                    ? null
                    : Activator.CreateInstance(type);
            }

            if (type == typeof(DateTime) || type == typeof(DateTime?))
            {
                if (DateTime.TryParseExact(scalar.Value, RoundTripFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
                {
                    return dateTime;
                }

                if (DateTime.TryParse(scalar.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dateTime))
                {
                    return dateTime;
                }

                return type == typeof(DateTime?) ? null : default(DateTime);
            }

            if (DateTimeOffset.TryParseExact(scalar.Value, RoundTripFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var offset))
            {
                return offset;
            }

            if (DateTimeOffset.TryParse(scalar.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out offset))
            {
                return offset;
            }

            return type == typeof(DateTimeOffset?) ? null : default(DateTimeOffset);
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer nestedObjectSerializer)
        {
            if (value is null)
            {
                emitter.Emit(new Scalar("null"));
                return;
            }

            string text = value switch
            {
                DateTimeOffset offset => offset.ToUniversalTime().ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                DateTime dateTime => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
            };

            emitter.Emit(new Scalar(text));
        }
    }
}
