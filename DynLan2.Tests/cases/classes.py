class Animal:
    kind = "animal"
    def __init__(self, name):
        self.name = name
    def speak(self):
        return self.name + " makes a sound"
    def __repr__(self):
        return f"Animal({self.name!r})"

class Dog(Animal):
    def __init__(self, name, breed):
        super().__init__(name)
        self.breed = breed
    def speak(self):
        return self.name + " barks; " + super().speak()

d = Dog("Rex", "lab")
print(d.speak(), d.kind, d.breed, isinstance(d, Animal), isinstance(d, Dog), issubclass(Dog, Animal))
print(repr(Animal("cat")), [Animal("x")])

class Vec:
    def __init__(self, x, y):
        self.x, self.y = x, y
    def __add__(self, o):
        return Vec(self.x + o.x, self.y + o.y)
    def __mul__(self, k):
        return Vec(self.x * k, self.y * k)
    def __eq__(self, o):
        return self.x == o.x and self.y == o.y
    def __len__(self):
        return 2
    def __getitem__(self, i):
        return (self.x, self.y)[i]
    def __str__(self):
        return f"Vec({self.x}, {self.y})"
    def __lt__(self, o):
        return (self.x, self.y) < (o.x, o.y)
    def __hash__(self):
        return hash((self.x, self.y))

v = Vec(1, 2) + Vec(3, 4) * 2
print(v, v == Vec(7, 10), len(v), v[1], list(v), sorted([Vec(2, 0), Vec(1, 5)])[0], {Vec(1, 1): "a"}[Vec(1, 1)])

class Counter:
    count = 0
    def __init__(self):
        Counter.count += 1
    @staticmethod
    def stat(x):
        return x * 2
    @classmethod
    def make(cls):
        return cls()
    @property
    def double(self):
        return Counter.count * 2
    @double.setter
    def double(self, v):
        Counter.count = v // 2

c = Counter.make()
Counter()
print(Counter.count, Counter.stat(4), c.double)
c.double = 10
print(Counter.count)

class Shape:
    def area(self):
        raise NotImplementedError
class Sq(Shape):
    def __init__(self, s): self.s = s
    def area(self): return self.s ** 2
print([Sq(i).area() for i in range(4)])
try:
    Shape().area()
except NotImplementedError:
    print("not impl")

class A:
    def who(self): return "A"
class B(A):
    def who(self): return "B" + super().who()
class C(A):
    def who(self): return "C" + super().who()
class D(B, C):
    def who(self): return "D" + super().who()
print(D().who(), [k.__name__ for k in D.__mro__])

class It:
    def __init__(self, n): self.n = n; self.i = 0
    def __iter__(self): return self
    def __next__(self):
        if self.i >= self.n:
            raise StopIteration
        self.i += 1
        return self.i
print(list(It(3)), sum(It(4)))
class Ctx:
    def __enter__(self):
        print("enter")
        return self
    def __exit__(self, *a):
        print("exit")
with Ctx() as cc:
    print("inside", type(cc).__name__)
print(hasattr(d, "name"), getattr(d, "zzz", "dflt"), type(d).__name__, d.__class__.__name__)
setattr(d, "age", 3)
print(d.age, vars(Vec(1, 2)))
