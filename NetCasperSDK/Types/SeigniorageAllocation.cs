using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetCasperSDK.Types
{
    /// <summary>
    /// Information about a seigniorage allocation
    /// </summary>
    public class SeigniorageAllocation
    {
        public bool IsDelegator { get; init; }

        public PublicKey DelegatorPublicKey { get; init; }

        public PublicKey ValidatorPublicKey { get; init; }

        public BigInteger Amount { get; init; }

        public class SeigniorageAllocationConverter : JsonConverter<SeigniorageAllocation>
        {
            public override SeigniorageAllocation Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("Cannot deserialize SeigniorageAllocation. StartObject expected");

                reader.Read(); // start object

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Cannot deserialize SeigniorageAllocation. PropertyName expected");

                var propertyName = reader.GetString();
                reader.Read();

                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("Cannot deserialize SeigniorageAllocation. StartObject expected");

                reader.Read(); // start object

                string delegatorPk = null;
                string validatorPk = null;
                BigInteger amount = BigInteger.Zero;

                while (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var field = reader.GetString();
                    reader.Read();
                    if (field == "delegator_public_key")
                        delegatorPk = reader.GetString();
                    else if (field == "validator_public_key")
                        validatorPk = reader.GetString();
                    else if (field == "amount")
                        amount = BigInteger.Parse(reader.GetString());
                    reader.Read();
                }

                reader.Read(); // end object

                return new SeigniorageAllocation()
                {
                    IsDelegator = propertyName?.ToLower() == "delegator",
                    DelegatorPublicKey = delegatorPk != null ? PublicKey.FromHexString(delegatorPk) : null,
                    ValidatorPublicKey = PublicKey.FromHexString(validatorPk),
                    Amount = amount
                };
            }

            public override void Write(
                Utf8JsonWriter writer,
                SeigniorageAllocation value,
                JsonSerializerOptions options)
            {
                throw new NotImplementedException("Write method for SeigniorageAllocation not yet implemented");
            }
        }
    }
}