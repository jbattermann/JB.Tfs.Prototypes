// <copyright file="Program.cs" company="Joerg Battermann">
//     (c) 2012 Joerg Battermann.
//     License: see https://github.com/jbattermann/JB.Tfs.Prototypes/blob/master/LICENSE
// </copyright>
// <author>Joerg Battermann</author>

using System;
using CLAP;

namespace JB.Tfs.Prototypes.TestResultCoverage
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("TFS Test Coverage Checker. This is a PROTOTYPE. I");
            Console.WriteLine("I repeat - a prototype.");
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine("For the description go to:");
            Console.WriteLine("https://github.com/jbattermann/JB.Tfs.Prototypes/tree/master/JB.Tfs.Prototypes.TestResultCoverage");
            Console.WriteLine("");
            Console.WriteLine("This Prototype may only be used if you agree to the License details that can be found at:");
            Console.WriteLine("https://github.com/jbattermann/JB.Tfs.Prototypes/blob/master/LICENSE");
            Console.WriteLine("");
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine("For the final product go to: http://tfs-extended.joergbattermann.com");
            Console.WriteLine();
            try
            {
                Parser.Run<TestCoverageChecker>(args);
            }
            catch (Exception exception)
            {
                var currentConsoleColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Something went wrong:");
                Console.Write(exception);
                Console.ForegroundColor = currentConsoleColor;
            }
        }
    }
}
