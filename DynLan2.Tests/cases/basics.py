x = 10
y = 3
print(x + y, x - y, x * y, x / y, x // y, x % y, x ** y)
print(-7 // 2, -7 % 2, 7 // -2, 7 % -2, 2 ** -1, 2 ** 100)
print(1.5 + 2, 0.1 + 0.2, 1e16, 1e-5, 123.456, 3.0, -0.0, 1/3)
print(True + 1, True and 5, 0 or "x", not 0, None is None)
print(9223372036854775807 + 1, -9223372036854775808 - 1, 3037000500 * 3037000500)
print(7 & 3, 7 | 8, 7 ^ 2, ~5, 1 << 70, -16 >> 2)
print(1 < 2 < 3, 1 < 3 < 2, 1 == 1.0, "a" < "b", [1, 2] < [1, 3], (1, 2) == (1, 2))
print(abs(-3), abs(-2.5), round(2.5), round(3.5), round(2.675, 2), round(1234, -2), divmod(17, 5), pow(3, 4, 5))
print(int("42"), int("-0x1f", 16), int(3.9), float("1.5"), str(12), bool([]), bool([0]))
print(max(3, 9, 2), min([4, 1, 7]), sum([1, 2, 3]), sum(range(101)), max("abc", key=len), min([], default=0))
print(hex(255), oct(8), bin(5), chr(65), ord("a"))
a, b = 1, 2
a, b = b, a
print(a, b)
first, *rest = [1, 2, 3, 4]
print(first, rest)
*init, last = "hello"
print(init, last)
(p, q), r = (1, 2), 3
print(p, q, r)
x = y = 5
print(x, y)
n = 0
n += 5
n -= 1
n *= 3
n //= 2
n **= 2
print(n)
print(type(1).__name__, type(1.0).__name__, type("").__name__, type([]).__name__, type(None).__name__)
print(isinstance(True, int), isinstance(1, float), isinstance("x", (int, str)))
print(10 if x > 3 else 20, "yes" if [] else "no")
