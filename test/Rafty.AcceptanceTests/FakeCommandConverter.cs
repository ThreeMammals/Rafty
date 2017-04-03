using System;
using Newtonsoft.Json.Linq;
using Rafty.Infrastructure;

namespace Rafty.AcceptanceTests
{
    public class FakeCommandConverter : JsonCreationConverter<FakeCommand>
    {
        protected override FakeCommand Create(Type objectType, JObject jObject)
        {
            return new FakeCommand();
        }
    }
}