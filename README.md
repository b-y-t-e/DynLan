# [DYNLAN](https://dynlan.com) | Dynamic Scripting Language | in .NET

## Features
 + evaluate code in C# at runtime (see examples below):
   + expressions
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

## Performance
 22808 lines/second on single core (Intel Core i5-3210M@2.50Ghz)

## Examples

 + Simple expressions:
```
// result = 4
Object result = new Compiler().
  Compile(" 1 + 3 ").
  Eval();
```

 + Multiline scripts:
```
// result = 100
Object result = new Compiler().
  Compile(@"
    i = 0
    while i < 100 {
      i = i + 1
    }
    return i
").Eval();
```

 + C# interoperability:
```
// result = "11 string"
var variables = new Dictionary<String, Object>();
variables["A"] = "Test string";

Object result = new Compiler().
  Compile(@"
    A.Length + ' ' + A.Substring(5,6)
").Eval(variables);
```

 + Objects + classes:
```
// result = 70
Object result = new Compiler().
  Compile(@"

class TestClass() {
  a = 1
  b = 2
  def Sum(c) {
    return this.a + this.b + c
  }
}

def multiply(a, b) {
  c = a * b 
  return c
}

obj = TestClass()
return multiply( obj.Sum(3) + obj.a, 10)

").Eval();
```

## More examples
[Explore](https://github.com/b-y-t-e/DynLan/blob/master/EXAMPLES.md)
