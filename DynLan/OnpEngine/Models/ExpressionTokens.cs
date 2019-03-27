using DynLan.OnpEngine.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace DynLan.OnpEngine.Models
{
    public class ExpressionTokens : List<ExpressionToken>
    {
        public ExpressionTokens()
        {

        }

        public ExpressionTokens(IEnumerable<ExpressionToken> Items)
            : base(Items)
        {

        }

        public ExpressionTokens(ExpressionToken Item)
        {
            if (Item != null)
                this.Add(Item);
        }

        //////////////////////////////////////////////////

        public Int32 IndexOfSequence(IEnumerable<ExpressionToken> Sequence)
        {
            Int32 index = -1;
#if !NET20
            if (Sequence.Any())
#else
            if (Linq2.Any(Sequence))
#endif

            {
#if !NET20
                index = this.IndexOf(Sequence.First());
#else
                index = this.IndexOf(Linq2.FirstOrDefault(Sequence));
#endif
                if (index >= 0)
                {
                    Int32 nextIndex = index;
                    foreach (ExpressionToken item in Sequence)
                    {
                        if (this.IndexOf(item) != nextIndex)
                            return -1;
                        nextIndex++;
                    }
                }
            }

            return index;
        }

        public Int32 RemoveSequence(IEnumerable<ExpressionToken> Sequence)
        {
            Int32 index = -1;
#if !NET20
            if (Sequence.Any())
#else
            if (Linq2.Any(Sequence))
#endif
            {
#if !NET20
                index = this.IndexOf(Sequence.First());
#else
                index = this.IndexOf(Linq2.FirstOrDefault(Sequence));
#endif
                if (index >= 0)
                {
                    Int32 nextIndex = index;
                    foreach (ExpressionToken item in Sequence)
                    {
                        if (this.IndexOf(item) != nextIndex)
                            return -1;
                        nextIndex++;
                    }

                    foreach (ExpressionToken item in Sequence)
                        this.Remove(item);
                }
            }

            return index;
        }

        public Boolean CloseInBrackets()
        {
#if !NET20
            if (this.Count >= 1 &&
                this.First().TokenType != TokenType.BRACKET_BEGIN &&
                this.Last().TokenType != TokenType.BRACKET_END)
#else
            if (this.Count >= 1 &&
                Linq2.FirstOrDefault(this).TokenType != TokenType.BRACKET_BEGIN &&
                Linq2.LastOrDefault(this).TokenType != TokenType.BRACKET_END)
#endif
            {
                this.Insert(0, new ExpressionToken(new[] { '(' }, TokenType.BRACKET_BEGIN));
                this.Add(new ExpressionToken(new[] { ')' }, TokenType.BRACKET_END));
                return true;
            }
            return false;
        }

        public Boolean InBrackets()
        {
#if !NET20
            if (this.Count >= 1 &&
                this.First().TokenType != TokenType.BRACKET_BEGIN &&
                this.Last().TokenType != TokenType.BRACKET_END)
#else
            if (this.Count >= 1 &&
                Linq2.FirstOrDefault(this).TokenType != TokenType.BRACKET_BEGIN &&
                Linq2.LastOrDefault(this).TokenType != TokenType.BRACKET_END)
#endif
            {
                return false;
            }
            return this.Count > 1;
        }

        public void RemoveBrackets()
        {
            while (this.Count > 1)
            {
#if !NET20
                if (this.First().TokenType == TokenType.BRACKET_BEGIN &&
                   this.Last().TokenType == TokenType.BRACKET_END)
#else
                if (Linq2.FirstOrDefault(this).TokenType == TokenType.BRACKET_BEGIN &&
                   Linq2.LastOrDefault(this).TokenType == TokenType.BRACKET_END)
#endif
                {
                    this.RemoveAt(0);
                    this.RemoveAt(this.Count - 1);
                }
                else
                {
                    break;
                }
            }
        }

        public String JoinToString(Int32? LastIndex)
        {
            StringBuilder str = new StringBuilder();
            for (var i = 0; i <= (LastIndex != null ? LastIndex.Value : this.Count); i++)
                if (i < this.Count)
                    str.Append(this[i].TokenName);
            return str.ToString();
        }
    }
}