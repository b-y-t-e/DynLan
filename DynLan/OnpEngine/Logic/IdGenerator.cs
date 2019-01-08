using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;

namespace DynLan.OnpEngine.Logic
{
    public static class IdGenerator
    {
#if SERVER_SIDE
        private static Object lck = new Object();

        private static Boolean wasInit = false;

        private static Thread thread;

        private static List<String> items;

        private static Int32 maxCount = 1000;

        ////////////////////////////////////////////////////

        public static String Generate()
        {
            if (!wasInit)
                lock (lck)
                    if (!wasInit)
                        Init();

            lock (items)
            {
                if (items.Count > 0)
                {
                    String result = items[0];
                    items.RemoveAt(0);
                    return result;
                }
            }
            return Peek();
        }

        ////////////////////////////////////////////////////

        private static String Peek()
        {
            return String.Format(
                "{0}{1}",
                "GID",
                Guid.NewGuid()).Replace("-", "").ToUpper();
        }

        private static void Init()
        {
            items = new List<String>(maxCount);
            thread = new Thread(Generator);
            thread.Start();
            wasInit = true;
        }

        private static void Generator(Object State)
        {
            while (true)
            {
                Int32 count = -1;

                lock (items)
                    count = items.Count;

                if (count < maxCount)
                    lock (items)
                        while (count < maxCount)
                        {
                            items.Add(Peek());
                            count++;
                        }

                Thread.Sleep(15);
            }
        }
#else

        public static String Generate()
        {
            return String.Format(
                "{0}{1}",
                "GID",
                Guid.NewGuid()).Replace("-", "").ToUpper();
        }

#endif
    }
}