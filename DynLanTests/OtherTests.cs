using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using DynLan;
using DynLan.Exceptions;
using DynLan.OnpEngine.Logic;
using DynLan.Helpers;
using DynLan.OnpEngine.Models;
using System.Reflection;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Runtime.Serialization.Formatters;
using DynLan.Evaluator;
using DynLan.Classes;
using System.Dynamic;

namespace DynLanTests
{
    [TestClass]
    public class OtherTests
    {
        [TestMethod]
        public void test_linq_select()
        {
            var array = new[] { "aaa", "bb", "dddd", "c" };

            var r1 = string.Join(",", array.Select(i => i.Length).ToArray());
            var r2 = string.Join(",", Linq2.From(array).Select(i => i.Length).ToArray());

            Assert.AreEqual(r1, r2);
        }

        [TestMethod]
        public void test_linq_orderby1()
        {
            var array = new[] { "aaa", "bb", "dddd", "c" };

            var r1 = string.Join(",", array.Select(i => i.Length).OrderBy(i => i).ToArray());
            var r2 = string.Join(",", Linq2.From(array).Select(i => i.Length).OrderBy(i => i).ToArray());

            Assert.AreEqual(r1, r2);
        }

        [TestMethod]
        public void test_linq_orderby2()
        {
            var array = new[] { "aaa", "bb", "dddd", "c" };

            var r1 = string.Join(",", array.Select(i => i.Length).OrderBy(i => i).ToArray());
            var r2 = string.Join(",", Linq2.OrderBy(Linq2.Select(array, i => i.Length), i => i).ToArray());

            Assert.AreEqual(r1, r2);
        }
    }
}