using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using DynLan;
using DynLan.Exceptions;
using DynLan.OnpEngine.Logic;
using DynLan.Helpers;
using DynLan.OnpEngine.Models;
using DynLan.Evaluator;
using DynLan.Classes;
using System.Dynamic;
using Xunit;

namespace DynLanTests
{
    public class DynLanSerializationTests
    {

        [Fact]
        public void list_of_dictionaries_round_trips_through_custom_serialize_deserialize_methods()
        {

            foreach (var method in GetMethods())
                DynLan.OnpEngine.Internal.BuildinMethods.Add(method);

            var r = new Compiler().Compile(@"

list = list()

a = dictionary()
a.Created = getdate()
a.Name = 'A'
list.Add(a)

a = dictionary()
a.Created = getdate()
a.Name = 'B'
list.Add(a)

list2 = serialize(list)
list2 = deserialize(list2)

return list2
");
            var v = r.Eval();
            Assert.IsAssignableFrom<IList>(v);
            Assert.Equal(2, (v as IList).Count);
            Assert.IsAssignableFrom<IDictionary>((v as IList)[0]);
            Assert.Equal(DateTime.Now.Date, ((DateTime)((v as IList)[0] as IDictionary)["Created"]));
        }

        private static IEnumerable<DynMethod> GetMethods()
        {
            yield return new DynMethod()
            {
                Names = new[] { "serialize" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        return new DynMethodResult(JsonSerializerPrecise.SerializeJson(Parameters.FirstOrDefault()));
                    }
                    return new DynMethodResult(null);
                }
            };

            yield return new DynMethod()
            {
                Names = new[] { "deserialize" },
                Body = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        string json = UniConvert.ToString(Parameters.FirstOrDefault());
                        return new DynMethodResult(JsonSerializerPrecise.DeserializeJson(json));
                    }
                    return new DynMethodResult(null);
                }
            };
        }

    }
}
