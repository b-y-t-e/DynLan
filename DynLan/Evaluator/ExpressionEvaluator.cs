using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using DynLan.Classes;
using DynLan.OnpEngine.Models;

namespace DynLan.Evaluator
{
    public static class ExpressionEvaluator
    {
        public static Boolean NextStep(
            DynLanContext DynLanContext)
        {
            if (DynLanContext == null || DynLanContext.CurrentState == null)
                return true;

            ExpressionContext curExpressionContext = DynLanContext.
                CurrentState.
                ExpressionContext;

            ExpressionGroup curExpressionGroup = curExpressionContext.
                ExpressionGroup;

            if (curExpressionContext == null ||
                curExpressionContext.IsFinished ||
                curExpressionContext.Current == null)
                return true;

            if (curExpressionContext.Current.Expression.IsOnpExecution)
            {
                return ExpressionEvaluatorOnp.EvaluateOnp(
                    DynLanContext);
            }
            else
            {
                return ExpressionEvaluatorQueue.EvaluateQueue(
                    DynLanContext);
            }
        }

    }

    public class OnpExpressionResult
    {
        public Object Result;

        public Boolean Finished;

        public DynLanProgram ProgramToExecute;
    }


}