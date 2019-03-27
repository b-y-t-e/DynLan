using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Runtime.Serialization;
using DynLan.EventArgs;
using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.Internal;
using DynLan.Helpers;

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
    public class DynLanContext : IDisposable
    {
#if !NET20
        [DataMember]
#endif
        public Guid ID { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [DataMember]
#endif
        public DynLanStates Stack { get; set; }

#if !NET20
        [DataMember]
#endif
        public Boolean BreakEveryLine { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [DataMember]
#endif
        public Exception Error { get; set; }

#if !NET20
        [DataMember]
#endif
        public Object Result { get; set; }

#if !NET20
        [DataMember]
#endif
        public Boolean IsFinished { get; set; }

#if !NET20
        [IgnoreDataMember]
#endif
        public Exception ExceptionToThrow { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [DataMember]
#endif
        public DynLanState CurrentState { get; set; }

#if !NET20
        [DataMember]
#endif
        public DynLanState GlobalState { get; set; }

#if !NET20
        [DataMember]
#endif
        public Boolean IsDisposed { get; set; }

        //////////////////////////////////////////////

#if !NET20
        [IgnoreDataMember]
#endif
        public DynLanObject GlobalObject
        {
            get { return GlobalState != null ? GlobalState.Object : null; }
        }

        public object this[string PropertyName]
        {
            get { return GlobalObject[PropertyName]; }
            set { GlobalObject[PropertyName] = value; }
        }

        //////////////////////////////////////////////

#if !NET20
        [IgnoreDataMember]
#endif
        public ExpressionContext CurrentExpressionContext
        {
            get
            {
                return CurrentState != null ?
                    CurrentState.ExpressionContext :
                    null;
            }
        }

#if !NET20
        [IgnoreDataMember]
#endif
        public ExpressionGroup CurrentExpressionGroup
        {
            get
            {
                return CurrentExpressionContext != null ?
                    CurrentExpressionContext.ExpressionGroup :
                    null;
            }
        }

#if !NET20
        [IgnoreDataMember]
#endif
        public ExpressionState CurrentExpressionState
        {
            get
            {
                return CurrentExpressionContext != null ?
                    CurrentExpressionContext.Current :
                    null;
            }
        }

        //////////////////////////////////////////////

        public event EventHandler<DynLanProgramChangedEventArgs> OnProgramStart;

        public event EventHandler<DynLanProgramChangedEventArgs> OnProgramEnd;

        public event EventHandler<DynLanProgramErrorEventArgs> OnProgramError;

        public event EventHandler<DynLanErrorEventArgs> OnError;

        //////////////////////////////////////////////

        public DynLanContext(DynLanProgram MainProgram)
        {
            this.GlobalState = new DynLanState(
                MainProgram,
                DynLanContextType.GLOBAL);

            this.Stack = new DynLanStates();
            this.Stack.Add(this.GlobalState);
            this.CurrentState = this.GlobalState;

            this.ID = Guid.NewGuid();
        }

        //////////////////////////////////////////////

        public void AddValues(IDictionary<string, object> Values)
        {
            if (Values != null)
            {
                foreach (var key in Values.Keys)
                {
                    CurrentState.Object.DynamicValues[key] = Values[key];
                }

                /*if (CopyParameters)
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
                }*/
            }
        }

        //public DynLanState PushContext(String DisplayName, Guid ObjectID, DynLanContextType ContextType)
        public DynLanState PushContext(
            DynLanProgram Program,
            DynLanContextType ContextType,
            IList<Object> Parameters)
        {
            DynLanState state = new DynLanState(Program, ContextType); // DisplayName, ObjectID, ContextType);
            DynLanObject obj = state.Object;
            this.Stack.Add(state);
            this.CurrentState = state;

            List<DynLanMethodParam> finalParameters = new List<DynLanMethodParam>();
            Int32 index = -1;

            if (Program is DynLanMethod)
            {
                DynLanMethod method = (DynLanMethod)Program;
                foreach (String parameter in method.Parameters)
                {
                    index++;

                    Object parameterValue = Parameters == null ? null :
                        index < Parameters.Count ?
                            Parameters[index] :
                            new Undefined();

                    if (obj != null)
                    {
                        obj[parameter] = parameterValue;
                        finalParameters.Add(new DynLanMethodParam()
                        {
                            Name = parameter,
                            Value = parameterValue
                        });
                    }
                }
            }

            if (obj != null)
            {
                foreach (DynLanMethod DynLanMethod in Program.Methods)
                {
                    if (state.Program is DynLanClass)
                    {
                        DynLanMethod newMethod = (DynLanMethod)DynLanMethod.Clone();
                        newMethod.ParentObject = obj;
                        obj[newMethod.Name] = newMethod;
                    }
                    else
                    {
                        obj[DynLanMethod.Name] = DynLanMethod;
                    }
                }

                foreach (DynLanClass DynLanClass in Program.Classes)
                {
                    obj[DynLanClass.Name] = DynLanClass;
                }
            }

            RaiseProgramStart(
                state,
                finalParameters);

            return state;
        }

        public DynLanState PopContext(Exception ex)
        {
            DynLanState currentState = MyCollectionsExtenders.Peek(this.Stack);

            if (currentState != null &&
                currentState.ContextType != DynLanContextType.GLOBAL)
            {
                RaiseProgramEnd(currentState, ex);

                currentState.Clean();

                DynLanState popedState = MyCollectionsExtenders.Pop(this.Stack);
                this.CurrentState = MyCollectionsExtenders.Peek(this.Stack);

                return currentState;
            }
            return null;
        }

        //////////////////////////////////////////////

        public void RaiseProgramStart(DynLanState state, IList<DynLanMethodParam> finalParameters)
        {
            DynLanState currentState = MyCollectionsExtenders.Peek(this.Stack);

            if (this.OnProgramStart != null)
            {
                DynLanProgramChangedEventArgs args = new DynLanProgramChangedEventArgs()
                {
                    Context = this,
                    Program = state.Program,
                    State = state,
                    Parameters = finalParameters
                };
                this.OnProgramStart(this, args);
                args.Clean();
            }
        }

        public void RaiseProgramEnd(DynLanState currentState, Exception ex)
        {
            if (this.OnProgramEnd != null)
            {
                DynLanProgramChangedEventArgs args = new DynLanProgramChangedEventArgs()
                {
                    Context = this,
                    Program = currentState.Program,
                    State = currentState,
                    Error = ex
                };
                this.OnProgramEnd(this, args);
                args.Clean();
            }
        }

        public void RaiseProgramError(DynLanState currentState)
        {
            if (this.OnProgramError != null)
            {
                DynLanProgramErrorEventArgs args = new DynLanProgramErrorEventArgs()
                {
                    Context = this,
                    Program = currentState.Program,
                    State = currentState
                };
                this.OnProgramError(this, args);
                args.Clean();
            }
        }

        public Boolean RaiseError(DynLanState currentState, Exception Error)
        {
            if (this.OnError != null)
            {
                DynLanErrorEventArgs args = new DynLanErrorEventArgs()
                {
                    Context = this,
                    Program = currentState.Program,
                    State = currentState,
                    Error = Error,
                    Handled = false
                };
                this.OnError(this, args);
                args.Clean();
                return args.Handled;
            }
            return false;
        }

        //////////////////////////////////////////////

        public virtual ExpressionValue GetValue(
            DynLanContext EvalContext,
            String Name,
            Boolean SeekForExtenders,
            Boolean SeekForMethods,
            Boolean SeekInContexts)
        {
#if CASE_INSENSITIVE
            Name = Name.ToUpper();
#endif

            DynLanContext DynLanContext = EvalContext as DynLanContext;
            if (DynLanContext == null)
                return null;

            // szukanie extender'a
            if (SeekForExtenders)
            {
                ExpressionExtenderInfo extender = BuildinExtenders.FindByName(Name);
                if (extender != null)
                    return new ExpressionValue(extender);
            }

            if (SeekInContexts)
            {
                // szukanie zmiennej w lokalnym kontekście
                if (DynLanContext.CurrentState != DynLanContext.GlobalState)
                {
                    // szukanie zmiennej w metodach gdzie została zadeklarowana metoda
                    if (DynLanContext.Stack != null)
                    {
#if !NET20
                        DynLanState currentState = DynLanContext.
                            Stack.
                            LastOrDefault();
#else
                        DynLanState currentState = Linq.LastOrDefault(
                            DynLanContext.Stack);
#endif
                        DynLanMethod method = currentState == null ? null :
                            currentState.Program as DynLanMethod;

                        if (method != null)
                        {
                            for (Int32 i = DynLanContext.Stack.Count - 2; i >= 0; i--)
                            {
                                DynLanState state = DynLanContext.Stack[i];

                                DynLanMethod thisMethod = state.Program as DynLanMethod;
                                DynLanObject thisObject = state.Object;
                                if (thisMethod == null)
                                    break;

                                if (thisMethod != null &&
                                    thisMethod.Methods != null &&
                                    thisMethod.Methods.Contains(method))
                                {
                                    if (thisObject != null &&
                                        thisObject.Contains(Name))
                                    {
                                        return new ExpressionValue(thisObject[Name]);
                                    }
                                }
                                else
                                {
                                    break;
                                }

                                method = thisMethod;
                            }
                        }
                    }

                    if (DynLanContext.CurrentState.Object != null &&
                        DynLanContext.CurrentState.Object.Contains(Name))
                    {
                        return new ExpressionValue(DynLanContext.CurrentState.Object[Name]);
                    }
                }

                // szukanie zmiennej w globalnym kontekście
                if (DynLanContext.GlobalState.Object != null &&
                    DynLanContext.GlobalState.Object.Contains(Name))
                {
                    return new ExpressionValue(DynLanContext.GlobalState.Object[Name]);
                }
            }

            // przeniesione na dło aby metody i zmiennej których nazwy pokrywaja sie z globalnymi
            // mobly byc używane
            // szukanie metody
            if (SeekForMethods)
            {
                ExpressionMethodInfo method = BuildinMethods.FindByName(Name);
                if (method != null)
                    return new ExpressionValue(method);
            }

            /*for (var i = DynLanContext.Stack.IndexOf(DynLanContext.Current); i >= 0; i--)
            {
                DynLanState context = DynLanContext.Stack[i];

                if ((context == DynLanContext.Current || context.ContextType == DynLanContextType.GLOBAL) &&
                    context.Object != null &&
                    context.Object.Contains(Name))
                {
                    return new ExpressionValue(context.Object[Name]);
                }
            }*/

            // szukanie po globalnych zmiennych
            /*if (DynLanContext.GlobalContext.ContextType == DynLanContextType.GLOBAL)
            {
                if (DynLanContext.GlobalContext.Object.Contains(Name))
                    return new ExpressionValue(DynLanContext.GlobalContext.Object[Name]);
            }*/

            return null;
        }

        public virtual Boolean SetValue(DynLanContext EvalContext, String Name, Object Value)
        {
#if CASE_INSENSITIVE
            Name = Name.ToUpper();
#endif

            DynLanContext DynLanContext = EvalContext as DynLanContext;
            if (DynLanContext == null)
                return false;

            DynLanContext.CurrentState.Object[Name] = Value;

            return true;
        }

        public void Dispose()
        {
            IsDisposed = true;

            if (Stack != null)
            {
                foreach (DynLanState state in Stack)
                {
                    if (state == null || state == GlobalState)
                        continue;

                    state.Clean();
                }
                Stack.Clear();
                Stack = null;
            }

            /*if (GlobalState != null)
            {
                GlobalState.Clean();
                GlobalState = null;
            }*/

            /*if (CurrentState != null && CurrentState != GlobalState)
            {
                CurrentState.Clean();
                CurrentState = null;
            }*/

            GlobalState = null;
            CurrentState = null;
            Result = null;
            Error = null;

            /*GlobalState;
            CurrentState;
            Result;
            Error;
            Stack;*/
        }
    }

    public class DynLanContextes : List<DynLanContext>
    {

    }
}