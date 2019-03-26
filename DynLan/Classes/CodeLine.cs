using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.Classes
{
    public class CodeLine : List<Char>
    {
        public Int32 LineIndex;

        public CodeLine()
        {

        }

        public CodeLine(String Text)
        {
            this.AddRange(Text.ToCharArray());
        }

        public CodeLine(IEnumerable<Char> Chars)
        {
            this.AddRange(Chars);
        }

        public override string ToString()
        {
#if PCL || NET20
            StringBuilder str = new StringBuilder();
            foreach (char ch in this)
                str.Append(ch);
            return str.ToString();
#else
            return String.Join("", this.ToArray()); // base.ToString();
#endif
        }
    }
}
