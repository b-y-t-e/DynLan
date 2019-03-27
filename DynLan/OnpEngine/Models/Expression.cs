using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DynLan;
#if !NETCE
using System.Runtime.Serialization;
#endif
using DynLan.OnpEngine.Logic;


namespace DynLan.OnpEngine.Models
{
#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class Expression
    {
#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public String ID { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public Boolean IsOnpExecution { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public ExpressionTokens Tokens { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public ExpressionTokens OnpTokens { get; set; }

        //////////////////////////////

        public Expression() :
            this(null)
        {
        }

        public Expression(String ID)
        {
            this.ID = ID ?? IdGenerator.Generate();
            this.IsOnpExecution = true;
        }

        //////////////////////////////

        public override string ToString()
        {
            return String.Format(
                "{0}",
                //ID,
                String.Join(" ", Linq2.ToArray(Linq2.Select(this.Tokens, i => i.TokenName))));
        }

        public virtual Expression Clone()
        {
            Expression item = (Expression)this.MemberwiseClone();
            if (item.Tokens != null)
                item.Tokens = new ExpressionTokens(Linq2.Select(item.Tokens, i => i.Clone()));
            if (item.OnpTokens != null)
                item.OnpTokens = new ExpressionTokens(Linq2.Select(item.OnpTokens, i => i.Clone()));
            return item;
        }
    }
}