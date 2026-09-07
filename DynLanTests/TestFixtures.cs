using System;
using System.Collections;
using System.Collections.Generic;
using DynLan;
using DynLan.Helpers;
using DynLan.OnpEngine.Logic;
using DynLan.OnpEngine.Models;

namespace DynLanTests
{
    internal static class TestFixtures
    {
        public static string nl = Environment.NewLine;

        public class testparams
        {
            public Osoba OSOBA1;
            public Osoba OSOBA2;
            public List<Osoba> OSOBY;
            public Dictionary<String, Object> DICT;
            public ArrayList LIST;
            public Dictionary<String, Object> VARIABLES;
            public Tokenizer tokenizer;
        }

        public static testparams GetParams()
        {
            testparams result = new testparams();

            result.OSOBA1 = new Osoba();
            result.OSOBA2 = new Osoba();
            result.OSOBY = new List<Osoba>();
            result.OSOBY.Add(result.OSOBA1);
            result.OSOBY.Add(result.OSOBA2);

            result.DICT = new Dictionary<String, Object>();
            result.DICT["1"] = "aaa";
            result.DICT["2"] = "jjj";
            result.DICT["Pole1"] = "p1";
            result.DICT["Pole2"] = 56;

            Func<Decimal> m1 = () =>
            {
                return 1M;
            };
            result.DICT["Metoda1"] = m1;

            Func<DynLanMethodParameters, Int32> m2 = (items) =>
            {
                return items.Parameters.Length;
            };
            result.DICT["Metoda2"] = m2;

            Func<DynLanMethodParameters, Object> m3 = (items) =>
            {
                return items.Parameters[1];
            };
            result.DICT["Metoda3"] = m3;

            result.LIST = new ArrayList();
            result.LIST.Add(2);
            result.LIST.Add("3");

            result.VARIABLES = new Dictionary<String, Object>();
            result.VARIABLES["OSOBY"] = result.OSOBY;
            result.VARIABLES["OSOBA"] = result.OSOBA1;
            result.VARIABLES["DICT"] = result.DICT;
            result.VARIABLES["LIST"] = result.LIST;

            result.tokenizer = new Tokenizer();

            return result;
        }
    }
}
