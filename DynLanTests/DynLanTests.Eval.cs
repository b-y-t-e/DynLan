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
    public class DynLanEvalTests
    {

        [Fact]
        public void test_value_on_last_line_should_make_result()
        {
            var r = new Compiler().Compile(@"
str = 'return 556'; eval(str);
");
            var v = r.Eval();
            Assert.Equal(556L, v);
        }

        [Fact]
        public void test_new_code_line()
        {
            {
                var r = new Compiler().Compile(@"
str = 'return 556'; return eval(str)
");
                var v = r.Eval();
                Assert.Equal(556L, v);
            }
        }

        [Fact]
        public void test1()
        {
            {
                var r = new Compiler().Compile(@"
str = 'return 556'
return eval(str)
");
                var v = r.Eval();
                Assert.Equal(556L, v);
            }

        }

        [Fact]
        public void eval_string_literal_returns_value()
        {

            var r = new Compiler().Compile(@"
return eval('return 556')
");
            var v = r.Eval();
            Assert.Equal(556L, v);
        }

        [Fact]
        public void eval_variable_holding_code_string_returns_value()
        {
            var r = new Compiler().Compile(@"
str = 'return 556'
return eval(str)
");
            var v = r.Eval();
            Assert.Equal(556L, v);
        }

        [Fact]
        public void eval_code_string_can_reference_outer_variable()
        {
            var r = new Compiler().Compile(@"
A = 3
str = 'return 556 + A'
return eval(str)
");
            var v = r.Eval();
            Assert.Equal(559L, v);
        }

        [Fact]
        public void eval_code_string_with_extra_whitespace_around_operators()
        {
            var r = new Compiler().Compile(@"
A = 3
str = 'return 556   +   A'
return eval(str)
");
            var v = r.Eval();
            Assert.Equal(559L, v);
        }

        [Fact]
        public void eval_lowercase_true_literal()
        {
            var r = new Compiler().Compile(@"
str = 'return true'
return eval(str)
");
            var v = r.Eval();
            Assert.Equal(true, v);
        }

        [Fact]
        public void eval_capitalized_true_literal()
        {
            var r = new Compiler().Compile(@"
str = 'return True'
return eval(str)
");
            var v = r.Eval();
            Assert.Equal(true, v);
        }

        [Fact]
        public void eval_lowercase_false_literal()
        {
            var r = new Compiler().Compile(@"
str = 'return false'
return eval(str)
");
            var v = r.Eval();
            Assert.Equal(false, v);
        }

        [Fact]
        public void eval_capitalized_false_literal()
        {
            var r = new Compiler().Compile(@"
str = 'return False'
return eval(str)
");
            var v = r.Eval();
            Assert.Equal(false, v);
        }

        [Fact]
        public void eval_with_multiline_code_string_built_using_newline_variable()
        {
            var complied = new Compiler().Compile(
                "F = 5.0" + nl +
                "F = eval('C = F + 2'+nl+'return C / 2')" + nl +
                "return F+1");
            var r1 = (decimal?)complied.Eval(GetParams().VARIABLES);
            Assert.Equal(4.5M, r1);
        }

        [Fact]
        public void eval_call_with_extra_whitespace_and_escaped_quote_in_nested_string()
        {
            var r1 = (String)new Compiler().Compile(" return  eval   (  ' return 5  +  6 + \\'!\\'   '  ) ").Eval(GetParams().VARIABLES);
            var r2 = 5 + 6 + "!";
            Assert.Equal(r2, r1);
        }

    }
}
