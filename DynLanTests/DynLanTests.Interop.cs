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
using static DynLanTests.TestFixtures;

namespace DynLanTests
{
    public class DynLanInteropTests
    {

        [Fact]
        public void test_expandoobject_input()
        {
            {
                dynamic osoba = new ExpandoObject();
                osoba.imie = "andrew";

                var dict = new Dictionary<string, object>();
                dict["osoba"] = osoba;

                var r = new Compiler().Compile(@"
item = dictionary(); item.imie = osoba.imie; return item.imie;
");
                var v = r.Eval(dict);
                Assert.Equal("andrew", v);
            }
        }

        [Fact]
        public void test_dictionary_input()
        {
            {
                var osoba = new Dictionary<string, object>();
                osoba["imie"] = "andrew";

                var dict = new Dictionary<string, object>();
                dict["osoba"] = osoba;

                var r = new Compiler().Compile(@"
item = dictionary(); item.imie = osoba.imie; return item.imie;
");
                var v = r.Eval(dict);
                Assert.Equal("andrew", v);
            }
        }

        [Fact]
        public void new_creates_dotnet_arraylist_instance_by_type_name()
        {
            var r = new Compiler().Compile(@"
lista = new('ArrayList')
lista.Add('babb')
return lista[0]
");
            var v = r.Eval();
            Assert.Equal("babb", v);
        }

        [Fact]
        public void dictionary_value_holding_zero_arg_func_delegate_is_invoked_as_method()
        {
            var complied = new Compiler().Compile("return DICT.Metoda1()");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(1M, r1);
        }

        [Fact]
        public void dictionary_value_holding_dynlan_method_parameters_delegate_receives_parameter_count()
        {
            var complied = new Compiler().Compile("return DICT.Metoda2(1,2,3)");
            var r1 = Convert.ToInt64(complied.Eval(GetParams().VARIABLES));
            Assert.Equal(3L, r1);
        }

        [Fact]
        public void dictionary_value_holding_dynlan_method_parameters_delegate_returns_indexed_parameter()
        {
            var complied = new Compiler().Compile("return DICT.Metoda3(1,2,3)");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(2L, r1);
        }

        [Fact]
        public void type_function_resolves_dotnet_type_by_name_and_calls_its_static_method()
        {
            var complied = new Compiler().Compile("return type('UniConvert').ToInt32(3.77)");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(4, r1);
        }

        [Fact]
        public void assigning_property_on_object_inside_list_element_mutates_source_object()
        {
            var paramsObj = GetParams();
            var complied = new Compiler().Compile(" OSOBY[0].Imie = 'sadasda!' " + nl + "return OSOBY[0].Imie");
            var r1 = (String)complied.Eval(paramsObj.VARIABLES);
            Assert.Equal("sadasda!", r1);
            Assert.Equal("sadasda!", paramsObj.OSOBY[0].Imie);
        }

        [Fact]
        public void assigning_property_on_dotnet_object_input_mutates_source_object()
        {
            var paramsObj = GetParams();
            var complied = new Compiler().Compile(" OSOBA.Imie = 'sadasda' " + nl + "return OSOBA.Imie");
            var r1 = (String)complied.Eval(paramsObj.VARIABLES);
            Assert.Equal("sadasda", r1);
            Assert.Equal("sadasda", paramsObj.OSOBA1.Imie);
        }

        [Fact]
        public void typeof_function_returns_int64_type_for_integer_expression()
        {
            var r1 = (Type)new Compiler().Compile(" return  typeof(321312+33) ").Eval(GetParams().VARIABLES);
            var r2 = typeof(Int64);
            Assert.Equal(r2, r1);
        }

        [Fact]
        public void getdatetime_short_date_string_matches_dotnet_datetime_now_with_extra_whitespace_in_call()
        {
            var r1 = (String)new Compiler().Compile(" return  'abc '.Length +  ' ' +getdatetime   (  ).ToShortDateString  (  ).Substring(0,3) ").Eval(GetParams().VARIABLES);
            var r2 = "abc ".Length + " " + DateTime.Now.ToShortDateString().Substring(0, 3);
            Assert.Equal(r2, r1);
        }

        [Fact]
        public void getdatetime_ticks_string_prefix_matches_dotnet_datetime_now_ticks()
        {
            var r1 = (String)new Compiler().Compile(" return  getdatetime().Ticks.ToString().Substring(0,6) ").Eval(GetParams().VARIABLES);
            var r2 = DateTime.Now.Ticks.ToString().Substring(0, 6);
            Assert.Equal(r2, r1);
        }

        [Fact]
        public void getdatetime_short_date_string_substring_matches_dotnet_datetime_now()
        {
            var r1 = (String)new Compiler().Compile(" return  'abc'.Length + ' ' + getdatetime().ToShortDateString().Substring(100-90-3-7,3) ").Eval(GetParams().VARIABLES);
            var r2 = "abc".Length + " " + DateTime.Now.ToShortDateString().Substring(100 - 90 - 3 - 7, 3);
            Assert.Equal(r2, r1);
        }

        [Fact]
        public void getdatetime_short_date_string_substring_matches_dotnet_datetime_now_duplicate()
        {
            var r1 = (String)new Compiler().Compile(" return  'abc'.Length + ' ' + getdatetime().ToShortDateString().Substring(100-90-3-7,3) ").Eval(GetParams().VARIABLES);
            var r2 = "abc".Length + " " + DateTime.Now.ToShortDateString().Substring(100 - 90 - 3 - 7, 3);
            Assert.Equal(r2, r1);
        }

        [Fact]
        public void custom_dynmethod_injected_as_parameter_is_callable_with_no_arguments()
        {
            var parameters = new Dictionary<string, object>()
            {
                {
                    "test" ,
                    new DynMethod((DynLanContext, Parameters) =>
                    {
                        return new DynMethodResult("test " + (Parameters?.Count??0));
                    })
                }
            };
            var r1 = (String)new Compiler().Compile(" return  test() ").Eval(parameters);
            var r2 = "test 0";
            Assert.Equal(r2, r1);
        }

        [Fact]
        public void custom_dynmethod_injected_as_parameter_receives_argument_count()
        {
            var parameters = new Dictionary<string, object>()
            {
                {
                    "test" ,
                    new DynMethod((DynLanContext, Parameters) =>
                    {
                        return new DynMethodResult("test " + (Parameters?.Count??0));
                    })
                }
            };
            var r1 = (String)new Compiler().Compile(" return  test(123) ").Eval(parameters);
            var r2 = "test 1";
            Assert.Equal(r2, r1);
        }

    }
}
