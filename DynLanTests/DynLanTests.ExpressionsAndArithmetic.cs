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
    public class DynLanExpressionsAndArithmeticTests
    {

        [Fact]
        public void test2()
        {
            {
                var r = new Compiler().Compile(@"
STR = '123'
str = '321'
return str + STR
");
                var v = r.Eval();
                Assert.Equal("321123", v);
            }

        }

        [Fact]
        public void one_line_statement()
        {
            {
                var r = new Compiler().Compile(@"return param1");

                var d = new Dictionary<string, object>();
                d["param1"] = 123;

                var v = r.Eval(d);
                Assert.Equal(123, v);
            }

        }

        [Fact]
        public void script_reads_input_dictionary_variable_by_name()
        {
            var r = new Compiler().Compile(@"
return j + 1
");
            var dict = new Dictionary<String, Object>();
            dict["j"] = 10;
            var v = r.Eval(dict);
            Assert.Equal(11L, v);
        }

        [Fact]
        public void str_function_converts_int_to_string()
        {
            var r = new Compiler().Compile(@"
return str(1)
");
            var v = r.Eval();
            Assert.Equal("1", v);
        }

        [Fact]
        public void string_literal_spanning_multiple_lines_preserves_newline()
        {
            var r = new Compiler().Compile(@"
def test2(txt)
{
  return txt
}
return test2('11(
)11')
");
            var v = r.Eval();
            Assert.Equal("11(" + Environment.NewLine + ")11", v);
        }

        [Fact]
        public void not_equal_null_comparison_is_true_for_non_null()
        {
            var r = new Compiler().Compile(@"
return 1 != null
");
            var v = r.Eval();
            Assert.Equal(true, v);
        }

        [Fact]
        public void equal_null_comparison_is_false_for_non_null()
        {
            var r = new Compiler().Compile(@"
return 1 == null
");
            var v = r.Eval();
            Assert.Equal(false, v);
        }

        [Fact]
        public void not_equal_undefined_comparison_is_true_for_non_null()
        {
            var r = new Compiler().Compile(@"
return 1 != undefined
");
            var v = r.Eval();
            Assert.Equal(true, v);
        }

        [Fact]
        public void equal_undefined_comparison_is_false_for_non_null()
        {
            var r = new Compiler().Compile(@"
return 1 == undefined
");
            var v = r.Eval();
            Assert.Equal(false, v);
        }

        [Fact]
        public void string_literal_with_hash_character_is_not_treated_as_comment()
        {
            var r = new Compiler().Compile(@"
return 'ab#c'
");
            var v = r.Eval();
            Assert.Equal("ab#c", v);
        }

        [Fact]
        public void hash_after_string_literal_starts_line_comment()
        {
            var r = new Compiler().Compile(@"return 'ab'#+'c'");
            var v = r.Eval();
            Assert.Equal("ab", v);
        }

        [Fact]
        public void variable_assigned_string_with_hash_character_round_trips()
        {
            var r = new Compiler().Compile(@"
i= 'ab#c'
return i
");
            var v = r.Eval();
            Assert.Equal("ab#c", v);
        }

        [Fact]
        public void trailing_line_comment_after_statement_is_ignored()
        {
            var r = new Compiler().Compile(@"
i = 2
i= 3 # + 1
return i
");
            var v = r.Eval();
            Assert.Equal(3L, v);
        }

        [Fact]
        public void full_line_comment_is_ignored()
        {
            var r = new Compiler().Compile(@"
i = 2
# i= 1
return i
");
            var v = r.Eval();
            Assert.Equal(2L, v);
        }

        [Fact]
        public void coalesce_falls_back_to_default_when_method_call_result_is_null()
        {
            var r = new Compiler().Compile(@"

v = null

def m1(v) {
  v = v + 'c'
  return v
}

return '!'+coalesce(m1(v),'kkk')+'@'
");
            var v = r.Eval();
            Assert.Equal("!kkk@", v);
        }

        [Fact]
        public void return_true_literal_with_external_variables_dictionary()
        {
            var complied = new Compiler().Compile("return true");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(true, r1);
        }

        [Fact]
        public void return_mixed_case_true_literal_with_external_variables_dictionary()
        {
            var complied = new Compiler().Compile("return tRUE");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(true, r1);
        }

        [Fact]
        public void return_false_literal_with_external_variables_dictionary()
        {
            var complied = new Compiler().Compile("return false");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(false, r1);
        }

        [Fact]
        public void return_mixed_case_false_literal_with_external_variables_dictionary()
        {
            var complied = new Compiler().Compile("return fALSe");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(false, r1);
        }

        [Fact]
        public void identifier_that_looks_like_true_but_is_misspelled_throws_when_undefined()
        {
            var complied = new Compiler().Compile("return trueee");
            Assert.Throws<DynLanExecuteException>(() => complied.Eval(GetParams().VARIABLES));
        }

        [Fact]
        public void identifier_that_looks_like_false_but_is_misspelled_throws_when_undefined()
        {
            var complied = new Compiler().Compile("return ffalse");
            Assert.Throws<DynLanExecuteException>(() => complied.Eval(GetParams().VARIABLES));
        }

        [Fact]
        public void identifier_that_looks_like_true_can_be_assigned_and_read_back_as_variable()
        {
            var complied = new Compiler().Compile("trueee = 33" + Environment.NewLine + "return trueee");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(33L, r1);
        }

        [Fact]
        public void logical_or_and_short_circuit_with_double_bar_and_double_ampersand_operators()
        {
            var complied = new Compiler().Compile("return (days(todate('2015-02-19') - todate('2015-02-17')) <= 7 || (days(todate('2015-02-19') - todate('2015-02-15')) <= 7 && 5==2))");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(true, r1);
        }

        [Fact]
        public void logical_or_and_short_circuit_with_lowercase_or_and_and_keywords()
        {
            var complied = new Compiler().Compile("return (days(todate('2015-02-19') - todate('2015-02-17')) <= 7 or (days(todate('2015-02-19') - todate('2015-02-15')) <= 7 and 5==2))");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(true, r1);
        }

        [Fact]
        public void logical_or_and_keywords_are_case_insensitive()
        {
            var complied = new Compiler().Compile("return (days(todate('2015-02-19') - todate('2015-02-17')) <= 7 oR (days(todate('2015-02-19') - todate('2015-02-15')) <= 7 ANd 5==2))");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(true, r1);
        }

        [Fact]
        public void chained_dotnet_method_calls_on_str_result_substring_of_tostring()
        {
            var complied = new Compiler().Compile("return str(434.5).ToString().ToString().Substring(0,3)");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal("434", r1);
        }

        [Fact]
        public void decimal_plus_dotnet_tostring_of_decimal_concatenates_as_string()
        {
            var complied = new Compiler().Compile("return 4324.6546 + (4234.55).ToString()");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal("4324.6546" + (4234.55).ToString(), r1);
        }

        [Fact]
        public void coalesce_with_at_symbol_call_syntax_inside_arithmetic_expression()
        {
            var complied = new Compiler().Compile("return (0-(1-2+2)+((coalesce@(null,4-1))))");
            var r1 = complied.Eval(GetParams().VARIABLES);
            Assert.Equal(2L, r1);
        }

        [Fact]
        public void string_concatenated_with_int_literal_produces_string()
        {
            var complied = new Compiler().Compile("a = '321312' + 6" + nl + "return a");
            var r1 = (String)complied.Eval(GetParams().VARIABLES);
            Assert.Equal("3213126", r1);
        }

        [Fact]
        public void int_plus_dotnet_tostring_of_subtraction_result_concatenates_as_string()
        {
            var complied = new Compiler().Compile(" return 5 + (4-2).ToString()  ");
            var r1 = (String)complied.Eval(GetParams().VARIABLES);
            Assert.Equal("52", r1);
        }

        [Fact]
        public void coalesce_with_at_symbol_call_syntax_repeated_arithmetic_expression()
        {
            var complied = new Compiler().Compile("return (0-(1-2+2)+((coalesce@(null,4-1))))");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(2L, r1);
        }

        [Fact]
        public void coalesce_with_parenthesized_call_syntax_inside_arithmetic_expression()
        {
            var complied = new Compiler().Compile("return (0-(1-2+2)+((coalesce(null,4-1))))");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(2L, r1);
        }

        [Fact]
        public void arithmetic_result_concatenated_with_substring_of_string_literal()
        {
            var complied = new Compiler().Compile("return ((1-2+2)+((('aa').Substring(0,4-3))))");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal("1a", r1);
        }

        [Fact]
        public void arithmetic_result_concatenated_with_substring_of_concatenated_string()
        {
            var complied = new Compiler().Compile("return ((1-2+2)+((('aa'+'a').Substring(0,4-2))))");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal("1aa", r1);
        }

        [Fact]
        public void substring_arguments_computed_from_dotnet_length_property_and_arithmetic()
        {
            var complied = new Compiler().Compile("return ((1-2+2)+((('ab'+'c').Substring('b'.Length+1-1-1+1,4-2))))");
            var r1 = (Object)complied.Eval(GetParams().VARIABLES);
            Assert.Equal("1bc", r1);
        }

        [Fact]
        public void coalesce_with_multiple_decimal_candidate_arguments_returns_first_non_null()
        {
            var complied = new Compiler().Compile("return 3+coalesce(null,(4.5-1),(5))+7");
            var r1 = (Decimal)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(13.5M, r1);
        }

        [Fact]
        public void string_literal_concatenated_with_substring_call_result()
        {
            var complied = new Compiler().Compile("return 'a'+'abc'.Substring(2)");
            var r1 = (String)complied.Eval(GetParams().VARIABLES);
            Assert.Equal("a" + "c", r1);
        }

        [Fact]
        public void round_function_rounds_decimal_to_given_number_of_places()
        {
            var complied = new Compiler().Compile("return  round(33.3333,2) ");
            var r1 = complied.Eval(GetParams().VARIABLES);
            Assert.Equal(33.33M, r1);
        }

        [Fact]
        public void chained_assignment_sets_both_variables_to_same_value()
        {
            var paramsObj = GetParams();
            var complied = new Compiler().Compile("return  JJJ = HHH2 = 33-3   ");
            var r1 = complied.Eval(paramsObj.VARIABLES);
            Assert.Equal(30L, r1);
            Assert.Equal(30L, paramsObj.VARIABLES["HHH2"]);
            Assert.Equal(30L, paramsObj.VARIABLES["JJJ"]);
        }

        [Fact]
        public void round_function_matches_dotnet_math_round_for_division_result()
        {
            var r1 = new Compiler().Compile("return  100 + round(384128491.32 / 3313, 4) ").Eval();
            var r2 = 100 + Math.Round(384128491.32M / 3313.0M, 4);
            Assert.Equal(r2, r1);
        }

        [Fact]
        public void assignment_combined_with_coalesce_of_null_default_and_arithmetic()
        {
            var paramsObj = GetParams();
            var complied = new Compiler().Compile("return  HHH = 33-3  + coalesce(null, 0) ");
            var r1 = complied.Eval(paramsObj.VARIABLES);
            Assert.Equal(30L, r1);
            Assert.Equal(30L, paramsObj.VARIABLES["HHH"]);
        }

        [Fact]
        public void return_null_literal_yields_null_result()
        {
            var r1 = new Compiler().Compile("return null").Eval(GetParams().VARIABLES);
            Assert.Null(r1);
        }

        [Fact]
        public void empty_script_body_yields_null_result()
        {
            var r1 = new Compiler().Compile("").Eval(GetParams().VARIABLES);
            Assert.Null(r1);
        }

        [Fact]
        public void escaped_quotes_in_string_literal_multiplied_by_int_repeats_string()
        {
            var r1 = (String)new Compiler().Compile(" return   '\\'\\'\"' * 2 ").Eval(GetParams().VARIABLES);
            var r2 = "''\"''\"";
            Assert.Equal(r2, r1);
        }

        [Fact]
        public void escaped_quotes_in_string_literal_multiplied_by_int_repeats_string_duplicate()
        {
            var r1 = (String)new Compiler().Compile(" return  '\\'\\'\"' * 2 ").Eval(GetParams().VARIABLES);
            var r2 = "''\"''\"";
            Assert.Equal(r2, r1);
        }

    }
}
