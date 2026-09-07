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
    public class DynLanTryCatchAndErrorsTests
    {

        [Fact]
        public void test_try_catch()
        {
            {
                var r = new Compiler().Compile(@"

def hhhh(a) {
  try {
    a = 1 + h.getdate()
  }
  catch ex {
    throw ex.Message+'!'
  }
}
j = ''
try {
  hhhh(j)
}
catch (ex) 
{
  j = ex.Message
}
return j
");
                var v = r.Eval();
                Assert.IsType<string>(v);
                Assert.EndsWith("!", (string)v);
            }
        }

        [Fact]
        public void test3()
        {
            {
                var r = new Compiler().Compile(@"

return str1 + STR1
");
                Assert.Throws<DynLanExecuteException>(() => r.Eval());
            }

        }

        [Fact]
        public void calling_method_on_undefined_object_variable_throws()
        {
            var r = new Compiler().Compile(@"

if not(cip.CMDON())
{
  return 1
}
");
            Assert.Throws<DynLanExecuteException>(() => r.Eval());
        }

        [Fact]
        public void unhandled_exception_inside_try_block_without_catch_propagates()
        {
            var r = new Compiler().Compile(@"

a = 0
try{
  a = 1 + h.getdate()
}
a = a + 1
return a
");
            Assert.Throws<DynLanExecuteException>(() => r.Eval());
        }

        [Fact]
        public void exception_thrown_in_nested_method_call_is_caught_by_outer_try_catch()
        {
            var r = new Compiler().Compile(@"

def hhhh(a) {
  try {
    a = 1 + h.getdate()
  }
  catch ex {
    throw ex.Message+'!'
  }
}
j = ''
try {
  hhhh(j)
}
catch (ex) 
{
  j = ex.Message
}
return j
");
            var v = r.Eval();
            Assert.IsType<string>(v);
            Assert.EndsWith("!", (string)v);
        }

        [Fact]
        public void syntax_error_inside_try_block_is_caught()
        {
            var r = new Compiler().Compile(@"
j = 1
try {
  j = h.dd dd()
}
catch (ex) {
  j = 2
}
return j
");
            var v = r.Eval();
            Assert.Equal(2L, v);
        }

        [Fact]
        public void try_block_without_exception_still_executes_statements_after_catch()
        {
            var r = new Compiler().Compile(@"
j = 1
try {
  j = j + 1
}
catch (ex) {
  j = j + 1
}
j = j + 1
return j
");
            var v = r.Eval();
            Assert.Equal(3L, v);
        }

        [Fact]
        public void catch_variable_captures_the_thrown_exception_object()
        {
            var r = new Compiler().Compile(@"

def hhhh(a)
{
  a = 1 + h.getdate()
  return a
}
j = 1
try {
  j = j + 1
  hhhh(j)
}
catch (ex) {
  j = ex
}
return j
");
            var v = r.Eval();
            Assert.IsAssignableFrom<Exception>(v);
        }

        [Fact]
        public void catch_block_runs_and_can_read_variable_set_before_try()
        {
            var r = new Compiler().Compile(@"
a = 4
try
{
  g = a + 5 + 7 - h.getdate() + 1
  a = 10
}
catch ex
{
  a = a + 1
}
return a
");
            var v = r.Eval();
            Assert.Equal(5L, v);
        }

        [Fact]
        public void nested_try_catch_inner_catch_handles_exception_before_outer()
        {
            var r = new Compiler().Compile(@"
a = 4
try
{
  try {
    g = h.getdate() + 1
    a = 5
  }
  catch {
    a = 10
  }
}
catch
{
  a = 6
}
return a
");
            var v = r.Eval();
            Assert.Equal(10L, v);
        }

    }
}
