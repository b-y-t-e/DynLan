using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using DynLan.OnpEngine.Models;

namespace DynLan.Classes
{
    [DataContract(IsReference = true)]
    public class DynLanCodeLine
    {
        [DataMember]
        public Guid ID { get; set; }

        //////////////////////////////////////////////

        [DataMember]
        public String Code { get; set; }

        [DataMember]
        public EOperatorType OperatorType { get; set; }

        [DataMember]
        public ExpressionGroup ExpressionGroup { get; set; }

        [DataMember]
        public Int32 Depth { get; set; }

        [DataMember]
        public Boolean IsLineEmpty { get; set; }

        //////////////////////////////////////////////////

        public DynLanCodeLine()
        {
            Code = "";
            OperatorType = EOperatorType.NONE;
            ID = Guid.NewGuid();
        }

        //////////////////////////////////////////////////

        public Boolean ContainsAnyExpressions()
        {
            return
                ExpressionGroup != null &&
                ExpressionGroup.MainExpression != null &&
                ExpressionGroup.MainExpression.Tokens != null &&
                ExpressionGroup.MainExpression.Tokens.Count > 0;
        }

        //////////////////////////////////////////////////

        public override string ToString()
        {
            return String.Format("{0} {1}", OperatorType, ExpressionGroup.MainExpression.Tokens.JoinToString(null));
        }

        public virtual DynLanCodeLine Clone()
        {
            DynLanCodeLine item = (DynLanCodeLine)this.MemberwiseClone();
            if (item.ExpressionGroup != null)
                item.ExpressionGroup = item.ExpressionGroup.Clone();    
            return item;
        }
    }

    public enum EOperatorType
    {
        NONE,
        RETURN,
        IF,
        ELSE,
        ELIF,
        WHILE,
        FOR,
        TRY,
        CATCH,
        FINALLY,
        THROW,
        PASS,
        BREAK
    }
}
