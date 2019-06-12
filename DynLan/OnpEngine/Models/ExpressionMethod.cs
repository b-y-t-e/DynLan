using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
#if !NETCE
using System.Runtime.Serialization;
#endif
using DynLan.Classes;

namespace DynLan.OnpEngine.Models
{
#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class ExpressionMethodInfo
    {
#if !NET20
        [DataMember]
#endif
        public Guid ID { get; set; }
    }

    public class DynMethod
    {
        private Guid? id;

        //////////////////////////////////////////////////////////////////////

        public Guid ID
        {
            get
            {
                if (id == null)
                {
                    Int32 v = 0;

                    if (Names != null)
                        foreach (String operationName in Names)
                            v += operationName.GetHashCode();

                    id = new Guid(v, 0, 0, new byte[8]);
                }
                return id.Value;
            }
        }

        //////////////////////////////////////////////////////////////////////

        public String[] Names { get; set; }

        public Func<DynContext, IList<Object>, DynMethodResult> Body { get; set; }

        //////////////////////////////////////////////////////////////////////

        public DynMethod()
        {
            this.Names = new String[0];
        }

        public DynMethod(Func<DynContext, IList<Object>, DynMethodResult> CalculateValueDelegate)
        {
            this.Names = new String[0];
            this.Body = CalculateValueDelegate;
        }

        //////////////////////////////////////////////////////////////////////
    }

    public class DynMethodResult
    {
        public Object Value;

        public Boolean NewContextCreated;

        public DynMethodResult()
        {

        }

        public DynMethodResult(Object Value)
        {
            this.Value = Value;
        }
    }
}