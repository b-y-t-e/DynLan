# [DYNLAN](https://dynlan.com)
## Dynamic Scripting Language in C#

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

## Limitations
 + one statement per line

## Inspiration
 + python :)

## Sample performance
 22808 lines/second on single core (Intel Core i5-3210M@2.50Ghz)

## Examples

 + Basic usage:
```
Object result = new Compiler().
  Compile(" return 1 + 3 ").
  Eval();

# result is 4
```

## [More examples](https://github.com/b-y-t-e/DynLan/blob/master/EXAMPLES.md)
