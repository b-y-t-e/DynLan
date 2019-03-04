using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using DynLan;
using DynLan.Exceptions;
using DynLan.OnpEngine.Logic;
using DynLan.Helpers;
using DynLan.OnpEngine.Models;
using System.Reflection;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Runtime.Serialization.Formatters;
using DynLan.Evaluator;
using DynLan.Classes;
using System.Dynamic;

namespace DynLanTests
{
    [TestClass]
    public class UnitTest1
    {

        [TestMethod]
        public void DynLanuageTests()
        {
            /*IDictionary<String, ClientV> dict = new ConcurrentDictionary<String, ClientV>();
            IDictionary dict2 = (IDictionary)dict;
            dict2["ff"] = default;
            dict["ff"] = null;*/

            Test_ONP_Language();
            Test_ONP_Zero();
        }

        [TestMethod]
        public void test_value_on_last_line_should_make_result()
        {
            {
                var r = new Compiler().Compile(@"
str = 'return 556'; eval(str);
");
                var v = r.Eval();
                if (!(556M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }
        }

        [TestMethod]
        public void test_new_code_line()
        {
            {
                var r = new Compiler().Compile(@"
str = 'return 556'; return eval(str)
");
                var v = r.Eval();
                if (!(556M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }
        }

        [TestMethod]
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
                Assert.AreEqual("andrew", v);
                //if (!("andrew").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }
        }

        [TestMethod]
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
                if (!("andrew").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }
        }

        [TestMethod]
        public void test_many_methods_and_result_in_one_line()
        {
            {
                var r = new Compiler().Compile(@"def aa() { return 1 } def bb(){ return 2} return aa() + bb() ");
                var v = r.Eval();
                if (!(3L).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }
        }

        [TestMethod]
        public void test_many_methods_and_result_in_one_line_with_brackets()
        {
            {
                var r = new Compiler().Compile(@"def aa() {a = 1;   return a + 0}  def bb() { return 2;}    return aa() + bb() ");
                var v = r.Eval();
                if (!(3L).Equals(v)) throw new Exception("Nieprawidłowa wartość " + v + "!");
            }
        }

        [TestMethod]
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
                if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }
        }

        [TestMethod]
        public void test_list_as_list()
        {
            {
                var r = new Compiler().Compile(@"
list = list()
return list
");
                var v = r.Eval();
                if (!(v is IList)) throw new Exception("Nieprawidłowa wartość!");
            }
        }

        [TestMethod]
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
                if (!(v is String) || !((String)v).EndsWith("!")) throw new Exception("Nieprawidłowa wartość!");
            }
        }

        [TestMethod]
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
                if (!(2M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }
        }


        /*  [TestMethod]
          public void test_invalid_if()
          {
              {
                  var r = new Compiler().Compile(@"
  if : {
    return 1
  }
  return 2
  ");
                  var v = r.Eval();
                  if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
              }
          }*/

        [TestMethod]
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
                if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }

        }

