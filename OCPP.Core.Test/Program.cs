using System;

namespace OCPP.Core.Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            OCPP16Test.Execute();
            OCPP20Test.Execute();
        }
    }
}