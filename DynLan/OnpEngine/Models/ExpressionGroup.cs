using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Runtime.Serialization;
using DynLan.Classes;

namespace DynLan.OnpEngine.Models
{
    [DataContract(IsReference = true)]
    public class ExpressionGroup
    {
        [DataMember(EmitDefaultValue = false)]
        public Guid ID { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Expression MainExpression { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Dictionary<String, Expression> Expressions { get; private set; }

        ////////////////////////////////////

        public ExpressionGroup(Boolean CreateExpressions = true)
        {
            this.ID = Guid.NewGuid();
            if (CreateExpressions)
                this.Expressions = new Dictionary<String, Expression>();
        }

        //////////////////////////////

        public Expression FindExpression(String VariableName, DynLanContext DynLanContext)
        {
            Expression foundExpression = null;
            if (Expressions != null)
                Expressions.TryGetValue(VariableName, out foundExpression);

            return foundExpression;
        }

        public virtual ExpressionGroup Clone()
        {
            ExpressionGroup item = (ExpressionGroup)this.MemberwiseClone();
            if (item.MainExpression != null)
                item.MainExpression = item.MainExpression.Clone();
            if (item.Expressions != null)
                item.Expressions = item.Expressions.ToDictionary(
                    i => i.Key,
                    i => i.Value.Clone());
            return item;
        }
    }
}