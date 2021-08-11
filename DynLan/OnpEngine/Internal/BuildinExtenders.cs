using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using DynLan.OnpEngine.Models;
using DynLan.OnpEngine.InternalExtenders;
using DynLan.Helpers;

namespace DynLan.OnpEngine.Internal
{
    public static class BuildinExtenders
    {
        private static Dictionary<String, ExpressionExtender> methodsByNames;

        private static Dictionary<Guid, ExpressionExtender> methodsByIds;

        private static Object lck = new Object();

        ////////////////////////////////////////////////////////////////////
        
        public static ExpressionExtenderInfo FindByName(String Name)
        {
            Init();
            ExpressionExtender expressionExtender = null;
            if (methodsByNames.TryGetValue(Name, out expressionExtender))
                return new ExpressionExtenderInfo() { ID = expressionExtender.ID };
            return null;
        }

        public static ExpressionExtender GetByID(Guid ID)
        {
            Init();
            ExpressionExtender expressionExtender = null;
            if (methodsByIds.TryGetValue(ID, out expressionExtender))
                return expressionExtender;
            return null;
        }

        public static void Init()
        {
            if (methodsByIds == null)
                lock (lck)
                    if (methodsByIds == null)
                    {
                        methodsByIds = new Dictionary<Guid, ExpressionExtender>();
                        methodsByNames = new Dictionary<String, ExpressionExtender>();
                        foreach (ExpressionExtender operation in BuildExtenders())
                        {
                            foreach (String name in operation.OperationNames)
                                if (!GlobalSettings.CaseSensitive)
                                {
                                    methodsByNames[name.ToUpperInvariant()] = operation;
                                }
                                else
                                {
                                    methodsByNames[name] = operation;
                                }
                            methodsByIds[operation.ID] = operation;
                        }
                    }
        }

        ////////////////////////////////////////////////////////////////////

        private static IEnumerable<ExpressionExtender> BuildExtenders()
        {
            yield return new ExpressionExtender()
            {
                OperationNames = new[] { ExtenderSetValue.Name },
                CalculateValueDelegate = ExtenderSetValue.Execute
            };
            yield return new ExpressionExtender()
            {
                OperationNames = new[] { ExtenderCollectionSetter.Name },
                CalculateValueDelegate = ExtenderCollectionSetter.Execute
            };
            yield return new ExpressionExtender()
            {
                OperationNames = new[] { ExtenderCollectionGetter.Name },
                CalculateValueDelegate = ExtenderCollectionGetter.Execute
            };
        }
    }
}