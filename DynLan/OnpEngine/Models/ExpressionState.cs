using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DynLan;
using System.Runtime.Serialization;
#if PCL
using System.Collections.ObjectModel2;
using DynLan.Helpers;
#else
using System.Collections.ObjectModel;
using DynLan.Helpers;
#endif


namespace DynLan.OnpEngine.Models
{
    [DataContract(IsReference = true)]
    public class ExpressionState
    {
        [DataMember]
        public ExpressionTokens ParameterTokens { get; set; }

        [DataMember]
        public Int32 ParameterIndex { get; set; }

        [DataMember]
        public List<Object> Parameters { get; set; }

        //////////////////////////////////////////////

        [DataMember]
        public Expression Expression { get; set; }

        [DataMember]
        public List<Object> ValueStack { get; set; }

        [DataMember]
        public Int32 TokenIndex { get; set; }

        [DataMember]
        public Object Result { get; set; }

        [DataMember]
        public Boolean Finished { get; set; }

        //////////////////////////////////////////////

        [IgnoreDataMember]
        public Boolean AreParametersCalculating
        {
            get
            {
                return Parameters != null && ParameterIndex < ParameterTokens.Count;
            }
        }

        [IgnoreDataMember]
        public Boolean AreParametersCalculated
        {
            get
            {
                return Parameters != null && ParameterIndex >= ParameterTokens.Count;
            }
        }

        //////////////////////////////////////////////

        public ExpressionState()
        {
            this.ParameterIndex = -1;
            this.ValueStack = new List<Object>();
            this.TokenIndex = 0;
        }

        //////////////////////////////////////////////

        public void PushValue(Object Value)
        {
            if (AreParametersCalculating)
            {
                this.Parameters.Push(Value);
            }
            else
            {
                this.ValueStack.Push(Value);
            }
        }

        public void CleanParametersState()
        {
            if (this.Parameters != null)
                this.Parameters.Clear();

            if (this.ParameterTokens != null)
                this.ParameterTokens.Clear();

            this.Parameters = null;
            this.ParameterTokens = null;
            this.ParameterIndex = -1;
        }

        public void Clean()
        {
            this.Result = null;
            if (this.ValueStack != null)
                this.ValueStack.Clear();
            this.ValueStack = null;
            this.Expression = null;
            this.CleanParametersState();
        }
    }

    public class ExpressionStates : ObservableCollection<ExpressionState>
    {

    }
}