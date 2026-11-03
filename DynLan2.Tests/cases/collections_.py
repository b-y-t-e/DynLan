l = [3, 1, 2]
l.append(4)
l.extend([5, 6])
l.insert(0, 0)
print(l, len(l), l.pop(), l.pop(0), l)
l.sort()
print(l, sorted([3, 1, 2], reverse=True), sorted(["bb", "a", "ccc"], key=len))
l.reverse()
print(l, l.index(2), l.count(2), 2 in l, l[1:3], l[::-1])
l[0] = 99
l[1:3] = [7, 8, 9]
del l[0]
print(l)
d = {"a": 1, "b": 2}
d["c"] = 3
print(d, d["a"], d.get("x"), d.get("x", 0), list(d.keys()), list(d.values()), list(d.items()))
print(len(d), "a" in d, d.pop("a"), d, d.setdefault("z", 26), d)
for k, v in d.items():
    print(k, v)
del d["b"]
print(d, {**d, "q": 1}, dict(a=1, b=2), dict([("x", 1)]))
s = {1, 2, 3}
s.add(4)
s.discard(1)
print(sorted(s), 2 in s, sorted(s | {9}), sorted(s & {2, 3}), sorted(s - {2}), len(s))
t = (1, 2, 3)
print(t, t[0], t[-1], len(t), t + (4,), t * 2, (1,), ())
print([x * x for x in range(6)], [x for x in range(10) if x % 3 == 0])
print({x: x * 2 for x in range(3)}, sorted({x % 3 for x in range(10)}))
print([(i, j) for i in range(3) for j in range(i)])
print(list(zip([1, 2, 3], "abc")), list(enumerate(["a", "b"], 1)), list(map(str, [1, 2])), list(filter(None, [0, 1, 2])))
print(list(reversed([1, 2, 3])), list(range(10, 0, -3)), any([0, 0, 1]), all([1, 1, 0]))
m = [[0] * 3 for _ in range(2)]
m[0][1] = 5
print(m)
print(sum(x for x in range(10)), max(len(w) for w in ["a", "abc"]))
nested = {"a": [1, {"b": 2}]}
print(nested["a"][1]["b"])
print([1, 2, 3][1:], [1, 2, 3][:-1], list("abc"), tuple([1, 2]), set())
print(sorted({"b": 1, "a": 2}), sorted({"b": 1, "a": 2}.items(), key=lambda kv: kv[1]))
x = [1, 2]
y = x
y += [3]
print(x, x is y, x == [1, 2, 3])
