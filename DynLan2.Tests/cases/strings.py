s = "Hello, World"
print(s.upper(), s.lower(), s.replace("l", "L"), s.split(", "), s.find("o"), s.rfind("o"), s.count("l"))
print(s[0], s[-1], s[1:5], s[::-1], s[::2], s[-5:], s[:3])
print("  pad  ".strip() + "|", "xxhixx".strip("x"), "a,b,,c".split(","), " a  b ".split(), "a b c".split(None, 1))
print("-".join(["a", "b", "c"]), "abc".startswith("ab"), "abc".endswith(("x", "c")), "42".isdigit(), "ab1".isalnum())
print("hello world".title(), "HeLLo".swapcase(), "hello".capitalize(), "7".zfill(3), "-7".zfill(4))
print("abc".center(9, "*"), "abc".ljust(5) + "|", "abc".rjust(5))
print("a=%d b=%s c=%.2f d=%5s e=%-5s| f=%x g=%r" % (5, "x", 3.14159, "hi", "hi", 255, "q"))
print("{} and {}".format(1, 2), "{1}{0}".format("a", "b"), "{name}!".format(name="Bob"), "{:>6.2f}|".format(3.14159))
name = "Ann"
age = 30
print(f"{name} is {age} years", f"{age * 2}", f"{3.14159:.3f}", f"{name!r}", f"{age:05d}", f"{1234567:,}", f"{age=}")
print(f"{'nested'}", f"{[1, 2][0]}", f"{{literal}}", f"{0.5:%}", f"{255:x}", f"{255:#b}", f"{12:^8}|", f"{3.0:g}", f"{1e20:g}")
w = 10
print(f"{'x':>{w}}|")
print(repr("it's"), repr('say "hi"'), repr("a\nb"), str(None), repr([1, "a", None, 2.5]))
print("abc" * 3, 3 * "ab", "a" in "cat", "z" not in "cat", len("hello"))
print("x".join(str(i) for i in range(4)))
print("%s %s" % ("a", "b"), "%%", "%(a)s-%(b)s" % {"a": 1, "b": 2})
print("partition".partition("t"), "a.b.c".rpartition("."), "line1\nline2\r\nline3".splitlines())
print(str(1.0), str(2.5e-7), str(1e300 * 10), 1e22, 123456789012345678.0, float("nan"))
print(ord("€"), "ab"[-1], "abcdef"[1:-1:2], "racecar" == "racecar"[::-1])
