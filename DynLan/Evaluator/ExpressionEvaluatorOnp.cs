using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using DynLan.Classes;
using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.Logic;
using DynLan.OnpEngine.Symbols;
using DynLan.Helpers;

namespace DynLan.Evaluator
{
    public static class ExpressionEvaluatorOnp
    {
        public static Boolean EvaluateOnp(
            DynLanContext DynLanContext)
        {
            Boolean result = false;
            ExpressionState expState = DynLanContext.CurrentExpressionState;
            ExpressionContext expContext = DynLanContext.CurrentExpressionContext;

            // czy zakończyć i zapisać wynik
            if (expState.TokenIndex >= expState.Expression.OnpTokens.Count)
            {
                Object finResult = null;
                if (expState.ValueStack.Count > 0)
                    finResult = MyCollectionsExtenders.Pop(expState.ValueStack);

                expState.ValueStack.Clear();
                expState.Finished = true;
                expState.Result = InternalTypeConverter.ToOuter(finResult);

                MyCollectionsExtenders.Pop(expContext.Stack);

                if (expContext.Current != null)
                {
                    expContext.Current.PushValue(InternalTypeConverter.ToOuter(finResult));
                    return false;
                }
                else
                {
                    expContext.Result = InternalTypeConverter.ToOuter(finResult);
                    expContext.IsFinished = true;
                    return true;
                }
            }

            ExpressionToken token = expState.Expression.OnpTokens[expState.TokenIndex];

            // wykonanie następnej operacji
            if (token.TokenType == TokenType.VALUE)
            {
                ExpressionValue operationValue = StringHelper.
                    GetValueFromText(token.TokenChars);

                Object value = operationValue == null ? null :
                    InternalTypeConverter.ToInner(operationValue.Value);

                expState.PushValue(value);
            }
            else if (token.TokenType == TokenType.PROPERTY_NAME)
            {
                expState.PushValue(token.TokenName);
            }
            if (token.TokenType == TokenType.VARIABLE)
            {
                result = ObjectValueGetter.EvaluateValue(
                    token.TokenName,
                    DynLanContext);
            }
            else if (token.TokenType == TokenType.OPERATOR)
            {
                Object valueA = InternalTypeConverter.ToInner(
                    MyCollectionsExtenders.Pop(expState.ValueStack));

                Object valueB = InternalTypeConverter.ToInner(
                    MyCollectionsExtenders.Pop(expState.ValueStack));

                Object value = null;
                OperatorType operatorType = OperatorTypeHelper.
                    GetOperationType(token.TokenChars);

                try
                {
                    value = OperationHelper.Do(
                        operatorType,
                        valueB,
                        valueA);

                    expState.PushValue(value);
                }
                catch
                {
                    throw;
                }
            }

            expState.TokenIndex++;
            return result;
        }
    }

}