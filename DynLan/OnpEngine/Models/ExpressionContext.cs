using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DynLan;
using System.Runtime.Serialization;


namespace DynLan.OnpEngine.Models
{
#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class ExpressionContext
    {
#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public ExpressionStates Stack { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public ExpressionGroup ExpressionGroup { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public Boolean IsFinished { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public Object Result { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [IgnoreDataMember]
#endif
        public ExpressionState Current
        {
            get { return Linq2.LastOrDefault(Stack); }
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