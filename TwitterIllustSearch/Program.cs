using System;
using System.IO;

namespace TwitterIllustSearch
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var logic = new Logic();

                logic.IllustSearch();
            }
            catch (Exception e)
            {
                File.AppendAllText(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + @"\log.txt", e.Message + "\n");
            }
        }
    }
}
