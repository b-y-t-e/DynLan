using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
using DynLan;
using DynLan.Classes;
using DynLan.Helpers;
using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.Internal;

namespace DynLan.OnpEngine.Logic
{
    public static class ObjectValueGetter
    {
        public static Object GetValueFromObject(
            Object Obj,
            String PropertyPath)
        {
            Boolean foundValue = false;
            return GetValueFromObject(Obj, PropertyPath, -1, false, out foundValue);
        }

        public static Object GetValueFromObject(
            Object Obj,
            String PropertyPath,
            Int32 ParametersCount,
            Boolean ExtenderExists,
            out Boolean FoundValue)
        {
            FoundValue = false;

            if (Obj == null)
            {
                FoundValue = true;
                return null;
            }

            if (Obj is EmptyObject)
                throw new NotImplementedException();

            if (Obj is IDictionaryWithGetter)
            {
                IDictionaryWithGetter dict = Obj as IDictionaryWithGetter;
                Boolean contains = dict.Contains(PropertyPath);
                
                if ((contains || !ExtenderExists) && dict.CanGetValueFromDictionary(PropertyPath))
                {
                    FoundValue = true;
                    return dict.GetValueFromDictionary(PropertyPath);
                }
            }

            else if (Obj is IDictionary)
            {
                IDictionary dict = Obj as IDictionary;
                if (dict.Contains(PropertyPath))
                {
                    FoundValue = true;
                    return dict[PropertyPath];
                }
            }

            else if (Obj is DynLanObject)
            {
                PropertyPath = PropertyPath/*.ToUpper()*/;
                DynLanObject DynLanObj = Obj as DynLanObject;
                if (DynLanObj.Contains(PropertyPath))
                {
                    FoundValue = true;
                    return DynLanObj[PropertyPath];
                }
            }

            Object val = RefSensitiveHelper.I.GetValueOrMethod(
                Obj,
                PropertyPath,
                ParametersCount,
                out FoundValue);

            return val;
        }

        public static Boolean EvaluateValue(
            String FieldOrMethodName,
            DynLanContext DynLanContext)
        {
            return EvaluateValueOrMethod(
                null,
                FieldOrMethodName,
                -1,
                DynLanContext);
        }

        public static Boolean EvaluateValueOrMethod(
            Object Obj,
            String FieldOrMethodName,
            Int32 ParametersCount,
            DynLanContext DynLanContext)
        {
            Boolean seekInObject = (Obj != null && !(Obj is EmptyObject));

            ExpressionValue expressionValue = DynLanContext.GetValue(
                DynLanContext,
                FieldOrMethodName,
                seekInObject,
                !seekInObject,
                !seekInObject);

            if (seekInObject)
            {
                Boolean foundValue = false;

                Object value = GetValueFromObject(
                    Obj,
                    FieldOrMethodName,
                    ParametersCount,
                    expressionValue != null,
                    out foundValue);

                if (foundValue)
                {
                    if (value is MethodInfo)
                    {
                        OnpMethodInfo methodInfo = new OnpMethodInfo();
                        methodInfo.Obj = Obj;
                        methodInfo.MethodName = FieldOrMethodName;
                        DynLanContext.CurrentExpressionState.PushValue(methodInfo);
                        return false;
                    }
                    else
                    {
                        DynLanContext.CurrentExpressionState.PushValue(value);
                        return false;
                    }
                }
            }

            Expression expression = DynLanContext.
                CurrentExpressionGroup.
                FindExpression(FieldOrMethodName, DynLanContext);

            if (expression == null)
            {
                Object value = expressionValue == null ?
                    null :
                    expressionValue.Value;

                value = InternalTypeConverter.ToInner(
                    value);

                DynLanContext.CurrentExpressionState.PushValue(value);
                return false;
            }
            else
            {
                ExpressionState newExpressionState = new ExpressionState();
                newExpressionState.Expression = expression;

                DynLanContext.CurrentExpressionContext.Stack.Add(newExpressionState);
                return false;
            }
        }

    }

    public interface IDictionaryWithGetter : IDictionary
    {
        bool CanGetValueFromDictionary(Object Key);

        bool CanSetValueToDictionary(Object Key);

        object GetValueFromDictionary(Object Key);
    }
}