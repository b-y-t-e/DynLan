using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace DynLan2
{
    /// <summary>repr(), float formatting, format-spec mini-language, % and str.format.</summary>
    internal static class Format
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string StrRepr(string s)
        {
            char q = s.IndexOf('\'') >= 0 && s.IndexOf('"') < 0 ? '"' : '\'';
            var sb = new StringBuilder(s.Length + 2);
            sb.Append(q);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c == q) sb.Append('\\').Append(c);
                        else if (c < 0x20 || c == 0x7f) sb.Append("\\x").Append(((int)c).ToString("x2"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append(q);
            return sb.ToString();
        }

        /// <summary>Python repr() of a float: shortest round-trip digits, scientific when exp &lt; -4 or &gt;= 16.</summary>
        public static string FloatRepr(double d)
        {
            if (double.IsNaN(d)) return "nan";
            if (double.IsPositiveInfinity(d)) return "inf";
            if (double.IsNegativeInfinity(d)) return "-inf";
            if (d == 0) return (1 / d) < 0 ? "-0.0" : "0.0";
            string r = d.ToString("R", Inv);
            bool neg = r[0] == '-';
            if (neg) r = r.Substring(1);
            int exp = 0;
            int ei = r.IndexOfAny(new[] { 'E', 'e' });
            string mant = r;
            if (ei >= 0)
            {
                exp = int.Parse(r.Substring(ei + 1), Inv);
                mant = r.Substring(0, ei);
            }
            int point = mant.IndexOf('.');
            string digits = point >= 0 ? mant.Remove(point, 1) : mant;
            if (point < 0) point = mant.Length;
            int lz = 0;
            while (lz < digits.Length - 1 && digits[lz] == '0') lz++;
            digits = digits.Substring(lz);
            point -= lz;
            digits = digits.TrimEnd('0');
            if (digits.Length == 0) digits = "0";
            int e10 = point - 1 + exp;
            var sb = new StringBuilder();
            if (neg) sb.Append('-');
            if (e10 < -4 || e10 >= 16)
            {
                sb.Append(digits[0]);
                if (digits.Length > 1) sb.Append('.').Append(digits, 1, digits.Length - 1);
                sb.Append('e').Append(e10 < 0 ? '-' : '+').Append(Math.Abs(e10).ToString("00", Inv));
            }
            else if (e10 >= 0)
            {
                if (digits.Length <= e10 + 1)
                {
                    sb.Append(digits).Append('0', e10 + 1 - digits.Length).Append(".0");
                }
                else
                {
                    sb.Append(digits, 0, e10 + 1).Append('.').Append(digits, e10 + 1, digits.Length - e10 - 1);
                }
            }
            else
            {
                sb.Append("0.").Append('0', -e10 - 1).Append(digits);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ format spec
        struct Spec
        {
            public char Fill, Align, Sign, Type, Group;
            public bool Alt, Zero;
            public int Width, Precision;
        }

        static Spec ParseSpec(string s)
        {
            var sp = new Spec { Fill = ' ', Precision = -1, Width = -1 };
            int i = 0;
            if (s.Length >= 2 && "<>=^".IndexOf(s[1]) >= 0) { sp.Fill = s[0]; sp.Align = s[1]; i = 2; }
            else if (s.Length >= 1 && "<>=^".IndexOf(s[0]) >= 0) { sp.Align = s[0]; i = 1; }
            if (i < s.Length && "+- ".IndexOf(s[i]) >= 0) sp.Sign = s[i++];
            if (i < s.Length && s[i] == 'z') i++;
            if (i < s.Length && s[i] == '#') { sp.Alt = true; i++; }
            if (i < s.Length && s[i] == '0') { sp.Zero = true; i++; }
            int ws = i;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i > ws) sp.Width = int.Parse(s.Substring(ws, i - ws), Inv);
            if (i < s.Length && (s[i] == ',' || s[i] == '_')) sp.Group = s[i++];
            if (i < s.Length && s[i] == '.')
            {
                i++;
                int ps = i;
                while (i < s.Length && char.IsDigit(s[i])) i++;
                if (i == ps) throw Err.Value("Format specifier missing precision");
                sp.Precision = int.Parse(s.Substring(ps, i - ps), Inv);
            }
            if (i < s.Length) sp.Type = s[i++];
            if (i < s.Length) throw Err.Value("Invalid format specifier '" + s + "'");
            return sp;
        }

        public static string FormatValue(Value v, string spec)
        {
            if (v.O is PyInstance inst && inst.Class.Lookup("__format__", out _))
                return Ops.Str(VM.Current.CallMethod(v, "__format__", new[] { Value.Str(spec) }));
            if (string.IsNullOrEmpty(spec)) return Ops.Str(v);
            var sp = ParseSpec(spec);
            if (v.IsBool && sp.Type == '\0') return Pad(Ops.Str(v), sp, '<');
            if (v.IsInt || v.IsBool || v.O is BigInteger)
            {
                if ("eEfFgG%".IndexOf(sp.Type) >= 0) return FormatFloat((double)v.AsBig, sp);
                return FormatInt(v.AsBig, sp);
            }
            if (v.IsFloat) return FormatFloat(v.AsFloat, sp);
            if (v.O is string s)
            {
                if (sp.Type != '\0' && sp.Type != 's') throw Err.Value("Unknown format code '" + sp.Type + "' for object of type 'str'");
                if (sp.Precision >= 0 && s.Length > sp.Precision) s = s.Substring(0, sp.Precision);
                return Pad(s, sp, '<');
            }
            if (v.O is IFormattable f && Interop.IsHostObject(v.O)) return f.ToString(spec, Inv);
            return Pad(Ops.Str(v), sp, '<');
        }

        static string Pad(string body, Spec sp, char defAlign, string signPrefix = "")
        {
            char align = sp.Align != '\0' ? sp.Align : defAlign;
            char fill = sp.Fill;
            if (sp.Zero && sp.Align == '\0') { fill = '0'; align = '='; }
            int total = signPrefix.Length + body.Length;
            if (sp.Width <= total) return signPrefix + body;
            int pad = sp.Width - total;
            switch (align)
            {
                case '<': return signPrefix + body + new string(fill, pad);
                case '>': return new string(fill, pad) + signPrefix + body;
                case '^': return new string(fill, pad / 2) + signPrefix + body + new string(fill, pad - pad / 2);
                default: return signPrefix + new string(fill, pad) + body;
            }
        }

        static string SignOf(bool negative, Spec sp) => negative ? "-" : sp.Sign == '+' ? "+" : sp.Sign == ' ' ? " " : "";

        static string Group(string digits, char sep, int every = 3)
        {
            if (sep == '\0' || digits.Length <= every) return digits;
            var sb = new StringBuilder();
            int first = digits.Length % every;
            if (first > 0) sb.Append(digits, 0, first);
            for (int i = first; i < digits.Length; i += every)
            {
                if (sb.Length > 0) sb.Append(sep);
                sb.Append(digits, i, every);
            }
            return sb.ToString();
        }

        static string FormatInt(BigInteger v, Spec sp)
        {
            bool neg = v.Sign < 0;
            var a = BigInteger.Abs(v);
            string body, prefix = "";
            switch (sp.Type)
            {
                case '\0': case 'd': case 'n': body = Group(a.ToString(Inv), sp.Group); break;
                case 'x': body = ToBase(a, 16, false); prefix = sp.Alt ? "0x" : ""; body = Group(body, sp.Group == '_' ? '_' : '\0', 4); break;
                case 'X': body = ToBase(a, 16, true); prefix = sp.Alt ? "0X" : ""; body = Group(body, sp.Group == '_' ? '_' : '\0', 4); break;
                case 'o': body = ToBase(a, 8, false); prefix = sp.Alt ? "0o" : ""; break;
                case 'b': body = ToBase(a, 2, false); prefix = sp.Alt ? "0b" : ""; body = Group(body, sp.Group == '_' ? '_' : '\0', 4); break;
                case 'c': body = char.ConvertFromUtf32((int)v); return Pad(body, sp, '<');
                default: throw Err.Value("Unknown format code '" + sp.Type + "' for object of type 'int'");
            }
            return Pad(body, sp, '>', SignOf(neg, sp) + prefix);
        }

        internal static string ToBase(BigInteger a, int radix, bool upper)
        {
            if (a.IsZero) return "0";
            const string dl = "0123456789abcdef";
            var sb = new StringBuilder();
            while (!a.IsZero)
            {
                int d = (int)(a % radix);
                sb.Insert(0, upper ? char.ToUpperInvariant(dl[d]) : dl[d]);
                a /= radix;
            }
            return sb.ToString();
        }

        static string FormatFloat(double d, Spec sp)
        {
            bool neg = d < 0 || (d == 0 && 1 / d < 0);
            double a = Math.Abs(d);
            string body;
            char t = sp.Type;
            if (double.IsNaN(d)) body = char.IsUpper(t) ? "NAN" : "nan";
            else if (double.IsInfinity(d)) body = char.IsUpper(t) ? "INF" : "inf";
            else
            {
                switch (t)
                {
                    case 'f':
                    case 'F':
                        body = Fixed(a, sp.Precision < 0 ? 6 : sp.Precision, sp.Group, sp.Alt);
                        break;
                    case 'e':
                    case 'E':
                        body = Sci(a, sp.Precision < 0 ? 6 : sp.Precision, t == 'E', sp.Alt);
                        break;
                    case '%':
                        body = Fixed(a * 100, sp.Precision < 0 ? 6 : sp.Precision, sp.Group, sp.Alt) + "%";
                        break;
                    case 'g':
                    case 'G':
                        body = General(a, sp.Precision < 0 ? 6 : sp.Precision, t == 'G', sp.Alt, sp.Group);
                        break;
                    case '\0':
                        if (sp.Precision < 0)
                        {
                            body = FloatRepr(a);
                            if (sp.Group != '\0')
                            {
                                int dot = body.IndexOf('.');
                                if (dot > 0 && body.IndexOf('e') < 0) body = Group(body.Substring(0, dot), sp.Group) + body.Substring(dot);
                            }
                        }
                        else
                        {
                            body = General(a, sp.Precision, false, sp.Alt, sp.Group);
                            if (body.IndexOf('.') < 0 && body.IndexOf('e') < 0 && body.IndexOf("inf") < 0) body += ".0";
                        }
                        break;
                    default: throw Err.Value("Unknown format code '" + t + "' for object of type 'float'");
                }
            }
            return Pad(body, sp, '>', SignOf(neg, sp));
        }

        static string Fixed(double a, int prec, char group, bool alt)
        {
            string s = a.ToString("F" + prec, Inv);
            if (group != '\0')
            {
                int dot = s.IndexOf('.');
                string ip = dot < 0 ? s : s.Substring(0, dot);
                s = Group(ip, group) + (dot < 0 ? "" : s.Substring(dot));
            }
            if (alt && prec == 0) s += ".";
            return s;
        }

        static string Sci(double a, int prec, bool upper, bool alt)
        {
            string s = a.ToString((upper ? "E" : "e") + prec, Inv);
            // .NET: 1.234560e+003  -> Python: 1.234560e+03
            int ei = s.IndexOfAny(new[] { 'e', 'E' });
            string mant = s.Substring(0, ei);
            int exp = int.Parse(s.Substring(ei + 1), Inv);
            if (alt && prec == 0) mant += ".";
            return mant + (upper ? "E" : "e") + (exp < 0 ? "-" : "+") + Math.Abs(exp).ToString("00", Inv);
        }

        static string General(double a, int prec, bool upper, bool alt, char group)
        {
            if (prec == 0) prec = 1;
            if (a == 0) return alt ? "0." + new string('0', prec - 1) : "0";
            string e = a.ToString("E" + (prec - 1), Inv);
            int exp = int.Parse(e.Substring(e.IndexOf('E') + 1), Inv);
            string r;
            if (exp >= -4 && exp < prec)
            {
                r = Fixed(a, Math.Max(0, prec - 1 - exp), group, false);
                if (!alt && r.IndexOf('.') >= 0) r = r.TrimEnd('0').TrimEnd('.');
            }
            else
            {
                r = Sci(a, prec - 1, upper, false);
                if (!alt)
                {
                    int ei = r.IndexOfAny(new[] { 'e', 'E' });
                    string mant = r.Substring(0, ei);
                    if (mant.IndexOf('.') >= 0) mant = mant.TrimEnd('0').TrimEnd('.');
                    r = mant + r.Substring(ei);
                }
            }
            return r;
        }

        // ------------------------------------------------------------ f-string conversion
        public static string Convert(Value v, int conv, string spec)
        {
            if (conv == 1) v = Value.Str(Ops.Repr(v));
            else if (conv == 2) v = Value.Str(Ops.Str(v));
            else if (conv == 3) v = Value.Str(Ascii(Ops.Repr(v)));
            if (spec == null || spec.Length == 0) return Ops.Str(v);
            return FormatValue(v, spec);
        }

        internal static string Ascii(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c < 128) sb.Append(c);
                else if (c < 256) sb.Append("\\x").Append(((int)c).ToString("x2"));
                else sb.Append("\\u").Append(((int)c).ToString("x4"));
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ printf-style %
        public static string Percent(string fmt, Value args)
        {
            Value[] items;
            PyDict map = null;
            if (args.O is PyTuple t) items = t.Items;
            else
            {
                items = new[] { args };
                if (args.O is PyDict d) map = d;
            }
            int ai = 0;
            var sb = new StringBuilder();
            for (int i = 0; i < fmt.Length; i++)
            {
                char c = fmt[i];
                if (c != '%') { sb.Append(c); continue; }
                i++;
                if (i >= fmt.Length) throw Err.Value("incomplete format");
                if (fmt[i] == '%') { sb.Append('%'); continue; }
                Value arg;
                if (fmt[i] == '(')
                {
                    int close = fmt.IndexOf(')', i);
                    if (close < 0 || map == null) throw Err.Type("format requires a mapping");
                    arg = map.TryGet(Value.Str(fmt.Substring(i + 1, close - i - 1)), out var mv) ? mv : throw Err.Key(Value.Str(fmt.Substring(i + 1, close - i - 1)));
                    i = close + 1;
                }
                else arg = default;
                var spec = new StringBuilder();
                bool left = false;
                while (i < fmt.Length && "-+ #0".IndexOf(fmt[i]) >= 0)
                {
                    if (fmt[i] == '-') left = true;
                    else if (fmt[i] == '0') spec.Append('0');
                    else if (fmt[i] == '+' || fmt[i] == ' ') spec.Insert(0, fmt[i]);
                    else if (fmt[i] == '#') spec.Append('#');
                    i++;
                }
                string sign = spec.ToString();
                var w = new StringBuilder();
                while (i < fmt.Length && (char.IsDigit(fmt[i]) || fmt[i] == '*'))
                {
                    if (fmt[i] == '*') w.Append(Ops.Str(items[ai++]));
                    else w.Append(fmt[i]);
                    i++;
                }
                string prec = "";
                if (i < fmt.Length && fmt[i] == '.')
                {
                    i++;
                    var p = new StringBuilder(".");
                    while (i < fmt.Length && char.IsDigit(fmt[i])) p.Append(fmt[i++]);
                    prec = p.ToString();
                }
                if (i >= fmt.Length) throw Err.Value("incomplete format");
                char type = fmt[i];
                if (arg.IsUnbound)
                {
                    if (ai >= items.Length) throw Err.Type("not enough arguments for format string");
                    arg = items[ai++];
                }
                string signPart = sign.Replace("0", "").Replace("#", "");
                string zero = sign.Contains("0") && !left ? "0" : "";
                string alt = sign.Contains("#") ? "#" : "";
                string align = left ? "<" : "";
                string salign = left ? "<" : ">";
                string piece;
                switch (type)
                {
                    case 's': piece = FormatValue(Value.Str(Ops.Str(arg)), salign + w + prec); break;
                    case 'r': piece = FormatValue(Value.Str(Ops.Repr(arg)), salign + w + prec); break;
                    case 'a': piece = FormatValue(Value.Str(Ascii(Ops.Repr(arg))), salign + w + prec); break;
                    case 'd':
                    case 'i':
                    case 'u':
                        if (arg.IsFloat) arg = Value.BigInt(new BigInteger(Math.Truncate(arg.AsFloat)));
                        else if (!arg.IsIntegral) throw Err.Type("%" + type + " format: a real number is required, not " + Ops.TypeName(arg));
                        piece = FormatValue(arg, align + signPart + zero + w + "d");
                        break;
                    case 'x': case 'X': case 'o':
                        piece = FormatValue(arg, align + signPart + alt + zero + w + type);
                        break;
                    case 'c':
                        piece = arg.IsStr ? arg.AsStr : char.ConvertFromUtf32((int)arg.N);
                        break;
                    case 'f': case 'F': case 'e': case 'E': case 'g': case 'G':
                        piece = FormatValue(Value.Float(arg.ToDouble()), align + signPart + alt + zero + w + prec + type);
                        break;
                    default: throw Err.Value("unsupported format character '" + type + "'");
                }
                sb.Append(piece);
            }
            if (map == null && ai < items.Length && !(args.O is PyDict)) throw Err.Type("not all arguments converted during string formatting");
            return sb.ToString();
        }

        // ------------------------------------------------------------ str.format
        public static string StrFormat(string fmt, ReadOnlySpan<Value> args, KwArgs kw)
        {
            var sb = new StringBuilder();
            int auto = 0;
            for (int i = 0; i < fmt.Length; i++)
            {
                char c = fmt[i];
                if (c == '{')
                {
                    if (i + 1 < fmt.Length && fmt[i + 1] == '{') { sb.Append('{'); i++; continue; }
                    int depth = 1, j = i + 1;
                    for (; j < fmt.Length; j++)
                    {
                        if (fmt[j] == '{') depth++;
                        else if (fmt[j] == '}' && --depth == 0) break;
                    }
                    if (j >= fmt.Length) throw Err.Value("Single '{' encountered in format string");
                    string field = fmt.Substring(i + 1, j - i - 1);
                    i = j;
                    string spec = "";
                    char conv = '\0';
                    int colon = IndexTopLevel(field, ':');
                    if (colon >= 0) { spec = field.Substring(colon + 1); field = field.Substring(0, colon); }
                    int bang = field.IndexOf('!');
                    if (bang >= 0) { conv = field[bang + 1]; field = field.Substring(0, bang); }
                    if (spec.IndexOf('{') >= 0) spec = StrFormat(spec, args, kw);
                    // field name: [name|index](.attr|[key])*
                    int k = 0;
                    while (k < field.Length && field[k] != '.' && field[k] != '[') k++;
                    string head = field.Substring(0, k);
                    Value v;
                    if (head.Length == 0)
                    {
                        if (auto >= args.Length) throw Err.Index("Replacement index " + auto + " out of range for positional args tuple");
                        v = args[auto++];
                    }
                    else if (char.IsDigit(head[0]))
                    {
                        int idx = int.Parse(head, Inv);
                        if (idx >= args.Length) throw Err.Index("Replacement index " + idx + " out of range for positional args tuple");
                        v = args[idx];
                    }
                    else
                    {
                        if (kw == null || !kw.TryGet(head, out v)) throw Err.Key(Value.Str(head));
                    }
                    while (k < field.Length)
                    {
                        if (field[k] == '.')
                        {
                            int e = k + 1;
                            while (e < field.Length && field[e] != '.' && field[e] != '[') e++;
                            v = Ops.GetAttr(v, field.Substring(k + 1, e - k - 1));
                            k = e;
                        }
                        else
                        {
                            int e = field.IndexOf(']', k);
                            string key = field.Substring(k + 1, e - k - 1);
                            v = Ops.GetItem(v, long.TryParse(key, out long li) ? Value.Int(li) : Value.Str(key));
                            k = e + 1;
                        }
                    }
                    sb.Append(Convert(v, conv == 'r' ? 1 : conv == 's' ? 2 : conv == 'a' ? 3 : 0, spec));
                }
                else if (c == '}')
                {
                    if (i + 1 < fmt.Length && fmt[i + 1] == '}') { sb.Append('}'); i++; continue; }
                    throw Err.Value("Single '}' encountered in format string");
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static int IndexTopLevel(string s, char ch)
        {
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '[') depth++;
                else if (s[i] == ']') depth--;
                else if (s[i] == ch && depth == 0) return i;
            }
            return -1;
        }
    }
}
