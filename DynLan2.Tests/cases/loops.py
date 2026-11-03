total = 0
for i in range(1000):
    if i % 2 == 0:
        continue
    if i > 990:
        break
    total += i
print(total)
i = 0
while True:
    i += 1
    if i >= 10:
        break
print(i)
for x in []:
    pass
else:
    print("for-else")
n = 0
while n < 3:
    n += 1
else:
    print("while-else", n)
for i in range(3):
    for j in range(3):
        if j == 2:
            break
    else:
        print("never")
print(i, j)
res = []
for i in range(10, 0, -2):
    res.append(i)
print(res)
k = 5
while k:
    k -= 1
print(k)
acc = []
for ch in "abc":
    for d in range(2):
        acc.append(ch * d)
print(acc)
x = 0
for i in range(100000):
    x = x + i * 2 - 1
print(x)
fl = 0.0
for i in range(1000):
    fl += i * 0.5
print(fl)
s = ""
for i in range(5):
    s += str(i)
print(s)
cnt = {}
for w in "the cat the dog the end".split():
    cnt[w] = cnt.get(w, 0) + 1
print(cnt)
if (m := len(cnt)) > 2:
    print("walrus", m)
grid = [[r * 3 + c for c in range(3)] for r in range(3)]
print(grid, [row[1] for row in grid], sum(sum(r) for r in grid))
primes = [p for p in range(2, 50) if all(p % q for q in range(2, int(p ** 0.5) + 1))]
print(primes)
import math
print(math.sqrt(16), math.floor(2.7), math.ceil(2.1), math.pi, math.gcd(12, 18), math.factorial(20))
from math import sqrt as sq, inf
print(sq(2), inf > 10 ** 100)
def hot(n):
    s = 0
    for i in range(n):
        if i % 3 == 0:
            s += i
        elif i % 3 == 1:
            s -= 1
        else:
            s = s + 2
    return s
print([hot(k) for k in range(0, 50, 7)])
def typed(n: int) -> int:
    acc: int = 0
    i: int = 0
    while i < n:
        acc += i * i
        i += 1
    return acc
print(typed(1000), [typed(k) for k in range(5)])
def ftyped(n: int) -> float:
    s: float = 0
    for i in range(n):
        s += 1 / (i + 1)
    return s
print(ftyped(100))
