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
    public class DynLanCollectionsAndIndexersTests
    {

        [Fact]
        public void test_list_as_list()
        {
            {
                var r = new Compiler().Compile(@"
list = list()
return list
");
                var v = r.Eval();
                Assert.IsAssignableFrom<IList>(v);
            }
        }

        [Fact]
        public void list_of_dictionaries_built_with_dictionary_and_list_builtins()
        {
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

return list
");
            var v = r.Eval();
            Assert.IsAssignableFrom<IList>(v);
            Assert.Equal(2, (v as IList).Count);
            Assert.IsAssignableFrom<IDictionary>((v as IList)[0]);
            Assert.Equal(DateTime.Now.Date, ((DateTime)((v as IList)[0] as IDictionary)["Created"]));
        }

        [Fact]
        public void dotnet_method_call_chained_directly_on_dictionary_field_access()
        {
            var complied = new Compiler().Compile("return DICT.Pole1.Substring(0,1)");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal("p", r1);
        }

        [Fact]
        public void dictionary_field_accessed_via_dot_syntax_returns_its_value()
        {
            var complied = new Compiler().Compile("return DICT.Pole1");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal("p1", r1);
        }

        [Fact]
        public void assigning_new_dictionary_field_via_dot_syntax_and_reading_it_back_in_sum()
        {
            var complied = new Compiler().Compile("DICT.Pole3 = 77" + nl + "return DICT.Pole2 + DICT.Pole3");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(77L + 56L, r1);
        }

        [Fact]
        public void assigning_property_on_list_element_selected_by_computed_round_index()
        {
            var paramsObj = GetParams();
            var complied = new Compiler().Compile(" return OSOBY[round(33.2-32)].Imie = 'sadasda!' ");
            var r1 = (String)complied.Eval(paramsObj.VARIABLES);
            Assert.Equal("sadasda!", r1);
            Assert.Equal("sadasda!", paramsObj.OSOBY[1].Imie);
        }

        [Fact]
        public void assignment_to_arraylist_index_with_literal_zero_mutates_source_list()
        {
            var paramsObj = GetParams();
            var complied = new Compiler().Compile(" return LIST[ 0 ] = 'abc'+'h' ");
            var r1 = (String)complied.Eval(paramsObj.VARIABLES);
            Assert.Equal("abc" + "h", r1);
            Assert.Equal("abc" + "h", paramsObj.LIST[0]);
        }

        [Fact]
        public void assignment_to_list_index_computed_from_arithmetic_expression()
        {
            var paramsObj = GetParams();
            var complied = new Compiler().Compile("return  LIST[ 0+100-(90+10) ] = 'abc'+'h' ");
            var r1 = (string)complied.Eval(paramsObj.VARIABLES);
            Assert.Equal("abc" + "h", r1);
            Assert.Equal("abc" + "h", paramsObj.LIST[0]);
        }

        [Fact]
        public void assignment_to_dictionary_key_computed_from_nested_arithmetic_and_coalesce()
        {
            var paramsObj = GetParams();
            var complied = new Compiler().Compile("return  DICT[ ( 0- (1-2+1) + ((coalesce(null,4-1))) ) ] = 33 ");
            var r1 = complied.Eval(paramsObj.VARIABLES);
            Assert.Equal(33L, r1);
            Assert.Equal(33L, paramsObj.DICT["3"]);
        }

        [Fact]
        public void assignment_to_dictionary_key_computed_as_int_literal_subtraction()
        {
            var paramsObj = GetParams();
            var complied = new Compiler().Compile("return  DICT[4-1] = 33 ");
            var r1 = (Int64)complied.Eval(paramsObj.VARIABLES);
            Assert.Equal(33L, r1);
            Assert.Equal(33L, paramsObj.DICT["3"]);
        }

        [Fact]
        public void indexing_string_literal_with_literal_index_returns_char()
        {
            var r1 = (Char?)new Compiler().Compile("return  'abc'[1] ").Eval(GetParams().VARIABLES);
            var r2 = "abc"[1];
            Assert.Equal(r2, r1);
        }

        [Fact]
        public void indexing_string_literal_with_coalesce_and_floor_computed_index_returns_char()
        {
            var p = new Compiler().Compile("return  'abc'[coalesce(2+null,floor(2.9))] ");
            var r1 = (Char?)p.Eval(GetParams().VARIABLES);
            var r2 = "abc"[2];
            Assert.Equal(r2, r1);
        }

        [Fact]
        public void indexing_dictionary_with_int_key_returns_value()
        {
            var r1 = (String)new Compiler().Compile("return  DICT[2] ").Eval(GetParams().VARIABLES);
            //var r2 = "jjj";
            Assert.Equal("jjj", r1);
        }

    }
}
