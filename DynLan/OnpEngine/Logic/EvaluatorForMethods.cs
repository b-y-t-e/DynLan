using DynLan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DynLan.Exceptions;
using DynLan.Classes;
using DynLan.OnpEngine.Models;
using DynLan.Helpers;
using DynLan.OnpEngine.Internal;

namespace DynLan.OnpEngine.Logic
{
    public static class EvaluatorForMethods
    {
        public static Boolean EvaluateMethod(
            Object Object,
            Object MethodObject,
            IList<Object> Parameters,
            DynLanContext DynLanContext)
        {
            if (MethodObject is DynLanMethod)
            {
                if (Parameters == null)
                    Parameters = new Object[0];

                DynLanMethod method = (DynLanMethod)MethodObject;
                DynLanContextType contextType = DynLanContextType.METHOD;

                // jesli tworzenie klasy (wolanie konstruktora)
                if (MethodObject is DynLanClass)
                    contextType = DynLanContextType.CLASS;

                DynLanState newContext = DynLanContext.
                    PushContext(method, contextType, Parameters);

                newContext.Object.ParentObject = method.ParentObject;

                return true;
            }
            else if (MethodObject is DynLanProgram)
            {
                DynLanProgram program = (DynLanProgram)MethodObject;

                IDictionary<String, Object> currentValues = (DynLanContext == null || DynLanContext.CurrentState == null || DynLanContext.CurrentState.Object == null ?
                    null :
                    DynLanContext.
                    CurrentState.
                    Object.
                    DynamicValues);

                DynLanState newState = DynLanContext.PushContext(
                    program,
                    DynLanContextType.METHOD,
                    null);

                if (currentValues != null)
                    foreach (String key in currentValues.Keys)
                        newState.Object.DynamicValues[key] = currentValues[key];

                return true;
            }
            else
            {
                ExpressionMethodResult methodResult = EvaluateInlineMethod(
                    Object,
                    MethodObject,
                    Parameters,
                    DynLanContext);

                if (methodResult != null &&
                    methodResult.NewContextCreated)
                {
                    return true;
                }
                else
                {
                    var v = methodResult == null ? null : methodResult.Value;
                    DynLanContext.CurrentExpressionState.PushValue(v);
                    return false;
                }
            }
        }

