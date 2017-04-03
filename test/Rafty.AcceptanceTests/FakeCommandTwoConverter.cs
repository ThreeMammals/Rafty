using System;
using Newtonsoft.Json.Linq;
using Rafty.Infrastructure;

namespace Rafty.AcceptanceTests
{
    public class FakeCommandTwoConverter : JsonCreationConverter<FakeCommandTwo>
    {
        protected override FakeCommandTwo Create(Type objectType, JObject jObject)
        {
            return new FakeCommandTwo();
        }
    }
}