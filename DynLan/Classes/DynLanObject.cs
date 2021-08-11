using DynLan;
using DynLan.Classes;
using DynLan.Helpers;
using DynLan.OnpEngine.Logic;
using DynLan.OnpEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
#if !NETCE
using System.Runtime.Serialization;
#endif
using System.Text;

namespace DynLan.Classes
{
#if !NET20
    [DataContract(IsReference = true)]
    [KnownType(typeof(DynLanMethod))]
    [KnownType(typeof(DynLanObject))]
    [KnownType(typeof(DynLanProgram))]
    [KnownType(typeof(DynLanClass))]
    [KnownType(typeof(ExpressionExtenderInfo))]
    [KnownType(typeof(ExpressionMethodInfo))]
    [KnownType(typeof(Undefined))]
    [KnownType(typeof(EmptyObject))]
#endif
    public class DynLanObject
    {
        public object this[String PropertyName]
        {
            get
            {
                Object val = null;

                if (!GlobalSettings.CaseSensitive)
                {
                    PropertyName = PropertyName.ToUpperInvariant();

                    if (DynamicValues.TryGetValue2(PropertyName, out val))
                        return val;

                    if (PropertyName == "THIS")
                        return ParentObject ?? this;
                }
                else
                {
                    if (DynamicValues.TryGetValue(PropertyName, out val))
                        return val;

                    if (PropertyName == "this")
                        return ParentObject ?? this;
                }

                return null;
            }
            set
            {
                if (!GlobalSettings.CaseSensitive)
                    PropertyName = PropertyName.ToUpper();

                DynamicValues.Set2(PropertyName, value);

                if (ValueChanged != null)
                    ValueChanged.Invoke(this, new System.EventArgs());
            }
        }

#if !NET20
        [IgnoreDataMember]
#endif
        public Int32 TotalCount
        {
            get
            {
                return DynamicValues.Count;
            }
        }

        ////////////////////////////////////////////////

#if !NET20
        [DataMember]
#endif
        public IDictionary<String, Object> DynamicValues { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanObject ParentObject { get; set; }

        ////////////////////////////////////////////////

        public event EventHandler ValueChanged;

        ////////////////////////////////////////////////

        public DynLanObject()
        {
            this.DynamicValues = new DictionaryCloneShallow<String, Object>();
        }

        ////////////////////////////////////////////////
#if !NET20
        public DynLanObject CloneBySerialize()
        {
            DynLanObject obj = this.SerializeToBytes().Deserialize<DynLanObject>();
            return obj;
        }
#endif

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>(this.DynamicValues);
        }

        public Boolean Contains(String PropertyName)
        {
            if (!GlobalSettings.CaseSensitive)
            {
                PropertyName = PropertyName.ToUpperInvariant();

                if (PropertyName == "THIS")
                    return true;

                return DynamicValues.Contains2(PropertyName);
            }
            else
            {
                if (PropertyName == "this")
                    return true;
                return DynamicValues.ContainsKey(PropertyName);
            }

        }
    }
}
