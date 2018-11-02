# DYNLAN 1.0
## Dynamic Scripting Language in C#

## Features
 + evaluate code in C# at runtime:
   + simple expressions 
   + complex scripts (with objects and classes)
 + interoperability:
 + entirely written in plain C# (without DLR / Reflection.Emit)
 + compatible with:
   + .net 4.0 and higher
   + .net core
   + .net standard
   + PCL (including silverlight)
   + .net compact framework
 + pausing script execution

## Limitations
 + one statement per line

## Inspiration
 + python :)

## Sample performance
 22808 lines/second on single core (Intel Core i5-3210M@2.50Ghz)

## Examples

 + Basic usage:
```
new PainCompiler().Compile(" return 1 + 3.1 ").Eval();

# result is 4.1
```
 + Usage of global functions:
```
new Compiler().Compile(" return substring(round(33.3333, 2) + 'ABC', 0, 6) ").Eval();

# result is '33.33A'
```
 + Conditional statements:
```
new Compiler().Compile(@"

if 100 < 10:
  return 'Not true..'
elif 100 < 90:
  return 'Also not true..'
elif 100 < 101:
  return 'This is true..'
else:
  return '..' 
  
").Eval();

# result is 'This is true..'
```
 + While statement:
```
new Compiler().Compile(@"

i = 0
while i < 100:
  i = i + 1
return i

").Eval();

# result is 100
```
 + Function definition:
```
new Compiler().Compile(@"

def increment(a): 
  b = a + 1 
  return b 
return increment(10)

").Eval();

# result is 11
```
 + Try / catch:
```
new Compiler().Compile(@"

error = null
try:
  throw 'Error!'
catch (ex):
  error = ex
return error.Message

").Eval();

# result is 'Error!'
```
 + Classes:
```
new Compiler().Compile(@"

class TestClass():
  a = 1
  b = 2
  def Sum(c):
    return this.a + this.b + c
obj = TestClass()
return obj.Sum(3)+obj.a

").Eval();

# result is 7
```
 + .NET interoperability example (using c# variable from PainLang):
```
Dictionary<String,Object> variables = new Dictionary<String,Object>();
variables["A"] = "Test string";

new Compiler().Compile(@"

return A.Length + ' ' + A.Substring(5,6) 

").Eval(variables);

# result is '11 string'
```
 + .NET interoperability example (using PainLang method in C#):
```
DynLanContext context = new Compiler().Compile(@"

def method1(a,b,c):
  if a:
    return b
  else:
    return c

").CreateContext();
Object result = context.InvokeMethod("method1", new object [] { 0, "Result1", "Result2" });

# result is 'Result2'
```