        private static ExpressionMethodResult EvaluateInlineMethod(
            Object Object,
            Object Method,
            IList<Object> MethodParameters,
            DynLanContext DynLanContext)
        {
            if (Method is OnpMethodInfo)
            {
                OnpMethodInfo methodInfo = Method as OnpMethodInfo;

                DynamicCallResult callResult = MyReflectionHelper.CallMethod(
                    methodInfo.Obj,
                    methodInfo.MethodName,
                    CorrectParameters(MethodParameters));

                if (callResult != null)
                    return new ExpressionMethodResult(callResult.Value);
            }
            else if (Method is OnpActionMethodInfo)
            {
                OnpActionMethodInfo methodInfo = Method as OnpActionMethodInfo;

                if (methodInfo != null && methodInfo.Action != null)
                    return new ExpressionMethodResult(methodInfo.Action(CorrectParameters(MethodParameters)));
            }
            else if (Method is ExpressionMethod)
            {
                ExpressionMethod onpMethod = Method as ExpressionMethod;
                ExpressionMethodResult result = null;

                if (Object is EmptyObject)
                {
                    result = onpMethod.
                        CalculateValueDelegate(
                            DynLanContext,
                            CorrectParameters(MethodParameters));
                }
                else
                {
#if !NET20
                    var tmp = new[] { Object }.Union(MethodParameters);
#else
                    var tmp = Linq2.From(new[] { Object }).Union(MethodParameters).ToArray();
#endif
                    result = onpMethod.
                        CalculateValueDelegate(
                            DynLanContext,
                            CorrectParameters(tmp));
                }

                return result == null ?
                    new ExpressionMethodResult(null) :
                    result;
            }
            else if (Method is ExpressionMethodInfo)
            {
                ExpressionMethodInfo onpMethodInfo = Method as ExpressionMethodInfo;
                ExpressionMethod onpMethod = BuildinMethods.GetByID(onpMethodInfo.ID);
                ExpressionMethodResult result = null;

                if (onpMethod == null)
                    return new ExpressionMethodResult(result);

                if (Object is EmptyObject)
                {
                    result = onpMethod.
                        CalculateValueDelegate(
                            DynLanContext,
                            CorrectParameters(MethodParameters));
                }
                else
                {
#if !NET20
                    var tmp = new[] { Object }.Union(MethodParameters);
#else
                    var tmp = Linq2.From(new[] { Object }).Union(MethodParameters).ToArray();
#endif
                    result = onpMethod.
                        CalculateValueDelegate(
                            DynLanContext,
                            CorrectParameters(tmp));
                }

                if (result != null)
                    result.Value = InternalTypeConverter.ToInner(result.Value);

                return result == null ?
                    new ExpressionMethodResult(null) :
                    result;
            }
            else if (Method is ExpressionExtender)
            {
                ExpressionExtender onpExtender = Method as ExpressionExtender;

                return new ExpressionMethodResult(
                    onpExtender.
                        CalculateValueDelegate(
                            DynLanContext,
                            Object,
                            MethodParameters));
            }
            else if (Method is ExpressionExtenderInfo)
            {
                ExpressionExtenderInfo onpExtenderInfo = Method as ExpressionExtenderInfo;
                ExpressionExtender onpExtender = BuildinExtenders.GetByID(onpExtenderInfo.ID);

                if (onpExtender == null)
                    return new ExpressionMethodResult(null);

                return new ExpressionMethodResult(
                    onpExtender.
                        CalculateValueDelegate(
                            DynLanContext,
                            Object,
                            MethodParameters));
            }
            else if (Method is Delegate)
            {
#if NETCE
                throw new NotSupportedException("Calling delegates is forbidden on wince2.0!");
#else
                Delegate m = Method as Delegate;

                DynamicCallResult callResult = MyReflectionHelper.CallMethod(
                    m.Target,
                    m.Method,
                    CorrectParameters(MethodParameters));

                if (callResult != null)
                    return new ExpressionMethodResult(callResult.Value);
#endif
            }

            if (Method == null)
            {
                if (Object == null)
                {
                    throw new DynLanMethodNotFoundException("Cannot find a method to call");
                }
                else
                {
                    throw new DynLanMethodNotFoundException("Cannot find a method to call in object " + Object.GetType().Name + "");
                }
            }
            throw new DynLanUnsupportedMethodTypeException("Unsupported method type " + Method.GetType() + "!");
        }

        private static IList<object> CorrectParameters(IEnumerable<object> Parameters)
        {
            try
            {
                if (Parameters == null)
                    return null;

                List<object> newParameters = new List<object>();
                var i = 0;
                foreach (var parameter in Parameters)
                    newParameters[i++] = InternalTypeConverter.ToOuter(parameter);
                return newParameters;
            }
            catch
            {
                throw;
            }
        }

        private static IList<object> CorrectParameters(IList<object> Parameters)
        {
            if (Parameters == null)
                return null;

            object[] newParameters = new object[Parameters.Count];
            try
            {

                for (var i = 0; i < Parameters.Count; i++)
                    newParameters[i] = InternalTypeConverter.ToOuter(Parameters[i]);
                return newParameters;
            }
            catch
            {
                throw;
            }
        }

        /*public static Boolean EvaluateValue(
            Object VariableValue,
            DynLanContext DynLanContext)
        {
            if (VariableValue == null || VariableValue is ExpressionValue)
            {
                Object value = VariableValue == null ? null : ((ExpressionValue)VariableValue).Value;

                value = InternalTypeConverter.ToInner(
                    value);

                DynLanContext.CurrentExpressionState.PushValue(value);
                return false;
            }
            else if (VariableValue is Expression)
            {
                Expression expression = (Expression)VariableValue;

                ExpressionState newExpressionState = new ExpressionState();
                newExpressionState.Expression = expression;

                DynLanContext.CurrentExpressionContext.Stack.Add(newExpressionState);
                return false;
            }
            else
            {
                DynLanContext.CurrentExpressionState.PushValue(VariableValue);
                return false;
            }
        }*/

    }

    public class OnpMethodInfo
    {
        public Object Obj { get; set; }

        public String MethodName { get; set; }
    }

    public class OnpActionMethodInfo
    {
        // custom action with parameters and result
        public Func<IList<Object>, Object> Action { get; set; }
    }

}
