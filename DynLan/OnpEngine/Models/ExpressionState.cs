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
#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class ExpressionState
    {
#if !NET20
        [DataMember]
#endif
        public ExpressionTokens ParameterTokens { get; set; }
        
#if !NET20
        [DataMember]
#endif
        public Int32 ParameterIndex { get; set; }
        
#if !NET20
        [DataMember]
#endif
        public List<Object> Parameters { get; set; }

        //////////////////////////////////////////////
        
#if !NET20
        [DataMember]
#endif
        public Expression Expression { get; set; }
        
#if !NET20
        [DataMember]
#endif
        public List<Object> ValueStack { get; set; }
        
#if !NET20
        [DataMember]
#endif
        public Int32 TokenIndex { get; set; }
        
#if !NET20
        [DataMember]
#endif
        public Object Result { get; set; }
        
#if !NET20
        [DataMember]
#endif
        public Boolean Finished { get; set; }

        //////////////////////////////////////////////
        
#if !NET20
        [IgnoreDataMember]
#endif
        public Boolean AreParametersCalculating
        {
            get
            {
                return Parameters != null && ParameterIndex < ParameterTokens.Count;
            }
        }
        
#if !NET20
        [IgnoreDataMember]
#endif
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
                this.Parameters.Add(Value);
            }
            else
            {
                this.ValueStack.Add(Value);
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