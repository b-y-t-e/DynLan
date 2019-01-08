using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DynLan;
using System.Runtime.Serialization;


namespace DynLan.OnpEngine.Models
{
    [DataContract(IsReference = true)]
    public class ExpressionContext
    {
        [DataMember(EmitDefaultValue = false)]
        public ExpressionStates Stack { get; set; }

        //////////////////////////////////////////////

        [DataMember(EmitDefaultValue = false)]
        public ExpressionGroup ExpressionGroup { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Boolean IsFinished { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Object Result { get; set; }

        //////////////////////////////////////////////

        [IgnoreDataMember]
        public ExpressionState Current
        {
            get { return Stack.LastOrDefault(); }
        }

        //////////////////////////////////////////////

        public ExpressionContext(ExpressionGroup ExpressionGroup)
            : this()
        {
            this.ExpressionGroup = ExpressionGroup;
            this.Current.Expression = ExpressionGroup.MainExpression;
        }

        public ExpressionContext()
        {
            Stack = new ExpressionStates();
            Stack.Add(new ExpressionState());
        }

        //////////////////////////////////////////////

        public void Clean()
        {
            if (Stack != null)
            {
                foreach (ExpressionState state in Stack)
                    state.Clean();
                Stack.Clear();
            }
            Stack = null;
            Result = null;
            ExpressionGroup = null;
        }
    }
}