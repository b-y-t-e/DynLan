import math
print([math.log2(2 ** k) for k in range(-60, 64, 7)])
print(math.log2(2 ** 29), math.log2(2 ** 31), math.log2(2 ** 47), math.log2(1), math.log2(0.5), math.log2(2 ** -1074))
print(math.log2(1024), math.log2(10 ** 300 // 10 ** 300 * 8))
print(math.copysign(3, -0.0), math.copysign(-3, 2), math.copysign(0.0, -1), math.copysign(float("inf"), -1), math.copysign(float("nan"), -1))
print([(2 ** k).bit_length() for k in range(0, 200, 13)], (-1).bit_length(), (0).bit_length(), (-(2 ** 100)).bit_length(), (255).bit_length())
print(math.log10(1000), math.log(math.e), math.log(8, 2), math.sqrt(2), math.hypot(3, 4))
