using DynLan;
using DynLan.Classes;
using DynLan.Helpers;
using DynLan.OnpEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace DynLan.Classes
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(DynLanMethod))]
    [KnownType(typeof(DynLanObject))]
    [KnownType(typeof(DynLanProgram))]
    [KnownType(typeof(DynLanClass))]
    [KnownType(typeof(ExpressionExtenderInfo))]
    [KnownType(typeof(ExpressionMethodInfo))]
    [KnownType(typeof(Undefined))]
    [KnownType(typeof(EmptyObject))]
    public class DynLanObject
    {
        public object this[String PropertyName]
        {
            get
            {
                Object val = null;
                // PropertyName = PropertyName.ToUpper();

                if (DynamicValues.TryGetValue(PropertyName, out val))
                    return val;

                if (PropertyName == "this")
                    return ParentObject ?? this;

                return null;
            }
            set
            {
                //PropertyName = PropertyName.ToUpper();

                DynamicValues[PropertyName] = value;

                if (ValueChanged != null)
                    ValueChanged.Invoke(this, new System.EventArgs());
            }
        }

        [IgnoreDataMember]
        public Int32 TotalCount
        {
            get
            {
                return DynamicValues.Count;
            }
        }

        ////////////////////////////////////////////////

        [DataMember]
        public IDictionary<String, Object> DynamicValues { get; set; }

        [DataMember]
        public DynLanObject ParentObject { get; set; }

        ////////////////////////////////////////////////

        public event EventHandler ValueChanged;

        ////////////////////////////////////////////////

        public DynLanObject()
        {
            this.DynamicValues = new DictionaryCloneShallow<String, Object>();
        }

        ////////////////////////////////////////////////

        public DynLanObject CloneBySerialize()
        {
            DynLanObject obj = this.SerializeToBytes().Deserialize<DynLanObject>();
            return obj;
        }

        public Boolean Contains(String PropertyName)
        {
            PropertyName = PropertyName/*.ToUpper()*/;

            if (PropertyName == "this")
                return true;
            
            return DynamicValues.ContainsKey(PropertyName);
        }
    }
}
