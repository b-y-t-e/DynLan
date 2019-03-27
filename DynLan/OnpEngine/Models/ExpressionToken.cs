using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if !NETCE
using System.Runtime.Serialization;
#endif
using DynLan.OnpEngine.Logic;
#if PCL
using System.Linq2;
#endif

namespace DynLan.OnpEngine.Models
{
#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class ExpressionToken
    {
#if !NET20
        [IgnoreDataMember]
#endif
        private IList<Char> tokenChars;

        ////////////////////////////////////////////////////

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public TokenType TokenType { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public String TokenName { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public Int32 Priority { get; set; }

#if !NET20
        [IgnoreDataMember]
#endif
        public IList<Char> TokenChars
        {
            get
            {
                if (tokenChars == null && TokenName != null)
                    tokenChars = Linq2.ToList(TokenName.ToCharArray());
                return tokenChars;
            }
            set { tokenChars = value; }
        }

        ////////////////////////////////////////////////////

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public Int32? TokenLength { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public OnpTokenData TokenData { get; set; }

        ////////////////////////////////////////////////////

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public Int32? StartIndex { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public Int32? EndIndex { get; set; }

        ////////////////////////////////////////////////////

        public ExpressionToken(String Token, TokenType TokenType)
        {
            this.Set(Token);
            this.TokenType = TokenType;
        }

        public ExpressionToken(Char TokenChar, TokenType TokenType)
        {
            this.Set(new[] { TokenChar });
            this.TokenType = TokenType;
        }

        public ExpressionToken(IList<Char> TokenChars, TokenType TokenType)
        {
            this.Set(TokenChars);
            this.TokenType = TokenType;
        }

        ////////////////////////////////////////////////////

        public Int32 GetFinalTokenLength()
        {
            return TokenLength == null ? (TokenChars != null ? TokenChars.Count : 0) : TokenLength.Value;
        }
        
        public void Set(String TokenName)
        {
            Set(TokenName, true);
        }

        public void Set(String TokenName, Boolean CorrectPriority)
        {
            this.TokenChars = Linq2.ToArray(TokenName.ToCharArray());
            this.TokenName = TokenName;

            if (CorrectPriority)
                this.Priority = OnpOnpTokenHelper.GetPriority(this);
        }
        
        public void Set(IList<Char> TokenName)
        {
            Set(TokenName, true);
        }

        public void Set(IList<Char> TokenName, Boolean CorrectPriority)
        {
            this.TokenChars = TokenName;
            this.TokenName = new String(Linq2.ToArray(this.TokenChars));

            if (CorrectPriority)
                this.Priority = OnpOnpTokenHelper.GetPriority(this);
        }

        ////////////////////////////////////////////////////

        public override string ToString()
        {
            return String.Format(
                "{0} {1}",
                TokenType,
                TokenName);
        }

        public ExpressionToken Clone()
        {
            ExpressionToken item = (ExpressionToken)this.MemberwiseClone();
            item.TokenChars = Linq2.ToArray(this.TokenChars);
            if (item.TokenData != null)
                item.TokenData = item.TokenData.Clone();
            return item;
        }
    }

#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class OnpTokenData
    {
#if !NET20
        [DataMember]
#endif
        public Int32? FunctionParametersCount { get; set; }

        public OnpTokenData Clone()
        {
            return (OnpTokenData)this.MemberwiseClone();
        }
    }
}
