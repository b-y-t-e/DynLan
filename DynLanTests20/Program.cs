using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

namespace DynLanTests20
{
    class Program
    {
        static void Main(string[] args)
        {
            var obj = new DynLanTests.DynLanTests();
            foreach (var method in obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if(method.GetParameters().Length == 0)
                {
                    method.Invoke(obj, new object[0]);
                }
                else
                {

                }
            }
        }
    }
}
