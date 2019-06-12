using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
#if !NETCE
using System.Runtime.Serialization;
#endif

namespace DynLan.Classes
{
#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class DynLanProgram
    {
#if !NET20
        [DataMember]
#endif
        public Guid ID { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [DataMember]
#endif
        public Int32 Depth { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanMethods Methods { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanClasses Classes { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanCodeLines Lines { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanContextType ContextType { get; set; }

        //////////////////////////////////////////////////

#if !NET20
        [DataMember]
#endif
        public Object Tag { get; set; }

#if !NET20
        [DataMember]
#endif
        public Object Tag1 { get; set; }

#if !NET20
        [DataMember]
#endif
        public Object Tag2 { get; set; }

        //////////////////////////////////////////////////

        public DynLanProgram()
        {
            this.ID = Guid.NewGuid();
            this.Methods = new DynLanMethods();
            this.Classes = new DynLanClasses();
            this.Lines = new DynLanCodeLines();
            this.ContextType = DynLanContextType.GLOBAL;
        }

        //////////////////////////////////////////////////

        public Object Eval()
        {
            using (DynContext context = this.CreateContext(null))
                return Eval(context);
        }

        public Object Eval(
            IDictionary<String, Object> Parameters)
        {
            using (DynContext context = this.CreateContext(Parameters))
                return Eval(context);
        }

        private Object Eval(
            DynContext DynLanContext)
        {
            while (true)
            {
                if (DynLanContext.IsFinished)
                {
                    if (DynLanContext.Error != null)
                        throw DynLanContext.Error;
                    break;
                }

                try
                {
                    Boolean result = DynLan.Evaluator.ContextEvaluator.
                        ExecuteNext(DynLanContext);

                    if (DynLanContext.BreakEveryLine && result)
                        break;
                }
                catch
                {
                    throw;
                }
            }
            return DynLanContext.Result;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////

        public DynContext CreateContext()
        {
            return CreateContext(null, false, false);
        }

        public DynContext CreateContext(
            IDictionary<String, Object> Values)
        {
            return CreateContext(Values, false, false);
        }

        public DynContext CreateContext(
            IDictionary<String, Object> Values,
            Boolean BreakEveryLine,
            Boolean CopyParameters)
        {
            if (Values == null)
                Values = new Dictionary<String, Object>();

            foreach (DynLanMethod DynLanMethod in this.Methods)
                Values[DynLanMethod.Name] = DynLanMethod;

            foreach (DynLanClass DynLanClass in this.Classes)
                Values[DynLanClass.Name] = DynLanClass;

            return CreateContext(
                this.Lines,
                Values,
                BreakEveryLine,
                CopyParameters);
        }

        private static DynContext CreateContext(
            DynLanCodeLines Lines,
            IDictionary<String, Object> Values,
            Boolean BreakEveryLine,
            Boolean CopyParameters)
        {
            DynContext runContext = new DynContext(
                new DynLanProgram()
                {
                    ID = Guid.Empty,
                    Lines = Lines
                });
            runContext.BreakEveryLine = BreakEveryLine;

            if (runContext.CurrentState.Program.Lines.Count > 0)
                runContext.CurrentState.CurrentLineID = runContext.CurrentState.Program.Lines[0].ID;

            if (Values != null)
            {
                if (CopyParameters)
                {
                    if (Values is ICloneShallow)
                    {
                        runContext.CurrentState.Object.DynamicValues = (IDictionary<String, Object>)((ICloneShallow)Values).CloneShallow();
                    }
                    else
                    {
                        runContext.CurrentState.Object.DynamicValues = new DictionaryCloneShallow<String, Object>(Values);
                    }
                }
                else
                {
                    runContext.CurrentState.Object.DynamicValues = Values;
                }
            }
            return runContext;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////

        public virtual Object Clone()
        {
            DynLanProgram item = (DynLanProgram)this.MemberwiseClone();
#if !NET20
            if (item.Lines != null)
                item.Lines = new DynLanCodeLines(item.Lines.Select(i => i.Clone()));
            if (item.Classes != null)
                item.Classes = new DynLanClasses(item.Classes.Select(i => i.Clone() as DynLanClass));
            if (item.Methods != null)
                item.Methods = new DynLanMethods(item.Methods.Select(i => i.Clone() as DynLanMethod));
      
#else
            if (item.Lines != null)
                item.Lines = new DynLanCodeLines(Linq2.Select(item.Lines, i => i.Clone()));
            if (item.Classes != null)
                item.Classes = new DynLanClasses(Linq2.Select(item.Classes, i => i.Clone() as DynLanClass));
            if (item.Methods != null)
                item.Methods = new DynLanMethods(Linq2.Select(item.Methods, i => i.Clone() as DynLanMethod));

#endif

            return item;
        }
    }
}
