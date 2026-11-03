using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace DynLan2
{
    internal enum TK : byte { Name, Int, Float, Str, FStr, Op, Newline, Indent, Dedent, EOF }

    internal struct Token
    {
        public TK Kind;
        public string S;      // name / operator / decoded string / raw f-string body
        public object V;      // numeric value (long, BigInteger, double)
        public int Line;
        public int Col;
        public bool Raw;      // raw f-string
        public bool Kw;       // Name token that is a reserved word
        public sbyte Bin;     // binary operator precedence level + 1 (0 = not a binary operator)
        public override string ToString() => Kind + ":" + S;
    }

    /// <summary>Interns identifiers straight from the source buffer (no substring per occurrence).</summary>
    internal sealed class NameTable
    {
        string[] _names = new string[1024];
        bool[] _kw = new bool[1024];
        int[] _hashes = new int[1024];
        int _count;
        public int Count => _count;

        static readonly string[] Reserved =
        {
            "False","None","True","and","as","assert","async","await","break","class","continue","def","del",
            "elif","else","except","finally","for","from","global","if","import","in","is","lambda","nonlocal",
            "not","or","pass","raise","return","try","while","with","yield"
        };

        public NameTable()
        {
            foreach (var k in Reserved) Get(k, 0, k.Length, out _, true);
        }

        public string Get(string src, int start, int len, out bool kw, bool markKw = false)
        {
            uint h = 2166136261;
            for (int i = 0; i < len; i++) h = (h ^ src[start + i]) * 16777619;
            int hash = (int)(h & 0x7fffffff);
            int mask = _names.Length - 1;
            int slot = hash & mask;
            while (true)
            {
                var s = _names[slot];
                if (s == null) break;
                if (_hashes[slot] == hash && s.Length == len && string.CompareOrdinal(s, 0, src, start, len) == 0)
                {
                    kw = _kw[slot];
                    return s;
                }
                slot = (slot + 1) & mask;
            }
            string name = src.Substring(start, len);
            _names[slot] = name;
            _hashes[slot] = hash;
            _kw[slot] = markKw;
            kw = markKw;
            if (++_count * 2 > _names.Length) Grow();
            return name;
        }

        void Grow()
        {
            var on = _names; var ok = _kw; var oh = _hashes;
            _names = new string[on.Length * 2];
            _kw = new bool[on.Length * 2];
            _hashes = new int[on.Length * 2];
            int mask = _names.Length - 1;
            for (int i = 0; i < on.Length; i++)
            {
                if (on[i] == null) continue;
                int slot = oh[i] & mask;
                while (_names[slot] != null) slot = (slot + 1) & mask;
                _names[slot] = on[i];
                _kw[slot] = ok[i];
                _hashes[slot] = oh[i];
            }
        }
    }

    /// <summary>Single-pass tokenizer producing Python-style INDENT / DEDENT tokens.</summary>
    internal sealed class Lexer
    {
        readonly string _src;
        readonly string _file;
        int _pos;
        int _line = 1;
        int _lineStart;
        int _paren;
        bool _atLineStart = true;
        readonly List<int> _indents = new List<int> { 0 };
        readonly List<Token> _out;
        readonly NameTable _names = SharedNames();

        [ThreadStatic] static NameTable _shared;

        /// <summary>One identifier table per thread (f-string sub-expressions and repeated compiles reuse it).</summary>
        static NameTable SharedNames()
        {
            if (_shared == null || _shared.Count > 200_000) _shared = new NameTable();
            return _shared;
        }

        static readonly string[] Ops3 = { "**=", "//=", ">>=", "<<=", "..." };
        static readonly string[] Ops2 = { "->", ":=", "**", "//", "==", "!=", "<=", ">=", "<<", ">>", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "@=" };
        const string Ops1 = "+-*/%@&|^~<>()[]{},:.;=!";

        public Lexer(string src, string file = "<script>")
        {
            _src = src ?? "";
            _file = file;
            _out = new List<Token>(Math.Max(16, _src.Length / 4));
        }

        public static List<Token> Tokenize(string src, string file = "<script>") => new Lexer(src, file).Run();

        ScriptSyntaxException Error(string msg) =>
            new ScriptSyntaxException(msg, _file, _line, _pos - _lineStart + 1);

        void Add(TK kind, string s, int col, object v = null) =>
            _out.Add(new Token { Kind = kind, S = s, V = v, Line = _line, Col = col });

        public List<Token> Run()
        {
            string src = _src;
            int n = src.Length;
            if (n > 0 && src[0] == '﻿') _pos = 1;

            while (true)
            {
                if (_atLineStart && _paren == 0)
                {
                    if (!HandleIndent()) break;
                    continue;
                }
                if (_pos >= n) break;
                char c = src[_pos];
                int col = _pos - _lineStart + 1;

                if (c == ' ' || c == '\t' || c == '\f') { _pos++; continue; }
                if (c == '#') { while (_pos < n && src[_pos] != '\n' && src[_pos] != '\r') _pos++; continue; }
                if (c == '\\')
                {
                    int p = _pos + 1;
                    if (p < n && src[p] == '\r') p++;
                    if (p < n && src[p] == '\n') p++;
                    if (p == _pos + 1) throw Error("unexpected character after line continuation character");
                    _pos = p; _line++; _lineStart = p;
                    continue;
                }
                if (c == '\r' || c == '\n')
                {
                    _pos++;
                    if (c == '\r' && _pos < n && src[_pos] == '\n') _pos++;
                    if (_paren == 0)
                    {
                        Add(TK.Newline, "\n", col);
                        _atLineStart = true;
                    }
                    _line++; _lineStart = _pos;
                    continue;
                }

                if (IsIdStart(c))
                {
                    int start = _pos;
                    while (_pos < n && IsIdChar(src[_pos])) _pos++;
                    int wlen = _pos - start;
                    if (wlen <= 2 && _pos < n && (src[_pos] == '\'' || src[_pos] == '"'))
                    {
                        string pre = src.Substring(start, wlen);
                        if (IsStrPrefix(pre))
                        {
                            ReadString(pre.ToLowerInvariant(), col);
                            continue;
                        }
                    }
                    string word = _names.Get(src, start, wlen, out bool isKw);
                    _out.Add(new Token { Kind = TK.Name, S = word, Line = _line, Col = col, Kw = isKw });
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && _pos + 1 < n && char.IsDigit(src[_pos + 1])))
                {
                    ReadNumber(col);
                    continue;
                }

                if (c == '\'' || c == '"') { ReadString("", col); continue; }

                string op = MatchOp();
                if (op == null) throw Error("invalid character '" + c + "'");
                if (op == "(" || op == "[" || op == "{") _paren++;
                else if (op == ")" || op == "]" || op == "}") { if (_paren > 0) _paren--; }
                _out.Add(new Token { Kind = TK.Op, S = op, Line = _line, Col = col, Bin = BinLevel(op) });
            }

            if (_out.Count > 0 && _out[_out.Count - 1].Kind != TK.Newline && _out[_out.Count - 1].Kind != TK.Dedent)
                Add(TK.Newline, "\n", 0);
            while (_indents.Count > 1)
            {
                _indents.RemoveAt(_indents.Count - 1);
                Add(TK.Dedent, "", 0);
            }
            Add(TK.EOF, "", 0);
            return _out;
        }

        /// <summary>Measures indentation at line start; emits INDENT/DEDENT. Returns false at EOF.</summary>
        bool HandleIndent()
        {
            string src = _src;
            int n = src.Length;
            int width = 0;
            int p = _pos;
            while (p < n)
            {
                char c = src[p];
                if (c == ' ') width++;
                else if (c == '\t') width = (width / 8 + 1) * 8;
                else if (c == '\f') width = 0;
                else break;
                p++;
            }
            if (p >= n) { _pos = p; return false; }
            char ch = src[p];
            if (ch == '#' || ch == '\r' || ch == '\n' || (ch == '\\' && false))
            {
                // blank / comment-only line
                while (p < n && src[p] != '\n' && src[p] != '\r') p++;
                if (p < n) { if (src[p] == '\r' && p + 1 < n && src[p + 1] == '\n') p++; p++; }
                _pos = p; _line++; _lineStart = p;
                return p < n || true;
            }
            _pos = p;
            _atLineStart = false;
            int cur = _indents[_indents.Count - 1];
            int col = p - _lineStart + 1;
            if (width > cur)
            {
                _indents.Add(width);
                Add(TK.Indent, "", col);
            }
            else if (width < cur)
            {
                while (_indents.Count > 1 && _indents[_indents.Count - 1] > width)
                {
                    _indents.RemoveAt(_indents.Count - 1);
                    Add(TK.Dedent, "", col);
                }
                if (_indents[_indents.Count - 1] != width)
                    throw Error("unindent does not match any outer indentation level");
            }
            return true;
        }

        static bool IsStrPrefix(string w)
        {
            switch (w.ToLowerInvariant())
            {
                case "r": case "u": case "f": case "b": case "rf": case "fr": case "br": case "rb": return true;
                default: return false;
            }
        }

        static bool IsIdStart(char c) => c == '_' || (c < 128 ? (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') : char.IsLetter(c));
        static bool IsIdChar(char c) => c == '_' || (c < 128 ? (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') : char.IsLetterOrDigit(c));

        static sbyte BinLevel(string op)
        {
            switch (op)
            {
                case "|": return 1;
                case "^": return 2;
                case "&": return 3;
                case "<<": case ">>": return 4;
                case "+": case "-": return 5;
                case "*": case "/": case "//": case "%": case "@": return 6;
                default: return 0;
            }
        }

        static readonly string[] Single = BuildSingle();

        static string[] BuildSingle()
        {
            var a = new string[128];
            foreach (char c in Ops1) a[c] = c.ToString();
            return a;
        }

        string MatchOp()
        {
            string src = _src;
            int p = _pos;
            char c = src[p];
            char c1 = p + 1 < src.Length ? src[p + 1] : '\0';
            char c2 = p + 2 < src.Length ? src[p + 2] : '\0';
            string r = null;
            switch (c)
            {
                case '*': r = c1 == '*' ? (c2 == '=' ? "**=" : "**") : c1 == '=' ? "*=" : "*"; break;
                case '/': r = c1 == '/' ? (c2 == '=' ? "//=" : "//") : c1 == '=' ? "/=" : "/"; break;
                case '>': r = c1 == '>' ? (c2 == '=' ? ">>=" : ">>") : c1 == '=' ? ">=" : ">"; break;
                case '<': r = c1 == '<' ? (c2 == '=' ? "<<=" : "<<") : c1 == '=' ? "<=" : "<"; break;
                case '.': r = c1 == '.' && c2 == '.' ? "..." : "."; break;
                case '-': r = c1 == '>' ? "->" : c1 == '=' ? "-=" : "-"; break;
                case ':': r = c1 == '=' ? ":=" : ":"; break;
                case '=': r = c1 == '=' ? "==" : "="; break;
                case '!': r = c1 == '=' ? "!=" : "!"; break;
                case '+': case '%': case '&': case '|': case '^': case '@':
                    r = c1 == '=' ? c.ToString() + "=" : Single[c];
                    if (c1 == '=') r = string.Intern(r);
                    break;
                default:
                    if (c < 128) r = Single[c];
                    break;
            }
            if (r == null) return null;
            _pos += r.Length;
            return r;
        }

        void ReadNumber(int col)
        {
            string src = _src;
            int n = src.Length;
            int start = _pos;
            if (src[_pos] == '0' && _pos + 1 < n && "xXoObB".IndexOf(src[_pos + 1]) >= 0)
            {
                char kind = char.ToLowerInvariant(src[_pos + 1]);
                _pos += 2;
                int ds = _pos;
                while (_pos < n && (Uri.IsHexDigit(src[_pos]) || src[_pos] == '_')) _pos++;
                string digits = src.Substring(ds, _pos - ds).Replace("_", "");
                if (digits.Length == 0) throw Error("invalid number literal");
                int radix = kind == 'x' ? 16 : kind == 'o' ? 8 : 2;
                BigInteger v = BigInteger.Zero;
                foreach (char d in digits)
                {
                    int dv = Uri.FromHex(d);
                    if (dv >= radix) throw Error("invalid digit '" + d + "' in number literal");
                    v = v * radix + dv;
                }
                AddInt(v, src.Substring(start, _pos - start), col);
                return;
            }
            bool isFloat = false;
            // fast path: plain decimal integer
            {
                long v = 0;
                int p = _pos;
                while (p < n && src[p] >= '0' && src[p] <= '9' && p - start < 18) { v = v * 10 + (src[p] - '0'); p++; }
                if (p < n && !(src[p] >= '0' && src[p] <= '9') && src[p] != '_' && src[p] != '.' && src[p] != 'e' && src[p] != 'E' && src[p] != 'j' && src[p] != 'J'
                    && !(p - start > 1 && src[start] == '0' && v != 0))
                {
                    _pos = p;
                    _out.Add(new Token { Kind = TK.Int, V = v, Line = _line, Col = col });
                    return;
                }
            }
            while (_pos < n && (char.IsDigit(src[_pos]) || src[_pos] == '_')) _pos++;
            if (_pos < n && src[_pos] == '.' && !(_pos + 1 < n && src[_pos + 1] == '.'))
            {
                isFloat = true;
                _pos++;
                while (_pos < n && (char.IsDigit(src[_pos]) || src[_pos] == '_')) _pos++;
            }
            if (_pos < n && (src[_pos] == 'e' || src[_pos] == 'E'))
            {
                int save = _pos;
                _pos++;
                if (_pos < n && (src[_pos] == '+' || src[_pos] == '-')) _pos++;
                if (_pos < n && char.IsDigit(src[_pos]))
                {
                    isFloat = true;
                    while (_pos < n && (char.IsDigit(src[_pos]) || src[_pos] == '_')) _pos++;
                }
                else _pos = save;
            }
            if (_pos < n && (src[_pos] == 'j' || src[_pos] == 'J')) throw Error("complex numbers are not supported");
            string text = src.Substring(start, _pos - start).Replace("_", "");
            if (isFloat)
            {
                double d = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                _out.Add(new Token { Kind = TK.Float, S = text, V = d, Line = _line, Col = col });
            }
            else
            {
                if (text.Length > 1 && text[0] == '0' && text.Trim('0').Length > 0)
                    throw Error("leading zeros in decimal integer literals are not permitted");
                if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long l))
                    _out.Add(new Token { Kind = TK.Int, S = text, V = l, Line = _line, Col = col });
                else
                    AddInt(BigInteger.Parse(text, CultureInfo.InvariantCulture), text, col);
            }
        }

        void AddInt(BigInteger v, string text, int col)
        {
            object val = (v >= long.MinValue && v <= long.MaxValue) ? (object)(long)v : v;
            _out.Add(new Token { Kind = TK.Int, S = text, V = val, Line = _line, Col = col });
        }

        void ReadString(string prefix, int col)
        {
            bool raw = prefix.Contains("r");
            bool fstr = prefix.Contains("f");
            if (prefix.Contains("b")) throw Error("bytes literals are not supported");
            string src = _src;
            int n = src.Length;
            char q = src[_pos];
            bool triple = _pos + 2 < n && src[_pos + 1] == q && src[_pos + 2] == q;
            int startLine = _line;
            _pos += triple ? 3 : 1;
            int bodyStart = _pos;
            if (!triple && !fstr)
            {
                // fast path: single-line literal without escapes -> one substring, no builder
                int p = _pos;
                while (p < n && src[p] != q && src[p] != '\\' && src[p] != '\n' && src[p] != '\r') p++;
                if (p < n && src[p] == q)
                {
                    _out.Add(new Token { Kind = TK.Str, S = src.Substring(_pos, p - _pos), Line = startLine, Col = col, Raw = raw });
                    _pos = p + 1;
                    return;
                }
            }
            var sb = (raw || fstr) ? null : new StringBuilder();
            while (true)
            {
                if (_pos >= n) throw new ScriptSyntaxException("unterminated string literal", _file, startLine, col);
                char c = src[_pos];
                if (c == q)
                {
                    if (!triple) break;
                    if (_pos + 2 < n && src[_pos + 1] == q && src[_pos + 2] == q) break;
                }
                if (!triple && (c == '\n' || c == '\r'))
                    throw new ScriptSyntaxException("unterminated string literal", _file, startLine, col);
                if (c == '\n') { _line++; _lineStart = _pos + 1; }
                if (c == '\\' && _pos + 1 < n)
                {
                    if (sb != null)
                    {
                        _pos++;
                        DecodeEscape(src, ref _pos, sb);
                        continue;
                    }
                    if (src[_pos + 1] == '\n') { _line++; _lineStart = _pos + 2; }
                    _pos += 2;
                    continue;
                }
                sb?.Append(c);
                _pos++;
            }
            string body = sb != null ? sb.ToString() : src.Substring(bodyStart, _pos - bodyStart);
            _pos += triple ? 3 : 1;
            _out.Add(new Token { Kind = fstr ? TK.FStr : TK.Str, S = body, Line = startLine, Col = col, Raw = raw });
        }

        /// <summary>Decodes an escape; <paramref name="p"/> points just after the backslash.</summary>
        internal static void DecodeEscape(string s, ref int p, StringBuilder sb)
        {
            char e = s[p++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case '0':
                case '1': case '2': case '3': case '4': case '5': case '6': case '7':
                    {
                        int v = e - '0';
                        for (int k = 0; k < 2 && p < s.Length && s[p] >= '0' && s[p] <= '7'; k++) v = v * 8 + (s[p++] - '0');
                        sb.Append((char)v);
                        break;
                    }
                case '\\': sb.Append('\\'); break;
                case '\'': sb.Append('\''); break;
                case '"': sb.Append('"'); break;
                case 'a': sb.Append('\a'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'v': sb.Append('\v'); break;
                case '\n': break;
                case '\r': if (p < s.Length && s[p] == '\n') p++; break;
                case 'x': sb.Append((char)Convert.ToInt32(s.Substring(p, 2), 16)); p += 2; break;
                case 'u': sb.Append((char)Convert.ToInt32(s.Substring(p, 4), 16)); p += 4; break;
                case 'U': sb.Append(char.ConvertFromUtf32(Convert.ToInt32(s.Substring(p, 8), 16))); p += 8; break;
                default: sb.Append('\\').Append(e); break;
            }
        }
    }
}
