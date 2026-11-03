funcs = [lambda: i for i in range(3)]
print([f() for f in funcs])
adders = [(lambda x, n=n: x + n) for n in range(3)]
print([a(10) for a in adders])
def outer():
    items = []
    for i in range(3):
        def g(i=i):
            return i * 10
        items.append(g)
    return [g() for g in items]
print(outer())
d = {}
for k in "abcde":
    d[k] = ord(k)
del d["b"]
d["b"] = 0
print(list(d), d)
pairs = [(1, "b"), (0, "a"), (1, "a"), (0, "b")]
print(sorted(pairs), sorted(pairs, key=lambda p: p[0]), sorted(pairs, key=lambda p: p[1], reverse=True))
lst = list(range(10))
lst[2:5] = ["x"]
print(lst, lst[-3:], lst[::3], lst[8:2:-2])
del lst[::2]
print(lst)
for i, (a, b) in enumerate(zip("xyz", [1, 2, 3])):
    print(i, a, b, end="; ")
print()
x = 5
print(1 < x < 10, 10 < x < 20, x == 5 == 5.0, x != 4 != 5)
print(0.1 + 0.2 == 0.3, abs(0.1 + 0.2 - 0.3) < 1e-9, round(0.1 + 0.2, 10))
print(7 // 2, -7 // 2, 7.0 // 2, -7.5 // 2, 7 % 3, -7 % 3, 7.5 % 2, -7.5 % 2)
print(2 ** 10, 2 ** 0.5, 10 ** -2, (-8) ** 3, pow(2, 64), 1 / 4, 3 / 1)
print(int(-3.7), int("  12 "), float(" 1e3 "), int(True), float(2), str(10 ** 20), 10 ** 20 // 7)
print("%.3e" % 12345.678, "%g" % 0.00001234, "%10.4f|" % 3.14159, "%-6d|" % 42, "%+d" % 5, "%05.1f" % 3.14159)
print(f"{12345.678:.2e}", f"{0.1:.20f}", f"{1/3:.5}", f"{42:+}", f"{-42:08}", f"{3.5:>8.2f}", f"{True}", f"{None!s}")
print(f"{1000000:_}", f"{255:08b}", f"{0.25:.1%}", f"{'ab':*^7}", f"{12:x}", f"{12:X}", f"{12:o}", f"{-0.0}")
print(str(1e15), str(1e16), str(123456789.123456789), str(1.5e-10), repr(0.1), 2.0 * 3, 7 / 7)
s = "The Quick Brown Fox"
print(s.lower().split(), s.count("o"), s.index("Q"), s.replace(" ", "_", 2), s.lstrip("Teh "), s[4:9].upper())
print("a,b;c d".replace(";", ",").replace(" ", ",").split(","), "x".center(4, "-"), "%s|%r" % ("q", "q"))
words = ["banana", "Apple", "cherry"]
print(sorted(words), sorted(words, key=str.lower), max(words, key=len), min(words))
print(list(map(lambda x: x ** 2, filter(lambda x: x % 2, range(10)))))
print({k: v for k, v in zip("abc", range(3)) if v}, [y for x in [[1, 2], [3]] for y in x])
print(any(x > 3 for x in [1, 5]), all(isinstance(x, int) for x in [1, 2.0]), sum([[1], [2]], []))
def gen():
    try:
        yield 1
        yield 2
    finally:
        print("gen cleanup")
for v in gen():
    print("got", v)
counter = 0
def bump():
    global counter
    counter += 1
    return counter
print([bump() for _ in range(3)], counter)
class Base:
    registry = []
    def __init__(self):
        Base.registry.append(type(self).__name__)
class Child(Base):
    pass
Base(); Child(); Child()
print(Base.registry, Child.registry is Base.registry)
class Temp:
    def __init__(self, c):
        self._c = c
    @property
    def f(self):
        return self._c * 9 / 5 + 32
    def __eq__(self, other):
        return isinstance(other, Temp) and self._c == other._c
    def __hash__(self):
        return hash(self._c)
t = Temp(100)
print(t.f, Temp(1) == Temp(1), Temp(1) != Temp(2), len({Temp(1), Temp(1), Temp(2)}), Temp(3) in [Temp(3)])
try:
    t.f = 5
except AttributeError:
    print("read-only property")
def fn(*args, sep="-", **kw):
    return sep.join(map(str, args)) + str(sorted(kw))
print(fn(1, 2, 3), fn(sep="+"), fn(1, a=1, b=2))
a = [1, 2, 3]
b = a[:]
b.append(4)
print(a, b, a is b, a == b[:3])
n = None
print(n is None, n == None, not n, type(n) is type(None))
print(divmod(-7, 2), divmod(7.5, 2), abs(-0.0), max([3, 1], default=0), min(4, 2, key=lambda v: -v))
print(sorted({3: "c", 1: "a"}.items()), dict(zip([1, 2], "ab")), list({1: 2}.items())[0])
print(len(range(0, 10, 3)), range(5)[-1], list(range(5))[1:3], 3 in range(10), range(10)[2:5])
text = "line one\nline two\n\nline four"
print([ln.split()[-1] for ln in text.splitlines() if ln], text.count("\n"))
nums = [5, 3, 8, 1]
nums.sort(reverse=True)
print(nums, nums.index(8), nums.pop(1), nums)
matrix = [[1, 2, 3], [4, 5, 6]]
print([list(r) for r in zip(*matrix)], [sum(c) for c in zip(*matrix)])
print(isinstance([], list), isinstance({}, dict), type(()) == tuple, type(len), callable(len), callable(5))
i = 0
while i < 10:
    i += 1
    if i % 2:
        continue
    if i > 6:
        break
print(i)
print(chr(9731), "é", len("é"), "é".upper(), "ß".upper() if False else "ok")
print(hash(1) == hash(1.0), {1: "a", 1.0: "b"}, {True: 1, 1: 2})
print(sum(i * i for i in range(10 ** 4)), 2 ** 200 % 1000007, -(2 ** 70) // 3)
