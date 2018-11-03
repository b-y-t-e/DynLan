# DYNLAN | Dynamic Scripting Language in C#

## Examples

 + Basic usage:
```
// result = 4
Object result = new Compiler().
  Compile(" return 1 + 3 ").
  Eval();
```
 + Usage of global functions:
```
// result = '33.33A'
Object result = new Compiler().
  Compile(" return substring( round(33.3333, 2) + 'ABC', 0, 6) ").
  Eval();
```
 + Conditional statements:
```
// result = 'This is true..'
Object result = new Compiler().
  Compile(@"

if 100 < 10:
  return 'Not true..'
elif 100 < 90:
  return 'Also not true..'
elif 100 < 101:
  return 'This is true..'
else:
  return '..' 
  
").Eval();
```
 + While statement:
```
// result = 100
Object result = new Compiler().
  Compile(@"

i = 0
while i < 100:
  i = i + 1
return i

").Eval();
```
 + Function definition:
```
// result = 11
Object result = new Compiler().
  Compile(@"

def increment(a): 
  b = a + 1 
  return b 
return increment(10)

").Eval();
```
 + Try / catch:
```
// result = 'Error!'
Object result = new Compiler().
  Compile(@"

error = null
try:
  throw 'Error!'
catch (ex):
  error = ex
return error.Message

").Eval();
```
 + Classes:
```
// result = 7
Object result = new Compiler().
  Compile(@"

class TestClass():
  a = 1
  b = 2
  def Sum(c):
    return this.a + this.b + c
obj = TestClass()
return obj.Sum(3)+obj.a

").Eval();
```
 + .NET interoperability example (using c# variable from DynLan):
```
// result = '11 string'
Dictionary<String,Object> variables = new Dictionary<String,Object>();
variables["A"] = "Test string";

Object result = new Compiler().
  Compile(@"

return A.Length + ' ' + A.Substring(5,6) 

").Eval(variables);
```
 + .NET interoperability example (using DynLan method in C#):
```
// result = 'Result2'
DynLanContext context = new Compiler().
  Compile(@"

def method1(a,b,c):
  if a:
    return b
  else:
    return c

").CreateContext();

Object result = context.
  InvokeMethod("method1", new object [] { 0, "Result1", "Result2" });
```
