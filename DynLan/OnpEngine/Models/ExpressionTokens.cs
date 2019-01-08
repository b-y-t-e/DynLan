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

            if (Sequence.Any())
            {
                index = this.IndexOf(Sequence.First());
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

            if (Sequence.Any())
            {
                index = this.IndexOf(Sequence.First());
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
            if (this.Count >= 1 &&
                this.First().TokenType != TokenType.BRACKET_BEGIN &&
                this.Last().TokenType != TokenType.BRACKET_END)
            {
                this.Insert(0, new ExpressionToken(new[] { '(' }, TokenType.BRACKET_BEGIN));
                this.Add(new ExpressionToken(new[] { ')' }, TokenType.BRACKET_END));
                return true;
            }
            return false;
        }

        public Boolean InBrackets()
        {
            if (this.Count >= 1 &&
                this.First().TokenType != TokenType.BRACKET_BEGIN &&
                this.Last().TokenType != TokenType.BRACKET_END)
            {
                return false;
            }
            return this.Count > 1;
        }

        public void RemoveBrackets()
        {
            while (this.Count > 1)
            {
                if (this.First().TokenType == TokenType.BRACKET_BEGIN &&
                   this.Last().TokenType == TokenType.BRACKET_END)
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