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
    [DataContract(IsReference = true)]
    public class DynLanState
    {
        [DataMember(EmitDefaultValue = false)]
        public Guid ID { get; set; }

        //////////////////////////////////////////////

        [DataMember(EmitDefaultValue = false)]
        public DynLanProgram Program { get; set; }

        //[DataMember(EmitDefaultValue = false)]
        //public String DisplayName { get; set; }

        //[DataMember(EmitDefaultValue = false)]
        //public Guid ObjectID { get; set; }

        //////////////////////////////////////////////

        [DataMember(EmitDefaultValue = false)]
        public DynLanContextType ContextType { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public DynLanObject Object { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public DynLanObject ThisObject { get; set; }

        //[DataMember(EmitDefaultValue = false)]
        //public DynLanCodeLines Lines { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Int32 CurrentLineIndex { get; set; }

        //////////////////////////////////////////////

        [DataMember(EmitDefaultValue = false)]
        public ExpressionContext ExpressionContext { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Object Tag { get; set; }

        //////////////////////////////////////////////

        [IgnoreDataMember]
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