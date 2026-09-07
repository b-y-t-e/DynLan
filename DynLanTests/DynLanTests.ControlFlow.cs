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
    public class DynLanControlFlowTests
    {

        [Fact]
        public void test_if_scenario_2()
        {
            {
                var r = new Compiler().Compile(@"
if 1 == 2 {
  return 1
}
return 2
");
                var v = r.Eval();
                Assert.Equal(2L, v);
            }
        }

        [Fact]
        public void test_if_scenario_1()
        {
            {
                var r = new Compiler().Compile(@"
if 1 == 1
{
  return 1
}
return 2
");
                var v = r.Eval();
                Assert.Equal(1L, v);
            }

        }

        [Fact]
        public void pass_keyword_before_statement_still_executes_that_statement()
        {
            var r = new Compiler().Compile(@"
i = 1
pass i = i + 1
i = i + 1
pass
i = i + 2
pass i = i + 1
RETURN i
");
            var v = r.Eval();
            Assert.Equal(4L, v);
        }

        [Fact]
        public void while_loop_writes_back_final_value_of_input_dictionary_variable()
        {
            var r = new Compiler().Compile(@"
j = 0
i = 0
while j < 2
{
  i = i + 1
  j = j + 1
  pass j = j + 1
  if i > 1000000 {
    i = 0
  }
}
");
            var dict = new Dictionary<String, Object>();
            dict["j"] = 0;
            var v = r.Eval(dict);
            Assert.Equal(2L, dict["j"]);
        }

        [Fact]
        public void while_loop_without_explicit_return_writes_final_value_to_output_dictionary()
        {
            var values = new Dictionary<string, object>();
            var r = new Compiler().Compile(@"
i = 0
while i < 3{
  i = i + 1
}
");
            var c = r.Eval(values);
            Assert.Equal(3L, values["i"]);
        }

        [Fact]
        public void if_elif_else_with_method_reference_variable_and_while_loop_before_it()
        {
            var r = new Compiler().Compile(@"
a = 4444

def hh() {
  return 999
}

i=3
while i>=0 {
  i = i -2
  i = i + 1 
}

gg = hh
if gg() == eval('return 998+1') {
  a = 1
}
elif gg == 6 {
  a = 3
}
else {
  a = 2
}
return a
");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void nested_while_loops_accumulate_counter_across_outer_and_inner_iterations()
        {
            var r = new Compiler().Compile(@"
a = 0
b = 0
c = 0
while a < 4
{
  a = a + 1
  b = 0
  while b < 2 {
    b = b + 1
    c = c + 1
}}
return c
");
            var v = r.Eval();
            Assert.Equal(8L, v);
        }

        [Fact]
        public void method_with_multiple_if_returns_picks_matching_branch_for_value_2()
        {
            var r = new Compiler().Compile(@"
def m1(v) {
  if v == 1 {
    return 'jeden'
  }
  if v == 2 {
    return 'dwa' 
  }
  return 'nic'
}
return m1(2)
");
            var v = r.Eval();
            Assert.Equal("dwa", v);
        }

        [Fact]
        public void method_with_multiple_if_returns_falls_through_to_default_branch()
        {
            var r = new Compiler().Compile(@"
def m1(v) {
  if v == 1 {
    return 'jeden'
  }
  if v == 2 {
    return 'dwa'
  }
  return 'nIc'
}
return m1(4324234)
");
            var v = r.Eval();
            Assert.Equal("nIc", v);
        }

        [Fact]
        public void if_elif_else_with_braces_on_same_line_as_return_takes_else_branch()
        {
            var r = new Compiler().Compile(@"
def m1(v){
  if v == 1{
    return 'jeden'}
  elif v == 2{
    return 'dwa'}
  else {
    return 'nic'}
}
return m1(3)
");
            var v = r.Eval();
            Assert.Equal("nic", v);
        }

        [Fact]
        public void deeply_nested_if_elif_inside_elif_selects_innermost_elif_branch()
        {
            var r = new Compiler().Compile(@"
a = 7
gg = 6
if gg == 5{
  a = 1}
elif gg == 6{
  if 4==8/2{
    if 4==8/22{
      a = 2}
    elif 4==8/2{
      a = 33}
   }
  elif 4==8/4{
    a = 3}
}
elif 4==8/4{
  a = 3}
else{
  a = 8}
return a
");
            var v = r.Eval();
            Assert.Equal(33L, v);
        }

        [Fact]
        public void deeply_nested_if_elif_inside_elif_selects_innermost_if_branch()
        {
            var r = new Compiler().Compile(@"
a = 7
gg = 6
if gg == 5{
  a = 1}
elif gg == 6 {
  if 4==8/2
  {
    if 4==8/2
    {
      a = 22
    }
    elif 4==8/4
    {
      a = 3
    }
  }
  elif 4==8/4
  {
    a = 3
  }
}
elif 4==8/4
{
  a = 3
}
else
{
  a = 8
}
return a
");
            var v = r.Eval();
            Assert.Equal(22L, v);
        }

        [Fact]
        public void deeply_nested_if_elif_falls_through_all_branches_to_outer_else()
        {
            var r = new Compiler().Compile(@"
a = 7
gg = 3
if gg == 5 {
  a = 1
}
elif gg == 6
{
  if 4==8/4
   {
    a = 2
  }
  elif 4==8/4 {
    a = 3
   }
}
elif 4==8/4
{
  a = 3
}
else
{
  a = 8
}
return a
");
            var v = r.Eval();
            Assert.Equal(8L, v);
        }

        [Fact]
        public void nested_if_elif_condition_false_leaves_outer_variable_unchanged()
        {
            var r = new Compiler().Compile(@"
a = 7
gg = 6
if gg == 5
{
  a = 1
}
elif gg == 6
{
  if 4==8/4
  {
    a = 2
  }
  elif 4==8/4
  {
    a = 3
  }
}
elif 4==8/4
{
  a = 3
}
else
{
  a = 8
}
return a
");
            var v = r.Eval();
            Assert.Equal(7L, v);
        }

        [Fact]
        public void elif_branch_contains_nested_if_elif_that_matches_if()
        {
            var r = new Compiler().Compile(@"
a = 0
gg = 6
if gg == 5 {
  a = 1
}
elif gg == 6
{
  if 4==8/2
  {
    a = 2
  }
  elif 4==8/3
  {
    a = 3
  }
}
else
{
  if 7==7/1 {
    a = 4
  }
}
return a
");
            var v = r.Eval();
            Assert.Equal(2L, v);
        }

        [Fact]
        public void empty_else_block_body_is_allowed_and_execution_continues_after()
        {
            var r = new Compiler().Compile(@"
a = 0
gg = 5
if gg == 5{
  a = 1
}
elif gg == 6
{
  a = 3
}
else {}
a = 66

return a
");
            var v = r.Eval();
            Assert.Equal(66L, v);
        }

        [Fact]
        public void if_elif_else_takes_else_branch_when_neither_condition_matches()
        {
            var r = new Compiler().Compile(@"
a = 0
gg = 3
if gg == 5
{
  a = 1
}
elif gg == 6
{
  a = 3
}
else
{
  a = 2
}
return a

");
            var v = r.Eval();
            Assert.Equal(2L, v);
        }

        [Fact]
        public void if_elif_else_takes_elif_branch_when_it_matches()
        {
            var r = new Compiler().Compile(@"
a = 0
gg = 6
if gg == 5
{
  a = 1
}
elif gg == 6
{
  a = 3
}
else
{
  a = 2
}
return a

");
            var v = r.Eval();
            Assert.Equal(3L, v);
        }

        [Fact]
        public void simple_if_else_takes_if_branch_when_condition_true()
        {
            var r = new Compiler().Compile(@"
a = 0
gg = 5
if gg == 5{
  a = 1
}
else
{
  a = 2
}
return a

");
            var v = r.Eval();
            Assert.Equal(1L, v);
        }

        [Fact]
        public void simple_if_else_takes_else_branch_when_condition_false()
        {
            var r = new Compiler().Compile(@"
a = 0
gg = 5
if gg == 4
{
  a = 1
}
else
{
  a = 2
}
return a

");
            var v = r.Eval();
            Assert.Equal(2L, v);
        }

    }
}