        [TestMethod]
        public void test1()
        {
            {
                var r = new Compiler().Compile(@"
str = 'return 556'
return eval(str)
");
                var v = r.Eval();
                if (!(556M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }

        }

        [TestMethod]
        public void test2()
        {
            {
                var r = new Compiler().Compile(@"
STR = '123'
str = '321'
return str + STR
");
                var v = r.Eval();
                if (!("321123").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }

        }

        [TestMethod]
        public void one_line_statement()
        {
            {
                var r = new Compiler().Compile(@"return param1");

                var d = new Dictionary<string, object>();
                d["param1"] = 123;

                var v = r.Eval(d);
                if (!(123).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }

        }


        /*[TestMethod]
        public void test4()
        {
            {
                var r = new Compiler().Compile(@"
true = 1
return true
");
                var v = r.Eval();
                if (!("321123").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
            }

        }*/


        [TestMethod]
        public void test3()
        {
            {
                var r = new Compiler().Compile(@"

return str1 + STR1
");
                Exception ex = null;
                try
                {
                    var v = r.Eval();
                }
                catch (DynLanExecuteException ex2)
                {
                    ex = ex2;
                }

                if (ex == null)
                    throw new Exception("!");
            }

        }

        static void Test_ONP_Language()
        {
            {
                var nl = Environment.NewLine;

                /*{
                    var r = new DynLanCompiler().Compile(@"
s = 0
while s < 2:
  if true:
    s = s + 1 
";)a
                    var v = r.Eval();
                    if (!(10M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }*/

                {

                    var r = new Compiler().Compile(@"
return eval('return 556')
");
                    var v = r.Eval();
                    if (!(556L).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
str = 'return 556'
return eval(str)
");
                    var v = r.Eval();
                    if (!(556M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
A = 3
str = 'return 556 + A'
return eval(str)
");
                    var v = r.Eval();
                    if (!(559M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
A = 3
str = 'return 556   +   A'
return eval(str)
");
                    var v = r.Eval();
                    if (!(559M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
str = 'return true'
return eval(str)
");
                    var v = r.Eval();
                    if (!(true).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
str = 'return True'
return eval(str)
");
                    var v = r.Eval();
                    if (!(true).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
str = 'return false'
return eval(str)
");
                    var v = r.Eval();
                    if (!(false).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
str = 'return False'
return eval(str)
");
                    var v = r.Eval();
                    if (!(false).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
return str(1)
");
                    var v = r.Eval();
                    if (!("1").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
def method1(s)
{
  return s+1
}
return method1(1)
");
                    var v = r.Eval();
                    if (!(2M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"
def method1(str)
{
  return str+1
}
return method1(1)
");
                    var v = r.Eval();
                    if (!(2M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"
def method1(str)
{
  return str+1
}
return method1(1)+str(2)
");
                    var v = r.Eval();
                    if (!("22").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
def str(v)
{
  return v+1
}
return str(1)
");
                    var v = r.Eval();
                    if (!(2M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
def test2(txt)
{
  return txt
}
return test2('111')
");
                    var v = r.Eval();
                    if (!("111").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!("11(" + Environment.NewLine + ")11").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
return 1 != null
");
                    var v = r.Eval();
                    if (!(true).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
return 1 == null
");
                    var v = r.Eval();
                    if (!(false).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
return 1 != undefined
");
                    var v = r.Eval();
                    if (!(true).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
return 1 == undefined
");
                    var v = r.Eval();
                    if (!(false).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
return 'ab#c'
");
                    var v = r.Eval();
                    if (!("ab#c").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"return 'ab'#+'c'");
                    var v = r.Eval();
                    if (!("ab").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"
i= 'ab#c'
return i
");
                    var v = r.Eval();
                    if (!("ab#c").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (v != null) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"
a = 1
b = 2
return this.a
");
                    var v = r.Eval();
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
a = 1
b = 2
this = 5
return this
");
                    var v = r.Eval();
                    if (!(5M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(0M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(0M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    try
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
                        var v = r.Eval();
                        throw new Exception("Nieprawidłowa wartość!");
                    }
                    catch (DynLanExecuteException)
                    {

                    }

                    //if (!(0M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(2M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    try
                    {
                        var v = r.Eval();
                        throw new Exception("Zmienna 'a' nie moze byc widoczna w metodzie cc -> moga byc widoczne tylko zmienne lokalne / klasy lub globalne");
                    }
                    catch (DynLanExecuteException)
                    {

                    }
                    //if (!(0M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(100M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(4M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(33M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"

if not(cip.CMDON())
{
  return 1
}
");
                    try
                    {
                        var v = r.Eval();
                        throw new Exception("Skrypt nie powinien się wykonać!");
                    }
                    catch (DynLanExecuteException ex)
                    {

                    }
                }

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
                    if (!(4M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
return j + 1
");
                    var dict = new Dictionary<String, Object>();
                    dict["j"] = 10;
                    var v = r.Eval(dict);
                    if (!(11M).Equals(v)) throw new Exception("Nieprawidłowa wartość (" + dict["J"] + ")!");
                }

                /*{
                    var r = new Compiler().Compile(@"
return j + 1
");
                    var dict = new Dictionary<String, Object>();
                    dict["j"] = 10;
                    var v = r.Eval(dict);
                    if (!(11M).Equals(v)) throw new Exception("Nieprawidłowa wartość (" + v + "/" + dict["j"] + ")!");
                }*/

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
                    if (!(2M).Equals(dict["j"])) throw new Exception("Nieprawidłowa wartość (" + dict["j"] + ")!");
                }

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
                    if (!(32M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(22M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
class testowa(a,b,c) {
  return coalesce(a,0) + coalesce(b,0) + coalesce(c,1)
}
return testowa()
 
");
                    var v = r.Eval();
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
class testowa(a,b,c) {
  return a + b + c
}
return testowa()
 
");
                    var v = r.Eval();
                    if (v != null) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(4M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(4M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"
lista = new('ArrayList')
lista.Add('babb')
return lista[0]
");
                    var v = r.Eval();
                    if (!("babb").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"
i = 2
i= 3 # + 1
return i
");
                    var v = r.Eval();
                    if (!(3M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"
i = 2
#i= 1
return i
");
                    var v = r.Eval();
                    if (!(2M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var values = new Dictionary<string, object>();
                    var r = new Compiler().Compile(@"
i = 0
while i < 3{
  i = i + 1
}
");
                    var c = r.Eval(values);
                    if (!(3M).Equals(values["i"])) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"

a = 0
try{
  a = 1 + h.getdate()
}
a = a + 1
return a
");
                    try
                    {
                        var v = r.Eval();
                        throw new Exception("Błąd nie został przekazany w górę!");
                    }
                    catch (Exception ex)
                    {

                    }
                    //if (!(v is String) || !((String)v).EndsWith("!")) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(v is String) || !((String)v).EndsWith("!")) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(2M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(3M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(v is Exception)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(5M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(10M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    try
                    {

                        var v = r.Eval();
                        throw new Exception("method oo3 nie powinna być dostępna!");
                    }
                    catch (DynLanExecuteException ex)
                    {
                        // ok
                    }
                    catch (DynLanMethodNotFoundException ex)
                    {
                        // ok
                    }
                }
                /*{

                    var values = new Dictionary<string, object>();
                    var r = new Compiler().Compile(@"
class testowa():
  a = 2
  c = 1
  def oo():
    return this.a + this.c * 10
  def writeValues(dict):
    dict['a'] = this.a
    dict['c'] = this.c
  def writeValues2(dict):
    dict['A']['B'] = 123
obj = testowa()
");
                    var context = r.CreateContext();
                    context.Eval(values);
                    //DynLanObject result = context.Exec();
                    DynLanObject obj = context["obj"] as DynLanObject;

                    Object v = obj.InvokeObjectMethod(context, "oo", null);
                    if (!(12M).Equals(v)) throw new Exception("Nieprawidłowa wartość #1!");

                    Dictionary<String, Object> tmpValues = new Dictionary<string, object>();
                    Dictionary<String, Dictionary<String, Object>> tmpValues2 = new Dictionary<string, Dictionary<string, object>>();
                    tmpValues2["A"] = new Dictionary<string, object>();

                    for (var i = 0; i < 10000; i++)
                        obj.InvokeObjectMethod(context, "writeValues", new[] { tmpValues });

                    if (!(2).Equals(tmpValues.Count)) throw new Exception("Nieprawidłowa wartość!");
                    if (!(2M).Equals(tmpValues["a"])) throw new Exception("Nieprawidłowa wartość!");
                    if (!(1M).Equals(tmpValues["c"])) throw new Exception("Nieprawidłowa wartość!");

                    obj.InvokeObjectMethod(context, "writeValues2", new[] { tmpValues2 });
                    if (!(123M).Equals(tmpValues2["A"]["B"])) throw new Exception("Nieprawidłowa wartość #2!");

                    tmpValues = tmpValues;
                }*/
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
                    if (!(4M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(4M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(5M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(12M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(10100M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!("dwa").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!("1" + " " + "2" + " " + "66").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!("1" + " " + "2" + " " + "88").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }


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
                    if (!(v is IList)) throw new Exception("Wynik powinien byc listą!");
                    if ((v as IList).Count != 2) throw new Exception("Wynik powinien posiadać 2 elementy w liście!");
                    if (!((v as IList)[0] is IDictionary)) throw new Exception("Wynik powinien posiadać elementy typu dictinary!");
                    if (((DateTime)((v as IList)[0] as IDictionary)["Created"]) != DateTime.Now.Date) throw new Exception("Nieprawidłowa wartość!");
                    //if (!(77M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(v is IList)) throw new Exception("Wynik powinien byc listą " + v.GetType().Name + "!");
                    if ((v as IList).Count != 2) throw new Exception("Wynik powinien posiadać 2 elementy w liście!");
                    if (!((v as IList)[0] is IDictionary)) throw new Exception("Wynik powinien posiadać elementy typu dictinary " + (v as IList)[0].GetType().Name + "!");
                    if (((DateTime)((v as IList)[0] as IDictionary)["Created"]) != DateTime.Now.Date) throw new Exception("Nieprawidłowa wartość!");
                    //if (!(77M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(77M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(77M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(3M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(8M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!("!kkk@").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"
def m1(v) {
  v = v + 'c'
}
v = 'abc'
return '!'+m1(v)+'@'
");
                    var v = r.Eval();
                    if (null != v) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!("abcc").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!("nIc").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"
a = 4444
gg = 3
if gg == 5 {
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
                    //var runner = new DynLanRunner();
                    var context = Engine.CreateContext(r);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                    ContextEvaluator.ExecuteNext(context);
                }
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
                    if (!("nic").Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(33M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(22M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(8M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(7M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(2M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(66M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(2M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(3M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(1M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
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
                    if (!(2M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
                {
                    var r = new Compiler().Compile(@"
gg = 100
def   AAA(v1,    v2)
{
  v2 = v2 + 1
  return (v1* 1000 + v2) / gg
}
FF = AAA(2,3) 
return FF
");
                    var v = r.Eval();
                    if (!(20.04M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }

                {
                    var r = new Compiler().Compile(@"
def f1(v1,v2){
  return f2(100)
}
def f2(v2){
  return v1}
return f1(100, 200)
");
                    try
                    {
                        var v = r.Eval();
                        throw new Exception("Nieprawidłowa wartość!");
                    }
                    catch (DynLanExecuteException)
                    {

                    }
                }

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
                    if (v != null) throw new Exception("Nieprawidłowa wartość!");
                }

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
                    if (!(300M).Equals(v)) throw new Exception("Nieprawidłowa wartość!");
                }
            }
        }

        static void Test_ONP_Zero()
        {
            var nl = Environment.NewLine;
            var OSOBA1 = new Osoba();
            var OSOBA2 = new Osoba();
            var OSOBY = new List<Osoba>();
            OSOBY.Add(OSOBA1);
            OSOBY.Add(OSOBA2);

            var DICT = new Dictionary<String, Object>();
            DICT["1"] = "aaa";
            DICT["2"] = "jjj";
            DICT["Pole1"] = "p1";
            DICT["Pole2"] = 56;

            Func<Decimal> m1 = () =>
            {
                return 1M;
            };
            DICT["Metoda1"] = m1;

            Func<DynLanMethodParameters, Int32> m2 = (items) =>
            {
                return items.Parameters.Length;
            };
            DICT["Metoda2"] = m2;

            Func<DynLanMethodParameters, Object> m3 = (items) =>
            {
                return items.Parameters[1];
            };
            DICT["Metoda3"] = m3;

            var LIST = new ArrayList();
            LIST.Add(2);
            LIST.Add("3");

            /*DynLanContext evalContext = new DynLanContext();
            evalContext.GlobalValues["OSOBY"] = OSOBY;
            evalContext.GlobalValues["OSOBA"] = OSOBA1;
            evalContext.GlobalValues["DICT"] = DICT;
            evalContext.GlobalValues["LIST"] = LIST;
            IDictionary<String, Object> VARIABLES = evalContext.GlobalValues;
            */

            Dictionary<String, Object> VARIABLES = new Dictionary<String, Object>();
            VARIABLES["OSOBY"] = OSOBY;
            VARIABLES["OSOBA"] = OSOBA1;
            VARIABLES["DICT"] = DICT;
            VARIABLES["LIST"] = LIST;

            Tokenizer tokenizer = new Tokenizer();

            {
                var complied = new Compiler().Compile("return DICT.Metoda1()");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(1M).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return DICT.Metoda2(1,2,3)");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(3).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return DICT.Metoda3(1,2,3)");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(2M).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return true");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(true).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return tRUE");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(true).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return false");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(false).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return fALSe");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(false).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                try
                {
                    var complied = new Compiler().Compile("return trueee");
                    var r1 = (Object)Engine.Eval(complied, VARIABLES);
                    throw new Exception("Nieprawidłowa wartość!");
                }
                catch (DynLanExecuteException)
                {

                }
            }
            {

                try
                {
                    var complied = new Compiler().Compile("return ffalse");
                    var r1 = (Object)Engine.Eval(complied, VARIABLES);
                    throw new Exception("Nieprawidłowa wartość!");
                }
                catch (DynLanExecuteException)
                {

                }
            }
            {
                var complied = new Compiler().Compile("trueee = 33" + Environment.NewLine + "return trueee");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(33M).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return (days(todate('2015-02-19') - todate('2015-02-17')) <= 7 || (days(todate('2015-02-19') - todate('2015-02-15')) <= 7 && 5==2))");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(true).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return (days(todate('2015-02-19') - todate('2015-02-17')) <= 7 or (days(todate('2015-02-19') - todate('2015-02-15')) <= 7 and 5==2))");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(true).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return (days(todate('2015-02-19') - todate('2015-02-17')) <= 7 oR (days(todate('2015-02-19') - todate('2015-02-15')) <= 7 ANd 5==2))");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(true).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return str(434.5).ToString().ToString().Substring(0,3)");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!("434").Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return 4324.6546 + (4234.55).ToString()");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!("4324.6546" + (4234.55).ToString()).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return DICT.Pole1.Substring(0,1)");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!"p".Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return DICT.Pole1");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!"p1".Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("DICT.Pole3 = 77" + nl + "return DICT.Pole2 + DICT.Pole3");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!(77M + 56M).Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return type('UniConvert').ToInt32(3.77)");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!4.Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile(
                    "F = 5" + nl +
                    "F = eval('C = F + 2'+nl+'return C / 2')" + nl +
                    "return F+1");
                var r1 = (decimal?)Engine.Eval(complied, VARIABLES);
                if (!4.5M.Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return (0-(1-2+2)+((coalesce@(null,4-1))))");
                var r1 = (decimal)Engine.Eval(complied, VARIABLES);
                if (!2.0M.Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("a = '321312' + 6" + nl + "return a");
                var r1 = (String)Engine.Eval(complied, VARIABLES);
                if (r1 != "3213126")
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile(" return 5 + (4-2).ToString()  ");
                var r1 = (String)Engine.Eval(complied, VARIABLES);
                if (r1 != "52")
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile(" OSOBY[0].Imie = 'sadasda!' " + nl + "return OSOBY[0].Imie");
                var r1 = (String)Engine.Eval(complied, VARIABLES);
                if (r1 != "sadasda!" || OSOBY[0].Imie != "sadasda!")
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile(" OSOBA.Imie = 'sadasda' " + nl + "return OSOBA.Imie");
                var r1 = (String)Engine.Eval(complied, VARIABLES);
                if (r1 != "sadasda" || OSOBA1.Imie != "sadasda")
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile(" return OSOBY[round(33.2-32)].Imie = 'sadasda!' ");
                var r1 = (String)Engine.Eval(complied, VARIABLES);
                if (r1 != "sadasda!" || OSOBY[1].Imie != "sadasda!")
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile(" return LIST[ 0 ] = 'abc'+'h' ");
                var r1 = (String)Engine.Eval(complied, VARIABLES);
                if (r1 != ("abc" + "h") || !LIST[0].Equals("abc" + "h"))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return (0-(1-2+2)+((coalesce@(null,4-1))))");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!2.0M.Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return (0-(1-2+2)+((coalesce(null,4-1))))");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!2.0M.Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return ((1-2+2)+((('aa').Substring(0,4-3))))");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!"1a".Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return ((1-2+2)+((('aa'+'a').Substring(0,4-2))))");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!"1aa".Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return ((1-2+2)+((('ab'+'c').Substring('b'.Length+1-1-1+1,4-2))))");
                var r1 = (Object)Engine.Eval(complied, VARIABLES);
                if (!"1bc".Equals(r1))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return 3+coalesce(null,(4.5-1),(5))+7");
                var r1 = (Decimal)Engine.Eval(complied, VARIABLES);
                if (r1 != 13.5M)
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return 'a'+'abc'.Substring(2)");
                var r1 = (String)Engine.Eval(complied, VARIABLES);
                if (r1 != ("a" + "c"))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return  LIST[ 0+100-(90+10) ] = 'abc'+'h' ");
                var r1 = (String)Engine.Eval(complied, VARIABLES);
                if (r1 != ("abc" + "h") || !LIST[0].Equals("abc" + "h"))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return  DICT[ ( 0- (1-2+1) + ((coalesce(null,4-1))) ) ] = 33 ");
                var r1 = (Decimal)Engine.Eval(complied, VARIABLES);
                if (r1 != 33.0M || !(33M).Equals(DICT["3"]))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return  round(33.3333,2) ");
                var r1 = (Decimal)Engine.Eval(complied, VARIABLES);
                if (r1 != 33.33M)
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return  JJJ = HHH2 = 33-3   ");
                var r1 = (Decimal)Engine.Eval(complied, VARIABLES);
                if (r1 != 30.0M || !VARIABLES["HHH2"].Equals(30.0M) || !VARIABLES["JJJ"].Equals(30.0M))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var r1 = (Decimal)new Compiler().Compile("return  100 + round(384128491.32 / 3313, 4) ").Eval();
                var r2 = 100 + Math.Round(384128491.32M / 3313.0M, 4);
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var complied = new Compiler().Compile("return  HHH = 33-3  + coalesce(null, 0) ");
                var r1 = (Decimal)Engine.Eval(complied, VARIABLES);
                if (r1 != 30.0M || !VARIABLES["HHH"].Equals(30.0M))
                    throw new Exception("Nieprawidłowa wartość!");
            }

            {
                var complied = new Compiler().Compile("return  DICT[4-1] = 33 ");
                var r1 = (Decimal)Engine.Eval(complied, VARIABLES);
                if (r1 != 33.0M || !(33M).Equals(DICT["3"]))
                    throw new Exception("Nieprawidłowa wartość!");
            }
            /*{
                var complied = new DynLanCompiler().Compile(" gg = 33 ");
                var result = DynLanRunner.Run(complied, VARIABLES);
                if (!complied.MainExpression.VariableOut.Value.Equals(33.0M))
                    throw new Exception("Nieprawidłowa wartość!");
            }*/
            {
                var r1 = new Compiler().Compile("return null").Eval(VARIABLES);
                if (r1 != null)
                    throw new Exception("Nieprawidłowa wartość!");
            }
            {
                var r1 = new Compiler().Compile("").Eval(VARIABLES);
                if (r1 != null)
                    throw new Exception("Nieprawidłowa wartość!");
            }


            {
                var r1 = (Char?)new Compiler().Compile("return  'abc'[1] ").Eval(VARIABLES);
                var r2 = "abc"[1];
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }

            {
                var p = new Compiler().Compile("return  'abc'[coalesce(2+null,floor(2.9))] ");
                var r1 = (Char?)p.Eval(VARIABLES);
                var r2 = "abc"[2];
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }

            {
                var r1 = (String)new Compiler().Compile("return  DICT[2] ").Eval(VARIABLES);
                var r2 = "jjj";
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }

            {
                var r1 = (String)new Compiler().Compile(" return  eval   (  ' return 5  +  6 + \\'!\\'   '  ) ").Eval(VARIABLES);
                var r2 = 5 + 6 + "!";
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }

            {
                var r1 = (Type)new Compiler().Compile(" return  typeof(321312+33) ").Eval(VARIABLES);
                var r2 = typeof(Decimal);
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }

            {
                var r1 = (String)new Compiler().Compile(" return  'abc '.Length +  ' ' +getdatetime   (  ).ToShortDateString  (  ).Substring(0,3) ").Eval(VARIABLES);
                var r2 = "abc ".Length + " " + DateTime.Now.ToShortDateString().Substring(0, 3);
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }



            {
                var r1 = (String)new Compiler().Compile(" return  getdatetime().Ticks.ToString().Substring(0,6) ").Eval(VARIABLES);
                var r2 = DateTime.Now.Ticks.ToString().Substring(0, 6);
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }

            {
                var r1 = (String)new Compiler().Compile(" return  'abc'.Length + ' ' + getdatetime().ToShortDateString().Substring(100-90-3-7,3) ").Eval(VARIABLES);
                var r2 = "abc".Length + " " + DateTime.Now.ToShortDateString().Substring(100 - 90 - 3 - 7, 3);
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }

            {
                var r1 = (String)new Compiler().Compile(" return  'abc'.Length + ' ' + getdatetime().ToShortDateString().Substring(100-90-3-7,3) ").Eval(VARIABLES);
                var r2 = "abc".Length + " " + DateTime.Now.ToShortDateString().Substring(100 - 90 - 3 - 7, 3);
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }

            {
                var exp = new Compiler().Compile(" return 'abc'.  Length + ' ' + getdatetime ().ToShortDateString().Substring(100-90-3-7,3) ");
                var r2 = "abc".Length + " " + DateTime.Now.ToShortDateString().Substring(100 - 90 - 3 - 7, 3);
                for (var i = 0; i < 100; i++)
                {
                    {
                        var r1 = (String)exp.Eval(VARIABLES);
                        if (r1 != r2)
                            throw new Exception("Nieprawidłowa wartość!");
                    }
                }
            }


            {
                var r1 = (String)new Compiler().Compile(" return   '\\'\\'\"' * 2 ").Eval(VARIABLES);
                var r2 = "''\"''\"";
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }

            {
                var r1 = (String)new Compiler().Compile(" return  '\\'\\'\"' * 2 ").Eval(VARIABLES);
                var r2 = "''\"''\"";
                if (r1 != r2)
                    throw new Exception("Nieprawidłowa wartość!");
            }

        }

        private static IEnumerable<ExpressionMethod> GetMethods()
        {
            yield return new ExpressionMethod()
            {
                OperationNames = new[] { "serialize" },
                CalculateValueDelegate = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        return new ExpressionMethodResult(JsonSerializerPrecise.SerializeJson(Parameters.FirstOrDefault()));
                    }
                    return new ExpressionMethodResult(null);
                }
            };

            yield return new ExpressionMethod()
            {
                OperationNames = new[] { "deserialize" },
                CalculateValueDelegate = (DynLanContext, Parameters) =>
                {
                    if (Parameters.Count > 0)
                    {
                        string json = UniConvert.ToString(Parameters.FirstOrDefault());
                        return new ExpressionMethodResult(JsonSerializerPrecise.DeserializeJson(json));
                    }
                    return new ExpressionMethodResult(null);
                }
            };
        }

    }
    public class Osoba
    {
        public String Imie { get; set; }
    }
    /// <summary>
    /// defincja pola w resource dla klienta
    /// </summary>
    //[ProtoContract]
    public class ClientV
    {
        /// <summary>
        /// Value
        /// </summary>
        public Object RealValue { get; set; }

        /////////////////////////////////

        /// <summary>
        /// Value
        /// </summary>
        //[ProtoMember(1)]
        public String TransValue { get; set; }

        /// <summary>
        /// Status
        /// </summary>
        //[ProtoMember(2)]
        public String Status { get; set; }

        /////////////////////////////////

        public Object Value()
        {
            return RealValue;
        }

        /////////////////////////////////

        public Boolean IsSame(ClientV Other)
        {
            if (Other == null ||
                Other.TransValue != this.TransValue ||
                Other.Status != this.Status)
                return false;
            return true;
        }
    }
}




public static class JsonSerializerPrecise
{
    private static String BaseDir
    {
        get
        {
            var lUri = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            return Path.GetDirectoryName(lUri.LocalPath);
        }
    }

    private static JsonSerializerSettings getSettings()
    {
        JsonSerializerSettings customJsonSettings = new JsonSerializerSettings()
        {
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Local,
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple
        };
        return customJsonSettings;
    }

    //////////////////////////////////

    public static Boolean IsSerializedJson(String Json)
    {
        try
        {
            object obj = DeserializeJson(Json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static Boolean IsSerializedJsonString(String Json)
    {
        try
        {
            object obj = DeserializeJson(Json);
            return obj.GetType() == typeof(String);
        }
        catch
        {
            return false;
        }
    }

    public static String SerializeJson(Object Item)
    {
        if (Item == null)
        {
            return null;
        }
        else
        {
            return JsonConvert.SerializeObject(Item, getSettings());
        }
    }

    public static Byte[] SerializeJson2Bytes(Object Item)
    {
        if (Item == null)
        {
            return null;
        }
        else
        {
            return Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(Item, getSettings()));
        }
    }

    public static T TryDeserializeJson<T>(String String)
    {
        try
        {
            return DeserializeJson<T>(String);
        }
        catch
        {
            return default(T);
        }
    }

    public static T DeserializeJson<T>(String String)
    {
        if (String.IsNullOrEmpty(String))
        {
            return default(T);
        }
        else
        {
            return (T)JsonConvert.DeserializeObject<T>(String, getSettings());
        }
    }

    public static Object DeserializeJson(String String)
    {
        if (String.IsNullOrEmpty(String))
        {
            return null;
        }
        else
        {
            return JsonConvert.DeserializeObject(String, getSettings());
        }
    }

    public static T DeserializeJsonFromBytes<T>(Byte[] Bytes)
    {
        String String = Bytes == null ? null : Encoding.UTF8.GetString(Bytes, 0, Bytes.Length);
        if (String.IsNullOrEmpty(String))
        {
            return default(T);
        }
        else
        {
            return (T)JsonConvert.DeserializeObject<T>(String, getSettings());
        }
    }

    public static Object DeserializeJsonFromBytes(Byte[] Bytes)
    {
        String String = Bytes == null ? null : Encoding.UTF8.GetString(Bytes, 0, Bytes.Length);
        if (String.IsNullOrEmpty(String))
        {
            return null;
        }
        else
        {
            return JsonConvert.DeserializeObject(String, getSettings());
        }
    }

    //////////////////////////////////

    public static String SerializeJsonToFile(String FilePath, Object Item)
    {
        var json = SerializeJson(Item);

        if (!Path.IsPathRooted(FilePath))
            FilePath = Path.Combine(BaseDir, FilePath);

        if (File.Exists(FilePath)) File.Delete(FilePath);
        File.WriteAllText(FilePath, json ?? "", Encoding.UTF8);
        return json;
    }

    public static T DeserializeJsonFromFile<T>(String FilePath)
    {
        if (!Path.IsPathRooted(FilePath))
            FilePath = Path.Combine(BaseDir, FilePath);

        if (File.Exists(FilePath))
        {
            return DeserializeJson<T>(
                File.ReadAllText(FilePath, Encoding.UTF8));
        }
        else
        {
            return default(T);
        }
    }

    //////////////////////////////////


}