using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using DynLan.OnpEngine.Models;

namespace DynLan.Classes
{
#if !NET20
    [DataContract(IsReference = true)]
#endif
    public class DynLanState
    {
#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public Guid ID { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public DynLanProgram Program { get; set; }

#if !NET20
        //[DataMember(EmitDefaultValue = false)]
#endif
        //public String DisplayName { get; set; }

#if !NET20
        //[DataMember(EmitDefaultValue = false)]
#endif
        //public Guid ObjectID { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public DynLanContextType ContextType { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public DynLanObject Object { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public DynLanObject ThisObject { get; set; }

#if !NET20
        //[DataMember(EmitDefaultValue = false)]
#endif
        //public DynLanCodeLines Lines { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public Int32 CurrentLineIndex { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public ExpressionContext ExpressionContext { get; set; }

#if !NET20
        [DataMember(EmitDefaultValue = false)]
#endif
        public Object Tag { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [IgnoreDataMember]
#endif
        public Guid CurrentLineID
        {
            get
            {
                DynLanCodeLine currentLine = GetCurrentLine();
                return currentLine == null ? Guid.Empty : currentLine.ID;
            }
            set
            {
                if (Program == null)
                    return;

                DynLanCodeLine newCurrentLine = Program.Lines.Get_by_ID(value);
                this.CurrentLineIndex = newCurrentLine == null ? -1 : Program.Lines.IndexOf(newCurrentLine);
                this.ExpressionContext = null;

                if (CurrentLineChanged != null)
                    CurrentLineChanged.Invoke(this, new System.EventArgs());
            }
        }

        //////////////////////////////////////////////

        public event EventHandler CurrentLineChanged;

        //////////////////////////////////////////////

        /*public DynLanState(String DisplayName, Guid ObjectID, DynLanContextType ContextType)
        {
            this.DisplayName = DisplayName ?? "";
            this.ObjectID = ObjectID;
            this.ID = Guid.NewGuid();
            this.ContextType = ContextType;
            this.Object = new DynLanObject();
            this.Lines = new DynLanCodeLines();
            this.CurrentLineIndex = 0;
        }*/

        public DynLanState(DynLanProgram Program, DynLanContextType ContextType)
        {
            this.Program = Program;
            //this.DisplayName = DisplayName ?? "";
            //this.ObjectID = ObjectID;
            this.ID = Guid.NewGuid();
            this.ContextType = ContextType;
            this.Object = new DynLanObject();
            //this.Lines = new DynLanCodeLines();
            this.CurrentLineIndex = 0;
        }

        //////////////////////////////////////////////

        public DynLanCodeLine GetCurrentLine()
        {
            if (Program == null)
                return null;

            return
                CurrentLineIndex >= 0 && CurrentLineIndex < Program.Lines.Count ?
                Program.Lines[CurrentLineIndex] :
                null;
        }

        public DynLanCodeLines GetCurrentLines()
        {
            if (Program == null)
                return null;

            return Program.Lines;
        }

        //////////////////////////////////////////////

        public DynLanState Clone()
        {
            return (DynLanState)this.MemberwiseClone();
        }

        public void Clean()
        {
            Object = null;
            ThisObject = null;
            Program = null;
            if (ExpressionContext != null)
                ExpressionContext.Clean();
            ExpressionContext = null;
        }
    }

    public enum DynLanContextType
    {
        GLOBAL,
        METHOD,
        CLASS
    }
}