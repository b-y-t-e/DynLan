def div(a, b):
    try:
        return a / b
    except ZeroDivisionError as e:
        return "err: " + str(e)
    finally:
        print("finally", a)
print(div(1, 2), div(1, 0))
try:
    [][1]
except (KeyError, IndexError) as e:
    print(type(e).__name__, e)
try:
    {}["k"]
except LookupError as e:
    print("lookup", repr(e), str(e))
class MyErr(ValueError):
    def __init__(self, code):
        super().__init__("code " + str(code))
        self.code = code
try:
    raise MyErr(42)
except ValueError as e:
    print("caught", e, e.code, e.args)
def nested():
    try:
        try:
            raise TypeError("inner")
        except TypeError:
            print("handling inner")
            raise
    except Exception as e:
        print("outer got", repr(e))
nested()
def fin_return():
    try:
        return "try"
    finally:
        print("cleanup")
print(fin_return())
for i in range(5):
    try:
        if i == 1:
            continue
        if i == 3:
            break
        print("loop", i)
    finally:
        print("fin", i)
try:
    x = undefined_name
except NameError as e:
    print("NameError:", e)
try:
    int("abc")
except ValueError as e:
    print(e)
try:
    "a" + 1
except TypeError as e:
    print(e)
try:
    assert 1 == 2, "math broke"
except AssertionError as e:
    print("assert:", e)
def gen_err():
    yield 1
    raise RuntimeError("in gen")
try:
    for v in gen_err():
        print("v", v)
except RuntimeError as e:
    print(e)
try:
    try:
        raise ValueError("a")
    finally:
        print("inner finally")
except ValueError:
    print("outer except")
else:
    print("not here")
try:
    pass
except Exception:
    pass
else:
    print("else runs")
def deep(n):
    if n == 0:
        raise KeyError("deep")
    return deep(n - 1)
try:
    deep(50)
except KeyError as e:
    print("deep", e)
try:
    raise ValueError("x") from KeyError("y")
except ValueError as e:
    print(type(e.__cause__).__name__)
def f():
    x = 1
    def g():
        return y
    y = 2
    return g()
print(f())
def unbound():
    if False:
        z = 1
    return z
try:
    unbound()
except UnboundLocalError as e:
    print("unbound ok")
