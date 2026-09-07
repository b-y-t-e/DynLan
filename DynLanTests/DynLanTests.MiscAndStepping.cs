using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using DynLan;
using DynLan.Exceptions;
using DynLan.OnpEngine.Logic;
using DynLan.Helpers;
using DynLan.OnpEngine.Models;
using DynLan.Evaluator;
using DynLan.Classes;
using System.Dynamic;
using Xunit;
using static DynLanTests.TestFixtures;

namespace DynLanTests
{
    public class DynLanMiscAndSteppingTests
    {

        [Fact]
        public void if_elif_else_script_can_be_stepped_statement_by_statement_via_context_evaluator()
        {
            var r = new Compiler().Compile(@"
a = 4444
gg = 3
if gg == 5 {
  a = 1
}
elif gg == 6 {
  a = 3
}
else {
  a = 2
}
return a

");
            //var runner = new DynLanRunner();
            var context = r.CreateContext();
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
            ContextEvaluator.ExecuteNext(context);
        }

        [Fact]
        public void compiled_expression_can_be_evaluated_repeatedly_with_consistent_result()
        {
            var exp = new Compiler().Compile(" return 'abc'.  Length + ' ' + getdatetime ().ToShortDateString().Substring(100-90-3-7,3) ");
            var r2 = "abc".Length + " " + DateTime.Now.ToShortDateString().Substring(100 - 90 - 3 - 7, 3);
            for (var i = 0; i < 100; i++)
            {
                {
                    var r1 = (String)exp.Eval(GetParams().VARIABLES);
                    Assert.Equal(r2, r1);
                }
            }
        }

    }
}
