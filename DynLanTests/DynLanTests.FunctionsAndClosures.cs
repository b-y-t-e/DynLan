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
    public class DynLanFunctionsAndClosuresTests
    {

        [Fact]
        public void test_many_methods_and_result_in_one_line()
        {
            {
                var r = new Compiler().Compile(@"def aa() { return 1 } def bb(){ return 2} return aa() + bb() ");
                var v = r.Eval();
                Assert.Equal(3L, v);
            }
        }

        [Fact]
        public void test_many_methods_and_result_in_one_line_with_brackets()
        {
            {
                var r = new Compiler().Compile(@"def aa() {a = 1;   return a + 0}  def bb() { return 2;}    return aa() + bb() ");
                var v = r.Eval();
                Assert.Equal(3L, v);
            }
        }

        [Fact]
        public void user_method_with_parameter_named_s_adds_one()
        {
            var r = new Compiler().Compile(@"
def method1(s)
{
  return s+1
}
return method1(1)
");
            var v = r.Eval();
            Assert.Equal(2L, v);
        }

        [Fact]
        public void method_parameter_can_be_named_str_and_shadows_builtin()
        {
            var r = new Compiler().Compile(@"
def method1(str)
{
  return str+1
}
return method1(1)
");
            var v = r.Eval();
            Assert.Equal(2L, v);
        }

        [Fact]
        public void method_parameter_named_str_does_not_break_later_call_to_builtin_str()
        {
            var r = new Compiler().Compile(@"
def method1(str)
{
  return str+1
}
return method1(1)+str(2)
");
            var v = r.Eval();
            Assert.Equal("22", v);
        }

        [Fact]
        public void user_defined_method_can_be_named_str_and_overrides_builtin()
        {
            var r = new Compiler().Compile(@"
def str(v)
{
  return v+1
}
return str(1)
");
            var v = r.Eval();
            Assert.Equal(2L, v);
        }

        [Fact]
        public void user_defined_method_can_be_named_test2_and_returns_argument()
        {
            var r = new Compiler().Compile(@"
def test2(txt)
{
  return txt
}
return test2('111')
");
            var v = r.Eval();
            Assert.Equal("111", v);
        }

        [Fact]
        public void this_refers_to_script_local_variables_at_top_level()
        {
            var r = new Compiler().Compile(@"
a = 1
b = 2
return this.a
");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void this_can_be_reassigned_as_a_plain_variable()
        {
            var r = new Compiler().Compile(@"
a = 1
b = 2
this = 5
return this
");
            var v = r.Eval();
            Assert.Equal(5L, v);
        }

        [Fact]
        public void if_not_equal_undefined_is_false_when_argument_omitted_as_null()
        {
            var r = new Compiler().Compile(@"
def testowa(a,b,c)
{
  if a != undefined {
    return 0
  }
  else {
    return 1
   }
}
return testowa(null)
 
");
            var v = r.Eval();
            Assert.Equal(0L, v);
        }

        [Fact]
        public void if_equal_undefined_is_true_when_argument_passed_as_null()
        {
            var r = new Compiler().Compile(@"
def testowa(a,b,c){
  if a == undefined
  {
    return 0
  }
  else
  {
    return 1
  }
}
return testowa(null)
 
");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void if_equal_undefined_is_true_when_null_variable_passed_as_argument()
        {
            var r = new Compiler().Compile(@"
d = null
def testowa(a,b,c){
  if a == undefined {
    return 0 }
  else {
    return 1
  }
}
return testowa(d)
 
");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void if_equal_undefined_is_false_when_undefined_variable_passed_as_argument()
        {
            var r = new Compiler().Compile(@"
d = undefined
def testowa(a,b,c)
{
  if a == undefined
{
    return 0
}
  else
   {
    return 1
}
}
return testowa(d)
 
");
            var v = r.Eval();
            Assert.Equal(0L, v);
        }

        [Fact]
        public void if_not_equal_undefined_is_false_when_argument_not_supplied_at_all()
        {
            var r = new Compiler().Compile(@"
def testowa(a,b,c)
{
  if a != undefined {
    return 0
}
  else
{
    return 1
}
}
return testowa()
 
");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void nested_method_cannot_see_local_variable_of_sibling_caller_method()
        {
            var r = new Compiler().Compile(@"
def bb(){
  def cc()
  {
    return coalesce(a,0)
  }
  return cc()
}

def aa()
{
  a = 1
  return bb()
}

return aa()

");
            Assert.Throws<DynLanExecuteException>(() => r.Eval());
        }

        [Fact]
        public void nested_method_sees_parameter_passed_explicitly_through_call_chain()
        {
            var r = new Compiler().Compile(@"
def bb(a){
  def cc() {
    return coalesce(a,0)+1
  }
  return cc()
}

def aa()
{
  b = 1
  return bb(b)
}
return aa()

");
            var v = r.Eval();
            Assert.Equal(2L, v);
        }

        [Fact]
        public void nested_method_sees_global_script_level_variable_defined_after_def()
        {
            var r = new Compiler().Compile(@"
def bb()
{
  def cc()
  {
    return coalesce(a,0)
  }
  return cc()
}
a = 1

def aa()
{
  return bb()
}

return aa()

");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void deeply_nested_method_cannot_see_enclosing_methods_local_variable()
        {
            var r = new Compiler().Compile(@"
def cc():
{
  return coalesce(a,0)
}
def aa(b)
{
  a = 1
  def bb()
  {
    return cc()
  }
  return bb()
}
return aa()

");
            // 'a' should not be visible inside cc() -> only local/class/global variables are visible
            Assert.Throws<DynLanExecuteException>(() => r.Eval());
        }

        [Fact]
        public void deeply_nested_method_sees_local_variable_of_top_level_calling_method()
        {
            var r = new Compiler().Compile(@"
def aa(b){
  a = 1
  def bb(){
    def cc(){
      return coalesce(a,0)
    } 
    return cc()
  }
  return bb()
}
return aa()
");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void method_local_assignment_does_not_mutate_outer_global_variable()
        {
            var r = new Compiler().Compile(@"
d = 100
def METHOD_A() {
 d = 1
}
METHOD_A()
RETURN d
");
            var v = r.Eval();
            Assert.Equal(100L, v);
        }

        [Fact]
        public void nested_method_combines_global_local_and_parameter_variables()
        {
            var r = new Compiler().Compile(@"
d = 100
a = 1
def METHOD_A(d){
 b = 1
 def METHOD_B() { 
  c = 1
  return a + coalesce(b,0) + c + coalesce(d,0)
 }
 return METHOD_B()
}
i = METHOD_A(1)
RETURN i
");
            var v = r.Eval();
            Assert.Equal(4L, v);
        }

        [Fact]
        public void nested_method_sees_global_variable_declared_after_call_site()
        {
            var r = new Compiler().Compile(@"
c = 10
def aA(){
 def bB(){ 
  return 1 + c + d
 }
 return bB() + 2
}
d = 20
i = aA()
RETURN i
");
            var v = r.Eval();
            Assert.Equal(33L, v);
        }

        [Fact]
        public void one_method_calls_another_method_passing_computed_argument()
        {
            var r = new Compiler().Compile(@"
def f1(v1) {
  return f2(v1 + 1)
}
def f2(v2) {
  return v2 * 100
}
return f1(100)
");
            var v = r.Eval();
            Assert.Equal(10100L, v);
        }

        [Fact]
        public void concatenating_string_with_method_call_that_has_no_return_yields_null()
        {
            var r = new Compiler().Compile(@"
def m1(v) {
  v = v + 'c'
}
v = 'abc'
return '!'+m1(v)+'@'
");
            var v = r.Eval();
            Assert.Null(v);
        }

        [Fact]
        public void method_return_value_is_used_directly_in_string_concatenation()
        {
            var r = new Compiler().Compile(@"
def m1(v) {
  v = v + 'c'
  return v
}
v = 'abc'
return m1(v)
");
            var v = r.Eval();
            Assert.Equal("abcc", v);
        }

        [Fact]
        public void method_with_multiple_spaces_between_name_and_parameters_computes_decimal_result()
        {
            var r = new Compiler().Compile(@"
gg = 100.0
def   AAA(v1,    v2)
{
  v2 = v2 + 1
  return (v1* 1000 + v2) / gg
}
FF = AAA(2,3) 
return FF
");
            var v = r.Eval();
            Assert.Equal(20.04M, v);
        }

        [Fact]
        public void method_referencing_undefined_variable_from_unrelated_sibling_scope_throws()
        {
            var r = new Compiler().Compile(@"
def f1(v1,v2){
  return f2(100)
}
def f2(v2){
  return v1}
return f1(100, 200)
");
            Assert.Throws<DynLanExecuteException>(() => r.Eval());
        }

        [Fact]
        public void method_sees_null_global_variable_with_same_name_as_unrelated_caller_parameter()
        {
            var r = new Compiler().Compile(@"
v1 = null
def f1(v1,v2){
  return f2(100)
}
def f2(v2)
{
  return v1
}
return f1(100, 200)
");
            var v = r.Eval();
            Assert.Null(v);
        }

        [Fact]
        public void local_variable_named_a_in_two_methods_does_not_conflict_across_calls()
        {
            var r = new Compiler().Compile(@"
def f1(b) {
  a = 100
  b = f2(b + 1)
  return a + b
}
def f2(b) {
  a = 1000
  return b * 100
}
return f1(1)
");
            var v = r.Eval();
            Assert.Equal(300L, v);
        }

    }
}
