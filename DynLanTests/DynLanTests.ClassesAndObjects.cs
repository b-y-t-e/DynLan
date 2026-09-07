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
    public class DynLanClassesAndObjectsTests
    {

        [Fact]
        public void test_class()
        {
            {
                var r = new Compiler().Compile(@"
class testowa()
{
  a = 1
}
return testowa().a
");
                var v = r.Eval();
                Assert.Equal(1L, v);
            }
        }

        [Fact]
        public void class_method_can_accept_this_as_explicit_null_parameter()
        {
            var r = new Compiler().Compile(@"
class testowa()
{
  a = 1
  c = 3
  def oo(this)
  {
    return this
  } 
}
return testowa().oo(null)
");
            var v = r.Eval();
            Assert.Null(v);
        }

        [Fact]
        public void class_method_reads_field_via_this_when_call_arg_is_null()
        {
            var r = new Compiler().Compile(@"
class testowa()
{
  a = 1
  c = 3
  def oo()
  {
    return this.a
  }
}
return testowa().oo(null)
");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void class_method_invoked_dynamically_via_string_concatenated_indexer_name()
        {
            var r = new Compiler().Compile(@"
DEF SET_OBJECT_VALUE(obj, field, value)
{
  obj['INTERNAL_'+field](value)
}
clASS testowa()
{
  a = 1
  b = 10
  def INTERNAL_A(vv){
    this.a = vv + this.b
  }
  def setA(v) {
    SET_OBJECT_VALUE(this, 'A', v)
  }
}
obj = testowa()
obj.setA(22)
RETURN obj.a
");
            var v = r.Eval();
            Assert.Equal(32L, v);
        }

        [Fact]
        public void class_method_indirectly_sets_field_via_helper_function_and_indexer_call()
        {
            var r = new Compiler().Compile(@"
def SET_OBJECT_VALUE(obj, field, value){
  obj['INTERNAL_'+field](value)
}
class testowa()
{
  a = 1
  def INTERNAL_A(vv) {
    this.a = vv
  }
  def setA(v) {
    SET_OBJECT_VALUE(this, 'A', v)
  }
}
obj = testowa()
obj.setA(22)
return obj.a
");
            var v = r.Eval();
            Assert.Equal(22L, v);
        }

        [Fact]
        public void class_constructor_body_uses_coalesce_on_omitted_parameters()
        {
            var r = new Compiler().Compile(@"
class testowa(a,b,c) {
  return coalesce(a,0) + coalesce(b,0) + coalesce(c,1)
}
return testowa()
 
");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void class_constructor_body_returns_null_when_parameters_undefined()
        {
            var r = new Compiler().Compile(@"
class testowa(a,b,c) {
  return a + b + c
}
return testowa()
 
");
            var v = r.Eval();
            Assert.Null(v);
        }

        [Fact]
        public void class_constructor_body_can_contain_if_else_on_parameters()
        {
            var r = new Compiler().Compile(@"
class testowa(a,b,c) {
  if a != undefined {
    return 0
  }
  else {
    return 1
  }
}
return testowa()
 
");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void class_method_invoked_via_bracket_indexer_syntax_with_and_without_arguments()
        {
            var r = new Compiler().Compile(@"
class testowa() {
  a = 1
  c = 3
  def oo(v) {
    if v == undefined {
      return this.a
    }
    return this.c
  }
}
obj = testowa()
return obj['oo']() + obj['oo'](1)
");
            var v = r.Eval();
            Assert.Equal(4L, v);
        }

        [Fact]
        public void class_method_invoked_via_dot_syntax_with_and_without_arguments()
        {
            var r = new Compiler().Compile(@"
class testowa()
{
  a = 1
  c = 3
  def oo(v)
  {
    if v == undefined {
      return this.a
    }
    return this.c
  } 
}
obj = testowa()
return obj.oo() + obj.oo(1)
");
            var v = r.Eval();
            Assert.Equal(4L, v);
        }

        [Fact]
        public void top_level_method_is_not_reachable_as_a_class_instance_method()
        {
            var r = new Compiler().Compile(@"
def oo3()
{
  return this.a + 3
}
class testowa()
{
  a = 2
  c = 1
  def oo2() {
    return this.a + 3
  }
}
obj = testowa()
return obj.oo3()
");
            // method oo3 should not be reachable from testowa's instance - either exception type is acceptable
            try
            {
                var v = r.Eval();
                Assert.Fail("method oo3 nie powinna być dostępna!");
            }
            catch (DynLanExecuteException)
            {
                // ok
            }
            catch (DynLanMethodNotFoundException)
            {
                // ok
            }
        }

        [Fact]
        public void class_instance_calls_second_of_two_sibling_methods()
        {
            var r = new Compiler().Compile(@"
class testowa(){
  a = 2
  c = 1
  def oo1() {
    return this.a + 1
  }
  def oo2() {
    return this.a + 2
  }
}
obj = testowa()
return obj.oo2()
");
            var v = r.Eval();
            Assert.Equal(4L, v);
        }

        [Fact]
        public void class_method_branches_on_whether_optional_argument_is_undefined()
        {
            var r = new Compiler().Compile(@"
class testowa() {
  a = 2
  c = 1
  def oo(v) {
    if v == undefined {
      return 1
     }
    return 3
  }
}
obj = testowa()
return obj.oo() + obj.oo(1)
");
            var v = r.Eval();
            Assert.Equal(4L, v);
        }

        [Fact]
        public void class_instance_calls_second_of_two_sibling_methods_using_field_in_expression()
        {
            var r = new Compiler().Compile(@"
class testowa() {
  a = 2
  c = 1
  def oo1() {
    return this.a + 1
  }
  def oo3() {
    return this.a + 3
  }
}
obj = testowa()
return obj.oo3()
");
            var v = r.Eval();
            Assert.Equal(5L, v);
        }

        [Fact]
        public void class_method_body_return_statement_short_circuits_before_nested_def()
        {
            var r = new Compiler().Compile(@"
class testowa() {
  a = 2
  c = 1
  def oo() {
    return this.a + this.c * 10
    def oo2() {
      return 33
    }
   }
}
obj = testowa()
return obj.oo()
");
            var v = r.Eval();
            Assert.Equal(12L, v);
        }

        [Fact]
        public void class_fields_assigned_via_this_in_constructor_body_are_visible_on_instance()
        {
            var r = new Compiler().Compile(@"

class klasa1() {
  def m1() {
    this.b = 66
    return this.a + 1
   }
  this.a = 1
  this.b = 2
}
o1 = klasa1()
return str(o1.a) + ' ' + str(o1.m1())+ ' ' + str(o1.b)
");
            var v = r.Eval();
            Assert.Equal("1" + " " + "2" + " " + "66", v);
        }

        [Fact]
        public void class_fields_assigned_as_plain_names_in_constructor_body_are_visible_on_instance()
        {
            var r = new Compiler().Compile(@"

class klasa1() 
{
  def m1() {
    this.b = 88
    return this.a + 1
  }
  a = 1
  b = 2
}
o1 = klasa1()
return str(o1.a) + ' ' + str(o1.m1())+ ' ' + str(o1.b)
");
            var v = r.Eval();
            Assert.Equal("1" + " " + "2" + " " + "88", v);
        }

        [Fact]
        public void plain_def_returning_this_behaves_like_a_class_constructor()
        {
            var r = new Compiler().Compile(@"

def klasa1() {
  a = 1
  b = 2
  return this
}
o1 = klasa1()
return o1.a
");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void nested_def_reachable_via_this_dynamicvalues_indexer_returns_value()
        {
            var r = new Compiler().Compile(@"
def AA() {
  def a() {
    return 77
  }
  G = 3
  H = 7
  return this
}
return AA().DynamicValues['a']()
");
            var v = r.Eval();
            Assert.Equal(77L, v);
        }

        // Byte-for-byte duplicate of test_0074 - kept as-is (numbered test, not renamed/removed per migration policy).
        [Fact]
        public void nested_def_reachable_via_this_dynamicvalues_indexer_returns_value_duplicate()
        {
            var r = new Compiler().Compile(@"
def AA() {
  def a() {
    return 77
  }
  G = 3
  H = 7
  return this
}
return AA().DynamicValues['a']()
");
            var v = r.Eval();
            Assert.Equal(77L, v);
        }

        [Fact]
        public void plain_variable_reachable_via_this_dynamicvalues_indexer()
        {
            var r = new Compiler().Compile(@"
def AA() {
  G = 3
  H = 7
  return this
}
return AA().DynamicValues['G']
");
            var v = r.Eval();
            Assert.Equal(3L, v);
        }

    }
}
