using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynLan;
using System.Collections;
using DynLan.Classes;
using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.Logic;
using DynLan.Evaluator;

namespace DynLan
{
    public static class Engine
    {
        public static Object Eval(
#if !NET20
            this 
#endif
            DynLanProgram Program,
            IDictionary<String, Object> Parameters = null)
        {
            using (DynLanContext context = CreateContext(Program, Parameters))
            {
                return Eval(context);
            }
        }

        private static Object Eval(
#if !NET20
            this 
#endif
            DynLanContext DynLanContext/*,
            IDictionary<String, Object> Parameters = null*/)
        {
            //DynLanContext.
            //    AddValues(Parameters);

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
                    Boolean result = ContextEvaluator.
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

        ////////////////////////////////////////////////////////////////////

        /*public static DynLanObject Exec(
            this DynLanProgram Program,
            IDictionary<String, Object> Parameters = null)
        {
            using (DynLanContext context = CreateContext(Program, Parameters, false))
            {
                return Exec(context);
            }
        }*/

        /*public static DynLanObject Exec(
            this DynLanContext DynLanContext)
        {
            while (true)
            {
                if (DynLanContext.IsFinished)
                    break;

                Boolean result = ContextEvaluator.ExecuteNext(DynLanContext);

                if (DynLanContext.BreakEveryLine && result)
                    break;
            }
            return DynLanContext.GlobalObject;
        }*/

        //////////////////////////////////////////////

        /*public static Object InvokeObjectMethod(
            this DynLanObject Object,
            String Method,
            IList<Object> MethodParameters,
            IDictionary<String, Object> Values = null)
        {
            using (DynLanContext context = InvokeObjectMethodGetContext(
                Object,
                Method,
                MethodParameters,
                Values))
            {
                return context.Eval();
            }
        }*/

        /*public static Object InvokeMethod(
            String Method,
            IList<Object> MethodParameters,
            IDictionary<String, Object> Values = null)
        {
            using (DynLanContext context = InvokeMethodGetContext(
                Method,
                MethodParameters,
                Values))
            {
                return context.Eval();
            }
        }*/

        /*public static Object InvokeObjectMethod(
            this DynLanObject Object,
            DynLanContext DynLanContext,
            String Method,
            IList<Object> MethodParameters)
        {
            using (DynLanContext context = InvokeObjectMethodGetContext(
                Object,
                DynLanContext,
                Method,
                MethodParameters))
            {
                return context.Eval();
            }
        }*/

        /*public static Object InvokeObjectMethod(
            this DynLanContext DynLanContext,
            DynLanObject Object,
            String Method,
            IList<Object> MethodParameters)
        {
            using (DynLanContext context = InvokeObjectMethodGetContext(
                Object,
                Method,
                MethodParameters,
                DynLanContext != null && DynLanContext.GlobalObject != null ? DynLanContext.GlobalObject.DynamicValues : null))
            {
                return context.Eval();
            }
        }*/

        /*public static Object InvokeMethod(
            this DynLanContext DynLanContext,
            String Method,
            IList<Object> MethodParameters,
            IDictionary<String, Object> StaticValues = null)
        {
            using (DynLanContext context = InvokeMethodGetContext(
                DynLanContext,
                Method,
                MethodParameters,
                StaticValues))
            {
                return context.Eval();
            }
        }*/

        /*public static Object InvokeMethod(
            this DynLanContext DynLanContext,
            String Method,
            IList<Object> MethodParameters)
        {
            using (DynLanContext context = InvokeMethodGetContext(
                Method,
                MethodParameters,
                DynLanContext != null && DynLanContext.GlobalObject != null ? DynLanContext.GlobalObject.DynamicValues : null))
            {
                return context.Eval();
            }
        }*/

        //////////////////////////////////////////////

        /*public static DynLanContext InvokeObjectMethodGetContext(
            this DynLanObject Object,
            DynLanContext DynLanContext,
            String Method,
            IList<Object> MethodParameters)
        {
            return InvokeObjectMethodGetContext(
                Object,
                Method,
                MethodParameters,
                DynLanContext != null && DynLanContext.GlobalObject != null ? DynLanContext.GlobalObject.DynamicValues : null);
        }*/

        /*public static DynLanContext InvokeObjectMethodGetContext(
            this DynLanContext DynLanContext,
            String Method,
            IList<Object> MethodParameters)
        {
            return InvokeObjectMethodGetContext(
                DynLanContext.GlobalObject,
                Method,
                MethodParameters,
                DynLanContext != null && DynLanContext.GlobalObject != null ? DynLanContext.GlobalObject.DynamicValues : null);
        }*/

        /*public static DynLanContext InvokeMethodGetContext(
            this DynLanContext DynLanContext,
            String Method,
            IList<Object> MethodParameters)
        {
            return InvokeMethodGetContext(
                Method,
                MethodParameters,
                DynLanContext != null && DynLanContext.GlobalObject != null ? DynLanContext.GlobalObject.DynamicValues : null);
        }*/

        ////////////////////////////////////////////////////////

        /*private static DynLanContext InvokeObjectMethodGetContext(
            DynLanObject Object,
            String Method,
            IList<Object> MethodParameters,
            IDictionary<String, Object> Values)
        {
            DynLanContext newContext = CreateContext(
                new DynLanCodeLines(),
                Values,
                false, true);
            DynLanObject globalObject = newContext.GlobalObject;

            String objectName = IdGenerator.Generate();
            globalObject.DynamicValues[objectName] = Object; // much faster
            //newContext[objectName] = Object;

            ExpressionGroup expressionGroup = new ExpressionGroup(false);
            expressionGroup.MainExpression = new Expression();
            expressionGroup.MainExpression.IsOnpExecution = false;
            expressionGroup.MainExpression.Tokens = new ExpressionTokens();

            expressionGroup.MainExpression.Tokens.Add(
                new ExpressionToken(objectName, TokenType.VARIABLE));

            expressionGroup.MainExpression.Tokens.Add(
                new ExpressionToken('.', TokenType.OPERATOR));

            expressionGroup.MainExpression.Tokens.Add(
                new ExpressionToken(Method, TokenType.PROPERTY_NAME));

            expressionGroup.MainExpression.Tokens.Add(
                new ExpressionToken('@', TokenType.OPERATOR));

            expressionGroup.MainExpression.Tokens.Add(
                new ExpressionToken('(', TokenType.BRACKET_BEGIN));

            if (MethodParameters != null)
            {
                Int32 i = -1;
                foreach (Object parameter in MethodParameters)
                {
                    i++;
                    if (i > 0)
                    {
                        expressionGroup.MainExpression.Tokens.Add(
                            new ExpressionToken(',', TokenType.SEPARATOR));
                    }

                    String parameterName = IdGenerator.Generate();
                    globalObject.DynamicValues[parameterName] = parameter; // much faster
                    //newContext[parameterName] = parameter;

                    expressionGroup.MainExpression.Tokens.Add(
                        new ExpressionToken(parameterName, TokenType.VARIABLE));
                }
            }

            expressionGroup.MainExpression.Tokens.Add(
                new ExpressionToken(')', TokenType.BRACKET_END));

            DynLanCodeLine lineOfCode = new DynLanCodeLine()
            {
                ExpressionGroup = expressionGroup,
                Depth = 0,
                IsLineEmpty = false,
                OperatorType = EOperatorType.RETURN
            };
            newContext.GlobalState.Program.Lines.Add(lineOfCode);

            return newContext;
        }*/

        /*private static DynLanContext InvokeMethodGetContext(
            String Method,
            IList<Object> MethodParameters,
            IDictionary<String, Object> Values)
        {
            DynLanContext newContext = CreateContext(
                new DynLanCodeLines(),
                Values,
                false, true);
            DynLanObject globalObject = newContext.GlobalObject;
            
            ExpressionGroup expressionGroup = new ExpressionGroup(false);
            expressionGroup.MainExpression = new Expression();
            expressionGroup.MainExpression.IsOnpExecution = false;
            expressionGroup.MainExpression.Tokens = new ExpressionTokens();

            expressionGroup.MainExpression.Tokens.Add(
                new ExpressionToken(Method, TokenType.VARIABLE));

            expressionGroup.MainExpression.Tokens.Add(
                new ExpressionToken('@', TokenType.OPERATOR));

            expressionGroup.MainExpression.Tokens.Add(
                new ExpressionToken('(', TokenType.BRACKET_BEGIN));

            if (MethodParameters != null)
            {
                Int32 i = -1;
                foreach (Object parameter in MethodParameters)
                {
                    i++;
                    if (i > 0)
                    {
                        expressionGroup.MainExpression.Tokens.Add(
                            new ExpressionToken(',', TokenType.SEPARATOR));
                    }

                    String parameterName = IdGenerator.Generate();
                    globalObject.DynamicValues[parameterName] = parameter; // much faster
                    //newContext[parameterName] = parameter;

                    expressionGroup.MainExpression.Tokens.Add(
                        new ExpressionToken(parameterName, TokenType.VARIABLE));
                }
            }

            expressionGroup.MainExpression.Tokens.Add(
                new ExpressionToken(')', TokenType.BRACKET_END));

            DynLanCodeLine lineOfCode = new DynLanCodeLine()
            {
                ExpressionGroup = expressionGroup,
                Depth = 0,
                IsLineEmpty = false,
                OperatorType = EOperatorType.RETURN
            };
            newContext.GlobalState.Program.Lines.Add(lineOfCode);

            return newContext;
        }
        */
        //////////////////////////////////////////////

        public static DynLanContext CreateContext(
#if !NET20
            this 
#endif
             DynLanProgram Program,
            IDictionary<String, Object> Values = null,
            Boolean BreakEveryLine = false,
            Boolean CopyParameters = false)
        {
            if (Values == null)
                Values = new Dictionary<String, Object>();

            foreach (DynLanMethod DynLanMethod in Program.Methods)
                Values[DynLanMethod.Name] = DynLanMethod;

            foreach (DynLanClass DynLanClass in Program.Classes)
                Values[DynLanClass.Name] = DynLanClass;

            return CreateContext(
                Program.Lines,
                Values,
                BreakEveryLine,
                CopyParameters);
        }

        private static DynLanContext CreateContext(
            DynLanCodeLines Lines,
            IDictionary<String, Object> Values,
            Boolean BreakEveryLine = false,
            Boolean CopyParameters = false)
        {
            DynLanContext runContext = new DynLanContext(
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


    }


}