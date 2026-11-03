def add(a, b=10, *args, scale=1, **kw):
    return (a + b + sum(args)) * scale, kw

print(add(1), add(1, 2), add(1, 2, 3, 4), add(1, scale=2), add(1, x=5), add(b=1, a=2))

def fact(n):
    return 1 if n <= 1 else n * fact(n - 1)

print(fact(10), fact(30))

def fib(n):
    a, b = 0, 1
    for _ in range(n):
        a, b = b, a + b
    return a

print(fib(90))

def make_counter():
    count = 0
    def inc():
        nonlocal count
        count += 1
        return count
    return inc

c = make_counter()
c()
c()
print(c())

def outer():
    x = "outer"
    def mid():
        def inner():
            return x
        return inner()
    return mid()

print(outer())
square = lambda x: x * x
print(square(7), (lambda *a: len(a))(1, 2, 3), sorted([(1, 'b'), (0, 'a')], key=lambda p: p[1]))
g = 100
def use_global():
    global g
    g += 1
    return g
print(use_global(), g)
def gen(n):
    for i in range(n):
        yield i * i
print(list(gen(5)), sum(gen(4)))
def gen2():
    x = yield 1
    print("got", x)
    yield x * 2
it = gen2()
print(next(it))
print(it.send(21))
def chain(*its):
    for i in its:
        yield from i
print(list(chain([1, 2], (3,), "ab")))
def kwonly(a, *, b, c=3):
    return a + b + c
print(kwonly(1, b=2), kwonly(1, b=2, c=0))
def deco(f):
    def wrapper(*a, **k):
        return "<" + str(f(*a, **k)) + ">"
    return wrapper
@deco
def hello(name):
    return "hi " + name
print(hello("bob"))
fs = [lambda i=i: i for i in range(3)]
print([f() for f in fs])
def varargs(*a, **k):
    return a, sorted(k.items())
print(varargs(*[1, 2], **{"z": 1, "y": 2}))
def early(n):
    for i in range(10):
        if i == n:
            return i
    return -1
print(early(3), early(20))
def recur(n):
    return 0 if n == 0 else 1 + recur(n - 1)
print(recur(900))
