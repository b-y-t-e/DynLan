using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DynLan;
using System.Runtime.Serialization;
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

        public Expression(String ID = null)
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
                String.Join(" ", this.Tokens.Select(i => i.TokenName).ToArray()));
        }

        public virtual Expression Clone()
        {
            Expression item = (Expression)this.MemberwiseClone();
            if (item.Tokens != null)
                item.Tokens = new ExpressionTokens(item.Tokens.Select(i => i.Clone()));
            if (item.OnpTokens != null)
                item.OnpTokens = new ExpressionTokens(item.OnpTokens.Select(i => i.Clone()));
            return item;
        }
    }
}