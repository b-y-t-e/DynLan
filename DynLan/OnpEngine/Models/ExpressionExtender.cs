using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Runtime.Serialization;
using DynLan.Classes;

namespace DynLan.OnpEngine.Models
{
#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class ExpressionExtenderInfo
    {
#if !NET20
        [DataMember]
#endif
        public Guid ID { get; set; }
    }

    public class ExpressionExtender
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

                    if (OperationNames != null)
                        foreach (String operationName in OperationNames)
                            v += operationName.GetHashCode();

                    id = new Guid(v, 0, 0, new byte[8]);
                }
                return id.Value;
            }
        }

        //////////////////////////////////////////////////////////////////////

        public String[] OperationNames { get; set; }

        public Func<DynLanContext, Object, IList<Object>, Object> CalculateValueDelegate { get; set; }

        //////////////////////////////////////////////////////////////////////

        public ExpressionExtender()
        {
            this.OperationNames = new String[0];
        }

        public ExpressionExtender(Func<DynLanContext, Object, IList<Object>, Object> CalculateValueDelegate)
        {
            this.OperationNames = new String[0];
            this.CalculateValueDelegate = CalculateValueDelegate;
        }

        //////////////////////////////////////////////////////////////////////

    }
}